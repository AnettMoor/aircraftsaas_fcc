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
#     to a sibling worker via NodePort -- even though Kubernetes
#     NetworkPolicies would also block that, this is defence in depth.
#
# Why not also open UDP NodePort:
#   * The Aircraft SaaS stack has no UDP services. Adding UDP here
#     would open ~2800 useless UDP ports. The runbook documents the
#     two-line addition if a future service ever needs UDP.
# =====================================================================

NAME        = "aircraft-nodeport"
DESCRIPTION = "NodePort range (30000-32767/tcp) from the edge NIC only"

# Source = cp-1 NIC IP (.50 on the minione bridge). In production with
# the dedicated aircraft-br0/10.10.0.0/24 bridge this would be .254
# (the edge DNAT NIC); on minione cp-1 itself handles edge ingress
# because there is no separate edge NIC.
RULE = [
    PROTOCOL      = "TCP",
    RULE_TYPE     = "inbound",
    RANGE         = "30000:32767",
    SOURCE_PREFIX = "172.16.100.50/32"
]

RULE = [
    PROTOCOL  = "ALL",
    RULE_TYPE = "outbound"
]
