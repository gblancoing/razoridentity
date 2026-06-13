#!/usr/bin/env bash
# Despliega artefactos ya publicados (carpeta .publish/<target>) al EC2.
set -euo pipefail

HOST="${HOST:-3.92.248.0}"
SSH_USER="${SSH_USER:-ubuntu}"
SSH_KEY="${SSH_KEY:-}"
BASE_REPO="${BASE_REPO:-$(cd "$(dirname "$0")/.." && pwd)}"
PUBLISH_ROOT="${PUBLISH_ROOT:-$BASE_REPO/.publish}"
REMOTE_OWNER="${REMOTE_OWNER:-svc-comunaclic:svc-comunaclic}"

ssh_opts=(-o StrictHostKeyChecking=accept-new)
if [[ -n "$SSH_KEY" ]]; then
  ssh_opts+=(-i "$SSH_KEY")
fi

remote() {
  ssh "${ssh_opts[@]}" "${SSH_USER}@${HOST}" "$@"
}

sync_to_remote() {
  local src="$1"
  local dest="$2"
  if command -v rsync >/dev/null 2>&1; then
    local ssh_cmd="ssh ${ssh_opts[*]}"
    # No borrar uploads/ (imágenes de productos, servicios, logos) en despliegues de API.
    rsync -az --delete --exclude 'uploads/' -e "$ssh_cmd" "${src}/" "${SSH_USER}@${HOST}:${dest}/"
  else
    echo "[INFO] rsync no disponible; usando tar+ssh"
    tar -C "$src" -czf - . | ssh "${ssh_opts[@]}" "${SSH_USER}@${HOST}" "sudo tar -C '${dest}' -xzf -"
  fi
}

deploy_one() {
  local target="$1"
  local remote_dir="$2"
  local service="$3"
  local local_dir="${PUBLISH_ROOT}/${target}"

  if [[ ! -d "$local_dir" ]]; then
    echo "[FAIL] No existe $local_dir — ejecuta publish_and_deploy primero." >&2
    exit 1
  fi

  local ts
  ts="$(date -u +%Y%m%d-%H%M%S)"
  local backup="${remote_dir}.bak.${ts}"

  echo "[INFO] Deploy ${target} -> ${HOST}:${remote_dir}"
  remote "sudo test -d '${remote_dir}' || sudo mkdir -p '${remote_dir}'"
  remote "if [ -d '${remote_dir}' ] && [ \"\$(ls -A '${remote_dir}' 2>/dev/null)\" ]; then sudo cp -a '${remote_dir}' '${backup}'; fi"
  # Preservar uploads/ (archivos subidos por usuarios) al reemplazar binarios.
  remote "sudo find '${remote_dir}' -mindepth 1 -maxdepth 1 ! -name 'uploads' -exec rm -rf {} + 2>/dev/null || true"
  sync_to_remote "$local_dir" "$remote_dir"
  # No pisar config de producción ni medios subidos.
  remote "if [ -d '${backup}' ]; then
    for f in appsettings.json appsettings.Production.json; do
      if [ -f '${backup}/'\$f ]; then
        sudo cp '${backup}/'\$f '${remote_dir}/'\$f
        echo \"[INFO] Restaurado \$f desde backup\"
      fi
    done
    if [ ! -d '${remote_dir}/uploads' ] && [ -d '${backup}/uploads' ]; then
      sudo cp -a '${backup}/uploads' '${remote_dir}/uploads'
      echo \"[INFO] Restaurado uploads/ desde backup\"
    fi
  fi"
  remote "sudo chown -R ${REMOTE_OWNER} '${remote_dir}'"
  remote "sudo systemctl restart '${service}'"
  remote "sudo systemctl is-active --quiet '${service}'"
  echo "[OK] ${target} desplegado (${service} activo). Backup: ${backup}"
}

usage() {
  cat <<EOF
Uso: SSH_KEY=/ruta/llave.pem HOST=3.92.248.0 $0 <api|acl|app> [...]

Variables:
  HOST, SSH_USER, SSH_KEY, BASE_REPO, PUBLISH_ROOT, REMOTE_OWNER
EOF
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

declare -A REMOTE_DIRS=(
  [api]="/var/www/comunaclic/api"
  [acl]="/var/www/comunaclic/acl"
  [app]="/var/www/comunaclic/app"
  [admin]="/var/www/comunaclic/admin"
)
declare -A SERVICES=(
  [api]="comunaclic-api.service"
  [acl]="comunaclic-acl.service"
  [app]="comunaclic-app.service"
  [admin]="comunaclic-admin.service"
)

for target in "$@"; do
  if [[ -z "${REMOTE_DIRS[$target]:-}" ]]; then
    echo "[FAIL] Target desconocido: $target" >&2
    exit 1
  fi
  deploy_one "$target" "${REMOTE_DIRS[$target]}" "${SERVICES[$target]}"
done
