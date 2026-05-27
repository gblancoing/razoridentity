#!/usr/bin/env bash
set -euo pipefail

HOST="${HOST:-3.92.248.0}"
SSH_USER="${SSH_USER:-ubuntu}"
SSH_KEY="${SSH_KEY:?Set SSH_KEY to your .pem path}"
BASE_REPO="${BASE_REPO:-$(cd "$(dirname "$0")/.." && pwd)}"

ssh_opts=(-o StrictHostKeyChecking=accept-new)
[[ -n "$SSH_KEY" ]] && ssh_opts+=(-i "$SSH_KEY")

MIGRATIONS=(
  2026-04-23_product_image_url.sql
  20260525_inbox_messages.sql
  20260526_service_catalog_type_b.sql
  20260527_service_images_address.sql
  20260528_partner_professional_storefront.sql
  20260529_commerce_type_a.sql
  20260530_partner_bank_account.sql
  20260531_partner_logo_url.sql
  20260601_profile_web_links.sql
  20260602_service_geo_coordinates.sql
)

remote() { ssh "${ssh_opts[@]}" "${SSH_USER}@${HOST}" "$@"; }

remote "mkdir -p /tmp/comunaclic-migrations"
for f in "${MIGRATIONS[@]}"; do
  scp "${ssh_opts[@]}" "${BASE_REPO}/infra/migrations/${f}" "${SSH_USER}@${HOST}:/tmp/comunaclic-migrations/"
done
scp "${ssh_opts[@]}" "${BASE_REPO}/tmp_deploy_scripts/run_migration_on_prod.sh" "${SSH_USER}@${HOST}:/tmp/comunaclic-migrations/"
remote "sed -i 's/\r$//' /tmp/comunaclic-migrations/run_migration_on_prod.sh && chmod +x /tmp/comunaclic-migrations/run_migration_on_prod.sh"

for f in "${MIGRATIONS[@]}"; do
  echo "=== Running ${f} ==="
  remote "bash /tmp/comunaclic-migrations/run_migration_on_prod.sh /tmp/comunaclic-migrations/${f}"
done

echo "[OK] Pending migrations applied on production."
