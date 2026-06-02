# `opennebula/` — Aircraft SaaS IaaS automation

> **Authoritative plan:** [`plans/opennebula.md`](../plans/opennebula.md). This README is the operator-facing runbook; the plan explains the **why** behind every file in this directory.

The contents of this directory provision a 3-VM Kubernetes cluster on OpenNebula:

- **cp-1** — control-plane (2 vCPU, 4 GiB, 40 GiB)
- **wk-1**, **wk-2** — workers (4 vCPU, 8 GiB, 40 GiB each)
- vNet on minione's pre-existing `minionebr` bridge — `172.16.100.0/24`, AR `172.16.100.50-.59` (NAT egress + DNS + OneGate already wired by minione's installer). cp-1 deterministically lands on `.50`. **Production OpenNebula deployments should re-pin this back to an isolated `aircraft-br0` / `10.10.0.0/24` bridge** — see the header of [`vnet/aircraft-vnet.tpl`](vnet/aircraft-vnet.tpl) for the exact diff.
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
| [`vnet/aircraft-vnet.tpl`](vnet/aircraft-vnet.tpl) | Cluster vNet — rides on minione's `minionebr` / `172.16.100.0/24` |
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
#
# OpenNebula 7.x `oneflow-template instantiate` signature is:
#    instantiate <templateid> [<file>]
# It takes NO flags for custom attrs (no --params / --custom_attr / -i).
# Pass a JSON service-definition override either as a positional file
# argument OR on stdin. The override shape that 7.0.1 accepts is the
# bare `custom_attrs_values` map at top level (NOT wrapped in
# merge_template — that wrapper is rejected with:
#   "The Service template specifies User Inputs but no values have been found"
# Output the JSON with `cat <<EOF` (NOT <<'EOF') so ${OPERATOR_IP} expands.
oneflow-template create /tmp/onerender/aircraft.oneflow.json

# Look up the template ID that `create` just printed:
TEMPLATE_ID=$(oneflow-template list --no-header | awk '/aircraft/ {print $1; exit}')
echo "OneFlow template ID: $TEMPLATE_ID"

cat > /tmp/onerender/instantiate.json <<EOF
{
  "custom_attrs_values": {
    "K8S_EDGE_HOST": "aircraft.example.com",
    "K8S_VERSION":   "1.30",
    "OPERATOR_CIDR": "${OPERATOR_IP}/32"
  }
}
EOF

# Sanity check that the JSON is what you expect AND that ${OPERATOR_IP}
# expanded (no literal "${OPERATOR_IP}" string remaining):
cat /tmp/onerender/instantiate.json
grep -q OPERATOR_IP /tmp/onerender/instantiate.json && \
    { echo "ERROR: \${OPERATOR_IP} did not expand. Did you forget to 'export OPERATOR_IP=...'?"; exit 1; }

# Instantiate. Either form works on 7.0.1:
#   (a) positional file:
oneflow-template instantiate $TEMPLATE_ID /tmp/onerender/instantiate.json
#   (b) stdin:
#       oneflow-template instantiate $TEMPLATE_ID < /tmp/onerender/instantiate.json

# 6. Watch the cluster boot (5-10 minutes). Both roles must reach RUNNING.
watch -n 5 'oneflow show aircraft; echo; onevm list'

# 7. Pull the kubeconfig once cp-1 reports KUBECONFIG_B64 in its USER_TEMPLATE.
#
#    OneFlow auto-names VMs by role+service (e.g. "controlplane_0_(service_8)"),
#    NOT "cp-1". Find the CP VM ID by matching the VM template ID, which we
#    captured in /tmp/onerender/cp-template-id during render.sh:
CP_TPL_ID=$(awk '$4=="aircraft-cp" {print $1; exit}' < <(onetemplate list --no-header))
echo "aircraft-cp template ID: $CP_TPL_ID"

# Find the running VM that was instantiated from that template
CP_ID=$(onevm list --no-header --filter "TEMPLATE_ID=$CP_TPL_ID" 2>/dev/null \
        | awk '{print $1; exit}')

# Fallback if --filter isn't supported on your OpenNebula version: scan all
# VMs and check each one's TEMPLATE/TEMPLATE_ID:
if [ -z "$CP_ID" ]; then
  for vmid in $(onevm list --no-header | awk '{print $1}'); do
    if [ "$(onevm show $vmid --json 2>/dev/null | jq -r '.VM.TEMPLATE.TEMPLATE_ID // empty')" = "$CP_TPL_ID" ]; then
      CP_ID=$vmid
      break
    fi
  done
fi
echo "CP VM ID: $CP_ID"

# Sanity check: must be non-empty AND in LCM_STATE RUNNING
[ -z "$CP_ID" ] && { echo "ERROR: no VM found from template $CP_TPL_ID. Is the cluster RUNNING yet?"; }
onevm show $CP_ID | grep -E 'STATE|LCM_STATE'

# Make sure ~/.kube/ exists, then pull the kubeconfig
mkdir -p ~/.kube
onevm show $CP_ID --json \
  | jq -r '.VM.USER_TEMPLATE.KUBECONFIG_B64 // empty' \
  | base64 -d > ~/.kube/aircraft.config

# Validate the kubeconfig is non-empty
if [ ! -s ~/.kube/aircraft.config ]; then
  echo "ERROR: KUBECONFIG_B64 not in cp-1's USER_TEMPLATE yet."
  echo
  echo "Reason: cloud-init wrote it back via 'onegate vm update'."
  echo "If the field is empty, one of the following is true:"
  echo "  (a) cloud-init is still running (kubeadm init + Calico = 3-6 min)"
  echo "  (b) cloud-init failed before reaching runcmd step 7 in"
  echo "      opennebula/context/cloud-init.cp.yaml (line 164)"
  echo "  (c) OneGate is not reachable from the VM (token / endpoint missing"
  echo "      in CONTEXT) so 'onegate vm update' silently no-op'd"
  echo
  echo "Diagnose (the edge SG does NOT open :22, so SSH is only available"
  echo "if you ADD :22 to the edge SG first; otherwise use VNC console):"
  echo
  echo "  1) Watch OneGate / cloud-init progress via USER_TEMPLATE:"
  echo "       watch -n 5 \"onevm show $CP_ID | grep -E 'K8S_JOIN_COMMAND|KUBECONFIG_B64'\""
  echo
  echo "  2) Confirm OneGate is wired in CONTEXT (must show ONEGATE_ENDPOINT + TOKENTXT):"
  echo "       onevm show $CP_ID | grep -E 'ONEGATE_ENDPOINT|TOKENTXT|REPORT_READY'"
  echo
  echo "  3) Open the VNC console of cp-1 to read cloud-init logs:"
  echo "       onevm show $CP_ID | grep -E 'GRAPHICS|PORT|LISTEN'"
  echo "     then connect a VNC client to host:<PORT> and run on the console:"
  echo "       sudo cloud-init status --long"
  echo "       sudo tail -n 200 /var/log/cloud-init-output.log"
  echo
  echo "  4) If this is a single-node OpenNebula (front-end == KVM host), you"
  echo "     can also reach the VM serial via libvirt:"
  echo "       sudo virsh list           # find domain name (one-<vmid>)"
  echo "       sudo virsh console one-$CP_ID"
  echo
  echo "  5) Last-resort SSH fallback (requires you to FIRST add :22 to the"
  echo "     edge SG and have routable IP) -- almost never the right answer:"
  CP_IP=$(onevm show $CP_ID --json \
          | jq -r '(.VM.TEMPLATE.NIC | if type=="array" then .[0].IP else .IP end) // empty')
  if [ -n "$CP_IP" ]; then
    echo "       ssh ubuntu@$CP_IP sudo cat /etc/kubernetes/admin.conf > ~/.kube/aircraft.config"
    echo "       sed -i \"s#server: https://[^:]*:6443#server: https://${OPERATOR_IP}:6443#\" ~/.kube/aircraft.config"
  fi
fi

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
| `KUBECONFIG_B64` never appears in cp-1's USER_TEMPLATE, `K8S_JOIN_COMMAND` also missing, `onevm show $CP_ID \| grep ONEGATE_ENDPOINT` returns empty | Original `cp.tpl` / `wk.tpl` `CONTEXT` blocks lacked `TOKEN="YES"` and `REPORT_READY="YES"`, so OpenNebula never injected `ONEGATE_ENDPOINT` + `TOKENTXT`. Every `onegate vm update` in `cloud-init.cp.yaml` (lines 160 & 164) silently failed. | Added `TOKEN="YES"` and `REPORT_READY="YES"` to the `CONTEXT` block of both [`templates/cp.tpl`](templates/cp.tpl) and [`templates/wk.tpl`](templates/wk.tpl). **Existing instantiated VMs must be terminated and re-instantiated** — context is fixed at boot, not editable in-place. See "Recovering from a broken cluster instantiation" below. |
| `jq: error (at <stdin>:220): Cannot index array with string "IP"` in the kubeconfig fallback block | `jq -r '.VM.TEMPLATE.NIC.IP // .VM.TEMPLATE.NIC[0].IP'` — `jq`'s `//` only handles `null`/`false`, not runtime type errors, so a multi-NIC VM (`NIC` is an array) aborts before the fallback fires. | Replaced with type-aware expression: `(.VM.TEMPLATE.NIC \| if type=="array" then .[0].IP else .IP end) // empty` |
| `The Service template specifies User Inputs but no values have been found` from `oneflow-template instantiate` | Two stacked bugs: (1) the JSON was wrapped in `merge_template` (that wrapper is for `oneflow-template create`, not `instantiate`); (2) OpenNebula 7.x `oneflow-template instantiate` has **no `--params` / `--custom_attr` / `-i` flag** — the help is just `instantiate <templateid> [<file>]`. Passing flags returns `invalid option`. | Runbook now generates a JSON with **bare** top-level `custom_attrs_values` and passes it as a **positional file argument** (or via stdin): `oneflow-template instantiate $TEMPLATE_ID /tmp/onerender/instantiate.json`. Also added a `grep -q OPERATOR_IP` sanity check to catch un-expanded shell variables in the heredoc. |
| VMs are RUNNING, `ONEGATE_ENDPOINT` is populated, but the endpoint points at `http://10.10.0.1:5030` (or similar bridge IP that VMs can't reach) → `KUBECONFIG_B64` and `K8S_JOIN_COMMAND` are never written back to USER_TEMPLATE. | On a **minione** lab, OneGate listens only on the `minionebr` bridge (`172.16.100.1:5030`). The original vNet design used an isolated `aircraft-br0`/`10.10.0.0/24` bridge that has no route to `172.16.100.1`, so every `onegate vm update` from inside the VM times out and the bootstrap appears to hang at "cloud-init RUNNING but no KUBECONFIG_B64". | The vNet now rides on `minionebr` directly (`BRIDGE = "minionebr"`, `172.16.100.0/24`). All three security groups switched to `172.16.100.0/24` as the cluster CIDR. `cloud-init.cp.yaml` certSAN updated to `172.16.100.50`. Production deployments should re-pin to `aircraft-br0` per the comment block in [`vnet/aircraft-vnet.tpl`](vnet/aircraft-vnet.tpl). |

---

## Recovering from a broken cluster instantiation

If you have **any** previously-created OpenNebula objects from before the vNet / OneGate fixes (i.e. cp-1's `CONTEXT.ONEGATE_ENDPOINT` is empty OR points at an unreachable bridge IP like `http://10.10.0.1:5030`), tear everything down and recreate from scratch. The CONTEXT injected at boot is immutable, and the vNet's `BRIDGE` / `NETWORK_ADDRESS` cannot be changed while VMs are using it.

```bash
# 1. Verify the broken state (one of these should be true):
onevm show $CP_ID | grep -E 'ONEGATE_ENDPOINT|TOKENTXT'
# - Empty                                     => template missing TOKEN="YES"
# - ONEGATE_ENDPOINT=http://10.10.0.1:5030    => unreachable bridge IP

# 2. Tear down the OneFlow service (this terminates all 3 VMs)
FLOW_ID=$(oneflow list --no-header | awk '/aircraft/ {print $1; exit}')
[ -n "$FLOW_ID" ] && oneflow delete $FLOW_ID
watch -n 3 'onevm list'   # Ctrl-C when the VMs are gone

# 3. Delete the stale OneFlow service-template (points at old VM template IDs)
OLD_FLOW_TPL=$(oneflow-template list --no-header | awk '/aircraft/ {print $1; exit}')
[ -n "$OLD_FLOW_TPL" ] && oneflow-template delete $OLD_FLOW_TPL

# 4. Delete the VM templates (CONTEXT cannot be updated in-place to add
#    TOKEN="YES" / REPORT_READY="YES")
onetemplate delete aircraft-cp 2>/dev/null
onetemplate delete aircraft-wk 2>/dev/null

# 5. Delete the security groups (CIDRs need to change from
#    10.10.0.0/24 to 172.16.100.0/24 on minione, or vice versa)
onesecgroup delete aircraft-cluster  2>/dev/null
onesecgroup delete aircraft-edge     2>/dev/null
onesecgroup delete aircraft-nodeport 2>/dev/null

# 6. Delete the vNet last (it depends on nothing, but other objects
#    reference it; safe to delete after the above are gone). The BRIDGE
#    + NETWORK_ADDRESS need to change, and OpenNebula refuses to
#    re-create over an existing vnet with the same NAME.
onevnet delete aircraft-vnet 2>/dev/null

# 7. Resume the quick-start runbook from step 1 (export OPERATOR_IP)
#    and run cleanly through onevnet create -> onesecgroup create x3 ->
#    onetemplate create x2 -> render.sh (re-run) -> oneflow-template
#    create -> oneflow-template instantiate.
```

After re-instantiation, this command (with the new VM ID) should show **all** of these fields populated, where previously some were empty / unreachable:

```bash
onevm show <new-cp-id> --json | jq '.VM.TEMPLATE.CONTEXT | {ONEGATE_ENDPOINT, TOKEN, TOKENTXT, REPORT_READY}'
```

Expected on a minione lab:
```json
{
  "ONEGATE_ENDPOINT": "http://172.16.100.1:5030",
  "TOKEN":            "YES",
  "TOKENTXT":         "<long base64 token>",
  "REPORT_READY":     "YES"
}
```

Then, once cloud-init runs `onegate vm update --data "KUBECONFIG_B64=..."`, this:
```bash
onevm show <new-cp-id> | grep -E 'KUBECONFIG_B64|K8S_JOIN_COMMAND'
```
should print both fields with non-trivial values within ~5–7 minutes of boot.

If `TOKENTXT` is still `null` after re-instantiation but `ONEGATE_ENDPOINT` is correct, OneGate is not enabled on the OpenNebula front-end:

```bash
sudo systemctl enable --now opennebula-gate
sudo systemctl status opennebula-gate
ss -tlnp 2>/dev/null | grep 5030
```

---

## What this directory does NOT do

- Build or publish the Aircraft SaaS application images — that is owned by the GitHub Actions self-hosted runner in [`k8s/ci/`](../k8s/ci/) (see [`plans/deploy.md`](../plans/deploy.md) §4).
- Deploy any Kubernetes workload — that is owned by [`k8s/overlays/opennebula/kustomization.yaml`](../k8s/overlays/opennebula/kustomization.yaml) and applied AFTER the gates above pass.
- Manage day-2 operations (node rolling upgrade, kubelet rotation) — the runbook documents the manual procedure; an Ansible play is explicitly out of scope (see [`plans/opennebula.md`](../plans/opennebula.md) §8).
