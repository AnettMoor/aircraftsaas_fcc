#!/usr/bin/env bash
# =====================================================================
# bootstrap-wk.sh -- worker bootstrap.
#
# Mirror image of bootstrap-cp.sh minus the kubeadm init / CNI / Ingress
# steps. Polls OpenNebula for K8S_JOIN_COMMAND (published by cp-1 via
# bootstrap-cp.sh step 8) and runs `kubeadm join`.
#
# Variables injected by one-context from opennebula/templates/wk.tpl:
#   K8S_VERSION             — apt-pinned (e.g. "1.30")
#   K8S_CONTROL_PLANE_VM    — name of the cp VM (e.g. "controlplane_0_(service_17)")
#   K8S_JOIN_COMMAND_SOURCE — informational (USER_TEMPLATE/K8S_JOIN_COMMAND)
# =====================================================================

set -euxo pipefail
exec > >(tee -a /var/log/aircraft-wk-bootstrap.log) 2>&1
echo "=== aircraft-wk bootstrap starting at $(date -Is) ==="

: "${K8S_VERSION:?K8S_VERSION missing from CONTEXT}"
: "${K8S_CONTROL_PLANE_VM:?K8S_CONTROL_PLANE_VM missing from CONTEXT}"

# ---------------------------------------------------------------------
# 1. Kernel + containerd + kube* (same prep as cp).
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
apt-get install -y "kubelet=${K8S_VERSION}.*" "kubeadm=${K8S_VERSION}.*"
apt-mark hold kubelet kubeadm

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

# ---------------------------------------------------------------------
# 2. Poll OneGate for the join command. cp-1's bootstrap publishes it
#    as USER_TEMPLATE/K8S_JOIN_COMMAND once kubeadm init finishes.
#    We poll for up to 15 minutes (cp typically takes 5-7).
# ---------------------------------------------------------------------
JOIN=""
for i in $(seq 1 90); do
    JOIN=$(onegate vm show -j --filter "NAME=${K8S_CONTROL_PLANE_VM}" 2>/dev/null \
           | jq -r '(.VM // .[0].VM // .[0]) | .USER_TEMPLATE.K8S_JOIN_COMMAND // empty')
    if [[ -n "$JOIN" ]]; then
        echo "Got join command after ${i} poll(s)"
        break
    fi
    # Fallback: search by ROLE_NAME=controlplane (OneFlow auto-names
    # VMs to "controlplane_0_(service_NN)" which is fragile to match by
    # name; ROLE_NAME=controlplane is stable).
    JOIN=$(onegate service show -j 2>/dev/null \
           | jq -r '.. | objects | select(.ROLE_NAME?=="controlplane") | .USER_TEMPLATE.K8S_JOIN_COMMAND? // empty' \
           | head -1)
    if [[ -n "$JOIN" ]]; then
        echo "Got join command via ROLE_NAME=controlplane after ${i} poll(s)"
        break
    fi
    sleep 10
done

if [[ -z "$JOIN" ]]; then
    echo "FATAL: control-plane never published K8S_JOIN_COMMAND after 15 min" >&2
    exit 1
fi

# ---------------------------------------------------------------------
# 3. kubeadm join.
# ---------------------------------------------------------------------
echo "$JOIN --cri-socket=unix:///run/containerd/containerd.sock" > /root/join-command.sh
chmod +x /root/join-command.sh
bash /root/join-command.sh

echo "=== aircraft-wk bootstrap finished at $(date -Is) ==="
