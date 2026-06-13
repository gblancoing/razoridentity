#!/usr/bin/env python3
"""Diagnóstico read-only del pedido 6601541b: orden, liquidación y repartidor."""
import json
import subprocess

cfg = json.load(open("/var/www/comunaclic/api/appsettings.json", encoding="utf-8"))
cs = next(iter(cfg.get("ConnectionStrings", {}).values()))
parts = dict(
    (k.strip().lower(), v.strip())
    for k, v in (item.split("=", 1) for item in cs.split(";") if "=" in item)
)
conn = "host=%s dbname=%s user=%s password=%s" % (
    parts.get("host"), parts.get("database"),
    parts.get("username") or parts.get("user id"), parts.get("password"),
)


def run(q):
    r = subprocess.run(["psql", conn, "-tAc", q], capture_output=True, text=True)
    return r.stdout.strip() or r.stderr.strip()


print("--- orden 6601541b ---")
print(run(
    "SELECT id, partner_id, courier_id, status, delivery_status, delivery_type, "
    "subtotal, delivery_fee, total_amount "
    "FROM core.orders WHERE id::text LIKE '6601541b%';"))

print("--- liquidacion del pedido ---")
print(run(
    "SELECT s.id, s.partner_id, s.courier_id, s.status, s.gross_amount, "
    "s.platform_fee_amount, s.mercadopago_fee_amount, s.net_to_courier_amount "
    "FROM core.delivery_settlements s "
    "JOIN core.orders o ON o.id = s.order_id "
    "WHERE o.id::text LIKE '6601541b%';"))

print("--- repartidor asignado ---")
print(run(
    "SELECT c.id, c.name, c.email, c.user_id, c.partner_id "
    "FROM core.couriers c JOIN core.orders o ON o.courier_id = c.id "
    "WHERE o.id::text LIKE '6601541b%';"))

print("--- total liquidaciones por partner del pedido ---")
print(run(
    "SELECT count(*) FROM core.delivery_settlements s "
    "JOIN core.orders o ON o.id::text LIKE '6601541b%' "
    "WHERE s.partner_id = o.partner_id;"))
