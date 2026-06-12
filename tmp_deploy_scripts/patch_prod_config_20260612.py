#!/usr/bin/env python3
"""Parche de configuración productiva (release checkout/envío 2026-06-12).

El deploy preserva appsettings.json del servidor, así que la nueva config de
reserva/expiración de stock del repo no llega sola a producción:

- Inventory:ReservingOrderStatuses = ["payment_pending"]
  (órdenes pendientes reservan stock; sin esto la reserva queda APAGADA porque
  el default en código es lista vacía)
- Inventory:PendingOrderTtlMinutes = 5
  (la reserva vence a los 5 min; el job de expiración cancela la orden formalmente)

Crea .bak.<timestamp> del archivo antes de tocarlo.
"""
import json
import shutil
import time

STAMP = time.strftime("%Y%m%d-%H%M%S")
BASE = "/var/www/comunaclic/api/appsettings.json"


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    shutil.copy2(path, f"{path}.bak.{STAMP}")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


base = load(BASE)
inventory = base.setdefault("Inventory", {})
inventory["ReservingOrderStatuses"] = ["payment_pending"]
inventory["PendingOrderTtlMinutes"] = 5
save(BASE, base)
print("appsettings.json: Inventory.ReservingOrderStatuses=[payment_pending], PendingOrderTtlMinutes=5")
