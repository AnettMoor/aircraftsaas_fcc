# OpenNebula cut-over checklist

> Operator-facing checklist for moving the Aircraft SaaS workload onto a fresh OpenNebula cluster produced by the automation in [`opennebula/`](../opennebula/).
> Anchored to [`plans/opennebula.md`](../plans/opennebula.md) §5 (first-boot sequence) and §6 (acceptance gates).
> Once §2.1's manual cluster has been torn down (the current state), this is **the** procedure for getting back to a live cluster.

---

## 0. Prerequisites

- OpenNebula tenancy with the templates + vNet + security groups + oneflow service already imported from [`opennebula/`](../opennebula/) (one-time setup; see [`opennebula/runbook.md`](../opennebula/runbook.md) §§1–4).
- Edge router configured for the DNAT rules in [`opennebula/runbook.md`](../opennebula/runbook.md) §6.
- Operator workstation has `onevm`, `oneflow-template`, `kubectl`, `jq`, `curl`, `base64` on PATH.
- `OPERATOR_CIDR`, `EDGE_HOST`, `K8S_VERSION` env vars set (see [`opennebula/runbook.md`](../opennebula/runbook.md) §0).

---

## 1. Instantiate the cluster

```bash
oneflow-template instantiate aircraft \
    --custom_attrs "{\"OPERATOR_CIDR\":\"${OPERATOR_CIDR}\",\
                    \"K8S_EDGE_HOST\":\"${EDGE_HOST}\",\
                    \"K8S_VERSION\":\"${K8S_VERSION}\"}"
```

Wait for `oneflow show aircraft` to report both roles in state **RUNNING** (~10 minutes).

✅ Pass condition: `oneflow show aircraft | grep -E 'control-plane|workers' | grep -v RUNNING` returns nothing.

---

## 2. Extract the kubeconfig

```bash
onevm show cp-1 --json \
  | jq -r '.VM.USER_TEMPLATE.KUBECONFIG_B64' \
  | base64 -d > ~/.kube/aircraft.config
export KUBECONFIG=~/.kube/aircraft.config
kubectl get nodes
```

✅ Pass condition: 3 nodes Ready, 1 control-plane + 2 workers.

---

## 3. Gate 1 — cluster-ready

```bash
EDGE_HOST="${EDGE_HOST}" tests/opennebula/cluster-ready.sh
```

✅ Pass condition: exit 0.

❌ On failure: do NOT proceed. See [`tests/opennebula/cluster-ready.sh`](../tests/opennebula/cluster-ready.sh) error messages; they map 1:1 to a contract checked-in [`plans/opennebula.md`](../plans/opennebula.md) §3.3.

---

## 4. Update the GitHub Actions `KUBE_CONFIG` secret

The CI self-hosted runner picks up the new cluster only after the secret is rotated.

```bash
base64 -i ~/.kube/aircraft.config -o /tmp/kubeconfig.b64
gh secret set KUBE_CONFIG -b "$(cat /tmp/kubeconfig.b64)" -R "<owner>/<repo>"
rm -f /tmp/kubeconfig.b64
```

---

## 5. Wave 1 — apply controller + registry

Resolves the chicken-and-egg described in [`plans/opennebula.md`](../plans/opennebula.md) §5.2.

```bash
kubectl apply -k k8s/sealed-secrets
kubectl -n kube-system rollout status deploy/sealed-secrets-controller --timeout=120s

kubectl apply -k k8s/registry
kubectl -n ns-registry rollout status deploy/registry --timeout=120s
```

✅ Pass condition: `kubectl -n ns-registry get pods` shows `registry` Pod `Running 1/1`.

---

## 6. Gate 2 — registry-trust

The registry is up but empty. Verify EVERY worker actually trusts it (containerd config + live-fire pull). The script falls back gracefully on the "registry empty" case by probing the v2 catalog endpoint.

```bash
tests/opennebula/registry-trust.sh
```

✅ Pass condition: exit 0.

> If `registry-trust.sh` complains the probe image is not in the registry yet, that's the expected pre-Wave-1.5 state — skip ahead to §7, then re-run this gate after CI has pushed at least one image.

---

## 7. Wave 1.5 — CI pushes app images

Trigger any `git push` that exercises the four service workflows (or run them manually from the GitHub Actions UI).

```bash
# Verify the four images landed:
kubectl -n ns-registry port-forward svc/registry 5000:5000 &
PF_PID=$!
sleep 2
curl -s http://localhost:5000/v2/_catalog | jq
# Expected: {"repositories":["users-service","fleet-service","booking-service","vue-frontend"]}
kill $PF_PID
```

✅ Pass condition: all four repositories present.

Now re-run §6's gate:

```bash
tests/opennebula/registry-trust.sh
```

---

## 8. Wave 2 — apply the application overlay

```bash
kubectl apply -k k8s/overlays/opennebula
kubectl -n ns-users    rollout status deploy/users-service    --timeout=300s
kubectl -n ns-fleet    rollout status deploy/fleet-service    --timeout=300s
kubectl -n ns-booking  rollout status deploy/booking-service  --timeout=300s
kubectl -n ns-frontend rollout status deploy/vue-frontend     --timeout=300s
```

✅ Pass condition: each rollout reaches `successfully rolled out`.

---

## 9. Gate 3 — post-cut-over validation

```bash
EDGE_HOST="${EDGE_HOST}" tests/opennebula/post-cutover-validation.sh
```

✅ Pass condition: exit 0.

This is the load-bearing gate — it covers namespace labels, replica counts, Postgres reachability matrix (including the negative case from `default`), RabbitMQ consumers, HPA metric freshness, the four `/health` endpoints over the edge, and the CSP-whitelist patch applied to the frontend Ingress.

---

## 10. Gate 4 — network-policy chaos

```bash
tests/k8s/network-policy.sh
```

✅ Pass condition: exit 0 (cross-namespace traffic without the label match is dropped).

---

## 11. Day-2 / decommission

The cut-over is complete. The previous cluster (if any) can now be torn down (in this case it already is — §2.1 was torn down before this cut-over started).

If a roll-back is ever needed: the old kubeconfig stays in the GitHub Actions audit log of the last `gh secret set` call. Restore it by replaying §4 with the previous value. Note: the old cluster must still exist; if it has been deleted, roll-back means **re-instantiating** the oneflow service from §1, not resurrecting the previous one.

---

## 12. Troubleshooting quick reference

| Symptom | Likely cause | Resolution |
|---|---|---|
| `kubectl get nodes` returns 1 node | wk-* cloud-init failed; `K8S_JOIN_COMMAND` never published | `onevm ssh cp-1 -- cat /var/log/cloud-init-output.log` to inspect cp-1's step #6 |
| Pods stuck `ImagePullBackOff` from in-cluster registry | containerd hosts.toml not applied OR registry empty | re-run §6's gate; check `/etc/containerd/certs.d/...` on the failing node |
| `cluster-ready.sh` fails on metrics-server | self-signed kubelet TLS — patch missed | re-apply `--kubelet-insecure-tls` arg (see [`opennebula/context/cloud-init.cp.yaml`](../opennebula/context/cloud-init.cp.yaml) step #4) |
| Frontend SPA loads but XHR is blocked by CSP | overlay CSP patch missing | re-run §9; check `kubectl -n ns-frontend get ingress frontend-ingress -o yaml` annotations |
| HPAs stuck at `<unknown>` | metrics-server not Ready | `kubectl -n kube-system get pods -l k8s-app=metrics-server` |
| Booking creates fail with 503 from `fleet-service` | cross-namespace NetworkPolicy missing | re-run §10; check [`k8s/network-policies/allow-fleet.yaml`](../k8s/network-policies/allow-fleet.yaml) |
