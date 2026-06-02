# =====================================================================
# OpenNebula VM template -- Kubernetes worker node (wk-1 / wk-2).
#
# Same shape as cp.tpl, with three intentional differences:
#   1. Larger capacity (4 vCPU / 8 GiB / 40 GiB) -- workers host the
#      app workloads; HPAs scale to 10 replicas in some scenarios,
#      and the booking-service load test in deploy.md 6 has to land
#      somewhere.
#   2. Contextualisation loads cloud-init.wk.yaml, which runs
#      `kubeadm join` instead of `kubeadm init`.
#   3. Workers are NOT placed in the `aircraft-edge` security group --
#      80/443/6443 are control-plane-only. They DO get the
#      `aircraft-nodeport` SG so NodePort traffic from the edge can
#      reach them (used by ingress-nginx in some failover modes).
#
# The oneflow service in opennebula/service/aircraft.oneflow.json
# instantiates this template TWICE with role-cardinality=2.
# =====================================================================

NAME             = "aircraft-wk"
DESCRIPTION      = "Aircraft SaaS Kubernetes worker (joins cp-1 via kubeadm)"

# Sizing reduced for the lab tenancy. Originally 4 vCPU / 8 GiB; the lab
# front-end can't accommodate two workers of that size. VCPU drops to 1
# (the OpenNebula default), MEMORY to 2 GiB. Pod scheduling will still
# work for the Aircraft SaaS demo workloads (which sit at ~200 MiB
# requests each); HPAs that scale to 10 replicas will be capped by
# node capacity rather than CPU/memory pressure on the kubelet itself.
CPU              = "1"
VCPU             = "1"
MEMORY           = "2048"

# NOTE: same image-name caveat as cp.tpl -- must match `oneimage list`.
DISK = [
    IMAGE      = "Ubuntu 22.04",
    SIZE       = "40960",
    DEV_PREFIX = "vd"
]

# NETWORK_UNAME: see comment in cp.tpl. OneFlow services run as their
# OWNER user, not as the creator -- so even if you ran `onetemplate
# create` as oneadmin, the actual VM instantiation later fails with
# "User X does not own a network with name: aircraft-vnet" unless this
# is set explicitly.
NIC = [
    NETWORK         = "aircraft-vnet",
    NETWORK_UNAME   = "oneadmin",
    SECURITY_GROUPS = "aircraft-cluster,aircraft-nodeport"
]

GRAPHICS = [
    TYPE   = "VNC",
    LISTEN = "0.0.0.0"
]

# RAW serial console attempted previously but OpenNebula 7's
# domain.rng schema rejects the inline <serial>/<console> XML. Debug
# via VNC or via /var/log/aircraft-wk-bootstrap.log instead.

# IMPORTANT: TOKEN="YES" and REPORT_READY="YES" are REQUIRED so that
# OpenNebula injects ONEGATE_ENDPOINT + TOKENTXT into the VM. The
# poll loop in bootstrap-wk.sh calls `onegate vm show` / `onegate
# service show` to find the K8S_JOIN_COMMAND that cp-1 published.
# Without these, onegate exits non-zero ("ONEGATE_ENDPOINT not set")
# and the worker hangs at the join step.
#
# K8S_CONTROL_PLANE_VM here is the OneFlow-generated VM name. OneFlow
# auto-names role VMs as "<role>_<cardinality-index>_(service_<SID>)",
# so the cp is "controlplane_0_(service_<SID>)". We pass the partial
# match "controlplane" and bootstrap-wk.sh has a fallback that picks
# the VM by ROLE_NAME=controlplane via `onegate service show`.
CONTEXT = [
    NETWORK                 = "YES",
    TOKEN                   = "YES",
    REPORT_READY            = "YES",
    SSH_PUBLIC_KEY          = "$USER[SSH_PUBLIC_KEY]",
    K8S_NODE_ROLE           = "worker",
    K8S_VERSION             = "1.30",
    K8S_CONTROL_PLANE_VM    = "controlplane",
    K8S_JOIN_COMMAND_SOURCE = "USER_TEMPLATE/K8S_JOIN_COMMAND",
    START_SCRIPT_BASE64     = "BASE64_OF_BOOTSTRAP_WK"
]

USER_INPUTS = [
    K8S_VERSION = "M|list|Kubernetes minor version|1.30,1.31|1.30"
]

SCHED_RANK = "-RUNNING_VMS"

OS = [
    BOOT = "disk0",
    ARCH = "x86_64"
]
