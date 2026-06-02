# SealedSecrets — Phase C §C.13

Implements the secrets-management slice of [`plans/deploy.md`](../../plans/deploy.md:255) §5.2:

> Replace plaintext-checked-in `k8s/users/secret.yaml` (and siblings)
> with Bitnami SealedSecrets. Install the `sealed-secrets-controller`
> in `kube-system`. Convert every `Secret` to a `SealedSecret`
> committed to Git; the controller decrypts in-cluster.

## How this directory is wired

| File | Purpose |
|---|---|
| [`controller.yaml`](controller.yaml) | The Bitnami sealed-secrets-controller Deployment + RBAC + CRD, pinned to a release tag. |
| [`users-sealedsecret.yaml`](users-sealedsecret.yaml) | Replaces [`k8s/users/secret.yaml`](../users/secret.yaml). |
| [`fleet-sealedsecret.yaml`](fleet-sealedsecret.yaml) | Replaces [`k8s/fleet/secret.yaml`](../fleet/secret.yaml). |
| [`booking-sealedsecret.yaml`](booking-sealedsecret.yaml) | Replaces [`k8s/booking/secret.yaml`](../booking/secret.yaml). |
| [`postgres-sealedsecret.yaml`](postgres-sealedsecret.yaml) | Replaces the inline `postgres-secret` in [`k8s/infra/postgres.yaml`](../infra/postgres.yaml). |
| [`registry-auth-sealedsecret.yaml`](registry-auth-sealedsecret.yaml) | Replaces [`k8s/registry/auth-secret.yaml`](../registry/auth-secret.yaml). |
| [`kustomization.yaml`](kustomization.yaml) | Pulls the above into the base, alongside the controller. |

## Operator workflow

```bash
# (1) bootstrap the controller once per cluster.
kubectl apply -f k8s/sealed-secrets/controller.yaml

# (2) fetch the controller's public cert (used to encrypt locally).
kubeseal --controller-namespace kube-system \
         --controller-name sealed-secrets-controller \
         --fetch-cert > /tmp/sealed-secrets-cert.pem

# (3) re-seal a plaintext Secret (NEVER committed).
kubeseal --cert /tmp/sealed-secrets-cert.pem \
         --format yaml \
         < my-plaintext-secret.yaml \
         > k8s/sealed-secrets/<svc>-sealedsecret.yaml
```

## Rotation

Each `SealedSecret` is reproducible from a plaintext source that lives
*outside* Git (developer's password manager, an HSM-backed vault, or a
fresh `openssl rand -base64 48`). To rotate:

1. Generate a new plaintext value.
2. Re-run `kubeseal` to produce a new SealedSecret.
3. Commit + `kubectl apply`; the controller materialises the new
   `kind: Secret` and the next Pod reschedule picks it up.

## Pre-commit hook

[`scripts/git-hooks/pre-commit-no-plaintext-secrets.sh`](../../scripts/git-hooks/pre-commit-no-plaintext-secrets.sh)
scans the working tree for `kind: Secret` with `stringData:` /
`data:` blocks and refuses the commit. Symlink it into
`.git/hooks/pre-commit` to activate.

## Note on the committed placeholders

The `*-sealedsecret.yaml` files committed here are **structural
templates** — the `encryptedData` blocks are placeholders that must
be re-generated against the live controller's public key before
`kubectl apply` succeeds. They exist in Git so that:

* Kustomize references resolve at PR-review time.
* The Phase C wiring is reviewable without standing up a cluster.
* `kubeseal --re-encrypt` can rotate the entire set without a manual
  inventory of which Secret lives where.

When the controller is first installed on a real cluster, run
[`scripts/seal-secrets.sh`](../../scripts/seal-secrets.sh) (Phase C
operator tool, generated alongside) which loops over the SealedSecret
files, re-reads the plaintext source from the operator's vault, and
re-emits the `encryptedData` blocks.
