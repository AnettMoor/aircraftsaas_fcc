#!/usr/bin/env bash
# =====================================================================
# bootstrap-cp.sh -- control-plane bootstrap.
#
# Runs from one-context's START_SCRIPT_BASE64 slot. The previous design
# used a #cloud-config YAML file, but OpenNebula's START_SCRIPT_BASE64
# is a SHELL script field -- one-context decodes it and passes the bytes
# to `bash -lc`, NOT to cloud-init. Stuffing cloud-init YAML in there
# silently no-op'd (every YAML key got interpreted as a bash command
# "package_update:: not found" etc.), which is why kubeadm init never
# ran and KUBECONFIG_B64 / K8S_JOIN_COMMAND never made it into
# USER_TEMPLATE.
#
# All variables previously interpolated by cloud-init from CONTEXT
# (K8S_VERSION, K8S_EDGE_HOST, POD_CIDR) are injected by one-context
# into the script's environment before this runs, because
# opennebula/templates/cp.tpl declares them in CONTEXT = [ ... ].
#
# Output: every step is fail-fast (`set -euxo pipefail`). The two side
# effects we need OpenNebula to see are:
#   * USER_TEMPLATE/K8S_JOIN_COMMAND  (read by workers in bootstrap-wk.sh)
#   * USER_TEMPLATE/KUBECONFIG_B64    (read by the operator runbook)
# both written via `onegate vm update --data "KEY=value"`.
# =====================================================================

set -euxo pipefail
exec > >(tee -a /var/log/aircraft-cp-bootstrap.log) 2>&1
echo "=== aircraft-cp bootstrap starting at $(date -Is) ==="

# ---------------------------------------------------------------------
# Error reporter: on any failure, publish the last ~80 lines of the
# bootstrap log into USER_TEMPLATE.BOOTSTRAP_LOG_TAIL so the operator
# can read it from the OpenNebula host without VNC/sudo/serial console.
# Also publish BOOTSTRAP_STEP at every major step (heartbeat) so even
# a hang (rather than a crash) leaves a forensic breadcrumb.
# ---------------------------------------------------------------------
report_failure() {
    local rc=$?
    local lineno=${1:-unknown}
    set +e
    local tail80
    # OpenNebula USER_TEMPLATE values must be plain ASCII without
    # newlines or quotes; flatten the log tail to a single line.
    tail80=$(tail -n 80 /var/log/aircraft-cp-bootstrap.log 2>/dev/null \
             | tr '\n' '|' | tr -d '"\\' | sed 's/[^[:print:]|]/?/g' | cut -c1-3500)
    onegate vm update --data "BOOTSTRAP_RC=${rc}"            || true
    onegate vm update --data "BOOTSTRAP_FAIL_LINE=${lineno}" || true
    onegate vm update --data "BOOTSTRAP_LOG_TAIL=${tail80}"  || true
    exit $rc
}
trap 'report_failure $LINENO' ERR

step() {
    echo "=== STEP: $1 ==="
    onegate vm update --data "BOOTSTRAP_STEP=$1" 2>/dev/null || true
}

# ---------------------------------------------------------------------
# Defensive: defaults if CONTEXT didn't inject them (shouldn't happen,
# but fail loud rather than silently install 1.30.0 over nothing).
# ---------------------------------------------------------------------
: "${K8S_VERSION:?K8S_VERSION missing from CONTEXT}"
: "${K8S_EDGE_HOST:?K8S_EDGE_HOST missing from CONTEXT}"
: "${POD_CIDR:=192.168.0.0/16}"

step "1-prep-kernel"

# ---------------------------------------------------------------------
# 1. Kernel modules + sysctls required by kubelet + Calico.
# ---------------------------------------------------------------------
cat >/etc/modules-load.d/k8s.conf <<'EOF'
overlay
br_netfilter
EOF
modprobe overlay
modprobe br_netfilter

cat >/etc/sysctl.d/k8s.conf <<'EOF'
net.bridge.bridge-nf-call-iptables  = 1
net.bridge.bridge-nf-call-ip6tables = 1
net.ipv4.ip_forward                 = 1
EOF
sysctl --system

swapoff -a
sed -i '/ swap / s/^/#/' /etc/fstab

step "2-apt-kube"

# ---------------------------------------------------------------------
# 2. containerd + Kubernetes apt repos.
# ---------------------------------------------------------------------
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y apt-transport-https ca-certificates curl gpg jq containerd

install -d /etc/apt/keyrings
curl -fsSL "https://pkgs.k8s.io/core:/stable:/v${K8S_VERSION}/deb/Release.key" \
    | gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg

cat >/etc/apt/sources.list.d/kubernetes.list <<EOF
deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] https://pkgs.k8s.io/core:/stable:/v${K8S_VERSION}/deb/ /
EOF

apt-get update -y
apt-get install -y "kubelet=${K8S_VERSION}.*" "kubeadm=${K8S_VERSION}.*" "kubectl=${K8S_VERSION}.*"
apt-mark hold kubelet kubeadm kubectl

step "3-containerd-config"

# ---------------------------------------------------------------------
# 3. containerd config -- systemd cgroup driver + in-cluster registry trust.
# ---------------------------------------------------------------------
mkdir -p /etc/containerd /etc/containerd/certs.d/registry.ns-registry.svc.cluster.local:5000
cat >/etc/containerd/config.toml <<'EOF'
version = 2
[plugins."io.containerd.grpc.v1.cri"]
  sandbox_image = "registry.k8s.io/pause:3.9"
  [plugins."io.containerd.grpc.v1.cri".containerd.runtimes.runc]
    runtime_type = "io.containerd.runc.v2"
    [plugins."io.containerd.grpc.v1.cri".containerd.runtimes.runc.options]
      SystemdCgroup = true
  [plugins."io.containerd.grpc.v1.cri".registry]
    config_path = "/etc/containerd/certs.d"
EOF

cat >'/etc/containerd/certs.d/registry.ns-registry.svc.cluster.local:5000/hosts.toml' <<'EOF'
server = "http://registry.ns-registry.svc.cluster.local:5000"
[host."http://registry.ns-registry.svc.cluster.local:5000"]
  capabilities = ["pull", "resolve"]
  skip_verify  = true
  plain_http   = true
EOF

systemctl daemon-reload
systemctl enable --now containerd

step "4-kubeadm-init"

# ---------------------------------------------------------------------
# 4. kubeadm config + init.
# ---------------------------------------------------------------------
# Discover the primary NIC IP so the kubeadm certSANs match the actual
# advertise address (cp lands on .50 on minione's minionebr by AR
# allocation order, but we don't hardcode it here).
PRIMARY_IP=$(ip -4 -o addr show scope global | awk '{print $4}' | cut -d/ -f1 | head -1)
echo "Primary IP detected: $PRIMARY_IP"

cat >/root/kubeadm-config.yaml <<EOF
apiVersion: kubeadm.k8s.io/v1beta3
kind: ClusterConfiguration
kubernetesVersion: "v${K8S_VERSION}.0"
controlPlaneEndpoint: "${K8S_EDGE_HOST}:6443"
networking:
  podSubnet: "${POD_CIDR}"
  serviceSubnet: "10.96.0.0/12"
apiServer:
  certSANs:
    - "${K8S_EDGE_HOST}"
    - "${PRIMARY_IP}"
    - "127.0.0.1"
---
apiVersion: kubeadm.k8s.io/v1beta3
kind: InitConfiguration
nodeRegistration:
  criSocket: "unix:///run/containerd/containerd.sock"
EOF

kubeadm init --config=/root/kubeadm-config.yaml --upload-certs

install -d /root/.kube
cp /etc/kubernetes/admin.conf /root/.kube/config
chown -R root:root /root/.kube

export KUBECONFIG=/etc/kubernetes/admin.conf

step "5-calico"

# ---------------------------------------------------------------------
# 5. Calico (Tigera operator + Installation).
# ---------------------------------------------------------------------
kubectl apply -f https://raw.githubusercontent.com/projectcalico/calico/v3.27.0/manifests/tigera-operator.yaml

cat >/root/calico-installation.yaml <<EOF
apiVersion: operator.tigera.io/v1
kind: Installation
metadata:
  name: default
spec:
  calicoNetwork:
    mtu: 1450
    ipPools:
      - cidr: ${POD_CIDR}
        encapsulation: VXLAN
        natOutgoing: Enabled
EOF
kubectl apply -f /root/calico-installation.yaml

step "6-metrics-server"

# ---------------------------------------------------------------------
# 6. metrics-server (for HPAs).
# ---------------------------------------------------------------------
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/download/v0.7.1/components.yaml
# kubelet TLS is self-signed at this stage; patch metrics-server to skip verification.
kubectl -n kube-system patch deployment metrics-server --type=json \
    -p='[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-insecure-tls"}]'

step "7-ingress-nginx"

# ---------------------------------------------------------------------
# 7. NGINX Ingress controller.
# ---------------------------------------------------------------------
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.10.1/deploy/static/provider/baremetal/deploy.yaml
kubectl label namespace ingress-nginx name=ns-gateway --overwrite

step "8-publish-join"

# ---------------------------------------------------------------------
# 8. Publish K8S_JOIN_COMMAND to USER_TEMPLATE for workers.
# ---------------------------------------------------------------------
JOIN=$(kubeadm token create --ttl 0 --print-join-command)
echo "Join command generated, length=${#JOIN}"
onegate vm update --data "K8S_JOIN_COMMAND=${JOIN}"

step "9-publish-kubeconfig"

# ---------------------------------------------------------------------
# 9. Publish kubeconfig (with server: rewritten to the edge host) to
#    USER_TEMPLATE for the operator's kubectl.
# ---------------------------------------------------------------------
KUBECONFIG_REWRITTEN=$(sed "s|server: https://[0-9.]*:6443|server: https://${K8S_EDGE_HOST}:6443|" \
                          /etc/kubernetes/admin.conf | base64 -w0)
onegate vm update --data "KUBECONFIG_B64=${KUBECONFIG_REWRITTEN}"

step "DONE"

echo "=== aircraft-cp bootstrap finished at $(date -Is) ==="
