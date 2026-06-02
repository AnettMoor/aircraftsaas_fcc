#!/usr/bin/env bash
# =====================================================================
# opennebula/render.sh
#
# One-shot renderer that takes the parameterised opennebula/ templates
# and produces tenancy-specific, parser-clean files under /tmp/onerender
# that can be fed directly to the `one*` CLI without further editing.
#
# What it does:
#   1. Discovers the numeric IDs of the aircraft-cp and aircraft-wk VM
#      templates (must already exist -- create them first via
#      `onetemplate create opennebula/templates/cp.tpl` etc.).
#   2. Substitutes the operator public IP into security-groups/edge.tpl,
#      replacing the "203.0.113.42" placeholder.
#   3. Converts service/aircraft.oneflow.yaml to JSON (oneflow requires
#      JSON, not YAML) AND substitutes the AIRCRAFT_CP_TEMPLATE_ID /
#      AIRCRAFT_WK_TEMPLATE_ID placeholders with the real numeric IDs.
#   4. Copies all other templates verbatim to /tmp/onerender so the
#      operator runs `one* create /tmp/onerender/<file>` from a single
#      directory.
#
# Idempotent: re-running overwrites /tmp/onerender. No OpenNebula state
# is modified -- this is a local-file renderer only.
#
# Usage:
#   export OPERATOR_IP=$(curl -s https://ifconfig.me)
#   ./opennebula/render.sh
#   # then follow the runbook from `onesecgroup create ...` onwards.
#
# Pre-requisites on the operator workstation:
#   * Bash 4+, python3 (for YAML->JSON conversion).
#   * `onetemplate` CLI configured against the target OpenNebula tenancy.
#
# Why a script (and not "just commit rendered files"):
#   * The operator IP varies per workstation.
#   * The aircraft-cp / aircraft-wk template IDs are assigned by
#     OpenNebula at `onetemplate create` time and are tenancy-specific.
#   * Hard-coding either in git would break on every other tenancy.
# =====================================================================

set -euo pipefail

# ---------------------------------------------------------------------
# Pre-flight checks
# ---------------------------------------------------------------------
if ! command -v onetemplate >/dev/null 2>&1; then
    echo "ERROR: onetemplate CLI not found in PATH. Are you on the OpenNebula front-end?" >&2
    exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required for YAML->JSON conversion." >&2
    exit 1
fi

OPERATOR_IP="${OPERATOR_IP:-}"
if [[ -z "$OPERATOR_IP" ]]; then
    echo "ERROR: OPERATOR_IP env var must be set (your public IP)." >&2
    echo "Hint:  export OPERATOR_IP=\$(curl -s https://ifconfig.me)" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="${OUT_DIR:-/tmp/onerender}"
mkdir -p "$OUT_DIR"

echo "=========================================================="
echo "Rendering OpenNebula templates -> $OUT_DIR"
echo "  OPERATOR_IP = $OPERATOR_IP"
echo "=========================================================="

# ---------------------------------------------------------------------
# 1. vNet -- copy verbatim (no substitution needed)
# ---------------------------------------------------------------------
cp "$SCRIPT_DIR/vnet/aircraft-vnet.tpl" "$OUT_DIR/aircraft-vnet.tpl"
echo "[ok] aircraft-vnet.tpl"

# ---------------------------------------------------------------------
# 2. Security groups -- copy verbatim except edge.tpl which gets the
#    operator IP substituted.
# ---------------------------------------------------------------------
cp "$SCRIPT_DIR/security-groups/cluster.tpl"  "$OUT_DIR/sg-cluster.tpl"
cp "$SCRIPT_DIR/security-groups/nodeport.tpl" "$OUT_DIR/sg-nodeport.tpl"
echo "[ok] sg-cluster.tpl"
echo "[ok] sg-nodeport.tpl"

sed "s|203\\.0\\.113\\.42|$OPERATOR_IP|g" \
    "$SCRIPT_DIR/security-groups/edge.tpl" > "$OUT_DIR/sg-edge.tpl"
if grep -q "203.0.113.42" "$OUT_DIR/sg-edge.tpl"; then
    echo "ERROR: edge.tpl placeholder substitution failed." >&2
    exit 1
fi
echo "[ok] sg-edge.tpl  (operator IP = $OPERATOR_IP)"

# ---------------------------------------------------------------------
# 3. VM templates -- inline the base64-encoded cloud-init script
#    into the START_SCRIPT_BASE64 slot. This is what cp.tpl/wk.tpl
#    reserve with the BASE64_OF_CLOUD_INIT_{CP,WK}_YAML placeholder.
# ---------------------------------------------------------------------
CP_INIT_B64="$(base64 -w0 "$SCRIPT_DIR/context/cloud-init.cp.yaml")"
WK_INIT_B64="$(base64 -w0 "$SCRIPT_DIR/context/cloud-init.wk.yaml")"

sed "s|BASE64_OF_CLOUD_INIT_CP_YAML|$CP_INIT_B64|" \
    "$SCRIPT_DIR/templates/cp.tpl" > "$OUT_DIR/cp.tpl"
sed "s|BASE64_OF_CLOUD_INIT_WK_YAML|$WK_INIT_B64|" \
    "$SCRIPT_DIR/templates/wk.tpl" > "$OUT_DIR/wk.tpl"
echo "[ok] cp.tpl  (cloud-init.cp.yaml inlined, $(wc -c < "$SCRIPT_DIR/context/cloud-init.cp.yaml") bytes)"
echo "[ok] wk.tpl  (cloud-init.wk.yaml inlined, $(wc -c < "$SCRIPT_DIR/context/cloud-init.wk.yaml") bytes)"

# ---------------------------------------------------------------------
# 4. OneFlow service -- YAML to JSON + numeric template ID substitution.
#    The aircraft-cp / aircraft-wk template IDs are looked up via
#    `onetemplate list`. If the templates don't exist yet, we cannot
#    render the oneflow file -- in that case skip with a warning so the
#    operator can re-run after `onetemplate create`.
# ---------------------------------------------------------------------
# onetemplate list columns: ID USER GROUP NAME REGTIME
# The NAME is column 4 (not 2 -- USER and GROUP sit between ID and NAME).
CP_ID=$(onetemplate list --no-header 2>/dev/null | awk '$4=="aircraft-cp" {print $1; exit}')
WK_ID=$(onetemplate list --no-header 2>/dev/null | awk '$4=="aircraft-wk" {print $1; exit}')

if [[ -z "$CP_ID" || -z "$WK_ID" ]]; then
    cat >&2 <<EOF
[warn] aircraft-cp / aircraft-wk VM templates not yet created in this tenancy.
[warn] Skipping oneflow render. After running:
[warn]   onetemplate create $OUT_DIR/cp.tpl
[warn]   onetemplate create $OUT_DIR/wk.tpl
[warn] re-run this script to produce aircraft.oneflow.json.
EOF
else
    python3 - <<PY
import json, re, sys, yaml, pathlib
src = pathlib.Path("$SCRIPT_DIR/service/aircraft.oneflow.yaml").read_text()
# yaml.safe_load chokes on the unquoted placeholders AIRCRAFT_CP_TEMPLATE_ID
# (it tries to interpret them as bare scalars). Substitute first.
src = src.replace("AIRCRAFT_CP_TEMPLATE_ID", "$CP_ID")
src = src.replace("AIRCRAFT_WK_TEMPLATE_ID", "$WK_ID")
data = yaml.safe_load(src)
pathlib.Path("$OUT_DIR/aircraft.oneflow.json").write_text(json.dumps(data, indent=2))
PY
    echo "[ok] aircraft.oneflow.json  (cp_template_id=$CP_ID, wk_template_id=$WK_ID)"
fi

# ---------------------------------------------------------------------
# 5. Static validation -- no unresolved macros should remain in
#    non-comment lines of rendered output. Comments may legitimately
#    mention the placeholders in their explanation (e.g. cluster.tpl's
#    header explains why $NETWORK[...] was REMOVED).
# ---------------------------------------------------------------------
LEAKED=$(grep -RnE '\$NETWORK\[|\$CONTEXT\[|AIRCRAFT_CP_TEMPLATE_ID|AIRCRAFT_WK_TEMPLATE_ID|203\.0\.113\.42|BASE64_OF_CLOUD_INIT' "$OUT_DIR" 2>/dev/null \
    | grep -vE ':[[:space:]]*#' || true)
if [[ -n "$LEAKED" ]]; then
    echo "ERROR: unresolved placeholder(s) remain in NON-COMMENT lines:" >&2
    echo "$LEAKED" >&2
    exit 1
fi

echo "=========================================================="
echo "OK -- all templates rendered cleanly."
echo "Next steps:"
echo "  onevnet     create $OUT_DIR/aircraft-vnet.tpl"
echo "  onesecgroup create $OUT_DIR/sg-cluster.tpl"
echo "  onesecgroup create $OUT_DIR/sg-edge.tpl"
echo "  onesecgroup create $OUT_DIR/sg-nodeport.tpl"
echo "  onetemplate create $OUT_DIR/cp.tpl"
echo "  onetemplate create $OUT_DIR/wk.tpl"
echo "  # re-run ./opennebula/render.sh to produce aircraft.oneflow.json"
echo "  oneflow-template create $OUT_DIR/aircraft.oneflow.json"
echo "  # OpenNebula 7.x instantiate takes NO flags -- pass a JSON file"
echo "  # with 'custom_attrs_values' at top level as positional arg:"
echo "  oneflow-template instantiate <template-id> $OUT_DIR/instantiate.json"
echo "=========================================================="
