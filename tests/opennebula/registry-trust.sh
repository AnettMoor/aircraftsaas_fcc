#!/usr/bin/env bash
# =====================================================================
# Phase D §19 — "Containerd registry trust" verification.
#
# After the cut-over from the §2.1 manual cluster to the §2.2
# automation-built cluster, every node MUST treat the in-cluster
# registry as a trusted insecure mirror on its internal Service IP.
# This is what the cloud-init in `opennebula/context/cloud-init.yaml`
# is responsible for wiring; this script proves it shipped correctly.
#
# What we verify:
#   1. Every node's containerd config has a registry.mirrors entry
#      that resolves to registry.ns-registry.svc.cluster.local:5000
#      (allowed to be insecure — the in-cluster Service has no TLS).
#   2. Pulling an image by registry FQDN from a freshly scheduled
#      Pod on each worker actually succeeds. This is the live-fire
#      proof that the configuration of (1) is *honoured* — Phase D
#      §19 explicitly calls this out, because containerd silently
#      ignores typos in `/etc/containerd/config.toml` hosts files.
#
# Exit 0 only if BOTH checks pass on EVERY worker node.
# Run order: AFTER tests/opennebula/cluster-ready.sh, BEFORE flipping
# the CI/CD `KUBE_CONFIG` secret to the new cluster (deploy.md §20).
# =====================================================================
set -euo pipefail

REGISTRY_HOST="${REGISTRY_HOST:-registry.ns-registry.svc.cluster.local:5000}"
PROBE_IMAGE_PATH="${PROBE_IMAGE_PATH:-users-service:latest}"  # already pushed by §C CI
NS="${NS:-default}"
FAIL=0

log()  { printf '\033[1;34m[registry-trust]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[registry-trust]\033[0m %s\n' "$*" >&2; }
err()  { printf '\033[1;31m[registry-trust]\033[0m %s\n' "$*" >&2; FAIL=1; }

# ---------------------------------------------------------------------
# 1. Static inspection — every node's containerd config must declare
#    the registry as an insecure mirror.
#
# We use a privileged debug Pod that mounts the host's /etc into the
# Pod so we can grep without needing SSH access to the nodes (the
# OpenNebula automation may not grant operator SSH; only kubeconfig).
# ---------------------------------------------------------------------
log "auditing containerd configuration on every node"
WORKERS=$(kubectl get nodes -l '!node-role.kubernetes.io/control-plane' -o jsonpath='{.items[*].metadata.name}')
[[ -n "$WORKERS" ]] || { err "no worker nodes found"; exit 1; }

for NODE in $WORKERS; do
  log "  -> node $NODE: searching containerd hosts.toml for $REGISTRY_HOST"
  POD="cfgaudit-$(echo "$NODE" | tr -c 'a-z0-9' '-' | cut -c1-30)-$$"
  if ! kubectl run "$POD" \
        --rm -i --restart=Never --namespace "$NS" \
        --image=busybox:1.36 \
        --overrides='{
          "apiVersion":"v1",
          "spec":{
            "nodeName":"'"$NODE"'",
            "hostPID":true,
            "tolerations":[{"operator":"Exists"}],
            "containers":[{
              "name":"audit","image":"busybox:1.36",
              "stdin":true,"tty":false,
              "command":["sh","-c","grep -RIl '"'"''"$REGISTRY_HOST"''"'"' /host-etc/containerd 2>/dev/null || true; grep -RIl '"'"'insecure_skip_verify'"'"' /host-etc/containerd 2>/dev/null || true"],
              "securityContext":{"privileged":true},
              "volumeMounts":[{"name":"etc","mountPath":"/host-etc","readOnly":true}]
            }],
            "volumes":[{"name":"etc","hostPath":{"path":"/etc"}}]
          }
        }' \
        --command -- sh -c "true" 2>/dev/null | tee /tmp/regaudit.$$.out | grep -q .
  then
    err "    node $NODE has NO containerd config referencing $REGISTRY_HOST"
    err "    -> verify opennebula/context/cloud-init.yaml registered the mirror"
  else
    log "    node $NODE: containerd config references the registry"
  fi
  rm -f /tmp/regaudit.$$.out
done

# ---------------------------------------------------------------------
# 2. Live-fire pull — schedule a one-shot Pod on each worker that
#    pulls $REGISTRY_HOST/$PROBE_IMAGE_PATH. If containerd rejected
#    the registry as untrusted, the pull fails with "x509: certificate
#    signed by unknown authority"; we catch that as the failure mode.
# ---------------------------------------------------------------------
log "live-fire pull from $REGISTRY_HOST/$PROBE_IMAGE_PATH on each worker"
for NODE in $WORKERS; do
  POD="regpull-$(echo "$NODE" | tr -c 'a-z0-9' '-' | cut -c1-30)-$$"
  log "  -> $NODE: kubectl run $POD"
  if ! kubectl run "$POD" \
        --rm --restart=Never --namespace "$NS" \
        --image="${REGISTRY_HOST}/${PROBE_IMAGE_PATH}" \
        --overrides='{"apiVersion":"v1","spec":{"nodeName":"'"$NODE"'","imagePullSecrets":[{"name":"local-registry"}],"containers":[{"name":"c","image":"'"${REGISTRY_HOST}/${PROBE_IMAGE_PATH}"'","command":["/bin/true"]}]}}' \
        --command --timeout=60s -- /bin/true 2>&1 \
        | tee "/tmp/regpull.$NODE.$$.log"; then
    if grep -qiE 'x509|tls|unknown authority|insecure' "/tmp/regpull.$NODE.$$.log"; then
      err "    $NODE: TLS / trust failure — containerd does NOT trust $REGISTRY_HOST"
    else
      err "    $NODE: pull failed for another reason; see /tmp/regpull.$NODE.$$.log"
    fi
  else
    log "    $NODE: pull OK"
  fi
done

if [[ "$FAIL" -eq 0 ]]; then
  log "OK — every worker trusts $REGISTRY_HOST as an insecure mirror"
  exit 0
fi
err "FAIL — at least one worker does NOT trust the in-cluster registry"
err "       fix opennebula/context/cloud-init.yaml mirror section and re-run"
exit 1
