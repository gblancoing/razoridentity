#!/usr/bin/env bash
# Ejecuta un archivo SQL en RDS usando ConnectionStrings:CoreDb de appsettings de la API.
set -euo pipefail
SQL_FILE="${1:?Uso: $0 /ruta/migracion.sql}"
API_SETTINGS="${API_SETTINGS:-/var/www/comunaclic/api/appsettings.json}"

python3 <<PY
import json, os, subprocess, sys
with open("${API_SETTINGS}") as f:
    cs = json.load(f)["ConnectionStrings"]["CoreDb"]
parts = {}
for token in cs.split(";"):
    if "=" in token:
        k, v = token.split("=", 1)
        parts[k.strip()] = v.strip()
env = os.environ.copy()
env["PGPASSWORD"] = parts["Password"]
env["PGSSLMODE"] = "require"
cmd = [
    "psql",
    "-h", parts["Host"],
    "-p", parts.get("Port", "5432"),
    "-U", parts["Username"],
    "-d", parts["Database"],
    "-v", "ON_ERROR_STOP=1",
    "-f", "${SQL_FILE}",
]
r = subprocess.run(cmd, env=env)
sys.exit(r.returncode)
PY
