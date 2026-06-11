#!/usr/bin/env bash
# dotnet publish local + deploy al EC2 (api, acl, app, ...).
set -euo pipefail

BASE_REPO="${BASE_REPO:-$(cd "$(dirname "$0")/.." && pwd)}"
DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$BASE_REPO/.dotnet_home}"
PUBLISH_ROOT="${PUBLISH_ROOT:-$BASE_REPO/.publish}"
CONFIGURATION="${CONFIGURATION:-Release}"
FRAMEWORK="${FRAMEWORK:-net8.0}"

export DOTNET_CLI_HOME
mkdir -p "$DOTNET_CLI_HOME" "$PUBLISH_ROOT"

declare -A PROJECTS=(
  [api]="src/ComunaClick.Api/ComunaClick.Api.csproj"
  [acl]="src/ComunaClick.Acl/ComunaClick.Acl.csproj"
  [app]="src/ComunaClick/ComunaClick.App.csproj"
  [admin]="src/ComunaClick.Admin/ComunaClick.Admin.csproj"
)

usage() {
  cat <<EOF
Uso: SSH_KEY=/ruta/llave.pem $0 <api|acl|app> [...]

Ejemplo (solo frontend):
  SSH_KEY=~/.ssh/comunaclic.pem $0 app
EOF
}

publish_one() {
  local target="$1"
  local project_rel="${PROJECTS[$target]}"
  local project_path="$BASE_REPO/$project_rel"
  local out_dir="$PUBLISH_ROOT/$target"

  echo "[INFO] Publish $target -> $out_dir"
  rm -rf "$out_dir"
  dotnet publish "$project_path" \
    -c "$CONFIGURATION" \
    -f "$FRAMEWORK" \
    -o "$out_dir" \
    -m:1 \
    -nr:false \
    -v minimal
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

cd "$BASE_REPO"

for target in "$@"; do
  if [[ -z "${PROJECTS[$target]:-}" ]]; then
    echo "[FAIL] Target desconocido: $target" >&2
    exit 1
  fi
  publish_one "$target"
done

bash "$(dirname "$0")/deploy_comunaclic.sh" "$@"

echo "[INFO] Smoke opcional: bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh"
