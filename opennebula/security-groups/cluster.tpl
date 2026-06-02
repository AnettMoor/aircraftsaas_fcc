# =====================================================================
# Security group: aircraft-cluster
#
# Intra-cluster Kubernetes control-plane and kubelet traffic. Applied
# to EVERY node NIC (cp + wk) via aircraft-vnet.tpl's default
# SECURITY_GROUPS attribute.
#
# This is the *baseline* SG -- it locks down the cluster API surface
# to the cluster vNet itself. Inbound from outside the vNet is then
# selectively opened by stacking `aircraft-edge` (cp only) and
# `aircraft-nodeport` (wk only) on top.
#
# Why each port:
#   * 6443/tcp  -- kube-apiserver. Workers talk to the API on this
#                  port; the operator-side connection (kubectl from a
#                  laptop) goes through the edge DNAT, not the vNet.
#   * 10250/tcp -- kubelet API. Used by the kube-apiserver to fetch
#                  logs / exec / port-forward; MUST be intra-cluster.
#   * 10256/tcp -- kube-proxy health endpoint (load balancers probe it).
#   * 2379-2380 -- etcd peer + client. Single-cp cluster, so this is
#                  only ever loopback; we still match the "complete
#                  kubeadm spec" so a future cp-2 needs zero SG edits.
#   * 4789/udp  -- Calico VXLAN. Pod-to-pod traffic encapsulated here.
#   * 179/tcp   -- Calico BGP (if VXLAN is later swapped for BGP mode).
#   * ICMP      -- ping / traceroute for operator debugging on the vNet.
#
# Source restriction uses SOURCE_PREFIX (CIDR), NOT $NETWORK[...] --
# the macro is only resolved when the SG is referenced inline from a
# VM template. Standalone `onesecgroup create` does NOT expand it,
# which is why the previous version of this file failed validation
# with "Wrong NETWORK_ID". The CIDR here MUST match
# vnet/aircraft-vnet.tpl's NETWORK_ADDRESS / NETWORK_MASK.
#
# Outbound is left wide-open: the OpenNebula NAT gateway is the choke
# point (apt mirrors, Docker Hub, Let's Encrypt). Locking egress here
# too would block kubeadm preflight (`apt-get install`) during
# cloud-init.
# =====================================================================

NAME        = "aircraft-cluster"
DESCRIPTION = "Intra-cluster Kubernetes control-plane + kubelet + CNI"

# NOTE: SOURCE_PREFIX is 172.16.100.0/24 because aircraft-vnet now rides
# on minione's pre-existing `minionebr` bridge (see
# opennebula/vnet/aircraft-vnet.tpl header for the rationale). If you
# move this deployment to a production OpenNebula with a dedicated
# `aircraft-br0` bridge on 10.10.0.0/24, switch BOTH this file AND
# aircraft-vnet.tpl back to 10.10.0.0/24 in lockstep.
RULE = [
    PROTOCOL      = "TCP",
    RULE_TYPE     = "inbound",
    RANGE         = "6443",
    SOURCE_PREFIX = "172.16.100.0/24"
]

RULE = [
    PROTOCOL      = "TCP",
    RULE_TYPE     = "inbound",
    RANGE         = "10250",
    SOURCE_PREFIX = "172.16.100.0/24"
]

RULE = [
    PROTOCOL      = "TCP",
    RULE_TYPE     = "inbound",
    RANGE         = "10256",
    SOURCE_PREFIX = "172.16.100.0/24"
]

RULE = [
    PROTOCOL      = "TCP",
    RULE_TYPE     = "inbound",
    RANGE         = "2379:2380",
    SOURCE_PREFIX = "172.16.100.0/24"
]

RULE = [
    PROTOCOL      = "UDP",
    RULE_TYPE     = "inbound",
    RANGE         = "4789",
    SOURCE_PREFIX = "172.16.100.0/24"
]

RULE = [
    PROTOCOL      = "TCP",
    RULE_TYPE     = "inbound",
    RANGE         = "179",
    SOURCE_PREFIX = "172.16.100.0/24"
]

RULE = [
    PROTOCOL      = "ICMP",
    RULE_TYPE     = "inbound",
    SOURCE_PREFIX = "172.16.100.0/24"
]

RULE = [
    PROTOCOL  = "ALL",
    RULE_TYPE = "outbound"
]
