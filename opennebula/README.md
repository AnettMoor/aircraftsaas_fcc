# `opennebula/` — Aircraft SaaS IaaS automation

> **Authoritative plan:** [`plans/opennebula.md`](../plans/opennebula.md). This README is the operator-facing runbook; the plan explains the **why** behind every file in this directory.

The contents of this directory provision a 3-VM Kubernetes cluster on OpenNebula:

- **cp-1** — control-plane (2 vCPU, 4 GiB, 40 GiB)
- **wk-1**, **wk-2** — workers (4 vCPU, 8 GiB, 40 GiB each)
- L2-isolated `10.10.0.0/24` virtual network with NAT egress and a single :80/:443 inbound DNAT
- Three stacked security groups (`cluster`, `edge`, `nodeport`)
- Cloud-init scripts that run `kubeadm init` on cp-1 + `kubeadm join` on workers + apply Calico

Once the cluster is up, [`k8s/overlays/opennebula/kustomization.yaml`](../k8s/overlays/opennebula/kustomization.yaml) takes over and deploys the application stack (Users / Fleet / Booking services + Vue frontend + Postgres + RabbitMQ + Ingress + cert-manager).

---

## Directory layout

| Path | Purpose |
|---|---|
| [`render.sh`](render.sh) | One-shot script that substitutes operator IP + numeric template IDs into the templates and writes parser-clean copies to `/tmp/onerender/`. **Always run this first.** |
| [`templates/cp.tpl`](templates/cp.tpl) | OpenNebula VM template — control-plane |
| [`templates/wk.tpl`](templates/wk.tpl) | OpenNebula VM template — worker |
| [`vnet/aircraft-vnet.tpl`](vnet/aircraft-vnet.tpl) | L2-isolated `10.10.0.0/24` virtual network |
| [`security-groups/cluster.tpl`](security-groups/cluster.tpl) | Intra-cluster control-plane + kubelet + Calico ports |
| [`security-groups/edge.tpl`](security-groups/edge.tpl) | Public `:80` / `:443` + restricted `:6443` (cp-1 only) |
| [`security-groups/nodeport.tpl`](security-groups/nodeport.tpl) | `30000-32767/tcp` from the edge NIC only (wk-* only) |
| [`context/cloud-init.cp.yaml`](context/cloud-init.cp.yaml) | First-boot bootstrap for cp-1 (kubeadm init, Calico, Ingress) |
| [`context/cloud-init.wk.yaml`](context/cloud-init.wk.yaml) | First-boot bootstrap for wk-* (kubeadm join) |
| [`service/aircraft.oneflow.yaml`](service/aircraft.oneflow.yaml) | Source YAML for the OneFlow service template (converted to JSON by `render.sh`) |
| [`runbook.md`](runbook.md) | Extended verification / troubleshooting steps |

---

## Why `render.sh` exists

The templates in this directory cannot be fed directly to `one*` CLIs because three things vary per tenancy and per operator workstation:

1. **Numeric VM template IDs** — OpenNebula assigns these at `onetemplate create` time. OneFlow service templates require the numeric ID (not the name).
2. **Operator public IP** — embedded in `security-groups/edge.tpl` to restrict `:6443` (kube-apiserver) access.
3. **Cloud-init payloads** — must be base64-inlined into the VM templates' `CONTEXT/START_SCRIPT_BASE64` slot.

Additionally, `oneflow-template create` requires **JSON**, not YAML, even though the source file's natural format is YAML. `render.sh` does the YAML→JSON conversion as part of step (1) above.

The previous version of the templates relied on macros like `$NETWORK[aircraft-vnet]` and `$CONTEXT[OPERATOR_CIDR]`. Those macros are only resolved when the templates are inlined into a VM template — they do NOT resolve when the SG / oneflow files are fed standalone to `onesecgroup create` / `oneflow-template create`. That is why the previous runbook failed with `Wrong NETWORK_ID` and `unexpected character: '#'`. The templates have now been rewritten to use literal CIDRs (`10.10.0.0/24`) and to be rendered by `render.sh` before consumption.

---

## Quick-start runbook (copy-paste)

Run these from a shell on the **OpenNebula front-end** (where the `one*` CLIs are configured) with the repo checked out at `~/aircraftsaas_fcc`:

```bash
cd ~/aircraftsaas_fcc

# 0. Pre-flight
export OPERATOR_IP=$(curl -s https://ifconfig.me)
echo "Operator IP: $OPERATOR_IP"
one user show   # sanity check: ~/.one/one_auth is configured

# 1. Render templates (substitutes operator IP, inlines cloud-init)
./opennebula/render.sh

# 2. Provision the network layer
onevnet     create /tmp/onerender/aircraft-vnet.tpl
onesecgroup create /tmp/onerender/sg-cluster.tpl
onesecgroup create /tmp/onerender/sg-edge.tpl
onesecgroup create /tmp/onerender/sg-nodeport.tpl

# 3. Register the VM templates
onetemplate create /tmp/onerender/cp.tpl
onetemplate create /tmp/onerender/wk.tpl

# 4. Re-run render.sh now that the VM templates exist
#    (this generates aircraft.oneflow.json with the numeric IDs).
./opennebula/render.sh

# 5. Register and instantiate the oneflow service.
#    NOTE: oneflow-template's flag for passing custom_attrs varies by
#    version. Try these in order until one works:
#      (a) --custom_attr KEY=value   (modern)
#      (b) --custom-attr KEY=value   (dash variant)
#      (c) Interactive mode: `oneflow-template instantiate <id> -i`
#          and answer the prompts.
#      (d) Pass a one-shot JSON file via the API (most reliable):
oneflow-template create /tmp/onerender/aircraft.oneflow.json

# Look up the template ID that `create` just printed:
TEMPLATE_ID=$(oneflow-template list --no-header | awk '/aircraft/ {print $1; exit}')
echo "OneFlow template ID: $TEMPLATE_ID"

# Build the merge_template JSON inline -- this is the form that works
# across all OneFlow CLI versions (it's what the web UI uses internally):
cat > /tmp/onerender/instantiate.json <<EOF
{
  "merge_template": {
    "custom_attrs_values": {
      "K8S_EDGE_HOST": "aircraft.example.com",
      "K8S_VERSION":   "1.30",
      "OPERATOR_CIDR": "${OPERATOR_IP}/32"
    }
  }
}
EOF

oneflow-template instantiate $TEMPLATE_ID < /tmp/onerender/instantiate.json

# 6. Watch the cluster boot (5-10 minutes)
watch -n 5 'oneflow show aircraft; echo; onevm list'

# 7. Pull the kubeconfig once cp-1 reports K8S_JOIN_COMMAND in its USER_TEMPLATE
CP_ID=$(onevm list --no-header | awk '/cp-1/ {print $1; exit}')
onevm show $CP_ID --json \
  | jq -r '.VM.USER_TEMPLATE.KUBECONFIG_B64' \
  | base64 -d > ~/.kube/aircraft.config
export KUBECONFIG=~/.kube/aircraft.config
kubectl get nodes -o wide   # must show 3 Ready nodes

# 8. Acceptance gate
bash tests/opennebula/cluster-ready.sh
```

From here, the application stack is deployed via the standard k8s overlay; see [`plans/deploy.md`](../plans/deploy.md) Phase A or my deployment summary in chat.

---

## Common errors and how `render.sh` prevents them

| Symptom | Root cause | How render.sh fixes it |
|---|---|---|
| `[one.secgroup.allocate] Wrong NETWORK_ID.` | `cluster.tpl` referenced `$NETWORK[aircraft-vnet]` — only resolves when inlined into a VM template, not standalone | Templates now use `SOURCE_PREFIX = "10.10.0.0/24"` (literal CIDR) |
| `Parse error: syntax error, unexpected CBRACKET, expecting VARIABLE` | `edge.tpl` had a trailing comma + in-bracket `#` comment in a `RULE = [...]` block | Templates rewritten without in-bracket comments or trailing commas |
| `[oneflow-template create] unexpected character: '#' at line 1 column 1` | `oneflow-template create` requires JSON, not YAML | `render.sh` does YAML→JSON conversion via `python3 -m yaml + json.dumps` |
| `KEY: 'name' must match regexp /^\w+$/` | OneFlow rejected role name `control-plane` (hyphen not in `\w`) | Source YAML now uses `controlplane` (no hyphen) |
| `vm_template ID not numeric` | OneFlow requires numeric template ID, source YAML had `"aircraft-cp"` | `render.sh` looks up the IDs via `onetemplate list` and substitutes them |

---

## What this directory does NOT do

- Build or publish the Aircraft SaaS application images — that is owned by the GitHub Actions self-hosted runner in [`k8s/ci/`](../k8s/ci/) (see [`plans/deploy.md`](../plans/deploy.md) §4).
- Deploy any Kubernetes workload — that is owned by [`k8s/overlays/opennebula/kustomization.yaml`](../k8s/overlays/opennebula/kustomization.yaml) and applied AFTER the gates above pass.
- Manage day-2 operations (node rolling upgrade, kubelet rotation) — the runbook documents the manual procedure; an Ansible play is explicitly out of scope (see [`plans/opennebula.md`](../plans/opennebula.md) §8).
