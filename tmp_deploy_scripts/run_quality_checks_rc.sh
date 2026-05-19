#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT_DIR/.dotnet_home}"

required_vars=(
  QUALITY_QA_EMAIL
  QUALITY_QA_PASSWORD
  QUALITY_BOOKING_FLOW_SERVICE_ID
)

missing=()
for var_name in "${required_vars[@]}"; do
  if [[ -z "${!var_name:-}" ]]; then
    missing+=("$var_name")
  fi
done

if (( ${#missing[@]} > 0 )); then
  echo "[FAIL] Faltan variables requeridas para RC autenticado:"
  for var_name in "${missing[@]}"; do
    echo "  - $var_name"
  done
  echo
echo "Ejemplo:"
  echo "  QUALITY_QA_EMAIL=qa@comunaclic.cl \\"
  echo "  QUALITY_QA_PASSWORD='***' \\"
  echo "  QUALITY_BOOKING_FLOW_SERVICE_ID='00000000-0000-0000-0000-000000000000' \\"
  echo "  QUALITY_PARTNER_ID='00000000-0000-0000-0000-000000000000' \\"
  echo "  bash tmp_deploy_scripts/run_quality_checks_rc.sh"
  exit 1
fi

echo "[INFO] Ejecutando QualityChecks RC autenticado..."
echo "[INFO] App: ${QUALITY_APP_BASE_URL:-https://app.comunaclic.cl}"
echo "[INFO] ACL: ${QUALITY_ACL_BASE_URL:-https://acl.comunaclic.cl}"
echo "[INFO] API: ${QUALITY_API_BASE_URL:-https://api.comunaclic.cl}"

cd "$ROOT_DIR"
DOTNET_CLI_HOME="$DOTNET_CLI_HOME" dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj
