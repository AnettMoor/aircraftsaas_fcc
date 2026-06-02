# Deploying AircraftSaaS to the OpenNebula Kubernetes Cluster

This document is a focused, operator-facing playbook for deploying the AircraftSaaS microservices stack onto an **already-provisioned** Kubernetes cluster running on OpenNebula (1 control-plane `cp-1` + 2 workers `wk-1`, `wk-2`, Calico CNI, `kubectl get nodes` returns three `Ready` nodes).

It assumes the IaaS layer described in [`opennebula/runbook.md`](opennebula/runbook.md:1) and [`plans/deploy.md`](plans/deploy.md:1) §2 has already been executed. **Nothing below provisions VMs, installs kubeadm, or sets up Calico** — only the application + supporting platform services are deployed here.

For Kustomize overlay semantics, see [`k8s/overlays/opennebula/kustomization.yaml`](k8s/overlays/opennebula/kustomization.yaml:1).

---

## 0. Prerequisites on your workstation

| Tool | Version | Purpose |
|---|---|---|
| `kubectl` | matching cluster minor (v1.30.x) | apply manifests |
| `kustomize` | ≥ 5.0 (or `kubectl -k`) | render the overlay |
| `kubeseal` | ≥ 0.24 | re-seal `SealedSecret`s against the live controller key |
| `docker` / `buildx` | recent | build + push images to the in-cluster registry |
| `helm` | ≥ 3.12 (optional) | only needed if you reinstall ingress-nginx by hand |
| `stern` / `k9s` | optional | log streaming + tui |

Inputs you must obtain **from the cluster operator**:

```bash
# kubeconfig with cluster-admin (or at minimum, the ci-deployer role)
export KUBECONFIG=~/.kube/aircraft-opennebula.yaml
kubectl get nodes -o wide          # MUST return 3 Ready nodes (cp-1, wk-1, wk-2)
kubectl get ns                     # baseline kube-system, default, etc.

# Edge hostname DNAT'd to cp-1 (must resolve publicly)
export EDGE_HOST="aircraft.example.com"
```

If `kubectl get nodes` does NOT show 3 `Ready` nodes, STOP — the cluster is not in the expected state and the deploy will not succeed. Re-run [`opennebula/runbook.md`](opennebula/runbook.md:1) §5–§7 first.

---

## 1. Clone the repo and pick the overlay

```bash
git clone https://github.com/<org>/aircraftsaas_fcc.git
cd aircraftsaas_fcc
git checkout main                                    # or your release tag
```

The OpenNebula overlay lives at [`k8s/overlays/opennebula/`](k8s/overlays/opennebula/kustomization.yaml:1). Verify it renders cleanly **before** touching the cluster:

```bash
kustomize build k8s/overlays/opennebula | head -50
kustomize build k8s/overlays/opennebula > /tmp/aircraft.rendered.yaml
wc -l /tmp/aircraft.rendered.yaml                    # sanity: should be > 1500 lines
```

> The overlay is documented inline. It enforces 3 replicas, anti-affinity, `imagePullPolicy: Always`, swaps `*.aircraft.localtest.me` for `*.aircraft.example.com`, points images at `registry.ns-registry.svc.cluster.local:5000/<svc>`, and switches the cert-manager `ClusterIssuer` to `letsencrypt-staging`. Edit those four values if your edge hostname or issuer differs.

If your edge hostname is NOT `aircraft.example.com`, do a search-and-replace **before** applying:

```bash
# Replace every occurrence in the overlay (4 Ingress hosts + CSP connect-src).
sed -i.bak "s/aircraft\.example\.com/${EDGE_HOST}/g" \
    k8s/overlays/opennebula/kustomization.yaml
git diff k8s/overlays/opennebula/kustomization.yaml  # eyeball the diff
```

---

## 2. Bootstrap the in-cluster Docker registry

The overlay assumes images live at `registry.ns-registry.svc.cluster.local:5000`. The registry itself is part of the base — but it MUST come up before any app Deployment can pull, so install it first in isolation:

```bash
kubectl apply -k k8s/registry
kubectl -n ns-registry rollout status deploy/registry --timeout=180s
kubectl -n ns-registry get pvc                      # PVC must be Bound
```

Expose the registry to your workstation just long enough to push images:

```bash
kubectl -n ns-registry port-forward svc/registry 5000:5000 &
PF_PID=$!
```

---

## 3. Build and push the application images

The Compose-driven Dockerfiles in the repo root build all four images. Tag them with the current git sha so the CI pattern below works the same way by hand:

```bash
export TAG=$(git rev-parse --short HEAD)
export REG=localhost:5000                            # via the port-forward above

# Backend services (one multistage Dockerfile per WebHost)
docker buildx build --platform linux/amd64 \
    -f AircraftSaaS/Services/Users.WebHost/Dockerfile \
    -t ${REG}/users-service:${TAG} --push .

docker buildx build --platform linux/amd64 \
    -f AircraftSaaS/Services/Fleet.WebHost/Dockerfile \
    -t ${REG}/fleet-service:${TAG} --push .

docker buildx build --platform linux/amd64 \
    -f AircraftSaaS/Services/Booking.WebHost/Dockerfile \
    -t ${REG}/booking-service:${TAG} --push .

# Vue SPA
docker buildx build --platform linux/amd64 \
    -f frontend_vue/Dockerfile \
    -t ${REG}/vue-frontend:${TAG} --push frontend_vue

kill $PF_PID                                         # stop the port-forward
```

Confirm the four repositories now exist in the registry:

```bash
kubectl -n ns-registry exec deploy/registry -- \
    ls /var/lib/registry/docker/registry/v2/repositories
# Expected: booking-service  fleet-service  users-service  vue-frontend
```

---

## 4. Install the cluster-scoped controllers

Two controllers are cluster-scoped and live outside the per-namespace overlay: cert-manager and sealed-secrets. Install them once per cluster (idempotent):

```bash
# cert-manager (CRDs + controller + ClusterIssuers)
kubectl apply -k k8s/cert-manager
kubectl -n cert-manager rollout status deploy/cert-manager --timeout=180s
kubectl -n cert-manager rollout status deploy/cert-manager-webhook --timeout=180s
kubectl get clusterissuer                            # letsencrypt-staging must be Ready

# Bitnami sealed-secrets controller
kubectl apply -f k8s/sealed-secrets/controller.yaml
kubectl -n kube-system rollout status deploy/sealed-secrets-controller --timeout=180s
```

If you are NOT using the CI-managed ingress-nginx, install it now via its upstream Helm chart — the overlay's `Ingress` objects assume `ingressClassName: nginx` exists. Skip if your operator already installed it.

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
    --namespace ingress-nginx --create-namespace \
    --set controller.service.type=LoadBalancer
kubectl -n ingress-nginx rollout status deploy/ingress-nginx-controller --timeout=180s
```

---

## 5. Re-seal the SealedSecrets against the live controller key

The `*-sealedsecret.yaml` files in [`k8s/sealed-secrets/`](k8s/sealed-secrets/README.md:1) are **structural templates** with placeholder `encryptedData`. They will not decrypt on a fresh cluster. You MUST re-seal them against the controller key that was just generated:

```bash
# Fetch the live public cert
kubeseal --controller-namespace kube-system \
         --controller-name sealed-secrets-controller \
         --fetch-cert > /tmp/sealed-secrets-cert.pem

# Re-seal every Secret from plaintext sources held in your password manager /
# vault. The repo helper script loops over the inventory:
./scripts/seal-secrets.sh /tmp/sealed-secrets-cert.pem
git diff k8s/sealed-secrets/                        # encryptedData blocks change
```

If `scripts/seal-secrets.sh` does not yet exist in your checkout, do it manually per Secret:

```bash
# Example: users service Secret
kubeseal --cert /tmp/sealed-secrets-cert.pem \
         --format yaml \
         < /path/to/secret/vault/users-secret-plaintext.yaml \
         > k8s/sealed-secrets/users-sealedsecret.yaml
```

Repeat for `fleet-sealedsecret.yaml`, `booking-sealedsecret.yaml`, `postgres-sealedsecret.yaml`, `registry-auth-sealedsecret.yaml`.

> Do NOT commit the plaintext sources. The pre-commit hook at [`scripts/git-hooks/pre-commit-no-plaintext-secrets.sh`](scripts/git-hooks/pre-commit-no-plaintext-secrets.sh:1) refuses commits containing `kind: Secret` with raw `data:` / `stringData:`.

Apply the sealed secrets:

```bash
kubectl apply -k k8s/sealed-secrets
# Watch the controller materialise each into a real Secret:
kubectl get secret -A | grep -E '(users|fleet|booking|postgres|registry)-'
```

---

## 6. Apply the OpenNebula overlay

This is the headline step — Kustomize renders the full base + overlay and `kubectl` applies the result:

```bash
kubectl apply -k k8s/overlays/opennebula
```

Expected creation order (Kustomize does not order, but readiness probes do):

1. Namespaces (`ns-users`, `ns-fleet`, `ns-booking`, `ns-frontend`, `ns-infra`, `ns-registry`, `ns-ci`) and their labels.
2. Postgres + RabbitMQ StatefulSets in `ns-infra`.
3. NetworkPolicies (default-deny in each ns, plus the `allow-*.yaml` peering rules).
4. The four app `Deployment`s + `Service`s + `HorizontalPodAutoscaler`s.
5. The three EF Core migration `Job`s (`users-migrate`, `fleet-migrate`, `booking-migrate`).
6. The four `Ingress` objects (`users-ingress`, `fleet-ingress`, `booking-ingress`, `frontend-ingress`).
7. The CI namespace + self-hosted runner Deployment.

Watch the rollout:

```bash
kubectl get pods -A -w
# In a separate terminal:
stern -A '.*' --tail 0
```

---

## 7. Override the image tags to your freshly-pushed `${TAG}`

The overlay pins the image tag to `latest` for reviewability. In production every deploy MUST point at an immutable git-sha tag. Roll the Deployments to the tag you pushed in §3:

```bash
for SVC in users-service fleet-service booking-service; do
  kubectl -n ns-${SVC%-service} set image \
      deployment/${SVC} \
      ${SVC}=registry.ns-registry.svc.cluster.local:5000/${SVC}:${TAG}
done

kubectl -n ns-frontend set image \
    deployment/vue-frontend \
    vue-frontend=registry.ns-registry.svc.cluster.local:5000/vue-frontend:${TAG}

# Migration Jobs are one-shot — re-run them at the new tag:
for SVC in users fleet booking; do
  kubectl -n ns-${SVC} delete job ${SVC}-migrate --ignore-not-found
  kustomize build k8s/overlays/opennebula \
    | yq "select(.kind == \"Job\" and .metadata.name == \"${SVC}-migrate\")" \
    | sed "s|:latest|:${TAG}|g" \
    | kubectl apply -f -
  kubectl -n ns-${SVC} wait --for=condition=complete job/${SVC}-migrate --timeout=300s
done
```

> In a real CI pipeline the `set image` / migration-Job rerun is driven by GitHub Actions ([`.github/workflows/`](.github/workflows/)). The manual recipe above only matters for one-off deploys from a workstation.

---

## 8. Smoke-test the deploy

```bash
# 1. Every Deployment Ready.
kubectl get deploy -A | grep -vE 'READY|kube-system|cert-manager|ingress-nginx'
# Every app Deployment must show e.g. 3/3.

# 2. Migration Jobs Succeeded.
kubectl get jobs -A | grep migrate
# users-migrate / fleet-migrate / booking-migrate must each show 1/1 Completions.

# 3. Ingress hosts resolve and return 2xx/3xx.
for h in users fleet booking app; do
  echo -n "$h.${EDGE_HOST}: "
  curl -k -s -o /dev/null -w "%{http_code}\n" https://${h}.${EDGE_HOST}/healthz \
      || curl -k -s -o /dev/null -w "%{http_code}\n" https://${h}.${EDGE_HOST}/
done

# 4. TLS certificates issued by letsencrypt-staging.
kubectl get certificate -A
# Each entry must show READY=True. (Staging certs will not validate in browsers
# — promote to letsencrypt-prod by editing the overlay once the staging
#   issuance has succeeded at least once.)

# 5. NetworkPolicies enforced — cross-namespace deny works.
kubectl -n ns-users run probe --rm -it --image=curlimages/curl --restart=Never -- \
    curl -m 3 -sS http://fleet-service.ns-fleet.svc.cluster.local/healthz
# Expected: success (allow-fleet.yaml permits this peer).

kubectl -n default run probe --rm -it --image=curlimages/curl --restart=Never -- \
    curl -m 3 -sS http://fleet-service.ns-fleet.svc.cluster.local/healthz
# Expected: timeout (default ns is NOT in any allow-*.yaml peer list).
```

---

## 9. Optional — promote to `letsencrypt-prod`

Once §8 step 4 shows staging certs Ready, edit the overlay to point at the production issuer (rate-limited!) and re-apply:

```bash
sed -i.bak "s/letsencrypt-staging/letsencrypt-prod/g" \
    k8s/overlays/opennebula/kustomization.yaml
kubectl apply -k k8s/overlays/opennebula
kubectl get certificate -A -w                        # wait for the new Ready=True
```

Browsers will now trust the certs end-to-end.

---

## 10. Day-2 operations

### 10.1 Rolling out a new build

```bash
git pull && export TAG=$(git rev-parse --short HEAD)
# Build + push as in §3, then re-run §7.
# Watch the rollout:
kubectl -n ns-users rollout status deploy/users-service --timeout=300s
```

### 10.2 Inspecting Postgres / RabbitMQ

```bash
kubectl -n ns-infra exec -it sts/postgres -- psql -U postgres -l
kubectl -n ns-infra port-forward svc/rabbitmq 15672:15672    # web UI
```

### 10.3 Scaling

HPAs are wired (`min=3`, `max=10`, CPU 70 %). To force a manual scale:

```bash
kubectl -n ns-fleet scale deploy/fleet-service --replicas=5
```

### 10.4 Tearing it all down (DO NOT run on prod)

```bash
kubectl delete -k k8s/overlays/opennebula
kubectl delete -k k8s/sealed-secrets
kubectl delete -k k8s/cert-manager
kubectl delete -k k8s/registry
kubectl delete ns ns-users ns-fleet ns-booking ns-frontend ns-infra ns-registry ns-ci \
    --ignore-not-found
```

---

## 11. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| App pods stuck `ImagePullBackOff` | tag not pushed to in-cluster registry, or `local-registry` imagePullSecret not materialised from SealedSecret | re-run §3 + verify `kubectl -n ns-<svc> get secret local-registry` exists |
| `*-migrate` Job `BackoffLimitExceeded` | Postgres unreachable (NetworkPolicy or secret mismatch) | `kubectl logs job/<svc>-migrate -n ns-<svc>`; check `k8s/network-policies/allow-infra.yaml` peers ns-infra |
| Ingress returns `404` | NGINX ingress class mismatch | confirm `kubectl get ingressclass` shows `nginx` and overlay Ingresses set `ingressClassName: nginx` |
| Cert stuck `Pending` | edge DNS not yet pointing at cp-1 / port 80 closed for HTTP-01 | verify `dig +short <host>` returns the OpenNebula NAT IP, port 80 open in the `aircraft-edge` SG |
| Cross-ns curl unexpectedly succeeds | Calico not enforcing NetworkPolicy | check `calicoctl get felixconfiguration default -o yaml` and that the CNI is actually Calico (`kubectl -n kube-system get pods | grep calico`) |
| SealedSecret pods show `decryption error` | re-sealed against a different controller key | re-run §5 after `kubeseal --fetch-cert` against the CURRENT controller |

For the full design rationale and the optional CI/CD wiring (GitHub Actions → self-hosted runner in `ns-ci` → `kubectl set image`), see [`plans/deploy.md`](plans/deploy.md:1) §§4–7.
