# =====================================================================
# Security group: aircraft-cluster
#
# Intra-cluster Kubernetes control-plane and kubelet traffic. Applied
# to EVERY node NIC (cp + wk) via aircraft-vnet.tpl's default
# SECURITY_GROUPS attribute.
#
# This is the *baseline* SG — it locks down the cluster API surface
# to the cluster vNet itself. Inbound from outside the vNet is then
# selectively opened by stacking `aircraft-edge` (cp only) and
# `aircraft-nodeport` (wk only) on top.
#
# Why each port:
#   * 6443/tcp  — kube-apiserver. Workers talk to the API on this
#                 port; the operator-side connection (kubectl from a
#                 laptop) goes through the edge DNAT, not the vNet.
#   * 10250/tcp — kubelet API. Used by the kube-apiserver to fetch
#                 logs / exec / port-forward; MUST be intra-cluster.
#   * 10256/tcp — kube-proxy health endpoint (load balancers probe it).
#   * 2379-2380 — etcd peer + client. Single-cp cluster, so this is
#                 only ever loopback; we still match the "complete
#                 kubeadm spec" so a future cp-2 needs zero SG edits.
#   * 4789/udp  — Calico VXLAN. Pod-to-pod traffic encapsulated here.
#   * 179/tcp   — Calico BGP (if VXLAN is later swapped for BGP mode).
#   * ICMP      — ping / traceroute for operator debugging on the vNet.
#
# Outbound is left wide-open: the NAT gateway is the choke point
# (apt mirrors, Docker Hub, Let's Encrypt). Locking egress here too
# would block kubeadm preflight (`apt-get install`) during cloud-init.
# =====================================================================

NAME        = "aircraft-cluster"
DESCRIPTION = "Intra-cluster Kubernetes control-plane + kubelet + CNI"

# ---------- Inbound (all from the cluster vNet only) -----------------
RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "6443",
    NETWORK_ID = "$NETWORK[aircraft-vnet]"
]

RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "10250",
    NETWORK_ID = "$NETWORK[aircraft-vnet]"
]

RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "10256",
    NETWORK_ID = "$NETWORK[aircraft-vnet]"
]

# etcd (single-cp today; future-proofed for HA cp).
RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "2379:2380",
    NETWORK_ID = "$NETWORK[aircraft-vnet]"
]

# Calico VXLAN encap. Without this open the pod network silently
# black-holes — the trickiest failure mode in the §2.1 manual cluster.
RULE = [
    PROTOCOL  = "UDP",
    RULE_TYPE = "inbound",
    RANGE     = "4789",
    NETWORK_ID = "$NETWORK[aircraft-vnet]"
]

# Calico BGP — opened proactively for the BGP-mode swap path even
# though the default Calico install is VXLAN.
RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "179",
    NETWORK_ID = "$NETWORK[aircraft-vnet]"
]

# ICMP intra-cluster for operator-side traceroute / ping.
RULE = [
    PROTOCOL  = "ICMP",
    RULE_TYPE = "inbound",
    NETWORK_ID = "$NETWORK[aircraft-vnet]"
]

# ---------- Outbound — wide open via the OpenNebula NAT --------------
# We intentionally do NOT lock egress here. Cloud-init needs to:
#   * apt-get install containerd kubeadm kubelet kubectl
#   * Pull Calico operator + manifests from docker.io
#   * Pull metrics-server / ingress-nginx manifests
#   * Talk to Let's Encrypt staging for ACME-HTTP01
# Locking egress in this SG would require maintaining an apt/CDN
# allowlist, which is more brittle than the L2 isolation we already
# have on the vNet inbound side.
RULE = [
    PROTOCOL  = "ALL",
    RULE_TYPE = "outbound"
]
