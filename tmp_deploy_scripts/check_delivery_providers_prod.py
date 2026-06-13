#!/usr/bin/env python3
"""Verificación read-only: ¿hay proveedores de despacho activos en producción?

Lee la cadena de conexión de la API y consulta delivery_providers para saber
por qué la cotización de envío responde feeApplies=false (sin proveedor de zona
no se cobra envío, por diseño).
"""
import json
import re
import subprocess

cfg = json.load(open("/var/www/comunaclic/api/appsettings.json", encoding="utf-8"))
cs = None
for key, value in cfg.get("ConnectionStrings", {}).items():
    cs = value
    break
if not cs:
    raise SystemExit("Sin ConnectionStrings en appsettings.json")

parts = dict(
    (k.strip().lower(), v.strip())
    for k, v in (item.split("=", 1) for item in cs.split(";") if "=" in item)
)
host = parts.get("host") or parts.get("server")
db = parts.get("database")
user = parts.get("username") or parts.get("user id")
pwd = parts.get("password")

sql = (
    "SELECT count(*) FILTER (WHERE \"IsActive\") AS activos, count(*) AS total "
    "FROM delivery_providers;"
)
sql_zonas = (
    'SELECT "Name", "IsActive", "ComunaId" IS NOT NULL AS por_comuna, '
    '"RegionId" IS NOT NULL AS por_region FROM delivery_providers LIMIT 10;'
)

for query in (sql, sql_zonas):
    result = subprocess.run(
        ["psql", f"host={host} dbname={db} user={user} password={pwd}", "-tAc", query],
        capture_output=True,
        text=True,
    )
    print(result.stdout.strip() or result.stderr.strip())
