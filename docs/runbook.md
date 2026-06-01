# Operator Runbook

> Living document tracking operational procedures for the AircraftSaaS
> Kubernetes deployment. Phase B (CI/CD) populates the **rollback**
> section below. Phase C will add cert rotation, RBAC audit, and
> SealedSecret resealing. Phase E will add the Postgres restore drill
> and the JWT key rotation procedure.

---

## 1. Rollback a Service Deployment

Every per-service workflow (`.github/workflows/ci-<svc>.yaml`) pushes
two tags to the in-cluster registry for the image it built:

| Tag | Mutability | Used by |
|---|---|---|
| `<git_sha>` (12-char short SHA) | **Immutable** | `kubectl set image` during rollout — every committed SHA is permanently recoverable |
| `latest` | Mutable | Humans inspecting `kubectl describe` |

A bad deploy is rolled back by re-pointing the Deployment at a
previous SHA. Two equivalent procedures are documented; pick whichever
matches the failure mode.

### 1.1 Fast path — `kubectl rollout undo`

If the bad rollout is **still the most recent change to the Deployment's
ReplicaSet revision history**, Kubernetes already remembers the
previous image reference. No registry lookup needed.

```bash
# Substitute one of: users-service / fleet-service / booking-service / vue-frontend
SVC=users-service
NS=ns-${SVC%-service}     # ns-users, ns-fleet, ns-booking; for vue-frontend use ns-frontend

# 1. Inspect history (each row is a ReplicaSet revision).
kubectl -n "$NS" rollout history deployment/"$SVC"

# 2. Undo. Without --to-revision this targets the immediately previous
#    revision; pass --to-revision=<N> from the history table for a
#    specific roll-back point.
kubectl -n "$NS" rollout undo deployment/"$SVC"

# 3. Wait for the rollback to converge (<= 120s same as CI).
kubectl -n "$NS" rollout status deployment/"$SVC" --timeout=120s

# 4. Confirm the new image tag (should match a known-good <sha>).
kubectl -n "$NS" get deployment "$SVC" -o jsonpath='{.spec.template.spec.containers[0].image}{"\n"}'
```

**Caveats**

- `revisionHistoryLimit` on the Deployment caps how far `undo` can
  reach. Defaults to 10; check it before relying on this path.
- `rollout undo` does NOT replay Migration Jobs. If the bad release
  introduced a DB migration that the old code can't read, you must
  also restore the database (Phase E will document the Postgres PITR
  drill).

### 1.2 Pinned path — `kubectl set image` to a specific SHA

If the bad rollout was already followed by other commits (so the
history limit may have rotated the desired SHA out), or you want to
roll back to a release older than `revisionHistoryLimit`, pin the
image reference directly.

```bash
SVC=users-service
NS=ns-${SVC%-service}
CONTAINER=$SVC                # container name == service name (see deployment.yaml)
KNOWN_GOOD_SHA=abc123def456   # 12-char short SHA of the last green commit

# 1. Confirm the tag exists in the registry (HEAD against /v2/<repo>/manifests/<tag>).
kubectl -n ns-ci exec deploy/github-runner -c dind -- \
  curl -fsS -I \
    -u "$REGISTRY_USERNAME:$REGISTRY_PASSWORD" \
    "http://registry.ns-registry.svc.cluster.local:5000/v2/${SVC}/manifests/${KNOWN_GOOD_SHA}"

# 2. Pin the Deployment to it.
kubectl -n "$NS" set image \
  deployment/"$SVC" \
  "${CONTAINER}=registry.ns-registry.svc.cluster.local:5000/${SVC}:${KNOWN_GOOD_SHA}"

# 3. Wait + smoke test (mirrors what the CI workflow does on forward roll-outs).
kubectl -n "$NS" rollout status deployment/"$SVC" --timeout=120s
kubectl run smoke-rollback-$$ \
  --rm -i --restart=Never --namespace "$NS" \
  --image=curlimages/curl:8.10.1 \
  --command -- \
  curl -fsS --max-time 10 \
    "http://${SVC}.${NS}.svc.cluster.local:8080/health"
```

### 1.3 Verifying the rollback

After either path, the rollback is considered successful only when:

1. `kubectl rollout status` returned 0 within the 120s budget.
2. All Pods of the Deployment report `READY`.
3. The smoke test against `/health` returns HTTP 200.
4. The Ingress-level happy-path (Phase E `tests/e2e/booking-happy-path.sh`)
   passes from outside the cluster.

If any of those fail, escalate to a Postgres restore (Phase E) — the
schema may have drifted past the rolled-back image's expectations.

### 1.4 What rollback does NOT do

- **Does not revert Git.** A rolled-back Deployment still references
  the immutable SHA tag in the registry; the next merge to `main`
  re-triggers the per-service CI workflow and re-rolls forward. If
  the bug is in `main`, revert the offending commit on GitHub first,
  then let CI re-roll forward to the reverted SHA.
- **Does not rotate Secrets.** Any leaked secret remains valid.
  Rotation is a Phase C responsibility (SealedSecrets).
- **Does not roll back ConfigMaps or NetworkPolicies.** Kustomize
  manifests are applied separately from CI image pushes; if a bad
  config change is also in flight, revert it on Git and re-apply
  `kubectl apply -k k8s/overlays/<env>`.

---

## 2. Manually re-trigger a CI pipeline

Each `.github/workflows/ci-*.yaml` carries a `workflow_dispatch:`
trigger. From the GitHub Actions UI:

> Actions → (workflow) → Run workflow → choose `main` → Run.

The `ci-shared.yaml` workflow's `workflow_dispatch:` is the supported
way to rebuild all three backend images without committing.

---

## 3. Self-hosted runner — operational notes

The runner Deployment lives at [`k8s/ci/runner-deployment.yaml`](../k8s/ci/runner-deployment.yaml:1).
It is single-replica and ephemeral (each job re-registers with GitHub).

| Symptom | Action |
|---|---|
| Jobs stuck in "Queued" forever | `kubectl -n ns-ci get pods`; if no `github-runner-*` pod is Ready, check the runner container logs for token expiry. Rotate `RUNNER_TOKEN` in [`k8s/ci/runner-secret.yaml`](../k8s/ci/runner-secret.yaml:1) and `kubectl -n ns-ci rollout restart deploy/github-runner`. |
| `docker push` fails with "x509: certificate signed by unknown authority" | The DinD sidecar must declare `--insecure-registry=registry.ns-registry.svc.cluster.local:5000`. Confirm the flag is present in [`k8s/ci/runner-deployment.yaml`](../k8s/ci/runner-deployment.yaml:1) and the Pod has been recreated since the last edit. |
| `kubectl` from the runner returns `Forbidden` | The `ci-deployer` SA is scoped to ns-{users,fleet,booking,frontend}. If a workflow legitimately needs a new namespace, add a Role + RoleBinding to [`k8s/ci/rbac.yaml`](../k8s/ci/rbac.yaml:1) — do NOT escalate to cluster-admin. |

---

## 4. Future sections (placeholders — populated in later phases)

- **Postgres restore from PITR snapshot** — Phase E, §22.
- **JWT signing key rotation** — Phase C, §5.2.
- **cert-manager certificate renewal failure** — Phase C, §5.4.
- **Network-policy chaos test interpretation** — Phase E, §23.
