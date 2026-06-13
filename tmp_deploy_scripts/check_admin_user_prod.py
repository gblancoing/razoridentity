#!/usr/bin/env python3
"""Diagnóstico read-only: ¿existe el súper admin en el ACL de producción,
está activo y tiene rol platform_admin/tenant_admin?
"""
import json
import subprocess

cfg = json.load(open("/var/www/comunaclic/acl/appsettings.json", encoding="utf-8"))
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


def run(query):
    result = subprocess.run(["psql", conn, "-tAc", query], capture_output=True, text=True)
    return result.stdout.strip() or result.stderr.strip()


print("--- esquemas con tabla users ---")
schema_rows = run(
    "SELECT table_schema FROM information_schema.tables WHERE table_name = 'users';")
print(schema_rows or "(ninguno)")

schema = (schema_rows.splitlines()[0].strip() if schema_rows and "ERROR" not in schema_rows else None)
if not schema:
    print("Sin tabla users en esta base. Bases disponibles:")
    print(run("SELECT datname FROM pg_database WHERE datistemplate = false;"))
    print("dbname usado:", parts.get("database"))
    raise SystemExit(0)

print(f"--- usuarios admin/owner (esquema {schema}) ---")
print(run(
    f"SELECT email, display_name, is_active, is_super_admin, left(password_hash, 16) "
    f"FROM {schema}.users WHERE email IN ('admin@comunaclic.test','owner@comunaclic.test');"))

print("--- roles del admin/owner ---")
print(run(
    f"SELECT u.email, r.name FROM {schema}.users u "
    f"JOIN {schema}.user_roles ur ON ur.user_id = u.id "
    f"JOIN {schema}.roles r ON r.id = ur.role_id "
    f"WHERE u.email IN ('admin@comunaclic.test','owner@comunaclic.test');"))

print("--- total usuarios ---")
print(run(f"SELECT count(*) FROM {schema}.users;"))
