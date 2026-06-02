# Security Validation — Phase C

> Companion to [`plans/deploy.md`](../plans/deploy.md:1) §C (Phase C: Security
> Hardening). This document records the **mechanisms** delivered, the
> **evidence harness** that proves they work, and how to **re-run** every
> check against a live cluster.

---

## 1. Scope (what Phase C delivered)

| § | Mechanism | Code path |
|---|---|---|
| C.12 | Default-deny NetworkPolicy in every app namespace | [`k8s/network-policies/default-deny.yaml`](../k8s/network-policies/default-deny.yaml:1) |
| C.12 | Per-namespace allow rules (incl. new `ns-frontend`/`ns-registry`) | [`k8s/network-policies/allow-frontend.yaml`](../k8s/network-policies/allow-frontend.yaml:1), [`k8s/network-policies/allow-registry.yaml`](../k8s/network-policies/allow-registry.yaml:1) |
| C.12 | Chaos test (label assertion + allow path + deny path) | [`tests/k8s/network-policy.sh`](../tests/k8s/network-policy.sh:1) |
| C.13 | Bitnami SealedSecrets controller (kube-system) | [`k8s/sealed-secrets/controller.yaml`](../k8s/sealed-secrets/controller.yaml:1) |
| C.13 | SealedSecret replacements for every app/infra/registry Secret | [`k8s/sealed-secrets/`](../k8s/sealed-secrets/) |
| C.13 | Pre-commit hook + CI scanner blocking plaintext `kind: Secret` | [`scripts/git-hooks/pre-commit-no-plaintext-secrets.sh`](../scripts/git-hooks/pre-commit-no-plaintext-secrets.sh:1), [`.github/workflows/ci-security.yaml`](../.github/workflows/ci-security.yaml:1) |
| C.14 | Per-service runtime ServiceAccounts (no RoleBindings) | [`k8s/rbac/runtime-serviceaccounts.yaml`](../k8s/rbac/runtime-serviceaccounts.yaml:1) |
| C.14 | Strategic-merge patch: `serviceAccountName`, `automountServiceAccountToken: false`, `readOnlyRootFilesystem: true` | [`k8s/rbac/deployment-hardening-patch.yaml`](../k8s/rbac/deployment-hardening-patch.yaml:1) |
| C.14 | Least-privilege `ci-deployer` Role/RoleBinding audit | [`k8s/ci/rbac.yaml`](../k8s/ci/rbac.yaml:1) |
| C.15 | cert-manager install marker + Let's Encrypt staging/prod ClusterIssuer | [`k8s/cert-manager/`](../k8s/cert-manager/) |
| C.15 | Ingress TLS sections + HSTS / CSP / X-Frame-Options annotations | [`k8s/gateway/ingress.yaml`](../k8s/gateway/ingress.yaml:1) |
| C.16 | Trivy image scan (HIGH/CRITICAL fail-build) in the build/push composite | [`.github/actions/build-and-push/action.yaml`](../.github/actions/build-and-push/action.yaml:1) |

---

## 2. NetworkPolicy chaos test

[`tests/k8s/network-policy.sh`](../tests/k8s/network-policy.sh:1) runs three
assertions against the live cluster:

1. **Label assertion** — every namespace MUST carry `name=ns-xxx`.
   Without that label, `namespaceSelector` matchers in every
   `allow-*.yaml` silently fail and default-deny isolates the
   namespace from its own dependencies.
2. **Allow path** — a Pod labeled `app=booking-service` inside
   `ns-booking` MUST be able to TCP-connect to
   `fleet-service.ns-fleet.svc.cluster.local:8080`.
3. **Deny path (chaos)** — a Pod inside `ns-fleet` that does NOT
   carry `app=fleet-service` MUST NOT be able to reach
   `postgres.ns-infra:5432`. The default-deny is what blocks this.

Re-run:

```bash
bash tests/k8s/network-policy.sh
```

CI integration: [`.github/workflows/ci-security.yaml`](../.github/workflows/ci-security.yaml:1)
runs the script on every push to `main` and every PR touching
`k8s/**`.

---

## 3. Secrets posture (Phase C §C.13)

| Layer | Posture |
|---|---|
| At rest in Git | `kind: SealedSecret` only. The Bitnami controller in `kube-system` is the sole decryption point. |
| At rest in etcd | Standard kube-secret encryption (cluster-level — out of scope for this repo). |
| In containers | Mounted via `envFrom: secretRef:` only; never duplicated into ConfigMaps. |
| On developer machines | Plaintext sources live in the operator's vault (`$AIRCRAFT_VAULT_DIR`); never committed. |

**Re-validate**:

```bash
# Repo-wide scan (mirrors the pre-commit hook).
.github/workflows/ci-security.yaml::plaintext-secret-scan
# (locally:)
find k8s -name '*.yaml' -exec \
  awk '
    /^---[[:space:]]*$/ { kind=""; has_data=0; next }
    /^kind:[[:space:]]*Secret[[:space:]]*$/ { kind="Secret" }
    /^(stringData|data):[[:space:]]*$/ {
      if (kind == "Secret") { print FILENAME; exit 1 }
    }' {} +
```

**Rotation**:

```bash
export AIRCRAFT_VAULT_DIR=/path/to/operator/vault
./scripts/seal-secrets.sh   # re-encrypts every SealedSecret in-place
git diff k8s/sealed-secrets/
git add k8s/sealed-secrets && git commit -m "rotate sealed secrets"
```

---

## 4. RBAC audit (Phase C §C.14)

### 4.1 App pods

```bash
kubectl -n ns-users   get sa users-runtime    -o yaml
kubectl -n ns-fleet   get sa fleet-runtime    -o yaml
kubectl -n ns-booking get sa booking-runtime  -o yaml
kubectl -n ns-frontend get sa vue-frontend-runtime -o yaml
```

Expected for every runtime SA:

* `automountServiceAccountToken: false` (set on the SA + on the Deployment).
* `imagePullSecrets: [{ name: local-registry }]`.
* No RoleBindings reference it (cross-check `kubectl get rolebindings,clusterrolebindings -A -o json | jq '.items[].subjects'`).

Confirm the pod really has no API token:

```bash
kubectl -n ns-users exec deploy/users-service -- \
  ls /var/run/secrets/kubernetes.io/serviceaccount/ 2>&1 \
  | grep -q "No such" && echo OK || echo FAIL
```

### 4.2 CI deployer

The `ci-deployer` SA (`ns-ci`) has Roles bound ONLY into
`ns-users / ns-fleet / ns-booking / ns-frontend` ([`k8s/ci/rbac.yaml`](../k8s/ci/rbac.yaml:1)).
Verify scope:

```bash
kubectl auth can-i --as=system:serviceaccount:ns-ci:ci-deployer \
  --namespace=ns-infra patch deployment   # → no
kubectl auth can-i --as=system:serviceaccount:ns-ci:ci-deployer \
  --namespace=ns-users patch deployment   # → yes
kubectl auth can-i --as=system:serviceaccount:ns-ci:ci-deployer \
  '*' '*' --all-namespaces                 # → no (no cluster-admin)
```

---

## 5. Ingress TLS (Phase C §C.15)

### 5.1 OpenNebula (Let's Encrypt staging → prod)

```bash
kubectl -n ns-users get certificate users-tls -o yaml | yq '.status.conditions'
kubectl -n cert-manager logs deploy/cert-manager | tail -50
```

### 5.2 Header check

```bash
curl -ksI https://app.aircraft.example.com/ | grep -iE \
  '^(strict-transport-security|x-frame-options|x-content-type-options|content-security-policy|referrer-policy):'
```

Expected: HSTS (max-age=31536000), X-Frame-Options DENY, nosniff,
strict CSP, strict-origin Referrer-Policy.

---

## 6. Image scanning (Phase C §C.16)

Every CI run of [`ci-users.yaml`](../.github/workflows/ci-users.yaml:1) /
`ci-fleet` / `ci-booking` / `ci-frontend` invokes
[`.github/actions/build-and-push/action.yaml`](../.github/actions/build-and-push/action.yaml:1)
which:

1. Builds + pushes the image to the in-cluster registry.
2. Runs `trivy image --severity HIGH,CRITICAL --exit-code 1 --ignore-unfixed`
   against the pushed reference.
3. Emits a SARIF report (`trivy-reports/<service>.sarif`) regardless
   of pass/fail.

Override (emergency only, documented in [`docs/runbook.md`](runbook.md:1)):

```yaml
- uses: ./.github/actions/build-and-push
  with:
    service: users-service
    # ...
    trivy-skip: "true"   # MUST file an issue justifying the bypass.
```

Re-run locally against a deployed image:

```bash
trivy image --insecure \
  --severity HIGH,CRITICAL --ignore-unfixed \
  registry.ns-registry.svc.cluster.local:5000/users-service:latest
```

---

## 7. Sign-off checklist

Before declaring Phase C done on a cluster, all of the below must
hold:

- [ ] `kubectl get networkpolicy -A` lists default-deny + allow-* in
      every app namespace (incl. `ns-frontend`, `ns-registry`).
- [ ] `bash tests/k8s/network-policy.sh` exits 0.
- [ ] `kubectl get sealedsecret -A` returns the full set, each with
      `status: { observedGeneration: <current> }`.
- [ ] No `kind: Secret` with `stringData` survives in Git under
      `k8s/**` (CI `plaintext-secret-scan` job green).
- [ ] Every app Deployment shows
      `automountServiceAccountToken: false` and a `*-runtime` SA.
- [ ] `kubectl get certificate -A` shows `Ready=True` for every host.
- [ ] curl against the Ingress prints HSTS/CSP/X-Frame-Options.
- [ ] Latest CI run of `ci-users` shows a green Trivy step.
