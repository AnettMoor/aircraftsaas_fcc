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

# Sizing reduced for the lab tenancy. The original plan called for
# 2 vCPU / 4 GiB; the lab front-end can't host that many concurrent VMs
# so we drop RAM to 2 GiB (still enough for kubeadm init + Calico +
# metrics-server + ingress-nginx). VCPU stays at 2 -- the cp also runs
# etcd and the scheduler, which are CPU-sensitive under burst load.
# IMPORTANT: control-plane memory MUST be >= 4 GiB on this stack.
# We host etcd + kube-apiserver + kube-controller-manager + kube-scheduler
# + containerd + kube-proxy + tigera-operator + ingress-nginx + metrics-server
# all on this single node. 2 GiB was attempted; etcd/apiserver thrashed and
# CrashLoopBackOff'd inside ~3 minutes once Calico+ingress-nginx landed.
# Symptoms when undersized: kubectl returns "TLS handshake timeout" or
# "connect: connection refused" intermittently; static-pod RESTARTS climb
# into double digits; etcd logs show "raft member restarting" loops.
CPU              = "2"
VCPU             = "2"
MEMORY           = "4096"

# NOTE: the IMAGE here must match a name in `oneimage list`. The
# OpenNebula Marketplace import is usually named "Ubuntu 22.04" (with
# a space). Change the value below to whatever `oneimage list` shows
# for your Ubuntu 22.04 LTS image. The runbook documents the override.
DISK = [
    IMAGE      = "Ubuntu 22.04",
    SIZE       = "40960",
    DEV_PREFIX = "vd"
]

# NETWORK_UNAME pins lookup to the vNet's owner. Without it,
# `onetemplate instantiate` as one user fails with
# "User X does not own a network with name: aircraft-vnet" if the
# vNet was created by a different user. Adjust if your aircraft-vnet
# is owned by a non-oneadmin user (check with `onevnet show aircraft-vnet`).
NIC = [
    NETWORK         = "aircraft-vnet",
    NETWORK_UNAME   = "oneadmin",
    SECURITY_GROUPS = "aircraft-cluster,aircraft-edge"
]

GRAPHICS = [
    TYPE   = "VNC",
    LISTEN = "0.0.0.0"
]

# NOTE: serial console attachment was attempted via RAW = [ TYPE="kvm",
# DATA="<serial.../><console.../>" ] but OpenNebula 7.x rejects that
# block with "Invalid RAW section: cannot validate DATA with domain.rng".
# For now debugging happens via VNC -- look up the port with:
#   onevm show <id> | grep -E 'GRAPHICS|PORT|LISTEN'
# and connect a VNC client to localhost:<5900+VMID> (default minione
# binding). The bootstrap script also writes a full trace to
# /var/log/aircraft-cp-bootstrap.log which can be retrieved via VNC.

# IMPORTANT: TOKEN="YES" and REPORT_READY="YES" are REQUIRED so that
# OpenNebula injects ONEGATE_ENDPOINT + TOKENTXT into the VM. The
# `onegate vm update` calls in bootstrap-cp.sh (steps 8 and 9) use
# those to publish K8S_JOIN_COMMAND and KUBECONFIG_B64 back to
# USER_TEMPLATE. Without them, onegate silently fails and the operator
# observes "KUBECONFIG_B64 not in USER_TEMPLATE yet" forever.
#
# START_SCRIPT_BASE64 is executed by one-context as a BASH SCRIPT, NOT
# as cloud-init YAML. opennebula/context/bootstrap-cp.sh is therefore
# a plain bash script (#!/usr/bin/env bash); render.sh base64-encodes
# it and substitutes the BASE64_OF_BOOTSTRAP_CP placeholder below.
CONTEXT = [
    NETWORK             = "YES",
    TOKEN               = "YES",
    REPORT_READY        = "YES",
    SSH_PUBLIC_KEY      = "$USER[SSH_PUBLIC_KEY]",
    K8S_NODE_ROLE       = "control-plane",
    K8S_EDGE_HOST       = "aircraft.example.com",
    K8S_VERSION         = "1.30",
    POD_CIDR            = "192.168.0.0/16",
    START_SCRIPT_BASE64 = "BASE64_OF_BOOTSTRAP_CP"
]

USER_INPUTS = [
    K8S_EDGE_HOST = "M|text|Edge DNS name DNAT-ed into :6443| |aircraft.example.com",
    K8S_VERSION   = "M|list|Kubernetes minor version|1.30,1.31|1.30"
]

OS = [
    BOOT = "disk0",
    ARCH = "x86_64"
]
