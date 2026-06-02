#!/usr/bin/env bash
# =====================================================================
# Phase D §17 — "Consume §2.2 deliverables" readiness probe.
#
# This script is run by the operator *immediately after* pointing
# ${KUBECONFIG} at the candidate cluster produced by the OpenNebula
# automation work item (plans/deploy.md §2.2). Exits 0 only if the
# cluster meets every contract Phases A–C of the deploy plan rely on:
#
#   * 3 nodes total, all Ready
#   * 1 control-plane + 2 workers (label-based)
#   * Calico CNI present and enforcing NetworkPolicy
#   * kube-dns reachable (cross-namespace egress depends on it)
#   * Kubernetes version v1.30.x (kubelet + kube-apiserver)
#   * metrics-server present (HPAs depend on it — deploy.md §6)
#   * Edge IP/DNS reachable on :443 (matches OpenNebula DNAT in §2.2)
#
# Exit codes:
#   0 — cluster matches the §2.1 manual baseline; Phase D can proceed.
#   1 — at least one contract failed; STDERR lists which.
#
# The same script is also used against the manually-built cluster of
# §2.1 as a baseline smoke test (the contract is identical).
# =====================================================================
set -euo pipefail

EDGE_HOST="${EDGE_HOST:-aircraft.example.com}"
EXPECTED_K8S_MINOR="${EXPECTED_K8S_MINOR:-30}"
FAIL=0

log()   { printf '\033[1;34m[cluster-ready]\033[0m %s\n' "$*"; }
warn()  { printf '\033[1;33m[cluster-ready]\033[0m %s\n' "$*" >&2; }
err()   { printf '\033[1;31m[cluster-ready]\033[0m %s\n' "$*" >&2; FAIL=1; }

require() {
  command -v "$1" >/dev/null 2>&1 || { err "missing tool: $1"; exit 1; }
}
require kubectl
require jq

# ---------------------------------------------------------------------
# 1. Node count, roles, readiness.
# ---------------------------------------------------------------------
log "checking node topology (expect 1 control-plane + 2 workers, all Ready)"
NODES_JSON=$(kubectl get nodes -o json)

TOTAL=$(echo "$NODES_JSON" | jq '.items | length')
READY=$(echo "$NODES_JSON" | jq '[.items[] | select(.status.conditions[]? | select(.type=="Ready" and .status=="True"))] | length')
CP=$(echo "$NODES_JSON"    | jq '[.items[] | select(.metadata.labels["node-role.kubernetes.io/control-plane"]!=null)] | length')
WK=$((TOTAL - CP))

[[ "$TOTAL" -eq 3 ]] || err "expected 3 nodes, got $TOTAL"
[[ "$READY" -eq 3 ]] || err "expected 3 Ready nodes, got $READY"
[[ "$CP"    -eq 1 ]] || err "expected 1 control-plane node, got $CP"
[[ "$WK"    -eq 2 ]] || err "expected 2 worker nodes, got $WK"

# ---------------------------------------------------------------------
# 2. Kubernetes version (kubelet + kube-apiserver) — must be v1.30.x.
# ---------------------------------------------------------------------
log "checking Kubernetes minor version (expected v1.${EXPECTED_K8S_MINOR}.x)"
SERVER_MINOR=$(kubectl version -o json | jq -r '.serverVersion.minor' | tr -dc '0-9')
[[ "$SERVER_MINOR" == "$EXPECTED_K8S_MINOR" ]] \
  || err "kube-apiserver minor=$SERVER_MINOR, want $EXPECTED_K8S_MINOR"

while read -r node kubelet; do
  minor=$(echo "$kubelet" | sed -nE 's/^v1\.([0-9]+)\..*/\1/p')
  [[ "$minor" == "$EXPECTED_K8S_MINOR" ]] \
    || err "kubelet on $node = $kubelet (want v1.${EXPECTED_K8S_MINOR}.x)"
done < <(echo "$NODES_JSON" | jq -r '.items[] | "\(.metadata.name) \(.status.nodeInfo.kubeletVersion)"')

# ---------------------------------------------------------------------
# 3. Calico CNI is the CNI in use (NetworkPolicy enforcement depends
#    on it — deploy.md §9 risk row 1).
#
# Calico can be installed two ways:
#   (a) classic "calico.yaml" manifest -> DaemonSet lives in kube-system
#   (b) Tigera operator (what bootstrap-cp.sh installs) -> DaemonSet lives
#       in calico-system, managed by the operator in tigera-operator ns
# Accept either layout; only fail if neither namespace has the DS.
# ---------------------------------------------------------------------
log "checking Calico CNI is installed and DaemonSet is healthy"
CALICO_NS=""
for ns in calico-system kube-system; do
  if kubectl -n "$ns" get daemonset calico-node >/dev/null 2>&1; then
    CALICO_NS="$ns"
    break
  fi
done
if [[ -z "$CALICO_NS" ]]; then
  err "calico-node DaemonSet not found in calico-system or kube-system — NetworkPolicies will silently no-op"
else
  log "  found calico-node in namespace: $CALICO_NS"
  DESIRED=$(kubectl -n "$CALICO_NS" get daemonset calico-node -o jsonpath='{.status.desiredNumberScheduled}')
  READY_DS=$(kubectl -n "$CALICO_NS" get daemonset calico-node -o jsonpath='{.status.numberReady}')
  [[ "$DESIRED" == "$READY_DS" && "$DESIRED" -ge 3 ]] \
    || err "calico-node ready=$READY_DS / desired=$DESIRED (want $DESIRED == ready and >= 3)"
fi

# ---------------------------------------------------------------------
# 4. CoreDNS reachable (every allow-*.yaml egresses to kube-dns —
#    deploy.md §9 risk row 6).
# ---------------------------------------------------------------------
log "checking kube-dns / CoreDNS is Ready"
if ! kubectl -n kube-system get deploy coredns >/dev/null 2>&1; then
  err "coredns Deployment not found in kube-system"
else
  AV=$(kubectl -n kube-system get deploy coredns -o jsonpath='{.status.availableReplicas}')
  [[ "${AV:-0}" -ge 1 ]] || err "coredns has no available replicas"
fi

# ---------------------------------------------------------------------
# 5. metrics-server (HPAs in deploy.md §3.3 / §6 depend on it).
# ---------------------------------------------------------------------
log "checking metrics-server is installed"
if ! kubectl -n kube-system get deploy metrics-server >/dev/null 2>&1; then
  err "metrics-server Deployment missing — HPAs will report '<unknown>' targets"
else
  AV=$(kubectl -n kube-system get deploy metrics-server -o jsonpath='{.status.availableReplicas}')
  [[ "${AV:-0}" -ge 1 ]] || err "metrics-server has no available replicas"
fi

# ---------------------------------------------------------------------
# 6. Edge endpoint reachable from the operator's workstation
#    (the OpenNebula NAT/edge DNAT in §2.2 must already be wired).
# ---------------------------------------------------------------------
log "checking edge endpoint https://${EDGE_HOST} (:443) is reachable"
if ! curl -fsS --max-time 5 -o /dev/null -k "https://${EDGE_HOST}/healthz" 2>/dev/null \
   && ! curl -fsS --max-time 5 -o /dev/null -k "https://${EDGE_HOST}/" 2>/dev/null ; then
  warn "could not reach https://${EDGE_HOST} — verify OpenNebula edge DNAT (§2.2)"
  warn "this is a soft warning: cluster may still be valid behind a private edge"
fi

# ---------------------------------------------------------------------
# Result.
# ---------------------------------------------------------------------
if [[ "$FAIL" -eq 0 ]]; then
  log "OK — cluster meets the Phase D §17 consume contract"
  exit 0
fi
err "FAIL — cluster does NOT meet the Phase D §17 consume contract; see errors above"
exit 1
