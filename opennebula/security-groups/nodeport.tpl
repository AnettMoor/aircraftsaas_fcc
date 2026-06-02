# =====================================================================
# Security group: aircraft-nodeport
#
# NodePort range (30000-32767/tcp) restricted to the edge NIC.
# Applied to worker NICs only via opennebula/templates/wk.tpl. The
# Ingress controller normally consumes the :80/:443 path on cp-1 via
# aircraft-edge; this SG covers the fallback / blue-green case where
# the operator points the edge DNAT at a NodePort Service on a
# worker instead.
#
# Why constrain to the edge NIC source (10.10.0.254/32) and not the
# whole vNet:
#   * Intra-cluster traffic to NodePorts is unusual and almost always
#     a misconfiguration (apps should go through ClusterIP services).
#   * Restricting the source means a compromised app pod cannot pivot
#     to a sibling worker via NodePort — even though Kubernetes
#     NetworkPolicies would also block that, this is defence in depth.
#
# Why not also open UDP NodePort:
#   * The Aircraft SaaS stack has no UDP services. Adding UDP here
#     would open ~2800 useless UDP ports. The runbook documents the
#     two-line addition if a future service ever needs UDP.
# =====================================================================

NAME        = "aircraft-nodeport"
DESCRIPTION = "NodePort range (30000-32767/tcp) from the edge NIC only"

# ---------------------------------------------------------------------
# NodePort range — sourced from the operator-side edge NIC IP only.
# The edge NIC IP is the .254 reserved by aircraft-vnet.tpl's AR pool.
# ---------------------------------------------------------------------
RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "30000:32767",
    IP        = "10.10.0.254",
    SIZE      = "1"
]

# ---------------------------------------------------------------------
# Outbound — wide open.
# ---------------------------------------------------------------------
RULE = [
    PROTOCOL  = "ALL",
    RULE_TYPE = "outbound"
]
