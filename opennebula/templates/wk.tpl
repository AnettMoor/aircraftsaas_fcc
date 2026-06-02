# =====================================================================
# OpenNebula VM template — Kubernetes worker node (wk-1 / wk-2).
#
# Same shape as cp.tpl, with three intentional differences:
#   1. Larger capacity (4 vCPU / 8 GiB / 40 GiB) — workers host the
#      app workloads, HPAs scale to 10 replicas in some scenarios,
#      and the booking-service load test in deploy.md §6 has to land
#      somewhere.
#   2. Contextualisation loads cloud-init.wk.yaml, which runs
#      `kubeadm join` instead of `kubeadm init`.
#   3. Workers are NOT placed in the `aircraft-edge` security group —
#      80/443/6443 are control-plane-only. They DO get the
#      `aircraft-nodeport` SG so NodePort traffic from the edge can
#      reach them (used by ingress-nginx in some failover modes).
#
# The oneflow service in opennebula/service/aircraft.oneflow.yaml
# instantiates this template TWICE with role-cardinality=2, so the
# template itself has no node-index. The cloud-init reads the
# OpenNebula-assigned VM name (`one-<id>`) and derives a kubelet
# `--node-name` from it.
# =====================================================================

NAME             = "aircraft-wk"
DESCRIPTION      = "Aircraft SaaS Kubernetes worker (joins cp-1 via kubeadm)"

# ---------------------------------------------------------------------
# Capacity — see deploy.md §2.2 (4 vCPU, 8 GiB, 40 GiB).
# ---------------------------------------------------------------------
CPU              = "4"
VCPU             = "4"
MEMORY           = "8192"          # MiB

DISK = [
    IMAGE      = "ubuntu-2204-lts",
    SIZE       = "40960",          # MiB — 40 GiB
    DEV_PREFIX = "vd"
]

# ---------------------------------------------------------------------
# Networking — same vNet as cp-1, but a different SG set:
#   * aircraft-cluster:  intra-cluster 6443/10250.
#   * aircraft-nodeport: 30000-32767/tcp from edge only.
#
# Note the deliberate ABSENCE of `aircraft-edge` (which would open
# 80/443 inbound from anywhere) — that is on the control-plane only.
# Workers are reached only through ingress-nginx or kube-proxy.
# ---------------------------------------------------------------------
NIC = [
    NETWORK         = "aircraft-vnet",
    SECURITY_GROUPS = "aircraft-cluster,aircraft-nodeport"
]

GRAPHICS = [
    TYPE   = "VNC",
    LISTEN = "0.0.0.0"
]

# ---------------------------------------------------------------------
# Contextualisation — loads cloud-init.wk.yaml.
#
# K8S_JOIN_COMMAND_SOURCE points workers at the OpenNebula
# USER_TEMPLATE attribute on the control-plane VM where cp-1's
# cloud-init writes the `kubeadm token create --print-join-command`
# output. The wk cloud-init reads it via `onevm show cp-1 --json`
# from inside the VM (the oneflow service grants it the necessary
# IAM right via OPENNEBULA_AUTH context — see runbook.md).
# ---------------------------------------------------------------------
CONTEXT = [
    NETWORK                 = "YES",
    SSH_PUBLIC_KEY          = "$USER[SSH_PUBLIC_KEY]",
    K8S_NODE_ROLE           = "worker",
    K8S_VERSION             = "1.30",
    K8S_CONTROL_PLANE_VM    = "cp-1",
    K8S_JOIN_COMMAND_SOURCE = "USER_TEMPLATE/K8S_JOIN_COMMAND",
    USERDATA_ENCODING       = "base64",
    USER_DATA               = "$FILE[opennebula/context/cloud-init.wk.yaml]"
]

USER_INPUTS = [
    K8S_VERSION = "M|list|Kubernetes minor version|1.30,1.31|1.30"
]

SCHED_REQUIREMENTS = "CLUSTER_NAME=\"aircraft\""

# Anti-affinity at the IaaS layer: do NOT place both workers on the
# same hypervisor. Combined with topologySpreadConstraints inside
# the cluster (deploy.md §3.3) this means a single host loss removes
# at most one replica of each Deployment.
SCHED_RANK = "-RUNNING_VMS"

OS = [
    BOOT = "disk0",
    ARCH = "x86_64"
]
