#!/usr/bin/env bash
# =====================================================================
# scripts/seal-secrets.sh — Phase C §C.13 operator tool
#
# Re-encrypts every SealedSecret under k8s/sealed-secrets/ using the
# live cluster's controller public key. Plaintext sources are read
# from the operator's vault directory (env: AIRCRAFT_VAULT_DIR), one
# YAML per logical secret, named to match the SealedSecret it backs:
#
#   ${AIRCRAFT_VAULT_DIR}/users-secret.yaml
#   ${AIRCRAFT_VAULT_DIR}/fleet-secret.yaml
#   ${AIRCRAFT_VAULT_DIR}/booking-secret.yaml
#   ${AIRCRAFT_VAULT_DIR}/postgres-secret.yaml
#   ${AIRCRAFT_VAULT_DIR}/registry-auth.yaml
#   ${AIRCRAFT_VAULT_DIR}/local-registry-ns-{users,fleet,booking,frontend}.yaml
#
# Each vault file is an ordinary `kind: Secret` YAML with the real
# plaintext. The script feeds it to `kubeseal --cert <file>` and
# overwrites the corresponding committed SealedSecret with the result.
#
# Prerequisites:
#   * kubectl context pointing at the target cluster.
#   * kubeseal CLI installed (matches controller minor version).
#   * AIRCRAFT_VAULT_DIR exported, pointing at a path on local disk
#     that is NOT inside this repo.
# =====================================================================
set -euo pipefail

: "${AIRCRAFT_VAULT_DIR:?set AIRCRAFT_VAULT_DIR to your plaintext vault path}"

if ! command -v kubeseal >/dev/null 2>&1; then
  echo "kubeseal CLI not found in PATH" >&2
  exit 1
fi

CERT=$(mktemp)
trap 'rm -f "$CERT"' EXIT

echo "[seal] fetching controller public cert"
kubeseal --controller-namespace kube-system \
         --controller-name      sealed-secrets-controller \
         --fetch-cert > "$CERT"

reseal() {
  local plaintext=$1 out=$2
  if [ ! -f "$plaintext" ]; then
    echo "[seal] SKIP — vault file missing: $plaintext" >&2
    return 0
  fi
  echo "[seal] $plaintext -> $out"
  kubeseal --cert "$CERT" --format yaml < "$plaintext" > "$out"
}

# App secrets ----------------------------------------------------------
reseal "$AIRCRAFT_VAULT_DIR/users-secret.yaml"        k8s/sealed-secrets/users-sealedsecret.yaml
reseal "$AIRCRAFT_VAULT_DIR/fleet-secret.yaml"        k8s/sealed-secrets/fleet-sealedsecret.yaml
reseal "$AIRCRAFT_VAULT_DIR/booking-secret.yaml"      k8s/sealed-secrets/booking-sealedsecret.yaml
reseal "$AIRCRAFT_VAULT_DIR/postgres-secret.yaml"     k8s/sealed-secrets/postgres-sealedsecret.yaml

# Registry credentials + per-namespace imagePullSecrets ---------------
# These are concatenated into a single committed file; build it by
# stitching individual sealed outputs together.
TMP=$(mktemp -d); trap 'rm -rf "$TMP" "$CERT"' EXIT
reseal "$AIRCRAFT_VAULT_DIR/registry-auth.yaml"                "$TMP/registry-auth.yaml"
reseal "$AIRCRAFT_VAULT_DIR/local-registry-ns-users.yaml"      "$TMP/lr-users.yaml"
reseal "$AIRCRAFT_VAULT_DIR/local-registry-ns-fleet.yaml"      "$TMP/lr-fleet.yaml"
reseal "$AIRCRAFT_VAULT_DIR/local-registry-ns-booking.yaml"    "$TMP/lr-booking.yaml"
reseal "$AIRCRAFT_VAULT_DIR/local-registry-ns-frontend.yaml"   "$TMP/lr-frontend.yaml"

{
  cat "$TMP/registry-auth.yaml"
  for f in lr-users lr-fleet lr-booking lr-frontend; do
    printf -- '---\n'
    cat "$TMP/$f.yaml"
  done
} > k8s/sealed-secrets/registry-auth-sealedsecret.yaml

echo "[seal] DONE. Inspect git diff k8s/sealed-secrets/ and commit."
