# =====================================================================
# OpenNebula virtual network — aircraft-vnet
#
# Carries node-to-node Kubernetes traffic for the Aircraft SaaS cluster.
#
# IMPORTANT — lab vs. production topology
# ---------------------------------------
# The original design (commit history) used a separate L2-isolated
# bridge `aircraft-br0` on `10.10.0.0/24` with NAT egress to the
# Internet and a single DNAT for :80/:443 inbound. That design is
# correct for a production OpenNebula deployment where the operator
# has full control over host networking.
#
# On a **minione** lab installation the only pre-wired bridge is
# `minionebr` (172.16.100.0/24), which already has:
#   * NAT egress to the internet (iptables MASQUERADE on eth0)
#   * dnsmasq serving DNS + DHCP on 172.16.100.1
#   * OneGate listening on http://172.16.100.1:5030
#
# A separate 10.10.0.0/24 bridge on minione has none of those services
# routed/forwarded, so VMs on it cannot reach OneGate (=> KUBECONFIG_B64
# never gets published back), cannot resolve DNS, and cannot pull
# `kubeadm` packages from apt. This template therefore rides on
# `minionebr` directly.
#
# To move back to the production isolated-bridge topology, change:
#   BRIDGE          -> aircraft-br0
#   NETWORK_ADDRESS -> 10.10.0.0
#   GATEWAY/DNS     -> 10.10.0.1
#   AR.IP           -> 10.10.0.10  (size 10)
# AND configure the host to forward + NAT the new subnet (see
# opennebula/runbook.md §4 "Custom isolated bridge on a production
# OpenNebula host").
#
# Address allocation policy (matches cloud-init expectations on minione):
#   * 172.16.100.1   -> minione gateway / DNS / OneGate (pre-existing)
#   * 172.16.100.2-49  reserved for minione's own appliance VMs
#   * 172.16.100.50  -> cp-1   (control plane)
#   * 172.16.100.51  -> wk-1
#   * 172.16.100.52  -> wk-2
#   * 172.16.100.53-59 spare (HA cp-2/cp-3 etc.)
# =====================================================================

NAME        = "aircraft-vnet"
DESCRIPTION = "Aircraft SaaS Kubernetes cluster vNet (rides on minione's minionebr / 172.16.100.0/24)"

VN_MAD = "bridge"
BRIDGE = "minionebr"

# ---------------------------------------------------------------------
# Subnet -- match the pre-existing minione bridge exactly. NETWORK_MASK
# and GATEWAY must agree with `ip addr show minionebr` on the host or
# Kubernetes pods will lose default route after Calico installs.
# ---------------------------------------------------------------------
NETWORK_ADDRESS = "172.16.100.0"
NETWORK_MASK    = "255.255.255.0"
GATEWAY         = "172.16.100.1"
DNS             = "172.16.100.1"

# ---------------------------------------------------------------------
# Static address pool -- 8 addresses starting at .50, leaving the lower
# half free for minione's own marketplace appliance VMs (Alpine etc.).
# OpenNebula allocates IPs in instantiation order; OneFlow brings cp
# up before workers (parents: [controlplane]), so cp-1 lands on .50
# deterministically. cert-init.cp.yaml's certSANs entry for the cp IP
# pins to .50 to match.
# ---------------------------------------------------------------------
AR = [
    TYPE = "IP4",
    IP   = "172.16.100.50",
    SIZE = "10"
]

# ---------------------------------------------------------------------
# Default per-NIC security group. VM templates append additional groups
# (e.g. `aircraft-edge` for cp-1) without losing the cluster baseline.
# ---------------------------------------------------------------------
SECURITY_GROUPS = "aircraft-cluster"

# ---------------------------------------------------------------------
# Calico MTU note (unchanged from the production topology). minionebr
# is a Linux bridge with MTU 1500. Calico's VXLAN encap adds 50 bytes,
# so the inner pod MTU MUST be 1450. The Calico Installation manifest
# in cloud-init.cp.yaml line 135 pins `calicoNetwork.mtu: 1450`.
# ---------------------------------------------------------------------
MTU = "1500"

INBOUND_AVG_BW  = "0"
OUTBOUND_AVG_BW = "0"
