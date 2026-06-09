#!/usr/bin/env bash
# Deploys the release by building, pushing, applying, pinning, migrating, and smoke-testing all app images against the cluster.

set -euo pipefail

# --- Defaults & Configuration -----------------------------------------
SCRIPT_DIR="$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
REPO_ROOT="$( cd -- "${SCRIPT_DIR}/.." &> /dev/null && pwd )"

TAG=""
EDGE_HOST="${EDGE_HOST:-aircraft.example.com}"
REGISTRY="localhost:5000"
SKIP_BUILD=0
SKIP_APPLY=0
SKIP_PIN=0
SKIP_MIGRATE=0
SKIP_SMOKE=0
DRY_RUN=0

OVERLAY_PATH="${REPO_ROOT}/k8s/overlays/opennebula"
COMPOSE_CTX="${REPO_ROOT}/AircraftSaaS"
FRONTEND_CTX="${REPO_ROOT}/frontend_vue"

# Service → (namespace, deployment-name, container-name, image-repo, dockerfile)
# Backend services (built from AircraftSaaS context):
BACKEND_SERVICES=("users" "fleet" "booking")

# --- Argument parsing -------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)         TAG="$2"; shift 2 ;;
    --edge-host)   EDGE_HOST="$2"; shift 2 ;;
    --registry)    REGISTRY="$2"; shift 2 ;;
    --skip-build)  SKIP_BUILD=1;   shift ;;
    --skip-apply)  SKIP_APPLY=1;   shift ;;
    --skip-pin)    SKIP_PIN=1;     shift ;;
    --skip-migrate) SKIP_MIGRATE=1; shift ;;
    --skip-smoke)  SKIP_SMOKE=1;   shift ;;
    --dry-run)     DRY_RUN=1;      shift ;;
    -h|--help)
      sed -n '2,35p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown flag: $1" >&2
      exit 2
      ;;
  esac
done

# --- Helpers ----------------------------------------------------------
log()  { printf '\033[1;34m[deploy]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[deploy]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[deploy]\033[0m %s\n' "$*" >&2; exit 1; }

run() {
  if [[ "$DRY_RUN" -eq 1 ]]; then
    printf '  $ %s\n' "$*"
  else
    eval "$@"
  fi
}

require_cli() {
  command -v "$1" >/dev/null 2>&1 || die "missing required CLI: $1"
}

cleanup() {
  if [[ -n "${PF_PID:-}" ]] && kill -0 "$PF_PID" 2>/dev/null; then
    log "stopping registry port-forward (PID $PF_PID)"
    kill "$PF_PID" 2>/dev/null || true
    wait "$PF_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

# --- 1. Sanity --------------------------------------------------------
log "STAGE 1/6 — Sanity checks"

require_cli kubectl
# kustomize is only needed in stage 5 (to render the overlay for the
# migration Jobs). Defer the hard requirement until then.

if [[ -z "$TAG" ]]; then
  if command -v git >/dev/null 2>&1 && git -C "$REPO_ROOT" rev-parse --short HEAD >/dev/null 2>&1; then
    TAG="$(git -C "$REPO_ROOT" rev-parse --short HEAD)"
    log "TAG defaulted from git HEAD: $TAG"
  else
    die "no --tag supplied and git HEAD unavailable"
  fi
fi

log "TAG=$TAG  EDGE_HOST=$EDGE_HOST  REGISTRY=$REGISTRY"

if [[ "$DRY_RUN" -eq 0 ]]; then
  if ! kubectl version --request-timeout=5s >/dev/null 2>&1; then
    die "kubectl cannot reach the API server — check KUBECONFIG"
  fi
fi

# --- 2. Build & push --------------------------------------------------
if [[ "$SKIP_BUILD" -eq 0 ]]; then
  log "STAGE 2/6 — Building and pushing images @ ${TAG}"
  require_cli docker

  # Start a port-forward to the in-cluster registry when REGISTRY points
  # at localhost (default). Skip otherwise — caller has set up routing.
  if [[ "$REGISTRY" == localhost:* ]]; then
    log "starting port-forward to ns-registry/registry:5000"
    if [[ "$DRY_RUN" -eq 0 ]]; then
      kubectl -n ns-registry port-forward svc/registry 5000:5000 >/dev/null 2>&1 &
      PF_PID=$!
      sleep 3
      if ! kill -0 "$PF_PID" 2>/dev/null; then
        die "port-forward to ns-registry/registry failed — is the registry installed?"
      fi
    fi
  fi

  for SVC in "${BACKEND_SERVICES[@]}"; do
    case "$SVC" in
      users)   PASCAL="Users"   ;;
      fleet)   PASCAL="Fleet"   ;;
      booking) PASCAL="Booking" ;;
    esac
    DOCKERFILE="Services/${PASCAL}.WebHost/Dockerfile"
    IMAGE="${REGISTRY}/${SVC}-service:${TAG}"
    log "build+push ${IMAGE}"
    run docker buildx build \
        --platform linux/amd64 \
        -f "${COMPOSE_CTX}/${DOCKERFILE}" \
        -t "${IMAGE}" \
        --push \
        "${COMPOSE_CTX}"
  done

  FRONT_IMAGE="${REGISTRY}/vue-frontend:${TAG}"
  log "build+push ${FRONT_IMAGE}"
  run docker buildx build \
      --platform linux/amd64 \
      -f "${FRONTEND_CTX}/Dockerfile" \
      -t "${FRONT_IMAGE}" \
      --push \
      "${FRONTEND_CTX}"

  # Tear down the port-forward early so subsequent kubectl calls don't
  # contend with the local 5000 socket.
  if [[ -n "${PF_PID:-}" ]]; then
    kill "$PF_PID" 2>/dev/null || true
    wait "$PF_PID" 2>/dev/null || true
    unset PF_PID
  fi
else
  log "STAGE 2/6 — SKIPPED (--skip-build)"
fi

# --- 3. Apply overlay -------------------------------------------------
if [[ "$SKIP_APPLY" -eq 0 ]]; then
  log "STAGE 3/6 — kubectl apply -k ${OVERLAY_PATH}"
  run kubectl apply -k "${OVERLAY_PATH}"
else
  log "STAGE 3/6 — SKIPPED (--skip-apply)"
fi

# In-cluster image pull uses the cluster-internal registry FQDN, NOT
# the workstation-side ${REGISTRY} host that was used to push.
INTERNAL_REG="registry.ns-registry.svc.cluster.local:5000"

# --- 4. Pin image tags ------------------------------------------------
if [[ "$SKIP_PIN" -eq 0 ]]; then
  log "STAGE 4/6 — Pinning image tags to ${TAG}"
  for SVC in "${BACKEND_SERVICES[@]}"; do
    run kubectl -n "ns-${SVC}" set image \
        "deployment/${SVC}-service" \
        "${SVC}-service=${INTERNAL_REG}/${SVC}-service:${TAG}"
  done
  run kubectl -n ns-frontend set image \
      deployment/vue-frontend \
      "vue-frontend=${INTERNAL_REG}/vue-frontend:${TAG}"
else
  log "STAGE 4/6 — SKIPPED (--skip-pin)"
fi

# --- 5. Re-run migration Jobs at the new tag --------------------------
if [[ "$SKIP_MIGRATE" -eq 0 ]]; then
  log "STAGE 5/6 — Re-running migration Jobs @ ${TAG}"
  require_cli yq
  RENDERED="$(mktemp)"
  trap 'rm -f "$RENDERED"; cleanup' EXIT

  log "rendering overlay to ${RENDERED}"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    kustomize build "${OVERLAY_PATH}" > "${RENDERED}"
  fi

  for SVC in "${BACKEND_SERVICES[@]}"; do
    JOB_NAME="${SVC}-migrate"
    NS="ns-${SVC}"
    log "delete + re-apply Job ${NS}/${JOB_NAME}"
    run kubectl -n "${NS}" delete job "${JOB_NAME}" --ignore-not-found

    if [[ "$DRY_RUN" -eq 0 ]]; then
      yq "select(.kind == \"Job\" and .metadata.name == \"${JOB_NAME}\")" "${RENDERED}" \
        | sed "s|:latest|:${TAG}|g" \
        | kubectl apply -f -
      kubectl -n "${NS}" wait --for=condition=complete \
          "job/${JOB_NAME}" --timeout=300s \
        || die "migration Job ${NS}/${JOB_NAME} did not complete in 300s"
    fi
  done
else
  log "STAGE 5/6 — SKIPPED (--skip-migrate)"
fi

# --- 6. Smoke test ----------------------------------------------------
if [[ "$SKIP_SMOKE" -eq 0 ]]; then
  log "STAGE 6/6 — Smoke tests"

  log "waiting for Deployment rollouts"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    for SVC in "${BACKEND_SERVICES[@]}"; do
      kubectl -n "ns-${SVC}" rollout status "deployment/${SVC}-service" --timeout=300s
    done
    kubectl -n ns-frontend rollout status deployment/vue-frontend --timeout=300s
  fi

  log "ingress health probes via ${EDGE_HOST}"
  if [[ "$DRY_RUN" -eq 0 ]] && command -v curl >/dev/null 2>&1; then
    for h in users fleet booking app; do
      printf '  %s.%s: ' "$h" "$EDGE_HOST"
      curl -k -s -o /dev/null -w "%{http_code}\n" --max-time 5 \
          "https://${h}.${EDGE_HOST}/healthz" \
        || curl -k -s -o /dev/null -w "%{http_code}\n" --max-time 5 \
            "https://${h}.${EDGE_HOST}/" \
        || echo "unreachable"
    done
  fi

  log "certificate status"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    kubectl get certificate -A 2>/dev/null || true
  fi
else
  log "STAGE 6/6 — SKIPPED (--skip-smoke)"
fi

log "DONE — release ${TAG} deployed to cluster (edge ${EDGE_HOST})"
