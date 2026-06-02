# =====================================================================
# OpenNebula virtual network — aircraft-vnet
#
# Layer-2 isolated /24 carrying all Kubernetes node-to-node traffic.
# Outbound Internet is provided by the OpenNebula NAT on the gateway
# IP; inbound is only the single :80/:443 DNAT into cp-1 that the
# edge security group authorises.
#
# Why this shape (plans/opennebula.md §3.2):
#   * /24 is plenty for 3 cluster nodes + future expansion (the
#     pod-CIDR 192.168.0.0/16 lives inside the cluster, NOT on this
#     vNet — Calico VXLAN-encapsulates it before it touches the wire).
#   * Static address pool, not DHCP: kubeadm uses node IPs as kubelet
#     identity; DHCP renewal would race with Calico's BGP peerings.
#   * BRIDGE name `aircraft-br0` so the OpenNebula host's firewall
#     can match on it explicitly when stacking security groups.
#
# Address allocation policy (matches cloud-init expectations):
#   * .10 → cp-1
#   * .11 → wk-1
#   * .12 → wk-2
#   * .1  → OpenNebula gateway (set by GATEWAY below)
#   * .254 → reserved for the edge DNAT NIC (operator-side)
# =====================================================================

NAME        = "aircraft-vnet"
DESCRIPTION = "L2-isolated /24 for the Aircraft SaaS Kubernetes cluster"

VN_MAD = "bridge"
BRIDGE = "aircraft-br0"

# ---------------------------------------------------------------------
# Subnet — RFC1918 /24, deliberately a different /16 from the pod and
# service CIDRs so the routing table in each node stays unambiguous.
# ---------------------------------------------------------------------
NETWORK_ADDRESS = "10.10.0.0"
NETWORK_MASK    = "255.255.255.0"
GATEWAY         = "10.10.0.1"
DNS             = "10.10.0.1"

# ---------------------------------------------------------------------
# Static address pool. OpenNebula allocates in the order VMs are
# instantiated; the oneflow service in service/aircraft.oneflow.yaml
# instantiates cp before wk, so .10 lands on cp-1 deterministically.
#
# If the operator needs to rebuild a single node without renumbering,
# `onevnet hold aircraft-vnet --ip 10.10.0.10` reserves the IP first.
# ---------------------------------------------------------------------
AR = [
    TYPE = "IP4",
    IP   = "10.10.0.10",
    SIZE = "10"
]

# ---------------------------------------------------------------------
# Default per-NIC security groups. VM templates can append additional
# groups (e.g. `aircraft-edge` for cp-1) without losing the cluster
# baseline. The cluster group itself is defined in
# opennebula/security-groups/cluster.tpl.
# ---------------------------------------------------------------------
SECURITY_GROUPS = "aircraft-cluster"

# ---------------------------------------------------------------------
# Calico MTU — see plans/opennebula.md §7 risk row 2. The OpenNebula
# bridge uses the host MTU (usually 1500). Calico's VXLAN encap adds
# 50 bytes, so the inner MTU must be 1450. We pin the *physical* MTU
# advertised by DHCP/Cloud-init here to 1500 (unchanged) and let
# Calico's FELIX_MTU env var carry the 1450 to the workloads.
# ---------------------------------------------------------------------
MTU = "1500"

# ---------------------------------------------------------------------
# Filter rules at the vNet level — belt and braces in addition to the
# security groups. Drop everything by default; security groups open
# the necessary ports on a per-NIC basis.
# ---------------------------------------------------------------------
INBOUND_AVG_BW  = "0"   # no rate limit; the SGs handle access control
OUTBOUND_AVG_BW = "0"
