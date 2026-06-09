#!/usr/bin/env bash
# Pre-commit hook that blocks plaintext Kubernetes Secrets in staged k8s manifests, allowing only SealedSecrets or explicitly approved bootstrap exceptions.

set -euo pipefail

# Files staged for this commit, filtered to k8s manifests only.
STAGED=$(git diff --cached --name-only --diff-filter=ACM \
           | grep -E '^k8s/.*\.ya?ml$' || true)

[ -z "$STAGED" ] && exit 0

violations=0

while IFS= read -r f; do
  # Skip the SealedSecret directory itself — those files legitimately
  # look like Secret templates inside the SealedSecret.spec.template.
  case "$f" in
    k8s/sealed-secrets/*) continue ;;
  esac

  # Honour the explicit opt-in marker.
  if grep -qE '^[[:space:]]*#[[:space:]]*pre-commit:allow-plaintext-secret' "$f"; then
    continue
  fi

  # Detect a *top-level* `kind: Secret` (not nested inside template:).
  # awk handles multi-doc YAML separators (---) and resets per-block
  # state. `found` accumulates across blocks so a violation anywhere
  # in the file trips the exit code.
  if awk '
      /^---[[:space:]]*$/ { kind=""; next }
      /^kind:[[:space:]]*Secret[[:space:]]*$/ { kind="Secret" }
      /^kind:[[:space:]]*SealedSecret[[:space:]]*$/ { kind="SealedSecret" }
      /^(stringData|data):[[:space:]]*$/ {
        if (kind == "Secret") found=1
      }
      END { exit (found ? 0 : 1) }
    ' "$f"; then
    printf 'pre-commit: plaintext kind: Secret in %s\n' "$f" >&2
    violations=$((violations + 1))
  fi
done <<< "$STAGED"

if [ "$violations" -gt 0 ]; then
  cat >&2 <<EOF

Refusing commit: $violations plaintext Secret manifest(s) staged.

Phase C §C.13 requires every Secret to ship as a Bitnami SealedSecret
(k8s/sealed-secrets/). To fix:

  1. Re-seal the secret:
       kubeseal --cert /tmp/sealed-secrets-cert.pem --format yaml \\
         < my-plaintext-secret.yaml \\
         > k8s/sealed-secrets/<svc>-sealedsecret.yaml
  2. Delete the plaintext file from the staging area.
  3. Re-run git commit.

If the file is a controlled bootstrap baseline and review has signed off,
add a top-level
   # pre-commit:allow-plaintext-secret
marker and re-stage.
EOF
  exit 1
fi

exit 0
