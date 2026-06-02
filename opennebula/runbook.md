# OpenNebula provisioning runbook — Aircraft SaaS cluster

> Operator-facing companion to [`README.md`](README.md). Walks through the exact CLI invocations needed to stamp a fresh tenancy into the cluster that the PaaS layer in [`k8s/overlays/opennebula/`](../k8s/overlays/opennebula/) expects. **Read [`plans/opennebula.md`](../plans/opennebula.md) first** — this runbook assumes its context.

---

## 0. Pre-flight

Run from a workstation that already has the OpenNebula CLI configured for the target tenancy.

```bash
# Sanity check: must succeed before touching anything else.
oneuser show
onehost list                       # at least 2 hypervisors recommended
onedatastore list                  # need an "images" + a "system" datastore
onemarketapp list | grep -i ubuntu # find Ubuntu 22.04 LTS marketplace app
```

Environment variables expected by the rest of this runbook:

```bash
export ONE_AUTH=~/.one/one_auth                  # OPENNEBULA_AUTH file
export OPERATOR_CIDR="203.0.113.42/32"           # YOUR public IP /32 for kube-apiserver
export EDGE_HOST="aircraft.example.com"          # DNS name DNAT'd to cp-1
export K8S_VERSION="1.30"                        # MUST match tests/opennebula/cluster-ready.sh EXPECTED_K8S_MINOR
```

---

## 1. Import the Ubuntu 22.04 LTS marketplace image

Both [`templates/cp.tpl`](templates/cp.tpl) and [`templates/wk.tpl`](templates/wk.tpl) reference an image named **`ubuntu-2204-lts`**. Create it from the OpenNebula marketplace:

```bash
APPID=$(onemarketapp list -f NAME~"Ubuntu 22.04*" --csv | tail -n1 | cut -d, -f1)
onemarketapp export "$APPID" ubuntu-2204-lts --datastore default
# Wait for the image to enter READY:
onemarketapp show "$APPID" | grep IMAGE_ID    # note the resulting OneImage IDs
oneimage chmod ubuntu-2204-lts 644            # readable by all users in tenancy
```

> **Why this is a manual step, not an automation step**: the marketplace ID drifts per OpenNebula version. Pinning it here would force an automation edit on every OpenNebula upgrade. Importing once per tenancy is acceptable.

---

## 2. Create the virtual network

```bash
onevnet create opennebula/vnet/aircraft-vnet.tpl
onevnet show aircraft-vnet
# Expected: NETWORK_ADDRESS 10.10.0.0, BRIDGE aircraft-br0, AR with 10 free leases.
```

If your OpenNebula host's bridge is named differently (e.g. `br0` instead of `aircraft-br0`), edit `BRIDGE = ...` in [`vnet/aircraft-vnet.tpl`](vnet/aircraft-vnet.tpl) **before** running this step — recreating the vNet later requires deleting all VMs first.

---

## 3. Create the three security groups

Order matters: `aircraft-cluster` is the baseline referenced by the vNet itself.

```bash
onesecgroup create opennebula/security-groups/cluster.tpl
onesecgroup create opennebula/security-groups/edge.tpl
onesecgroup create opennebula/security-groups/nodeport.tpl
onesecgroup list
# Expected three groups: aircraft-cluster, aircraft-edge, aircraft-nodeport.
```

---

## 4. Create the VM templates

```bash
onetemplate create opennebula/templates/cp.tpl
onetemplate create opennebula/templates/wk.tpl
onetemplate list | grep aircraft
# Expected: aircraft-cp, aircraft-wk
```

> **Do NOT instantiate these templates directly.** The oneflow service in §5 wires the role dependency that ensures workers can find the kubeadm join command from cp-1. Driving `onevm instantiate` by hand reproduces the §2.1 manual pain that this automation exists to eliminate.

---

## 5. Create and instantiate the oneflow service

```bash
oneflow-template create opennebula/service/aircraft.oneflow.yaml
ONEFLOW_TID=$(oneflow-template list --csv | awk -F, '$2=="aircraft"{print $1}')

oneflow-template instantiate "$ONEFLOW_TID" \
    --custom_attrs "{\"OPERATOR_CIDR\":\"${OPERATOR_CIDR}\",\
                    \"K8S_EDGE_HOST\":\"${EDGE_HOST}\",\
                    \"K8S_VERSION\":\"${K8S_VERSION}\"}"
```

Watch the role state machine:

```bash
watch -n 5 'oneflow show aircraft | grep -E "Role|state"'
# Expected progression:
#   control-plane: PENDING -> DEPLOYING -> RUNNING
#   workers:       (waits)  -> PENDING   -> DEPLOYING -> RUNNING
# Total time ~10 minutes on a typical OpenNebula tenancy.
```

If the `control-plane` role gets stuck in `FAILED`, inspect cp-1's cloud-init log:

```bash
onevm ssh cp-1 -- tail -n 200 /var/log/cloud-init-output.log
```

---

## 6. Configure the OpenNebula edge DNAT

Outside the scope of this repo (your edge router / OpenNebula gateway), configure DNAT for:

| External | Internal | Purpose |
|---|---|---|
| `${EDGE_HOST}:443` | `10.10.0.10:443` | NGINX Ingress HTTPS |
| `${EDGE_HOST}:80`  | `10.10.0.10:80`  | ACME HTTP-01 challenge (Let's Encrypt) |
| `${EDGE_HOST}:6443` | `10.10.0.10:6443` | kube-apiserver (source-restricted to `OPERATOR_CIDR`) |

Verify DNS resolves to the edge IP before continuing:

```bash
dig +short "${EDGE_HOST}"
```

---

## 7. Extract the kubeconfig

cp-1's cloud-init publishes a kubeconfig (with `server:` rewritten to the edge host) into the VM's USER_TEMPLATE:

```bash
onevm show cp-1 --json \
  | jq -r '.VM.USER_TEMPLATE.KUBECONFIG_B64' \
  | base64 -d > ~/.kube/aircraft.config

export KUBECONFIG=~/.kube/aircraft.config
kubectl get nodes
# Expected: 3 nodes, all Ready, 1 control-plane + 2 workers.
```

---

## 8. Run the acceptance gates

Each gate must exit 0 before proceeding to the next. See [`plans/opennebula.md`](../plans/opennebula.md) §6.

```bash
EDGE_HOST="${EDGE_HOST}" tests/opennebula/cluster-ready.sh
tests/opennebula/registry-trust.sh
tests/opennebula/post-cutover-validation.sh
tests/k8s/network-policy.sh
```

---

## 9. Deploy the workloads (two-wave)

Per [`plans/opennebula.md`](../plans/opennebula.md) §5.1 — resolves the in-cluster-registry chicken-and-egg.

```bash
# Wave 1 — controller + registry (images from Docker Hub via NAT).
kubectl apply -k k8s/sealed-secrets
kubectl apply -k k8s/registry

# Build & push application images via the in-cluster CI runner.
# (Triggered by a normal git push; see .github/workflows/.)
# Wait for the four images to appear in the registry:
curl -fsS http://10.10.0.10:5000/v2/_catalog   # via edge port-forward / kubectl proxy

# Wave 2 — application overlay.
kubectl apply -k k8s/overlays/opennebula
kubectl -n ns-users    rollout status deploy/users-service    --timeout=300s
kubectl -n ns-fleet    rollout status deploy/fleet-service    --timeout=300s
kubectl -n ns-booking  rollout status deploy/booking-service  --timeout=300s
kubectl -n ns-frontend rollout status deploy/vue-frontend     --timeout=300s
```

---

## 10. Rollback / teardown

A single oneflow call removes the entire cluster:

```bash
oneflow delete aircraft
# Cleans up: cp-1, wk-1, wk-2.
# Does NOT clean up: vnet, secgroups, templates, images (operator decision).
```

To also remove the lower layers:

```bash
onetemplate delete aircraft-cp aircraft-wk
onesecgroup delete aircraft-nodeport aircraft-edge aircraft-cluster
onevnet delete aircraft-vnet
oneimage delete ubuntu-2204-lts        # only if you don't intend to redeploy
```

---

## 11. Day-2 — scaling workers

The oneflow `workers` role has `max_vms: 4`. Scale up at the IaaS level, then scale Kubernetes itself:

```bash
oneflow-template scale aircraft workers 4
# New workers run cloud-init.wk.yaml -> kubeadm join -> appear as
# Ready nodes within ~5 min.

# Then bump the HPA max if the scaling envelope needs more headroom:
kubectl -n ns-booking patch hpa booking-service \
    --type=merge -p '{"spec":{"maxReplicas":20}}'
```

---

## 12. Day-2 — Kubernetes version upgrade

Out of scope for this runbook; documented at a high level in [`plans/opennebula.md`](../plans/opennebula.md) §8 (out-of-scope). Short form: bump `K8S_VERSION` in the oneflow instantiation, rolling-replace nodes one at a time, run [`tests/opennebula/cluster-ready.sh`](../tests/opennebula/cluster-ready.sh) after each.
