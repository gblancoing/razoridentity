#!/usr/bin/env python3
"""Resetea la contraseña del súper admin del ACL en producción.

Uso: sudo python3 reset_admin_password_prod.py <email> <nueva_contraseña>
Genera el hash con el MISMO formato de ComunaClick.Acl PasswordHasher:
pbkdf2:100000:base64(salt16):base64(hash32) con PBKDF2-HMAC-SHA256.
"""
import base64
import hashlib
import json
import os
import subprocess
import sys

if len(sys.argv) != 3:
    raise SystemExit("Uso: reset_admin_password_prod.py <email> <password>")

email = sys.argv[1].strip().lower()
password = sys.argv[2]

salt = os.urandom(16)
derived = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, 100_000, dklen=32)
stored = "pbkdf2:100000:%s:%s" % (
    base64.b64encode(salt).decode("ascii"),
    base64.b64encode(derived).decode("ascii"),
)

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

query = (
    "UPDATE acl.users SET password_hash = '%s', updated_at = now() "
    "WHERE lower(email) = '%s' RETURNING email;" % (stored, email)
)
result = subprocess.run(["psql", conn, "-tAc", query], capture_output=True, text=True)
out = result.stdout.strip() or result.stderr.strip()
print("actualizado:", out if out else "(sin filas — email no encontrado)")
