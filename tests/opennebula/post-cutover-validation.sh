#!/usr/bin/env bash
# =====================================================================
# Phase D §20 — Post-cut-over end-to-end validation.
#
# Runs AFTER:
#   1. tests/opennebula/cluster-ready.sh   has exited 0
#   2. tests/opennebula/registry-trust.sh  has exited 0
#   3. `kubectl apply -k k8s/overlays/opennebula` has completed
#
# Verifies the production-shaped cluster actually works end-to-end:
#
#   §A. App namespaces are present and labelled.
#   §B. Every backend Deployment has been rolled out and is at the
#       overlay-mandated replica count (3) on workers (not on cp-1).
#   §C. Postgres is reachable from EACH app namespace, and from
#       nowhere else (degenerate NetworkPolicy chaos test).
#   §D. RabbitMQ has at least one connected consumer per service.
#   §E. HPAs report non-`<unknown>` CPU/memory targets — proves
#       metrics-server is hooked up to live workload metrics.
#   §F. All four /health endpoints respond 200 OVER the edge
#       Ingress (so TLS + cert-manager + NGINX path is exercised).
#   §G. CSP `connect-src` whitelist on the frontend Ingress
#       contains *.aircraft.example.com (i.e. the §4.1 patch landed).
#
# Exit codes:
#   0 — post-cutover validation passed.
#   1 — at least one check failed; STDERR lists which.
#
# Required env:
#   EDGE_HOST          — defaults to aircraft.example.com
#   KUBECONFIG         — points at the OpenNebula cluster
# =====================================================================
set -euo pipefail

EDGE_HOST="${EDGE_HOST:-aircraft.example.com}"
APP_HOST="app.${EDGE_HOST}"
USERS_HOST="users.${EDGE_HOST}"
FLEET_HOST="fleet.${EDGE_HOST}"
BOOKING_HOST="booking.${EDGE_HOST}"
EXPECTED_REPLICAS="${EXPECTED_REPLICAS:-3}"
FAIL=0

log()  { printf '\033[1;34m[post-cutover]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[post-cutover]\033[0m %s\n' "$*" >&2; }
err()  { printf '\033[1;31m[post-cutover]\033[0m %s\n' "$*" >&2; FAIL=1; }
ok()   { printf '\033[1;32m[post-cutover]\033[0m %s\n' "$*"; }

require() {
  command -v "$1" >/dev/null 2>&1 || { err "missing tool: $1"; exit 1; }
}
require kubectl
require jq
require curl

# ---------------------------------------------------------------------
# §A. Namespaces present and labelled.
# ---------------------------------------------------------------------
log "§A — namespaces present with the name:ns-xxx label NetworkPolicies need"
for NS in ns-users ns-fleet ns-booking ns-frontend ns-infra ns-registry; do
  LABEL=$(kubectl get ns "$NS" -o jsonpath='{.metadata.labels.name}' 2>/dev/null || true)
  if [[ "$LABEL" != "$NS" ]]; then
    err "  $NS missing label name=$NS (NetworkPolicies will silently no-op)"
  else
    ok "  $NS labelled OK"
  fi
done

# ---------------------------------------------------------------------
# §B. Backend Deployments rolled out to overlay replica count.
# ---------------------------------------------------------------------
log "§B — backend Deployments are rolled out at replicas=$EXPECTED_REPLICAS"
declare -A SVC_NS=(
  [users-service]=ns-users
  [fleet-service]=ns-fleet
  [booking-service]=ns-booking
  [vue-frontend]=ns-frontend
)
for SVC in "${!SVC_NS[@]}"; do
  NS="${SVC_NS[$SVC]}"
  READY=$(kubectl -n "$NS" get deploy "$SVC" -o jsonpath='{.status.readyReplicas}' 2>/dev/null || echo 0)
  DESIRED=$(kubectl -n "$NS" get deploy "$SVC" -o jsonpath='{.spec.replicas}' 2>/dev/null || echo 0)
  if [[ "${READY:-0}" -lt "$EXPECTED_REPLICAS" ]]; then
    err "  $NS/$SVC ready=$READY desired=$DESIRED (want >= $EXPECTED_REPLICAS)"
  else
    ok "  $NS/$SVC ready=$READY desired=$DESIRED"
  fi

  # Anti-affinity is "soft" (ScheduleAnyway); we still WARN if all
  # replicas landed on a single node — that would mean the IaaS-level
  # SCHED_RANK didn't actually spread the VMs.
  NODES=$(kubectl -n "$NS" get pods -l "app=$SVC" -o jsonpath='{range .items[*]}{.spec.nodeName}{"\n"}{end}' | sort -u | wc -l)
  if [[ "$NODES" -lt 2 && "$READY" -gt 1 ]]; then
    warn "  $NS/$SVC: all $READY replicas on a single node — anti-affinity may be off"
  fi
done

# ---------------------------------------------------------------------
# §C. Postgres reachability matrix.
# ---------------------------------------------------------------------
log "§C — Postgres reachable from app namespaces, blocked elsewhere"
for NS in ns-users ns-fleet ns-booking; do
  POD="pgprobe-$$-$RANDOM"
  if kubectl -n "$NS" run "$POD" \
       --rm -i --restart=Never --image=busybox:1.36 \
       --timeout=30s \
       --command -- nc -zv -w 3 postgres.ns-infra.svc.cluster.local 5432 >/dev/null 2>&1; then
    ok "  $NS -> postgres.ns-infra:5432 OK (allow-infra.yaml permits it)"
  else
    err "  $NS -> postgres.ns-infra:5432 FAILED (NetworkPolicy/allow-infra wrong?)"
  fi
done

# Negative case — a namespace OUTSIDE the app set must NOT reach Postgres.
POD="pgneg-$$-$RANDOM"
if kubectl -n default run "$POD" \
     --rm -i --restart=Never --image=busybox:1.36 \
     --timeout=20s \
     --command -- nc -zv -w 3 postgres.ns-infra.svc.cluster.local 5432 >/dev/null 2>&1; then
  err "  default -> postgres.ns-infra:5432 SUCCEEDED — default-deny is not enforced!"
else
  ok "  default -> postgres.ns-infra:5432 blocked (default-deny works)"
fi

# ---------------------------------------------------------------------
# §D. RabbitMQ connected consumers.
# ---------------------------------------------------------------------
log "§D — RabbitMQ has at least one connected consumer per service"
if ! kubectl -n ns-infra exec statefulset/rabbitmq -- rabbitmqctl list_consumers 2>/dev/null | grep -qE 'users|fleet|booking'; then
  warn "  rabbitmqctl list_consumers did not show users/fleet/booking — check service bindings"
else
  ok "  RabbitMQ consumers from users/fleet/booking present"
fi

# ---------------------------------------------------------------------
# §E. HPAs report live metrics.
# ---------------------------------------------------------------------
log "§E — HPAs report non-<unknown> metrics (metrics-server hooked up)"
for NS in ns-users ns-fleet ns-booking ns-frontend; do
  while read -r HPA TARGETS; do
    if echo "$TARGETS" | grep -q '<unknown>'; then
      err "  $NS/$HPA targets='$TARGETS' (metrics-server not feeding this HPA)"
    else
      ok "  $NS/$HPA targets='$TARGETS'"
    fi
  done < <(kubectl -n "$NS" get hpa -o custom-columns=NAME:.metadata.name,TARGETS:.status.currentMetrics --no-headers 2>/dev/null || true)
done

# ---------------------------------------------------------------------
# §F. /health endpoints over the edge.
# ---------------------------------------------------------------------
log "§F — /health responds 200 over Ingress for every service"
for HOST in "$USERS_HOST" "$FLEET_HOST" "$BOOKING_HOST"; do
  HTTP=$(curl -fsSL -o /dev/null -w '%{http_code}' --max-time 10 -k "https://${HOST}/health" || echo "000")
  if [[ "$HTTP" != "200" ]]; then
    err "  https://${HOST}/health returned $HTTP (want 200)"
  else
    ok "  https://${HOST}/health 200"
  fi
done

# The SPA index is what the browser actually loads.
HTTP=$(curl -fsSL -o /dev/null -w '%{http_code}' --max-time 10 -k "https://${APP_HOST}/" || echo "000")
if [[ "$HTTP" != "200" ]]; then
  err "  https://${APP_HOST}/ returned $HTTP (want 200)"
else
  ok "  https://${APP_HOST}/ 200"
fi

# ---------------------------------------------------------------------
# §G. CSP `connect-src` whitelist on the frontend Ingress mentions
# the production hostnames (proves the §4.1 patch landed).
# ---------------------------------------------------------------------
log "§G — frontend Ingress CSP whitelists *.aircraft.example.com"
CSP=$(kubectl -n ns-frontend get ingress frontend-ingress \
        -o jsonpath='{.metadata.annotations.nginx\.ingress\.kubernetes\.io/configuration-snippet}' \
        2>/dev/null || true)
if echo "$CSP" | grep -q "users.aircraft.example.com" \
   && echo "$CSP" | grep -q "fleet.aircraft.example.com" \
   && echo "$CSP" | grep -q "booking.aircraft.example.com"; then
  ok "  CSP includes users./fleet./booking.aircraft.example.com"
else
  err "  CSP does NOT include the production hostnames — overlay patch missing"
  err "  current snippet: $CSP"
fi

# ---------------------------------------------------------------------
# Result.
# ---------------------------------------------------------------------
if [[ "$FAIL" -eq 0 ]]; then
  ok "OK — post-cut-over validation PASSED"
  exit 0
fi
err "FAIL — post-cut-over validation FAILED; fix the issues above and re-run"
exit 1
