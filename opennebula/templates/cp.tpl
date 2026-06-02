# =====================================================================
# OpenNebula VM template -- Kubernetes control-plane node (cp-1).
#
# Consumed via:
#   onetemplate create opennebula/templates/cp.tpl
#   onetemplate instantiate <id> --name cp-1
#
# Sizing comes straight from plans/opennebula.md 3.1:
#   * 2 vCPU
#   * 4 GiB RAM
#   * 40 GiB root disk on the cluster datastore
#
# Contextualisation is split into a separate file
# (opennebula/context/cloud-init.cp.yaml) so the template and the
# bootstrap script can evolve independently. The template wires that
# file in via the START_SCRIPT_BASE64 contextualisation slot.
#
# Network: a single NIC on the aircraft vNet (10.10.0.0/24). The
# control-plane node is reachable from the OpenNebula edge DNAT on
# :6443 (kube-apiserver) and from the workers on the same NIC.
#
# IMAGE NAME placeholder:
#   The DISK block references the Ubuntu 22.04 LTS image by NAME.
#   That name MUST already exist in your OpenNebula image datastore
#   (typically imported from the OpenNebula Marketplace). If your
#   image is named differently, either:
#     (a) rename the marketplace import to "ubuntu-2204-lts", OR
#     (b) edit the IMAGE = "..." value below to match.
# =====================================================================

NAME             = "aircraft-cp"
DESCRIPTION      = "Aircraft SaaS Kubernetes control-plane (1 cp, 2 wk topology)"

CPU              = "2"
VCPU             = "2"
MEMORY           = "4096"

DISK = [
    IMAGE      = "ubuntu-2204-lts",
    SIZE       = "40960",
    DEV_PREFIX = "vd"
]

NIC = [
    NETWORK         = "aircraft-vnet",
    SECURITY_GROUPS = "aircraft-cluster,aircraft-edge"
]

GRAPHICS = [
    TYPE   = "VNC",
    LISTEN = "0.0.0.0"
]

CONTEXT = [
    NETWORK             = "YES",
    SSH_PUBLIC_KEY      = "$USER[SSH_PUBLIC_KEY]",
    K8S_NODE_ROLE       = "control-plane",
    K8S_EDGE_HOST       = "aircraft.example.com",
    K8S_VERSION         = "1.30",
    POD_CIDR            = "192.168.0.0/16",
    START_SCRIPT_BASE64 = "BASE64_OF_CLOUD_INIT_CP_YAML"
]

USER_INPUTS = [
    K8S_EDGE_HOST = "M|text|Edge DNS name DNAT-ed into :6443| |aircraft.example.com",
    K8S_VERSION   = "M|list|Kubernetes minor version|1.30,1.31|1.30"
]

OS = [
    BOOT = "disk0",
    ARCH = "x86_64"
]
