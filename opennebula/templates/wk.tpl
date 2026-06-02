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

CPU              = "4"
VCPU             = "4"
MEMORY           = "8192"

# NOTE: same image-name caveat as cp.tpl -- must match `oneimage list`.
DISK = [
    IMAGE      = "Ubuntu 22.04",
    SIZE       = "40960",
    DEV_PREFIX = "vd"
]

NIC = [
    NETWORK         = "aircraft-vnet",
    SECURITY_GROUPS = "aircraft-cluster,aircraft-nodeport"
]

GRAPHICS = [
    TYPE   = "VNC",
    LISTEN = "0.0.0.0"
]

CONTEXT = [
    NETWORK                 = "YES",
    SSH_PUBLIC_KEY          = "$USER[SSH_PUBLIC_KEY]",
    K8S_NODE_ROLE           = "worker",
    K8S_VERSION             = "1.30",
    K8S_CONTROL_PLANE_VM    = "cp-1",
    K8S_JOIN_COMMAND_SOURCE = "USER_TEMPLATE/K8S_JOIN_COMMAND",
    START_SCRIPT_BASE64     = "BASE64_OF_CLOUD_INIT_WK_YAML"
]

USER_INPUTS = [
    K8S_VERSION = "M|list|Kubernetes minor version|1.30,1.31|1.30"
]

SCHED_RANK = "-RUNNING_VMS"

OS = [
    BOOT = "disk0",
    ARCH = "x86_64"
]
