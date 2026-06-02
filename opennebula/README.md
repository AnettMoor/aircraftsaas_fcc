# `opennebula/` — Aircraft SaaS IaaS automation

> **Authoritative plan:** [`plans/opennebula.md`](../plans/opennebula.md). This README is the operator-facing entry point; the plan explains the **why** behind every file in this directory.

The contents of this directory **replace** the manually-built 3-VM Kubernetes cluster of `plans/deploy.md` §2.1 (which has been torn down). From now on, every `kubectl apply -k k8s/overlays/opennebula` run targets a cluster produced by the automation defined here.

## Directory layout

| Path | Purpose |
|---|---|
| [`templates/cp.tpl`](templates/cp.tpl) | OpenNebula VM template — control-plane (2 vCPU / 4 GiB / 40 GiB) |
| [`templates/wk.tpl`](templates/wk.tpl) | OpenNebula VM template — worker (4 vCPU / 8 GiB / 40 GiB) |
| [`vnet/aircraft-vnet.tpl`](vnet/aircraft-vnet.tpl) | L2-isolated `10.10.0.0/24` virtual network |
| [`security-groups/cluster.tpl`](security-groups/cluster.tpl) | Intra-cluster control-plane + kubelet + Calico ports |
| [`security-groups/edge.tpl`](security-groups/edge.tpl) | Public `:80` / `:443` + restricted `:6443` (cp-1 only) |
| [`security-groups/nodeport.tpl`](security-groups/nodeport.tpl) | `30000-32767/tcp` from the edge NIC only (wk-* only) |
| [`context/cloud-init.cp.yaml`](context/cloud-init.cp.yaml) | First-boot bootstrap for cp-1 (kubeadm init, Calico, Ingress) |
| [`context/cloud-init.wk.yaml`](context/cloud-init.wk.yaml) | First-boot bootstrap for wk-* (kubeadm join) |
| [`service/aircraft.oneflow.yaml`](service/aircraft.oneflow.yaml) | OneFlow service template wiring cp + wk roles with role dependency |
| [`runbook.md`](runbook.md) | Step-by-step `onetemplate` / `onevnet` / `onesecgroup` / `oneflow-template` commands |

## Quick start

```bash
# 1. Pre-requisites on the operator workstation:
#      onetemplate / onevm / oneflow-template / oneflow CLIs configured
#      against the target OpenNebula tenancy. OPENNEBULA_AUTH file with
#      "<user>:<password>" present in ~/.one/one_auth.

# 2. Provision in dependency order (details in runbook.md):
onevnet     create opennebula/vnet/aircraft-vnet.tpl
onesecgroup create opennebula/security-groups/cluster.tpl
onesecgroup create opennebula/security-groups/edge.tpl
onesecgroup create opennebula/security-groups/nodeport.tpl
onetemplate create opennebula/templates/cp.tpl
onetemplate create opennebula/templates/wk.tpl
oneflow-template create opennebula/service/aircraft.oneflow.yaml

# 3. Instantiate the cluster. OperatorCIDR must be provided.
oneflow-template instantiate aircraft \
    --custom_attrs '{"OPERATOR_CIDR":"203.0.113.42/32"}'

# 4. Wait for both roles to enter RUNNING, then extract the kubeconfig
#    that cp-1's cloud-init published into the VM's USER_TEMPLATE:
onevm show cp-1 --json \
  | jq -r '.VM.USER_TEMPLATE.KUBECONFIG_B64' \
  | base64 -d > ~/.kube/aircraft.config
export KUBECONFIG=~/.kube/aircraft.config

# 5. Acceptance gates — MUST all exit 0 before the cut-over runbook
#    in docs/opennebula-cutover.md is allowed to proceed.
tests/opennebula/cluster-ready.sh
tests/opennebula/registry-trust.sh
tests/opennebula/post-cutover-validation.sh
tests/k8s/network-policy.sh
```

See [`runbook.md`](runbook.md) for the full breakdown and [`docs/opennebula-cutover.md`](../docs/opennebula-cutover.md) for the cut-over checklist that ties this directory back into the PaaS overlay in [`k8s/overlays/opennebula/kustomization.yaml`](../k8s/overlays/opennebula/kustomization.yaml).

## What this directory does NOT do

- Build or publish the Aircraft SaaS application images — that is owned by the GitHub Actions self-hosted runner in [`k8s/ci/`](../k8s/ci/) (see `plans/deploy.md` §4).
- Deploy any Kubernetes workload — that is owned by [`k8s/overlays/opennebula/kustomization.yaml`](../k8s/overlays/opennebula/kustomization.yaml) and applied AFTER the gates above pass.
- Manage day-2 operations (node rolling upgrade, kubelet rotation) — the runbook documents the manual procedure; an Ansible play is explicitly out of scope (see [`plans/opennebula.md`](../plans/opennebula.md) §8).
