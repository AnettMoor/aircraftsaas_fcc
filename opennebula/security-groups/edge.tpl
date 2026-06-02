# =====================================================================
# Security group: aircraft-edge
#
# Public-facing inbound for the control-plane node only. Stacked on
# TOP of aircraft-cluster (which restricts everything to the vNet).
#
# This is the *only* SG that opens ports from outside the vNet.
# Applied solely to cp-1 via opennebula/templates/cp.tpl. Workers
# are NEVER in this group — that is what keeps NodePort traffic
# constrained to flow through ingress-nginx on cp-1.
#
# Ports:
#   * 80/tcp   — HTTP. Required for the ACME HTTP-01 challenge that
#                cert-manager uses against letsencrypt-staging (see
#                deploy.md §C.15 and k8s/cert-manager/issuer-letsencrypt.yaml).
#                Also serves as the redirect target before HSTS kicks in.
#   * 443/tcp  — HTTPS. The actual production entry point. All Ingress
#                hosts (users./fleet./booking./app.aircraft.example.com)
#                are served from here.
#   * 6443/tcp — kube-apiserver. Open to the OPERATOR_CIDR variable
#                only, NOT to the world (see SOURCE_PREFIX). The
#                OpenNebula DNAT on the edge already restricts the
#                source to the bastion's public IP; we belt-and-brace
#                here too.
#
# Why NOT 22 (SSH):
#   * The cut-over runbook deliberately does not require SSH onto the
#     nodes. kubeadm/kubectl is the only ingress channel. If SSH is
#     ever needed for emergency recovery the operator extends this SG
#     ad-hoc; baking it in would be a permanent attack surface.
# =====================================================================

NAME        = "aircraft-edge"
DESCRIPTION = "Edge-facing inbound for the control-plane (HTTP/HTTPS/API)"

# ---------------------------------------------------------------------
# Public HTTP — required for ACME HTTP-01 (Let's Encrypt). The
# Ingress controller force-redirects to HTTPS so this port carries
# no real traffic in steady state.
# ---------------------------------------------------------------------
RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "80",
    # No NETWORK_ID / SIZE  -> defaults to 0.0.0.0/0. We are aware.
]

# ---------------------------------------------------------------------
# Public HTTPS — the real entry point.
# ---------------------------------------------------------------------
RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "443"
]

# ---------------------------------------------------------------------
# kube-apiserver — restricted to the operator CIDR. The CIDR itself
# is parameterised via the OPENNEBULA_USER_INPUT block in cp.tpl so
# it can be set per-tenancy at instantiation time without editing
# this file. Default: REPLACE_WITH_OPERATOR_CIDR — instantiation
# fails closed if the operator forgets to override it.
# ---------------------------------------------------------------------
RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "6443",
    IP        = "$CONTEXT[OPERATOR_CIDR]",
    SIZE      = "1"
]

# ---------------------------------------------------------------------
# Outbound — wide open (same rationale as aircraft-cluster).
# ---------------------------------------------------------------------
RULE = [
    PROTOCOL  = "ALL",
    RULE_TYPE = "outbound"
]
