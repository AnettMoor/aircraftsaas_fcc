#!/usr/bin/env bash
# =====================================================================
# scripts/provision-vms.sh — one-time OpenNebula IaaS provisioner
#
# Wraps every `one*` CLI call required to stamp the Aircraft SaaS
# Kubernetes cluster onto a fresh OpenNebula tenancy. Mirrors
# opennebula/runbook.md §1–§7 in a single idempotent script.
#
# Stages:
#   1. Sanity: one* CLIs present, ONE_AUTH configured, host list non-empty
#   2. Import Ubuntu 22.04 LTS marketplace image (skipped if present)
#   3. Render templates via opennebula/render.sh (operator IP injected)
#   4. Create vNet  (aircraft-vnet)
#   5. Create 3 security groups (cluster / edge / nodeport)
#   6. Create 2 VM templates (aircraft-cp / aircraft-wk)
#   7. Re-render the oneflow JSON now that the template IDs exist
#   8. Create the oneflow template (aircraft)
#   9. Instantiate the service; wait for both roles to reach RUNNING
#  10. Extract the kubeconfig from cp-1's USER_TEMPLATE and write it to
#      ${KUBECONFIG_OUT:-~/.kube/aircraft.config}
#
# Flags:
#   --teardown          Delete the running oneflow service, then the
#                       templates, secgroups, vnet, and (optionally)
#                       the imported image. Reverse of stages 9 -> 2.
#   --dry-run           Print every one* command but do not execute it.
#   --skip-image        Skip stage 2 (image already imported).
#   --skip-render       Skip stage 3 (templates already rendered).
#   --skip-instantiate  Stop after stage 8 (don't spawn VMs yet).
#   --keep-image        On --teardown, do NOT delete the imported image.
#
# Environment:
#   ONE_AUTH            Path to ~/.one/one_auth (or rely on default).
#   OPERATOR_IP         Your public IP for the edge security group.
#                       Defaults to `curl -s https://ifconfig.me` if curl
#                       is available; otherwise --dry-run requires it.
#   OPERATOR_CIDR       /32 CIDR allowed to reach kube-apiserver via the
#                       edge DNAT. Defaults to "${OPERATOR_IP}/32".
#   EDGE_HOST           Edge DNS name DNAT'd to cp-1 (default
#                       aircraft.example.com).
#   K8S_VERSION         Kubernetes minor (1.30 | 1.31). Default 1.30.
#   KUBECONFIG_OUT      Where to write the extracted kubeconfig
#                       (default ~/.kube/aircraft.config).
#
# Idempotence model:
#   Every stage first checks whether the resource exists by name via
#   `one* list`. If yes -> skip + log. If no -> create. Re-runs after a
#   partial failure resume cleanly without manual cleanup.
# =====================================================================
set -euo pipefail

# --- Configuration ----------------------------------------------------
SCRIPT_DIR="$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
REPO_ROOT="$( cd -- "${SCRIPT_DIR}/.." &> /dev/null && pwd )"
ONE_DIR="${REPO_ROOT}/opennebula"
RENDER_DIR="${OUT_DIR:-/tmp/onerender}"

TEARDOWN=0
DRY_RUN=0
SKIP_IMAGE=0
SKIP_RENDER=0
SKIP_INSTANTIATE=0
KEEP_IMAGE=0

EDGE_HOST="${EDGE_HOST:-aircraft.example.com}"
K8S_VERSION="${K8S_VERSION:-1.30}"
KUBECONFIG_OUT="${KUBECONFIG_OUT:-${HOME}/.kube/aircraft.config}"

# Resource names (kept in lock-step with opennebula/*.tpl + oneflow yaml).
# IMAGE_NAME is the literal name in `oneimage list`. The default
# "Ubuntu 22.04" matches what `onemarketapp export <APPID>` writes when
# no second positional argument is given. Override with AIRCRAFT_IMAGE_NAME
# env-var if your tenancy renamed the marketplace image. The value is also
# exported into render.sh so opennebula/templates/{cp,wk}.tpl can resolve
# the numeric IMAGE_ID at render time (dodging the OpenNebula 7.x oneflow
# IMAGE-by-name resolution bug — see opennebula/templates/cp.tpl header).
IMAGE_NAME="${AIRCRAFT_IMAGE_NAME:-Ubuntu 22.04}"
export AIRCRAFT_IMAGE_NAME="$IMAGE_NAME"
VNET_NAME="aircraft-vnet"
SG_CLUSTER="aircraft-cluster"
SG_EDGE="aircraft-edge"
SG_NODEPORT="aircraft-nodeport"
TPL_CP="aircraft-cp"
TPL_WK="aircraft-wk"
FLOW_NAME="aircraft"

# --- Argument parsing -------------------------------------------------
for arg in "$@"; do
  case "$arg" in
    --teardown)         TEARDOWN=1 ;;
    --dry-run)          DRY_RUN=1 ;;
    --skip-image)       SKIP_IMAGE=1 ;;
    --skip-render)      SKIP_RENDER=1 ;;
    --skip-instantiate) SKIP_INSTANTIATE=1 ;;
    --keep-image)       KEEP_IMAGE=1 ;;
    -h|--help)
      sed -n '2,60p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown flag: $arg" >&2
      exit 2
      ;;
  esac
done

# --- Helpers ----------------------------------------------------------
log()  { printf '\033[1;34m[provision]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[provision]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[provision]\033[0m %s\n' "$*" >&2; exit 1; }

run() {
  if [[ "$DRY_RUN" -eq 1 ]]; then
    printf '  $ %s\n' "$*"
  else
    eval "$@"
  fi
}

require_cli() {
  command -v "$1" >/dev/null 2>&1 || die "missing required CLI: $1"
}

# Returns 0 if a resource exists by NAME, 1 otherwise.
# Args: <one* list cmd>  <name>  <name-column-index-1based>
resource_exists() {
  local listcmd="$1" name="$2" col="${3:-2}"
  [[ "$DRY_RUN" -eq 1 ]] && return 1   # always "missing" in dry-run
  eval "$listcmd" --no-header 2>/dev/null \
    | awk -v n="$name" -v c="$col" '$c==n {found=1; exit} END{exit !found}'
}

# --- 1. Sanity --------------------------------------------------------
log "STAGE 1/10 — Sanity checks"

if [[ "$DRY_RUN" -eq 1 ]]; then
  warn "dry-run: skipping one* CLI presence checks"
else
  for cli in onehost oneimage onevnet onesecgroup onetemplate oneflow-template oneflow onevm; do
    require_cli "$cli"
  done
  require_cli python3
  require_cli base64
  require_cli jq

  if ! onehost list >/dev/null 2>&1; then
    die "one* CLIs cannot reach OpenNebula — check ONE_AUTH / ONE_XMLRPC"
  fi
  HOST_COUNT=$(onehost list --no-header 2>/dev/null | wc -l | tr -d ' ')
  log "OpenNebula reachable; ${HOST_COUNT} hypervisor(s) registered"
  [[ "$HOST_COUNT" -lt 1 ]] && die "no hypervisors available in the tenancy"
fi

# --- TEARDOWN PATH ----------------------------------------------------
if [[ "$TEARDOWN" -eq 1 ]]; then
  log "TEARDOWN — reversing provisioning"

  # OpenNebula CLI columns: ID USER GROUP NAME ... -> NAME is $4
  if resource_exists "oneflow list" "$FLOW_NAME" 4; then
    log "delete oneflow service ${FLOW_NAME}"
    run oneflow delete "$FLOW_NAME"
    if [[ "$DRY_RUN" -eq 0 ]]; then
      while resource_exists "oneflow list" "$FLOW_NAME" 4; do
        log "  waiting for oneflow ${FLOW_NAME} to be reaped..."
        sleep 5
      done
    fi
  else
    log "oneflow service ${FLOW_NAME} not present — skip"
  fi

  if resource_exists "oneflow-template list" "$FLOW_NAME" 4; then
    log "delete oneflow-template ${FLOW_NAME}"
    run oneflow-template delete "$FLOW_NAME"
  fi

  for tpl in "$TPL_CP" "$TPL_WK"; do
    if resource_exists "onetemplate list" "$tpl" 4; then
      log "delete onetemplate ${tpl}"
      run onetemplate delete "$tpl"
    fi
  done

  for sg in "$SG_NODEPORT" "$SG_EDGE" "$SG_CLUSTER"; do
    if resource_exists "onesecgroup list" "$sg" 4; then
      log "delete onesecgroup ${sg}"
      run onesecgroup delete "$sg"
    fi
  done

  if resource_exists "onevnet list" "$VNET_NAME" 4; then
    log "delete onevnet ${VNET_NAME}"
    run onevnet delete "$VNET_NAME"
  fi

  if [[ "$KEEP_IMAGE" -eq 0 ]] && resource_exists "oneimage list" "$IMAGE_NAME" 4; then
    log "delete oneimage ${IMAGE_NAME}"
    run oneimage delete "$IMAGE_NAME"
  fi

  log "TEARDOWN complete."
  exit 0
fi

# --- 2. Import Ubuntu marketplace image -------------------------------
if [[ "$SKIP_IMAGE" -eq 0 ]]; then
  log "STAGE 2/10 — Import Ubuntu 22.04 LTS marketplace image"
  if resource_exists "oneimage list" "$IMAGE_NAME" 4; then
    log "  image ${IMAGE_NAME} already present — skip"
  else
    if [[ "$DRY_RUN" -eq 1 ]]; then
      printf '  $ %s\n' "onemarketapp list -f NAME~'Ubuntu 22.04*' --csv | tail -n1 | cut -d, -f1"
      printf '  $ %s\n' "onemarketapp export <APPID> ${IMAGE_NAME} --datastore default"
      printf '  $ %s\n' "oneimage chmod ${IMAGE_NAME} 644"
    else
      # Marketplace filter: name contains "Ubuntu 22.04" but NOT "aarch64"
      # / "arm" / "i386" -- we want the x86_64 variant for standard
      # OpenNebula KVM hosts. The OpenNebula marketplace ships at
      # least three arch variants and the first hit is often arm64.
      APPID=$(onemarketapp list --csv 2>/dev/null \
              | awk -F, '
                  tolower($2) ~ /ubuntu 22\.04/ \
                  && tolower($2) !~ /aarch64|arm64|arm|i386|ppc/ \
                  { print $1; exit }')
      if [[ -z "$APPID" ]]; then
        # Fallback: explicit x86_64 match, or first row with no arch suffix.
        APPID=$(onemarketapp list --csv 2>/dev/null \
                | awk -F, 'tolower($2) ~ /ubuntu 22\.04.*x86_64/ {print $1; exit}')
      fi
      [[ -z "$APPID" ]] && die "no x86_64 'Ubuntu 22.04*' app found in OpenNebula marketplace (only arm variants present?)"
      APPNAME=$(onemarketapp list --csv 2>/dev/null \
                | awk -F, -v id="$APPID" '$1==id {print $2; exit}')
      log "  exporting marketplace appid=${APPID} (\"${APPNAME}\") -> ${IMAGE_NAME}"
      onemarketapp export "$APPID" "$IMAGE_NAME" --datastore default

      # Wait for the image to reach READY. oneimage show formats lines as
      # "STATE          : rdy" so $3 is the value, NOT $2 (which is ":").
      # Possible state strings: init, rdy (READY), used, lock, err.
      for _ in $(seq 1 60); do
        STATE=$(oneimage show "$IMAGE_NAME" 2>/dev/null \
                | awk '/^STATE/ {print tolower($3); exit}')
        case "$STATE" in
          rdy|ready) break ;;
          err|error) die "image ${IMAGE_NAME} entered ERROR state" ;;
          *) log "  image state=${STATE:-?}; waiting..." ; sleep 10 ;;
        esac
      done
      [[ "$STATE" != "rdy" && "$STATE" != "ready" ]] \
        && die "image ${IMAGE_NAME} did not reach READY in 600s (last state: ${STATE:-?})"
      oneimage chmod "$IMAGE_NAME" 644
    fi
  fi
else
  log "STAGE 2/10 — SKIPPED (--skip-image)"
fi

# --- 3. Render templates (operator IP injected) -----------------------
if [[ "$SKIP_RENDER" -eq 0 ]]; then
  log "STAGE 3/10 — Render templates via opennebula/render.sh"
  if [[ -z "${OPERATOR_IP:-}" ]]; then
    if command -v curl >/dev/null 2>&1; then
      OPERATOR_IP="$(curl -fsS https://ifconfig.me || true)"
    fi
    [[ -z "${OPERATOR_IP:-}" ]] && die "OPERATOR_IP not set and could not auto-detect"
    log "  OPERATOR_IP auto-detected as ${OPERATOR_IP}"
  fi
  export OPERATOR_IP
  run "${ONE_DIR}/render.sh"
else
  log "STAGE 3/10 — SKIPPED (--skip-render)"
fi

# --- 4. vNet ----------------------------------------------------------
log "STAGE 4/10 — Create vNet ${VNET_NAME}"
if resource_exists "onevnet list" "$VNET_NAME" 4; then
  log "  vnet ${VNET_NAME} already present — skip"
else
  run onevnet create "${RENDER_DIR}/aircraft-vnet.tpl"
fi

# --- 5. Security groups ----------------------------------------------
log "STAGE 5/10 — Create security groups"
for pair in "${SG_CLUSTER}:sg-cluster.tpl" \
            "${SG_EDGE}:sg-edge.tpl" \
            "${SG_NODEPORT}:sg-nodeport.tpl"; do
  name="${pair%%:*}"
  file="${pair##*:}"
  if resource_exists "onesecgroup list" "$name" 4; then
    log "  secgroup ${name} already present — skip"
  else
    run onesecgroup create "${RENDER_DIR}/${file}"
  fi
done

# --- 6. VM templates --------------------------------------------------
log "STAGE 6/10 — Create VM templates"
for pair in "${TPL_CP}:cp.tpl" "${TPL_WK}:wk.tpl"; do
  name="${pair%%:*}"
  file="${pair##*:}"
  if resource_exists "onetemplate list" "$name" 4; then
    log "  template ${name} already present — skip"
  else
    run onetemplate create "${RENDER_DIR}/${file}"
  fi
done

# --- 7. Re-render the oneflow JSON now that template IDs exist --------
log "STAGE 7/10 — Re-render oneflow JSON (template IDs now resolvable)"
run "${ONE_DIR}/render.sh"

if [[ "$DRY_RUN" -eq 0 ]] && [[ ! -f "${RENDER_DIR}/aircraft.oneflow.json" ]]; then
  die "render.sh did not produce ${RENDER_DIR}/aircraft.oneflow.json"
fi

# --- 8. Create oneflow template --------------------------------------
log "STAGE 8/10 — Create oneflow template ${FLOW_NAME}"
# OpenNebula CLI columns: ID USER GROUP NAME ... -> NAME is $4
if resource_exists "oneflow-template list" "$FLOW_NAME" 4; then
  log "  oneflow-template ${FLOW_NAME} already present — skip"
else
  run oneflow-template create "${RENDER_DIR}/aircraft.oneflow.json"
fi

if [[ "$SKIP_INSTANTIATE" -eq 1 ]]; then
  log "STAGE 9-10 — SKIPPED (--skip-instantiate)"
  log "DONE — IaaS assets in place. Re-run without --skip-instantiate to spawn VMs."
  exit 0
fi

# --- 9. Instantiate service + wait for RUNNING ------------------------
log "STAGE 9/10 — Instantiate oneflow service"

OPERATOR_CIDR="${OPERATOR_CIDR:-${OPERATOR_IP:-}/32}"
[[ "$OPERATOR_CIDR" == "/32" ]] && die "OPERATOR_CIDR is empty"
log "  OPERATOR_CIDR=${OPERATOR_CIDR}  EDGE_HOST=${EDGE_HOST}  K8S_VERSION=${K8S_VERSION}"

if resource_exists "oneflow list" "$FLOW_NAME" 4; then
  log "  oneflow service ${FLOW_NAME} already running — skip instantiate"
else
  # OpenNebula 7.x: oneflow-template instantiate takes a JSON file with
  # 'custom_attrs_values' at the top level. Build it on the fly.
  INSTANTIATE_JSON="${RENDER_DIR}/instantiate.json"
  if [[ "$DRY_RUN" -eq 0 ]]; then
    cat > "$INSTANTIATE_JSON" <<JSON
{
  "custom_attrs_values": {
    "OPERATOR_CIDR": "${OPERATOR_CIDR}",
    "K8S_EDGE_HOST": "${EDGE_HOST}",
    "K8S_VERSION":   "${K8S_VERSION}"
  }
}
JSON
    # OpenNebula CLI columns: ID USER GROUP NAME ... -> NAME is $4
    FLOW_TID=$(oneflow-template list --no-header 2>/dev/null \
               | awk -v n="$FLOW_NAME" '$4==n {print $1; exit}')
    [[ -z "$FLOW_TID" ]] && die "could not resolve oneflow-template id for ${FLOW_NAME}"
    log "  instantiate template-id=${FLOW_TID} with ${INSTANTIATE_JSON}"
    oneflow-template instantiate "$FLOW_TID" "$INSTANTIATE_JSON"
  else
    printf '  $ %s\n' "oneflow-template instantiate <FLOW_TID> ${INSTANTIATE_JSON}"
  fi
fi

# Wait for both roles to reach RUNNING (state code 2 in OpenNebula).
if [[ "$DRY_RUN" -eq 0 ]]; then
  log "  waiting for roles controlplane + workers to reach RUNNING (≤30 min)"
  for _ in $(seq 1 180); do
    STATES=$(oneflow show "$FLOW_NAME" --json 2>/dev/null \
             | jq -r '.DOCUMENT.TEMPLATE.BODY.roles[].state' 2>/dev/null || true)
    if [[ -n "$STATES" ]] && ! grep -vqE '^2$' <<<"$STATES"; then
      log "  all roles RUNNING"
      break
    fi
    log "  states: $(tr '\n' ' ' <<<"$STATES")"
    sleep 10
  done

  FAILED=$(oneflow show "$FLOW_NAME" --json 2>/dev/null \
           | jq -r '.DOCUMENT.TEMPLATE.BODY.roles[] | select(.state >= 4) | .name' 2>/dev/null || true)
  [[ -n "$FAILED" ]] && die "oneflow role(s) FAILED: ${FAILED}"
fi

# --- 10. Extract kubeconfig ------------------------------------------
log "STAGE 10/10 — Extract kubeconfig from cp-1 USER_TEMPLATE"
mkdir -p "$(dirname "$KUBECONFIG_OUT")"

if [[ "$DRY_RUN" -eq 1 ]]; then
  printf '  $ %s\n' "onevm show cp-1 --json | jq -r .VM.USER_TEMPLATE.KUBECONFIG_B64 | base64 -d > ${KUBECONFIG_OUT}"
else
  # Wait for cp-1's cloud-init to publish KUBECONFIG_B64.
  for _ in $(seq 1 60); do
    B64=$(onevm show cp-1 --json 2>/dev/null \
          | jq -r '.VM.USER_TEMPLATE.KUBECONFIG_B64 // empty')
    [[ -n "$B64" ]] && break
    log "  cp-1 has not published KUBECONFIG_B64 yet; waiting..."
    sleep 10
  done
  [[ -z "$B64" ]] && die "cp-1 did not publish KUBECONFIG_B64 in 10 minutes"
  printf '%s' "$B64" | base64 -d > "$KUBECONFIG_OUT"
  chmod 600 "$KUBECONFIG_OUT"
  log "  kubeconfig written to ${KUBECONFIG_OUT}"
  log "  next: export KUBECONFIG=${KUBECONFIG_OUT} && kubectl get nodes"
fi

cat <<EOF

[provision] DONE — VMs provisioned.

NEXT STEPS (NOT automated by this script):
  1. Configure your edge router DNAT (cannot be done from OpenNebula):
        ${EDGE_HOST}:443   -> 10.10.0.10:443     (HTTPS / NGINX Ingress)
        ${EDGE_HOST}:80    -> 10.10.0.10:80      (ACME HTTP-01)
        ${EDGE_HOST}:6443  -> 10.10.0.10:6443    (kube-apiserver, ${OPERATOR_CIDR:-<OPERATOR_CIDR>} only)
     Verify with: dig +short ${EDGE_HOST}

  2. export KUBECONFIG=${KUBECONFIG_OUT}
     kubectl get nodes               # 3 Ready nodes expected

  3. ./scripts/bootstrap-cluster.sh  # in-cluster controllers
  4. ./scripts/deploy-apps.sh        # build + push + deploy apps
EOF
