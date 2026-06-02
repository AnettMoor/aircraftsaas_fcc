# =====================================================================
# OpenNebula VM template — Kubernetes control-plane node (cp-1).
#
# Consumed via:
#   onetemplate create opennebula/templates/cp.tpl
#   onetemplate instantiate <id> --name cp-1
#
# Sizing comes straight from plans/opennebula.md §3.1:
#   * 2 vCPU
#   * 4 GiB RAM
#   * 40 GiB root disk on the cluster datastore
#
# Contextualisation is intentionally split out into a separate file
# (opennebula/context/cloud-init.cp.yaml) so the template and the
# bootstrap script can evolve independently. The template wires that
# file in via USER_INPUTS so an operator can override it from the CLI
# without editing this file (e.g. for a one-off debugging boot with
# `kubeadm init` disabled).
#
# Network: a single NIC on the aircraft vNet (10.10.0.0/24). The
# control-plane node is reachable from the OpenNebula edge DNAT on
# :6443 (kube-apiserver) and from the workers on the same NIC.
# =====================================================================

NAME             = "aircraft-cp"
DESCRIPTION      = "Aircraft SaaS Kubernetes control-plane (1 cp, 2 wk topology)"

# ---------------------------------------------------------------------
# Capacity
# ---------------------------------------------------------------------
CPU              = "2"
VCPU             = "2"
MEMORY           = "4096"          # MiB

# ---------------------------------------------------------------------
# OS image — Ubuntu 22.04 LTS from the OpenNebula Marketplace.
# IMAGE_ID is set by the operator at instantiation time so this
# template doesn't pin a tenancy-local ID (which would break on a
# clean tenancy). The runbook describes how to discover it via
# `onemarket list` / `oneimage list`.
# ---------------------------------------------------------------------
DISK = [
    IMAGE     = "ubuntu-2204-lts",
    SIZE      = "40960",           # MiB — 40 GiB
    DEV_PREFIX = "vd"
]

# ---------------------------------------------------------------------
# Networking — single NIC on the isolated cluster vNet.
# The security group set is layered: `cluster` allows intra-cluster
# control-plane traffic; `edge` permits the operator-side inbound for
# 6443 (via DNAT) plus 80/443 routed through this node's NGINX Ingress.
# ---------------------------------------------------------------------
NIC = [
    NETWORK         = "aircraft-vnet",
    SECURITY_GROUPS = "aircraft-cluster,aircraft-edge"
]

# ---------------------------------------------------------------------
# Graphics — VNC for emergency console access only. The runbook never
# uses this in the happy path; cloud-init is the source of truth.
# ---------------------------------------------------------------------
GRAPHICS = [
    TYPE   = "VNC",
    LISTEN = "0.0.0.0"
]

# ---------------------------------------------------------------------
# Contextualisation — loads cloud-init.cp.yaml verbatim, and exposes
# two variables the cloud-init reads:
#   * K8S_EDGE_HOST     — DNS name pinned into the kubeadm
#                         --control-plane-endpoint flag.
#   * K8S_VERSION       — apt-pinned kubeadm / kubelet / kubectl version
#                         (must match tests/opennebula/cluster-ready.sh
#                         EXPECTED_K8S_MINOR).
#
# OpenNebula injects $TOKENTXT (used by workers to read the join
# command) as a private user-data field; cp-1's cloud-init writes the
# generated join command into a USER_TEMPLATE attribute via the
# `one_context` helper so wk-1/wk-2 can read it from their own
# contextualisation.
# ---------------------------------------------------------------------
CONTEXT = [
    NETWORK         = "YES",
    SSH_PUBLIC_KEY  = "$USER[SSH_PUBLIC_KEY]",
    K8S_NODE_ROLE   = "control-plane",
    K8S_EDGE_HOST   = "aircraft.example.com",
    K8S_VERSION     = "1.30",
    POD_CIDR        = "192.168.0.0/16",
    USERDATA_ENCODING = "base64",
    USER_DATA       = "$FILE[opennebula/context/cloud-init.cp.yaml]"
]

# ---------------------------------------------------------------------
# Operator-overridable inputs surfaced by `onetemplate instantiate -i`.
# These are NOT secrets — they merely tune the bootstrap.
# ---------------------------------------------------------------------
USER_INPUTS = [
    K8S_EDGE_HOST = "M|text|Edge DNS name the operator will DNAT into :6443| |aircraft.example.com",
    K8S_VERSION   = "M|list|Kubernetes minor version|1.30,1.31|1.30"
]

# ---------------------------------------------------------------------
# Scheduling — the control-plane stays pinned (HOST_REQUIREMENTS) so
# its IP/MAC bindings on the vNet are stable. Workers float.
# ---------------------------------------------------------------------
SCHED_REQUIREMENTS = "CLUSTER_NAME=\"aircraft\""

# ---------------------------------------------------------------------
# OS boot order — disk first, then network (PXE fallback explicitly
# disabled to keep boots deterministic).
# ---------------------------------------------------------------------
OS = [
    BOOT = "disk0",
    ARCH = "x86_64"
]
