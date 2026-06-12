#!/usr/bin/env python3
"""Crea (idempotente) un transportista global de prueba en producción.

Sin transportistas en core.delivery_providers el checkout cotiza envío $0 por
diseño. Este seed habilita el flujo completo; luego se edita o reemplaza por
los transportistas reales desde el panel Admin (/delivery-providers).
"""
import json
import subprocess

cfg = json.load(open("/var/www/comunaclic/api/appsettings.json", encoding="utf-8"))
cs = next(iter(cfg.get("ConnectionStrings", {}).values()))
parts = dict(
    (k.strip().lower(), v.strip())
    for k, v in (item.split("=", 1) for item in cs.split(";") if "=" in item)
)
conn = "host=%s dbname=%s user=%s password=%s" % (
    parts.get("host"),
    parts.get("database"),
    parts.get("username") or parts.get("user id"),
    parts.get("password"),
)

insert = """
INSERT INTO core.delivery_providers
    (tenant_id, region_id, comuna_id, name, contact_name, base_fee, estimated_minutes, is_active, created_at, updated_at)
SELECT NULL, NULL, NULL, 'Despacho ComunaClic (prueba)', 'Equipo ComunaClic', 2000, 60, true, now(), now()
WHERE NOT EXISTS (
    SELECT 1 FROM core.delivery_providers WHERE name = 'Despacho ComunaClic (prueba)'
);
"""
check = "SELECT id, name, base_fee, is_active FROM core.delivery_providers;"

for query in (insert, check):
    result = subprocess.run(["psql", conn, "-tAc", query], capture_output=True, text=True)
    print(result.stdout.strip() or result.stderr.strip())
