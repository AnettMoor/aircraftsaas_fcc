# Deploying AircraftSaaS Services onto an Existing Kubernetes Cluster

This guide is a **cluster-agnostic** operator playbook for deploying the AircraftSaaS repository and all of its services onto a Kubernetes cluster you already have running (any flavor: kubeadm, k3s, EKS, GKE, AKS, Rancher, OpenShift, etc.).

It assumes you have:

- `kubectl` access (via a working `KUBECONFIG`) with `cluster-admin` privileges.
- A functioning cluster (`kubectl get nodes` shows ≥ 1 `Ready` node).
- A CNI that enforces `NetworkPolicy` (Calico, Cilium, etc. — required for [`k8s/network-policies/`](k8s/network-policies/kustomization.yaml:1) to actually deny traffic).
- An `IngressClass` named `nginx` available (typically [ingress-nginx](https://kubernetes.github.io/ingress-nginx/)).
- A `StorageClass` that supports `ReadWriteOnce` PVCs (used by Postgres, RabbitMQ, and the in-cluster registry).

If any of the above is missing, see [§1.1](#11-installing-the-supporting-cluster-add-ons) before proceeding.

> For the **OpenNebula-specific** flow (which provisions the cluster from scratch and uses the `overlays/opennebula` overlay), see [`clusterrun.md`](clusterrun.md:1). This document is the generalized version.

---

## 0. What this stack deploys

The repository ships a Kustomize tree under [`k8s/`](k8s/base/kustomization.yaml:1) that creates the following on the cluster:

| Layer | Namespace | What gets created |
|---|---|---|
| Infra | `ns-infra` | Postgres `StatefulSet` + Service, RabbitMQ `StatefulSet` + Service |
| Registry | `ns-registry` | Docker Registry v2 `Deployment` + `PVC` + `Service` |
| App: Users | `ns-users` | `Deployment` + `Service` + `HPA` + EF Core migration `Job` |
| App: Fleet | `ns-fleet` | `Deployment` + `Service` + `HPA` + EF Core migration `Job` |
| App: Booking | `ns-booking` | `Deployment` + `Service` + `HPA` + EF Core migration `Job` |
| App: Frontend | `ns-frontend` | Vue SPA `Deployment` + `Service` + `HPA` |
| Edge | (each app ns) | One `Ingress` per service, all TLS-terminated by cert-manager |
| Security | (each app ns) | Default-deny `NetworkPolicy` + per-peer allow rules |
| RBAC | (each app ns) | Hardened runtime `ServiceAccount`s (`automountServiceAccountToken: false`, read-only root FS) |
| CI | `ns-ci` | (optional) Self-hosted GitHub Actions runner + `ci-deployer` `Role` |

Cluster-scoped pieces installed separately:

- **cert-manager** under [`k8s/cert-manager/`](k8s/cert-manager/kustomization.yaml:1) — issues TLS certs for every Ingress via Let's Encrypt.
- **Bitnami sealed-secrets** under [`k8s/sealed-secrets/`](k8s/sealed-secrets/kustomization.yaml:1) — encrypted-at-rest credentials sealed to the cluster's controller key.

---

## 1. Prerequisites on your workstation

| Tool | Version | Purpose |
|---|---|---|
| `kubectl` | matching the cluster minor (≥ v1.28) | apply manifests |
| `kustomize` | ≥ 5.0 (or use `kubectl -k`) | render the overlay |
| `kubeseal` | ≥ 0.24 | re-seal `SealedSecret`s against the live controller key |
| `docker` / `buildx` | recent | build + push application images |
| `helm` | ≥ 3.12 | install ingress-nginx / cert-manager if missing |
| `stern` or `k9s` | optional | log streaming / TUI |

Sanity-check the cluster:

```bash
export KUBECONFIG=~/.kube/config     # or your cluster's kubeconfig
kubectl get nodes -o wide            # at least 1 Ready node
kubectl get ns                       # baseline namespaces present
kubectl get storageclass             # at least one default SC
kubectl get ingressclass             # 'nginx' should appear
```

### 1.1 Installing the supporting cluster add-ons

Skip subsections you already have. Each is idempotent.

**ingress-nginx** (provides the `nginx` IngressClass):

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
    --namespace ingress-nginx --create-namespace \
    --set controller.service.type=LoadBalancer
kubectl -n ingress-nginx rollout status deploy/ingress-nginx-controller --timeout=180s
kubectl get ingressclass nginx
```

For bare-metal clusters without a cloud LoadBalancer, install [MetalLB](https://metallb.universe.tf/) or use `--set controller.service.type=NodePort` instead.

**Verify NetworkPolicy enforcement** — required for the manifests in [`k8s/network-policies/`](k8s/network-policies/kustomization.yaml:1) to mean anything:

```bash
kubectl -n kube-system get pods | grep -Ei 'calico|cilium|weave'
```

If nothing matches, the default kindnet/flannel CNIs may not enforce policies. Install Calico:

```bash
kubectl apply -f https://raw.githubusercontent.com/projectcalico/calico/v3.27.0/manifests/calico.yaml
```

---

## 2. Clone the repo

```bash
git clone https://github.com/<org>/aircraftsaas_fcc.git
cd aircraftsaas_fcc
git checkout main                    # or a release tag
```

The Kustomize layout:

```
k8s/
├── base/                # namespaces + wires everything together
├── cert-manager/        # cluster-scoped controller + ClusterIssuers
├── sealed-secrets/      # cluster-scoped controller + sealed manifests
├── registry/            # in-cluster Docker Registry v2
├── infra/               # Postgres + RabbitMQ
├── users/ fleet/ booking/ frontend/   # one app each
├── gateway/             # Ingress objects
├── network-policies/    # default-deny + per-app allow rules
├── rbac/                # runtime SA hardening Component
├── ci/                  # optional self-hosted GitHub Actions runner
└── overlays/
    └── opennebula/      # production preset (3 replicas, real hostnames)
```

---

## 3. Decide your overlay strategy

You have two choices:

**A. Use the existing OpenNebula overlay** (recommended starting point). It applies a production-grade preset: 3 replicas, anti-affinity, `imagePullPolicy: Always`, in-cluster registry image refs, hardened RBAC component. You only need to override:

- The edge hostname (currently `aircraft.example.com`).
- The cert-manager `ClusterIssuer` (currently `letsencrypt-staging`).

```bash
export EDGE_HOST="aircraft.mycompany.com"
sed -i.bak "s/aircraft\.example\.com/${EDGE_HOST}/g" \
    k8s/overlays/opennebula/kustomization.yaml
```

**B. Create your own overlay** under `k8s/overlays/<env>/` that references `../../base`, `../../cert-manager`, and (optionally) the `../../rbac` Component. Use this if your environment differs significantly (e.g., external Postgres, different image registry, no anti-affinity).

For both options, render and review **before** applying anything:

```bash
kustomize build k8s/overlays/opennebula > /tmp/aircraft.rendered.yaml
wc -l /tmp/aircraft.rendered.yaml                 # sanity: > 1500 lines
less /tmp/aircraft.rendered.yaml                  # eyeball the diff
```

---

## 4. Bring up the in-cluster Docker registry

The overlay assumes images live at `registry.ns-registry.svc.cluster.local:5000`. Install the registry first so app pods have somewhere to pull from:

```bash
kubectl apply -k k8s/registry
kubectl -n ns-registry rollout status deploy/registry --timeout=180s
kubectl -n ns-registry get pvc                    # must be Bound
```

> If you prefer an **external** registry (Docker Hub, ECR, GHCR, Harbor), skip this step and rewrite the `images:` block in your overlay accordingly. You will also need to delete `k8s/registry/` from your overlay's `resources:` and supply a different `imagePullSecret`.

Open a port-forward so you can push from your laptop:

```bash
kubectl -n ns-registry port-forward svc/registry 5000:5000 &
PF_PID=$!
```

---

## 5. Build and push the application images

The repository contains one multistage Dockerfile per service. Tag everything with the current git SHA:

```bash
export TAG=$(git rev-parse --short HEAD)
export REG=localhost:5000                          # via the port-forward

docker buildx build --platform linux/amd64 \
    -f AircraftSaaS/Services/Users.WebHost/Dockerfile \
    -t ${REG}/users-service:${TAG} --push .

docker buildx build --platform linux/amd64 \
    -f AircraftSaaS/Services/Fleet.WebHost/Dockerfile \
    -t ${REG}/fleet-service:${TAG} --push .

docker buildx build --platform linux/amd64 \
    -f AircraftSaaS/Services/Booking.WebHost/Dockerfile \
    -t ${REG}/booking-service:${TAG} --push .

docker buildx build --platform linux/amd64 \
    -f frontend_vue/Dockerfile \
    -t ${REG}/vue-frontend:${TAG} --push frontend_vue

kill $PF_PID                                       # stop the port-forward
```

Confirm the four repositories now exist:

```bash
kubectl -n ns-registry exec deploy/registry -- \
    ls /var/lib/registry/docker/registry/v2/repositories
# Expected: booking-service  fleet-service  users-service  vue-frontend
```

---

## 6. Install the cluster-scoped controllers

```bash
# cert-manager (CRDs + controller + ClusterIssuers)
kubectl apply -k k8s/cert-manager
kubectl -n cert-manager rollout status deploy/cert-manager --timeout=180s
kubectl -n cert-manager rollout status deploy/cert-manager-webhook --timeout=180s
kubectl get clusterissuer                          # letsencrypt-staging Ready

# Bitnami sealed-secrets controller
kubectl apply -f k8s/sealed-secrets/controller.yaml
kubectl -n kube-system rollout status deploy/sealed-secrets-controller --timeout=180s
```

---

## 7. Re-seal the SealedSecrets

The `*-sealedsecret.yaml` files in [`k8s/sealed-secrets/`](k8s/sealed-secrets/README.md:1) are **structural templates** with placeholder `encryptedData` that was sealed against a **different** controller key. They will **not decrypt on your cluster** until you re-seal them against the controller key your `kubectl apply -f k8s/sealed-secrets/controller.yaml` just generated.

```bash
# 1. Fetch the live public cert of YOUR controller
kubeseal --controller-namespace kube-system \
         --controller-name sealed-secrets-controller \
         --fetch-cert > /tmp/sealed-secrets-cert.pem

# 2. Re-seal every Secret from plaintext sources held in your vault / password manager.
#    The repo includes a helper:
./scripts/seal-secrets.sh /tmp/sealed-secrets-cert.pem
```

If `scripts/seal-secrets.sh` does not exist in your checkout, seal each Secret manually:

```bash
kubeseal --cert /tmp/sealed-secrets-cert.pem \
         --format yaml \
         < /path/to/vault/users-secret-plaintext.yaml \
         > k8s/sealed-secrets/users-sealedsecret.yaml
```

Repeat for `fleet-sealedsecret.yaml`, `booking-sealedsecret.yaml`, `postgres-sealedsecret.yaml`, `registry-auth-sealedsecret.yaml`.

> **Never commit plaintext.** The pre-commit hook at [`scripts/git-hooks/pre-commit-no-plaintext-secrets.sh`](scripts/git-hooks/pre-commit-no-plaintext-secrets.sh:1) refuses commits containing `kind: Secret` with raw `data:`/`stringData:`.

Apply the sealed material:

```bash
kubectl apply -k k8s/sealed-secrets
kubectl get secret -A | grep -E '(users|fleet|booking|postgres|registry)-'
```

Each `SealedSecret` should produce a matching `Secret` within a few seconds. If not, check the controller log:

```bash
kubectl -n kube-system logs deploy/sealed-secrets-controller --tail=50
```

---

## 8. Apply the overlay

This is the headline step — render the full base + overlay and apply it:

```bash
kubectl apply -k k8s/overlays/opennebula
```

Kustomize does not order resources by appearance, but the cluster's own readiness probes serialize the rollout: app pods stay `CrashLoopBackOff` (waiting for Postgres) until the DB is `Ready`. Watch progress in another terminal:

```bash
kubectl get pods -A -w
# or with stern:
stern -A '.*' --tail 0
```

Expected timeline on a healthy cluster:

| t+0s   | Namespaces + NetworkPolicies created |
| t+15s  | Postgres + RabbitMQ pods Running |
| t+45s  | Migration `Job`s start, complete in ~30s each |
| t+60s  | App Deployments Ready |
| t+90s  | Ingress objects assigned an address, cert-manager begins HTTP-01 challenge |
| t+180s | Certificates `Ready=True`, edge URLs serving 200 OK |

---

## 9. Pin Deployments to your freshly-pushed image tag

The overlay defaults to `:latest` for review-friendliness. In production every deploy MUST point at an immutable tag. Roll each Deployment to the tag you pushed in §5:

```bash
for SVC in users-service fleet-service booking-service; do
  kubectl -n ns-${SVC%-service} set image \
      deployment/${SVC} \
      ${SVC}=registry.ns-registry.svc.cluster.local:5000/${SVC}:${TAG}
done

kubectl -n ns-frontend set image \
    deployment/vue-frontend \
    vue-frontend=registry.ns-registry.svc.cluster.local:5000/vue-frontend:${TAG}
```

Re-run the migration `Job`s at the new tag (they are one-shot resources):

```bash
for SVC in users fleet booking; do
  kubectl -n ns-${SVC} delete job ${SVC}-migrate --ignore-not-found
  kustomize build k8s/overlays/opennebula \
    | yq "select(.kind == \"Job\" and .metadata.name == \"${SVC}-migrate\")" \
    | sed "s|:latest|:${TAG}|g" \
    | kubectl apply -f -
  kubectl -n ns-${SVC} wait --for=condition=complete \
      job/${SVC}-migrate --timeout=300s
done
```

> In CI this is encoded in the GitHub Actions workflows under [`.github/workflows/`](.github/workflows/). The manual recipe only matters for one-off workstation deploys.

---

## 10. Configure DNS at the edge

Point your wildcard DNS at the ingress controller's external address:

```bash
kubectl -n ingress-nginx get svc ingress-nginx-controller
# Note the EXTERNAL-IP (or hostname).
```

Add A/CNAME records in your DNS provider:

```
users.aircraft.mycompany.com    → <INGRESS_EXTERNAL_IP>
fleet.aircraft.mycompany.com    → <INGRESS_EXTERNAL_IP>
booking.aircraft.mycompany.com  → <INGRESS_EXTERNAL_IP>
app.aircraft.mycompany.com      → <INGRESS_EXTERNAL_IP>
```

cert-manager's HTTP-01 challenge requires the records to **resolve publicly** before it can issue the certificates.

---

## 11. Smoke-test the deployment

```bash
# 1. Every Deployment Ready.
kubectl get deploy -A | grep -vE 'READY|kube-system|cert-manager|ingress-nginx'

# 2. Migration Jobs Succeeded.
kubectl get jobs -A | grep migrate

# 3. Ingress hosts return 2xx.
for h in users fleet booking app; do
  echo -n "$h.${EDGE_HOST}: "
  curl -k -s -o /dev/null -w "%{http_code}\n" \
       https://${h}.${EDGE_HOST}/healthz \
    || curl -k -s -o /dev/null -w "%{http_code}\n" \
       https://${h}.${EDGE_HOST}/
done

# 4. TLS certificates Ready.
kubectl get certificate -A
# Staging certs are NOT browser-trusted — promote in §12 once issuance succeeded.

# 5. NetworkPolicy enforcement — cross-namespace deny works.
kubectl -n ns-users run probe --rm -it --image=curlimages/curl --restart=Never -- \
    curl -m 3 -sS http://fleet-service.ns-fleet.svc.cluster.local/healthz
# Expected: success (allow-fleet.yaml permits this peer).

kubectl -n default run probe --rm -it --image=curlimages/curl --restart=Never -- \
    curl -m 3 -sS http://fleet-service.ns-fleet.svc.cluster.local/healthz
# Expected: timeout (default ns is NOT in any allow list).
```

---

## 12. Promote to production TLS

Once staging certs are `Ready`, switch to the production issuer (rate-limited!) and re-apply:

```bash
sed -i.bak "s/letsencrypt-staging/letsencrypt-prod/g" \
    k8s/overlays/opennebula/kustomization.yaml
kubectl apply -k k8s/overlays/opennebula
kubectl get certificate -A -w
```

---

## 13. Day-2 operations

### 13.1 Rolling out a new build

```bash
git pull && export TAG=$(git rev-parse --short HEAD)
# Build + push as in §5, then re-run §9.
kubectl -n ns-users rollout status deploy/users-service --timeout=300s
```

### 13.2 Inspecting Postgres / RabbitMQ

```bash
kubectl -n ns-infra exec -it sts/postgres -- psql -U postgres -l
kubectl -n ns-infra port-forward svc/rabbitmq 15672:15672    # web UI on localhost
```

### 13.3 Scaling

HPAs are wired (`min=3`, `max=10`, CPU 70 %). To force a manual scale:

```bash
kubectl -n ns-fleet scale deploy/fleet-service --replicas=5
```

### 13.4 Tearing it all down

```bash
kubectl delete -k k8s/overlays/opennebula
kubectl delete -k k8s/sealed-secrets
kubectl delete -k k8s/cert-manager
kubectl delete -k k8s/registry
kubectl delete ns ns-users ns-fleet ns-booking ns-frontend \
                  ns-infra ns-registry ns-ci --ignore-not-found
```

---

## 14. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| App pods stuck `ImagePullBackOff` | image tag not pushed, or `local-registry` `imagePullSecret` not materialised from `SealedSecret` | re-run §5 and confirm `kubectl -n ns-<svc> get secret local-registry` exists |
| `*-migrate` Job `BackoffLimitExceeded` | Postgres unreachable (NetworkPolicy or wrong creds) | `kubectl logs job/<svc>-migrate -n ns-<svc>`; check [`k8s/network-policies/allow-infra.yaml`](k8s/network-policies/allow-infra.yaml:1) peers `ns-infra` |
| Ingress returns `404` | NGINX `IngressClass` mismatch | `kubectl get ingressclass` shows `nginx`; overlay Ingresses set `ingressClassName: nginx` |
| Certificate stuck `Pending` | edge DNS not pointing at the LB / port 80 blocked | `dig +short <host>` returns your LB IP; port 80 reachable for HTTP-01 |
| Cross-ns curl unexpectedly succeeds | CNI not enforcing `NetworkPolicy` | swap to Calico/Cilium; kindnet/flannel default doesn't enforce |
| `SealedSecret` controller logs `no key could decrypt secret` | Secrets sealed against a different controller key | re-run §7 with a fresh `kubeseal --fetch-cert` |
| `PVC` stuck `Pending` | no default `StorageClass`, or PV provisioner not running | `kubectl get sc`; ensure one is annotated `is-default-class: true` |
| `kustomize build` errors with "must be a directory or file" | a referenced path is not itself a kustomization root | every dir in `resources:` MUST contain a `kustomization.yaml` |

---

## 15. Automated alternative — the deploy scripts

The manual recipe above is encoded in idempotent shell scripts under [`scripts/`](scripts/):

| Script | Covers | When to run |
|---|---|---|
| [`scripts/bootstrap-cluster.sh`](scripts/bootstrap-cluster.sh:1) | §4 + §6 + §7 | Once per cluster |
| [`scripts/deploy-apps.sh`](scripts/deploy-apps.sh:1)            | §5 + §8 + §9 + §11 | Every release |
| [`scripts/seal-secrets.sh`](scripts/seal-secrets.sh:1)          | §7 only      | Whenever secrets change |

Typical end-to-end flow on a brand-new cluster:

```bash
export KUBECONFIG=~/.kube/config
export EDGE_HOST="aircraft.mycompany.com"
export AIRCRAFT_VAULT_DIR=/path/to/plaintext/vault

./scripts/bootstrap-cluster.sh           # cluster-scoped controllers + sealed secrets
./scripts/deploy-apps.sh                 # build, push, apply, smoke-test
```

Both support `--dry-run` and `--skip-*` flags so partial failures can be resumed without redoing earlier stages.

---

## 16. Where to look next

- [`clusterrun.md`](clusterrun.md:1) — OpenNebula-specific full-stack runbook (VM provisioning + cluster bootstrap + this guide combined).
- [`plans/deploy.md`](plans/deploy.md:1) — design rationale for the overlay structure, NetworkPolicy peering matrix, CI wiring.
- [`k8s/sealed-secrets/README.md`](k8s/sealed-secrets/README.md:1) — sealing/rotation workflow.
- [`k8s/cert-manager/README.md`](k8s/cert-manager/README.md:1) — ClusterIssuer options.
- [`.github/workflows/`](.github/workflows/) — the fully-automated CI/CD pipeline that performs the §5 / §8 / §9 steps on every push to `main`.
