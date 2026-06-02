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
    # OpenNebula USER_TEMPLATE rejects values containing newlines,
    # quotes, or backslashes; the previous tr/sed flattening produced
    # output that onegate silently dropped (or truncated at the first
    # `|`), so the operator saw <no tail>. Base64 is a safe transport:
    # it is pure [A-Za-z0-9+/=], has no shell-special chars, and the
    # operator can `| base64 -d` it back to a readable log.
    #
    # Onegate has an undocumented value-length cap (~4 KiB observed on
    # OpenNebula 7.x), so split the b64 stream across 8 USER_TEMPLATE
    # keys (LOG_B64_1..LOG_B64_8) of <=3500 chars each = ~28 KiB b64
    # = ~21 KiB raw log.
    #
    # CRITICAL: we must capture the *tail* of the log (where the failure
    # message is), not the head. Previously `tail -n 500 | base64 -w0`
    # produced ~45 KiB of b64 and the slicing copied only chunks 1..8 of
    # the FIRST 28 KiB — i.e. the OLDEST 14 KiB of raw text, which on a
    # typical run is dominated by apt-get install spam from steps 2-3
    # and CUTS OFF before the kubeadm-init error in step 4. Use
    # `tail -c 20000` (raw bytes) instead: ~20 KiB raw -> ~27 KiB b64,
    # fits cleanly across 8 chunks, and the last bytes (= the actual
    # error) are guaranteed to be included.
    local b64
    b64=$(tail -c 20000 /var/log/aircraft-cp-bootstrap.log 2>/dev/null | base64 -w0)
    onegate vm update --data "BOOTSTRAP_RC=${rc}"            || true
    onegate vm update --data "BOOTSTRAP_FAIL_LINE=${lineno}" || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_1=${b64:0:3500}"     || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_2=${b64:3500:3500}"  || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_3=${b64:7000:3500}"  || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_4=${b64:10500:3500}" || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_5=${b64:14000:3500}" || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_6=${b64:17500:3500}" || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_7=${b64:21000:3500}" || true
    onegate vm update --data "BOOTSTRAP_LOG_B64_8=${b64:24500:3500}" || true
    exit $rc
}
trap 'report_failure $LINENO' ERR

step() {
    echo "=== STEP: $1 ==="
    onegate vm update --data "BOOTSTRAP_STEP=$1" 2>/dev/null || true
}

# Retry a command up to 5 times with exponential backoff (5s, 10s, 20s,
# 40s, 80s = 155s total). Used for `kubectl apply -f https://...`
# pulls of upstream manifests (Calico, metrics-server, ingress-nginx),
# which routinely transient-fail on the first attempt on minione because:
#   * github.com / raw.githubusercontent.com rate-limits unauthenticated
#     GETs aggressively when many tenancies share an egress NAT IP;
#   * kube-apiserver is still stabilising for 10-30s after Calico's
#     `installation.operator.tigera.io/default` is applied (the operator
#     hot-patches the apiserver's admission controllers), so any kubectl
#     call right after step 5 sometimes returns "connection refused".
# Without retries the bootstrap dies at the FIRST flake; with them every
# subsequent attempt clearly logs "[retry N/5]" before the apply.
retry() {
    local n=0 delay=5 maxn=5
    until "$@"; do
        n=$((n+1))
        if (( n >= maxn )); then
            echo "[retry] giving up after ${n} attempts: $*" >&2
            return 1
        fi
        echo "[retry ${n}/${maxn}] '$*' failed; sleeping ${delay}s before retry" >&2
        sleep "$delay"
        delay=$((delay*2))
    done
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

# controlPlaneEndpoint MUST be a DNS name or IP that *every* node
# (cp + workers) can resolve. On a minione lab there is no public DNS
# for K8S_EDGE_HOST=aircraft.example.com, so we use the cp's primary
# IP directly. K8S_EDGE_HOST is still added to certSANs so the operator
# can wire a real DNS later without re-issuing the API server cert.
#
# If `kubeadm init` is re-run on a half-bootstrapped node (e.g. after a
# previous attempt failed at line 169), the manifests, etcd directory
# and listener ports linger from the first try. Reset before re-init.
if [[ -d /etc/kubernetes/manifests ]] && \
   compgen -G "/etc/kubernetes/manifests/*.yaml" >/dev/null; then
    echo "Detected previous half-init; running kubeadm reset before retry."
    kubeadm reset -f --cri-socket=unix:///run/containerd/containerd.sock || true
fi

cat >/root/kubeadm-config.yaml <<EOF
apiVersion: kubeadm.k8s.io/v1beta3
kind: ClusterConfiguration
kubernetesVersion: "v${K8S_VERSION}.0"
controlPlaneEndpoint: "${PRIMARY_IP}:6443"
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

# kubeadm init is wrapped in a small retry-with-reset loop because on
# minione it occasionally fails mid-bootstrap with transient errors:
#   - "context deadline exceeded" pulling pause:3.9 from registry.k8s.io
#     (when the apt update earlier in step 2 saturated the egress NAT);
#   - "etcd cluster is not available or misconfigured" if the kubelet
#     happens to crash-restart during the [api-check] wait window.
# A bare `kubeadm reset -f` between attempts is sufficient — the kubeadm
# config remains valid (deterministic PRIMARY_IP, same certSANs).
#
# We split kubeadm init into two parts:
#   (1) `kubeadm init --skip-phases=addon/coredns,addon/kube-proxy`
#   (2) `kubeadm init phase addon coredns` + `kubeadm init phase addon
#       kube-proxy` (idempotent, wrapped in our `retry` helper).
#
# Rationale: in the minione lab kubeadm reliably reaches the very last
# (addon) phase, then issues:
#   GET https://${PRIMARY_IP}:6443/apis/apps/v1/namespaces/kube-system/
#       deployments?labelSelector=k8s-app%3Dkube-dns
# which intermittently hangs past kubeadm's hardcoded 30s client timeout
# because, in the ~10s window between [mark-control-plane] and
# addon/coredns, the freshly-started apiserver re-acquires the etcd
# leader lease and the embedded etcd does a full fsync on a single tiny
# VM disk -- on minione's `fs_lvm_ssh` driver this routinely blocks the
# apiserver for >30s. The previous design's "retry whole init 3 times"
# strategy wasted ~90s per attempt and almost always hit the exact same
# stall again because each restart goes through the same fsync window.
# Splitting lets us retry only the addon phase (each attempt <5s),
# and adding a `/readyz` precheck means we don't issue the addon call
# until the apiserver is actually healthy.
KUBEADM_RC=0
for attempt in 1 2 3; do
    if kubeadm init --config=/root/kubeadm-config.yaml --upload-certs \
                    --skip-phases=addon/coredns,addon/kube-proxy; then
        KUBEADM_RC=0
        break
    fi
    KUBEADM_RC=$?
    echo "[kubeadm] init attempt ${attempt} failed (rc=${KUBEADM_RC}); resetting and retrying" >&2
    kubeadm reset -f --cri-socket=unix:///run/containerd/containerd.sock || true
    rm -rf /var/lib/etcd /etc/kubernetes/manifests/* /etc/kubernetes/pki \
           /etc/kubernetes/admin.conf /etc/kubernetes/super-admin.conf \
           /etc/kubernetes/kubelet.conf /etc/kubernetes/controller-manager.conf \
           /etc/kubernetes/scheduler.conf /var/lib/kubelet/pki || true
    # Give containerd a moment to GC the half-built pause/etcd containers
    # before the next preflight inspects /var/lib/containerd/.
    sleep 10
done
if [[ "$KUBEADM_RC" -ne 0 ]]; then
    echo "[kubeadm] init failed after 3 attempts; giving up" >&2
    exit "$KUBEADM_RC"
fi

install -d /root/.kube
cp /etc/kubernetes/admin.conf /root/.kube/config
chown -R root:root /root/.kube

export KUBECONFIG=/etc/kubernetes/admin.conf

# Wait for /readyz before invoking the addon phases. /readyz returns 200
# only when apiserver has finished its post-leader-election warmup AND
# all readiness checks (etcd, informers, apiserver-storage) pass. This
# is the precondition kubeadm's own --timeout flags assume is already
# met when entering addon phases, but on minione it routinely isn't.
echo "[kubeadm] waiting for apiserver /readyz to be healthy..."
for i in $(seq 1 60); do
    if kubectl get --raw=/readyz 2>/dev/null | grep -q '^ok$'; then
        echo "[kubeadm] apiserver /readyz OK after ${i}*5s"
        break
    fi
    sleep 5
done

# Now install the addons. Both `kubeadm init phase addon *` commands are
# idempotent (re-applies the same manifest), so the retry helper is safe.
retry kubeadm init phase addon coredns    --config=/root/kubeadm-config.yaml
retry kubeadm init phase addon kube-proxy --config=/root/kubeadm-config.yaml

step "5-calico"

# ---------------------------------------------------------------------
# 5. Calico (Tigera operator + Installation).
#
# Why --server-side: the tigera-operator manifest contains CRDs that
# exceed the 262 144 byte `last-applied-configuration` annotation that
# the legacy `kubectl apply` (client-side) writes to track diffs. With
# client-side apply the CRD install fails with:
#   "CustomResourceDefinition installations.operator.tigera.io is invalid:
#    metadata.annotations: Too long: must have at most 262144 bytes"
# `--server-side` skips that annotation entirely.
#
# Why the explicit wait: posting the Installation CR before the CRD has
# reached the Established condition fails with:
#   "resource mapping not found for name: default ... no matches for kind
#    Installation in version operator.tigera.io/v1"
# ---------------------------------------------------------------------
retry kubectl apply --server-side --force-conflicts \
    -f https://raw.githubusercontent.com/projectcalico/calico/v3.27.0/manifests/tigera-operator.yaml

# Wait up to 2 minutes for the Installation CRD to be Established.
kubectl wait --for=condition=Established --timeout=120s \
    crd/installations.operator.tigera.io

cat >/root/calico-installation.yaml <<EOF
apiVersion: operator.tigera.io/v1
kind: Installation
metadata:
  name: default
spec:
  calicoNetwork:
    mtu: 1450
    # bgp: Disabled is REQUIRED when encapsulation: VXLAN — without it,
    # the operator still launches BIRD inside calico-node AND the
    # readiness probe still includes 'bird-ready'. BIRD never comes up
    # (no /var/run/bird/bird.ctl, no /var/lib/calico/nodename) so the
    # probe kills the pod within ~30s — CrashLoopBackOff on every worker,
    # cluster has CNI only on cp-1, all cross-node pod traffic is broken
    # (metrics-server can't scrape kubelet, etc.). Setting BGP Disabled
    # makes the operator regenerate the DaemonSet without BIRD.
    bgp: Disabled
    ipPools:
      - cidr: ${POD_CIDR}
        encapsulation: VXLAN
        natOutgoing: Enabled
EOF
kubectl apply --server-side --force-conflicts -f /root/calico-installation.yaml

# Wait for the tigera-operator to materialise the `calico-system`
# namespace (the operator reconciles the Installation CR and creates
# the namespace + DaemonSet asynchronously). Otherwise the next
# rollout-status call fails with "namespaces calico-system not found".
for i in $(seq 1 60); do
    if kubectl get namespace calico-system >/dev/null 2>&1; then
        break
    fi
    sleep 5
done
# `|| true` so the bootstrap survives a Calico install that's slow
# enough to exceed the 300s rollout timeout -- Calico will still
# converge eventually and bootstrap should not block forever on this.
kubectl -n calico-system rollout status ds/calico-node --timeout=300s || true

step "6-metrics-server"

# ---------------------------------------------------------------------
# 6. metrics-server (for HPAs).
# ---------------------------------------------------------------------
retry kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/download/v0.7.1/components.yaml

# Wait for the metrics-server Deployment object to actually exist before
# patching it. `kubectl apply` returns immediately after the API accepts
# the manifest; the Deployment object itself may take 5-10s to appear in
# the watch cache. Without this wait, the patch below races and dies with
# "deployments.apps \"metrics-server\" not found" -> bootstrap rc=1 at
# line ~310 (was the cause of the 4-kubeadm-init/5-calico transient
# failures before retries were added).
for _ in $(seq 1 30); do
    kubectl -n kube-system get deployment metrics-server >/dev/null 2>&1 && break
    sleep 2
done

# Three fixes are needed on a fresh kubeadm cluster, otherwise the pod
# loops with CrashLoopBackOff and the deployment never goes Available:
#
#   (a) --kubelet-insecure-tls
#       kubelet serving cert is self-signed at this stage; without
#       this, metrics-server refuses to scrape (x509 verify failed).
#
#   (b) --kubelet-preferred-address-types=InternalIP
#       Default order is Hostname,InternalDNS,InternalIP,...
#       In this lab there is no DNS for node hostnames (worker-1,
#       worker-2, cp-1), so the Hostname lookup hangs long enough
#       that the /livez probe (3x10s) fails -> kubelet kills the
#       pod -> CrashLoopBackOff. Forcing InternalIP bypasses DNS.
#
#   (c) Pin to the control-plane node + tolerate the cp NoSchedule
#       taint. In the minione lab the workers are notably slower than
#       the cp (which doubles as the OpenNebula front-end host),
#       slow enough that felix's :9099 readiness probe inside
#       calico-node sometimes flaps on workers in the first ~3 min
#       after boot. When metrics-server lands on a flapping worker,
#       it dies along with calico-node and the deployment never
#       reports Available. Pinning to cp-1 makes the readiness
#       deterministic. Removing this pin is safe once the workers
#       are stable; it's defensible in prod because metrics-server
#       has trivial resource footprint and cp-1 already has spare
#       capacity.
retry kubectl -n kube-system patch deployment metrics-server --type=json -p='[
  {"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-insecure-tls"},
  {"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-preferred-address-types=InternalIP"},
  {"op":"add","path":"/spec/template/spec/tolerations","value":[
     {"key":"node-role.kubernetes.io/control-plane","operator":"Exists","effect":"NoSchedule"}
  ]},
  {"op":"add","path":"/spec/template/spec/nodeSelector","value":{
     "node-role.kubernetes.io/control-plane":""
  }}
]'

step "7-ingress-nginx"

# ---------------------------------------------------------------------
# 7. NGINX Ingress controller.
# ---------------------------------------------------------------------
retry kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.10.1/deploy/static/provider/baremetal/deploy.yaml

# Wait for the namespace to exist before labelling it (avoids races on
# slow apiservers right after Calico has just hot-patched admission).
for _ in $(seq 1 30); do
    kubectl get namespace ingress-nginx >/dev/null 2>&1 && break
    sleep 2
done
retry kubectl label namespace ingress-nginx name=ns-gateway --overwrite

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
