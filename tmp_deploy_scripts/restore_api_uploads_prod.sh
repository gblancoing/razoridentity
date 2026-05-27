#!/usr/bin/env bash
set -euo pipefail

HOST="${HOST:-3.92.248.0}"
SSH_USER="${SSH_USER:-ubuntu}"
SSH_KEY="${SSH_KEY:?Set SSH_KEY}"

ssh_opts=(-o StrictHostKeyChecking=accept-new -i "$SSH_KEY")

ssh "${ssh_opts[@]}" "${SSH_USER}@${HOST}" bash -s <<'REMOTE'
set -euo pipefail
API_DIR="/var/www/comunaclic/api"
best=""
best_size=0
for d in "${API_DIR}.bak."*; do
  [ -d "$d/uploads" ] || continue
  size=$(du -sk "$d/uploads" | cut -f1)
  echo "$d -> ${size}K"
  if [ "$size" -gt "$best_size" ]; then
    best="$d"
    best_size=$size
  fi
done
if [ -n "$best" ] && [ "$best_size" -gt 0 ]; then
  echo "Restoring from $best (${best_size}K)..."
  sudo rm -rf "${API_DIR}/uploads"
  sudo cp -a "${best}/uploads" "${API_DIR}/uploads"
  sudo chown -R svc-comunaclic:svc-comunaclic "${API_DIR}/uploads"
  du -sh "${API_DIR}/uploads"
else
  echo "No uploads found in backups."
fi
REMOTE
