# =====================================================================
# Security group: aircraft-edge
#
# Public-facing inbound for the control-plane node only. Stacked on
# TOP of aircraft-cluster (which restricts everything to the vNet).
#
# This is the *only* SG that opens ports from outside the vNet.
# Applied solely to cp-1 via opennebula/templates/cp.tpl. Workers
# are NEVER in this group -- that is what keeps NodePort traffic
# constrained to flow through ingress-nginx on cp-1.
#
# Ports:
#   * 80/tcp   -- HTTP. Required for the ACME HTTP-01 challenge that
#                 cert-manager uses against letsencrypt-staging (see
#                 deploy.md C.15 and k8s/cert-manager/issuer-letsencrypt.yaml).
#                 Also serves as the redirect target before HSTS kicks in.
#                 Open to the world (0.0.0.0/0) so Let's Encrypt can
#                 reach the challenge.
#   * 443/tcp  -- HTTPS. The actual production entry point. All Ingress
#                 hosts (users./fleet./booking./app.aircraft.example.com)
#                 are served from here. Open to the world.
#   * 6443/tcp -- kube-apiserver. Restricted to OPERATOR_PUBLIC_IP only
#                 via SOURCE_PREFIX.
#
# IMPORTANT: this file ships with a PLACEHOLDER operator IP
# ("203.0.113.42"). Before running `onesecgroup create` you MUST either:
#   (a) edit this file to set your real public IP, OR
#   (b) use opennebula/render.sh which substitutes the OPERATOR_IP
#       env-var for the placeholder automatically.
#
# Why NOT 22 (SSH):
#   * The cut-over runbook deliberately does not require SSH onto the
#     nodes. kubeadm/kubectl is the only ingress channel. If SSH is
#     ever needed for emergency recovery the operator extends this SG
#     ad-hoc; baking it in would be a permanent attack surface.
# =====================================================================

NAME        = "aircraft-edge"
DESCRIPTION = "Edge-facing inbound for the control-plane (HTTP/HTTPS/API)"

RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "80"
]

RULE = [
    PROTOCOL  = "TCP",
    RULE_TYPE = "inbound",
    RANGE     = "443"
]

RULE = [
    PROTOCOL      = "TCP",
    RULE_TYPE     = "inbound",
    RANGE         = "6443",
    SOURCE_PREFIX = "203.0.113.42/32"
]

RULE = [
    PROTOCOL  = "ALL",
    RULE_TYPE = "outbound"
]
