#!/usr/bin/env bash
# =====================================================================
# scripts/bootstrap-cluster.sh — one-time cluster bootstrap
#
# Mirrors clusterrun.md §2, §4, §5 in a single idempotent script.
# Run this ONCE per Kubernetes cluster (or whenever a cluster-scoped
# controller / SealedSecrets controller key has to be re-established).
#
# Stages:
#   1. Sanity: kubectl context + required CLIs
#   2. Install in-cluster Docker registry (k8s/registry)
#   3. Install cert-manager (k8s/cert-manager)
#   4. Install bitnami sealed-secrets controller
#   5. (Optional) re-seal Secrets from $AIRCRAFT_VAULT_DIR via
#      scripts/seal-secrets.sh — only when AIRCRAFT_VAULT_DIR is set
#   6. Apply SealedSecrets to materialise real Secrets
#
# Flags:
#   --skip-registry          Do not (re)apply k8s/registry
#   --skip-cert-manager      Do not (re)apply k8s/cert-manager
#   --skip-sealed-controller Do not (re)apply the SealedSecrets controller
#   --skip-reseal            Do not call scripts/seal-secrets.sh even if
#                            AIRCRAFT_VAULT_DIR is set
#   --skip-sealed-apply      Do not apply k8s/sealed-secrets
#   --dry-run                Print every kubectl/kustomize command but
#                            do not execute it
#
# Environment:
#   KUBECONFIG               Required — must point at the target cluster
#   AIRCRAFT_VAULT_DIR       Optional — if set AND --skip-reseal not
#                            given, scripts/seal-secrets.sh is invoked
# =====================================================================
set -euo pipefail

# --- Configuration ----------------------------------------------------
SKIP_REGISTRY=0
SKIP_CERT_MANAGER=0
SKIP_SEALED_CONTROLLER=0
SKIP_RESEAL=0
SKIP_SEALED_APPLY=0
DRY_RUN=0

SCRIPT_DIR="$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
REPO_ROOT="$( cd -- "${SCRIPT_DIR}/.." &> /dev/null && pwd )"

# --- Argument parsing -------------------------------------------------
for arg in "$@"; do
  case "$arg" in
    --skip-registry)          SKIP_REGISTRY=1 ;;
    --skip-cert-manager)      SKIP_CERT_MANAGER=1 ;;
    --skip-sealed-controller) SKIP_SEALED_CONTROLLER=1 ;;
    --skip-reseal)            SKIP_RESEAL=1 ;;
    --skip-sealed-apply)      SKIP_SEALED_APPLY=1 ;;
    --dry-run)                DRY_RUN=1 ;;
    -h|--help)
      sed -n '2,35p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown flag: $arg" >&2
      exit 2
      ;;
  esac
done

# --- Helpers ----------------------------------------------------------
log()  { printf '\033[1;34m[bootstrap]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[bootstrap]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[bootstrap]\033[0m %s\n' "$*" >&2; exit 1; }

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

# --- 1. Sanity --------------------------------------------------------
log "STAGE 1/6 — Sanity checks"

require_cli kubectl
# kustomize CLI is optional — modern `kubectl -k` bundles it.
if ! command -v kustomize >/dev/null 2>&1; then
  warn "kustomize CLI not found; relying on 'kubectl -k' (built-in)"
fi

if [[ -z "${KUBECONFIG:-}" ]]; then
  warn "KUBECONFIG is not set — relying on default kubeconfig location"
fi

if [[ "$DRY_RUN" -eq 0 ]]; then
  if ! kubectl version --request-timeout=5s >/dev/null 2>&1; then
    die "kubectl cannot reach the API server — check KUBECONFIG / cluster availability"
  fi
  NODE_COUNT=$(kubectl get nodes --no-headers 2>/dev/null | wc -l | tr -d ' ')
  log "cluster reachable; $NODE_COUNT node(s) registered"
fi

# --- 2. Registry ------------------------------------------------------
if [[ "$SKIP_REGISTRY" -eq 0 ]]; then
  log "STAGE 2/6 — Installing in-cluster Docker registry (k8s/registry)"
  run kubectl apply -k "${REPO_ROOT}/k8s/registry"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    run kubectl -n ns-registry rollout status deploy/registry --timeout=180s
  fi
else
  log "STAGE 2/6 — SKIPPED (--skip-registry)"
fi

# --- 3. cert-manager --------------------------------------------------
if [[ "$SKIP_CERT_MANAGER" -eq 0 ]]; then
  log "STAGE 3/6 — Installing cert-manager (k8s/cert-manager)"
  run kubectl apply -k "${REPO_ROOT}/k8s/cert-manager"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    # cert-manager ships CRDs in the same kustomization; rollout may need
    # a moment before the webhook reports Ready.
    run kubectl -n cert-manager rollout status deploy/cert-manager         --timeout=180s || true
    run kubectl -n cert-manager rollout status deploy/cert-manager-webhook --timeout=180s || true
    run kubectl -n cert-manager rollout status deploy/cert-manager-cainjector --timeout=180s || true
  fi
else
  log "STAGE 3/6 — SKIPPED (--skip-cert-manager)"
fi

# --- 4. SealedSecrets controller --------------------------------------
if [[ "$SKIP_SEALED_CONTROLLER" -eq 0 ]]; then
  log "STAGE 4/6 — Installing SealedSecrets controller"
  run kubectl apply -f "${REPO_ROOT}/k8s/sealed-secrets/controller.yaml"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    run kubectl -n kube-system rollout status deploy/sealed-secrets-controller --timeout=180s
  fi
else
  log "STAGE 4/6 — SKIPPED (--skip-sealed-controller)"
fi

# --- 5. Re-seal Secrets (optional) ------------------------------------
if [[ "$SKIP_RESEAL" -eq 0 && -n "${AIRCRAFT_VAULT_DIR:-}" ]]; then
  log "STAGE 5/6 — Re-sealing Secrets from \$AIRCRAFT_VAULT_DIR"
  require_cli kubeseal
  if [[ ! -x "${SCRIPT_DIR}/seal-secrets.sh" ]]; then
    warn "scripts/seal-secrets.sh not executable; attempting chmod +x"
    run chmod +x "${SCRIPT_DIR}/seal-secrets.sh"
  fi
  run "${SCRIPT_DIR}/seal-secrets.sh"
else
  if [[ "$SKIP_RESEAL" -eq 1 ]]; then
    log "STAGE 5/6 — SKIPPED (--skip-reseal)"
  else
    log "STAGE 5/6 — SKIPPED (AIRCRAFT_VAULT_DIR not set)"
    warn "committed k8s/sealed-secrets/*.yaml encryptedData blobs will only decrypt"
    warn "if they were sealed against the CURRENT controller key — otherwise re-run"
    warn "with AIRCRAFT_VAULT_DIR pointing at your plaintext vault"
  fi
fi

# --- 6. Apply SealedSecrets -------------------------------------------
if [[ "$SKIP_SEALED_APPLY" -eq 0 ]]; then
  log "STAGE 6/6 — Applying k8s/sealed-secrets"
  run kubectl apply -k "${REPO_ROOT}/k8s/sealed-secrets"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    sleep 5
    log "materialised Secrets:"
    kubectl get secret -A 2>/dev/null \
      | grep -E '(users|fleet|booking|postgres|registry|local-registry)-' || true
  fi
else
  log "STAGE 6/6 — SKIPPED (--skip-sealed-apply)"
fi

log "DONE — cluster bootstrapped. Next: scripts/deploy-apps.sh"
