#!/usr/bin/env python3
"""Verificación post-deploy release transportistas 2026-06-12 (read-only).

1. ¿Existe core.partners.preferred_delivery_provider_id? (migración bootstrap)
2. Transportistas existentes.
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

queries = [
    ("columna preferred_delivery_provider_id",
     "SELECT count(*) FROM information_schema.columns "
     "WHERE table_schema='core' AND table_name='partners' "
     "AND column_name='preferred_delivery_provider_id';"),
    ("transportistas",
     "SELECT id, name, base_fee, is_active FROM core.delivery_providers;"),
]

for label, query in queries:
    result = subprocess.run(["psql", conn, "-tAc", query], capture_output=True, text=True)
    print(f"{label}: {result.stdout.strip() or result.stderr.strip()}")
