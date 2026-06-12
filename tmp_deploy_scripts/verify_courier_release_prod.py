#!/usr/bin/env python3
"""Verificación post-deploy release portal repartidor 2026-06-12 (read-only):
¿la migración 20260615 (couriers.email/user_id) se aplicó al arrancar la API?
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

query = (
    "SELECT count(*) FROM information_schema.columns "
    "WHERE table_schema='core' AND table_name='couriers' "
    "AND column_name IN ('email','user_id');"
)
result = subprocess.run(["psql", conn, "-tAc", query], capture_output=True, text=True)
print("columnas couriers email/user_id (esperado 2):", result.stdout.strip() or result.stderr.strip())
