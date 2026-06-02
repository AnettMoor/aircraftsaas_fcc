#!/usr/bin/env bash
# =====================================================================
# tests/k8s/network-policy.sh — Phase C §C.12 chaos test
#
# Validates the NetworkPolicy posture committed under k8s/network-policies/.
#
# Three checks:
#
# (1) LABEL ASSERTION
#     Every app namespace MUST carry a `name=ns-xxx` label, otherwise
#     the namespaceSelector matchers in every allow-*.yaml silently
#     fail and default-deny keeps every flow blocked.
#
# (2) ALLOW-PATH CHECK
#     A pod inside ns-booking MUST be able to TCP-connect to
#     fleet-service.ns-fleet.svc.cluster.local:8080 (booking → fleet
#     is on the architectural allow-list).
#
# (3) DENY-PATH CHECK (chaos)
#     A pod inside ns-fleet without the `app: fleet-service` label
#     MUST NOT be able to TCP-connect to postgres.ns-infra:5432.
#     The default-deny + allow-* policies should drop the traffic;
#     the test counts a *successful* connection as a regression.
#
# Exit codes:
#   0  — all three checks pass.
#   1  — one or more checks failed (script prints which).
#
# Wired into CI: see .github/workflows/ci-security.yaml (Phase C §C.16).
# =====================================================================
set -euo pipefail

PASS=0
FAIL=0

log() { printf '[netpol-test] %s\n' "$*" >&2; }
ok()  { log "PASS — $*"; PASS=$((PASS+1)); }
bad() { log "FAIL — $*"; FAIL=$((FAIL+1)); }

# ---------------------------------------------------------------------
# (1) Label assertion — every NetworkPolicy in k8s/network-policies/
# selects peer namespaces by `name=ns-xxx`. Missing that label is a
# silent disaster: the policy compiles, the matcher matches nothing,
# and default-deny isolates the workload from its own dependencies.
# ---------------------------------------------------------------------
log "(1) namespace label assertion"
for ns in ns-users ns-fleet ns-booking ns-infra ns-frontend ns-registry; do
  got=$(kubectl get ns "$ns" -o jsonpath='{.metadata.labels.name}' 2>/dev/null || true)
  if [ "$got" = "$ns" ]; then
    ok "namespace $ns carries name=$ns"
  else
    bad "namespace $ns missing 'name=$ns' label (got='$got')"
  fi
done

# Helper: run `nc -zw 3 <host> <port>` from a throwaway pod in <ns>,
# returning 0 if the connection succeeded, non-zero otherwise.
nc_from() {
  local ns=$1 host=$2 port=$3 podname=$4
  # We add a label so the busybox pod is NOT selected by any
  # `app: <svc>-service` allow rule — important for the deny check.
  kubectl -n "$ns" run "$podname" \
    --rm -i --restart=Never --quiet \
    --labels=role=netpol-probe \
    --image=busybox:1.36 \
    --command -- sh -c "nc -zw 3 $host $port" 2>/dev/null
}

# ---------------------------------------------------------------------
# (2) Allow-path check — booking → fleet on :8080 is explicitly
# allowed by allow-fleet.yaml (ingress) + allow-booking.yaml (egress).
#
# We label this probe pod `app=booking-service` so the booking egress
# allow rule selects it. Without the label, default-deny would block
# the probe even though the architectural flow is allowed (this is the
# correct security default — only pods that *are* the booking service
# may make the call).
# ---------------------------------------------------------------------
log "(2) allow-path: booking -> fleet :8080"
if kubectl -n ns-booking run netpol-allow-probe \
     --rm -i --restart=Never --quiet \
     --labels=app=booking-service \
     --image=busybox:1.36 \
     --command -- sh -c "nc -zw 3 fleet-service.ns-fleet.svc.cluster.local 8080" \
     >/dev/null 2>&1; then
  ok "booking pod can reach fleet-service:8080"
else
  bad "booking pod cannot reach fleet-service:8080 (allow rule broken)"
fi

# ---------------------------------------------------------------------
# (3) Deny-path chaos check — an unlabeled pod in ns-fleet must NOT
# be able to talk to postgres.ns-infra:5432. allow-infra.yaml allows
# ns-fleet → postgres, but ONLY for pods with the matching app label
# — and the postgres ingress filter also requires that the source
# namespace is ns-fleet, which it is. The deny we exercise is the
# *egress* side of ns-fleet's default-deny: an unlabeled pod doesn't
# match `app: fleet-service` in allow-fleet-egress and therefore has
# no egress rules covering it at all.
# ---------------------------------------------------------------------
log "(3) deny-path: unlabeled ns-fleet pod -> postgres :5432"
if nc_from ns-fleet postgres.ns-infra.svc.cluster.local 5432 netpol-deny-probe; then
  bad "unlabeled ns-fleet pod reached postgres:5432 — default-deny broken"
else
  ok "unlabeled ns-fleet pod blocked from postgres:5432"
fi

# ---------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------
log "----- summary -----"
log "PASS: $PASS"
log "FAIL: $FAIL"
[ "$FAIL" -eq 0 ]
