#!/usr/bin/env python3
"""Parche puntual de configuración productiva (incidente pago pending 2026-06-11).

- Marketplace:MercadoPago:WebhookBaseUrl = https://api.comunaclic.cl
  (los webhooks de MP iban a comunaclic.cl, el sitio, y morían con 400)
- Jobs:Enabled = true  (activa PaymentReconciliationJob: sana pagos pegados)
- OrderNotifications:Enabled = true (notificación in-app de venta pagada;
  el email sigue apagado porque no hay SMTP configurado)
Crea .bak.<timestamp> de cada archivo antes de tocarlo.
"""
import json
import shutil
import time

STAMP = time.strftime("%Y%m%d-%H%M%S")
BASE = "/var/www/comunaclic/api/appsettings.json"
PROD = "/var/www/comunaclic/api/appsettings.Production.json"


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    shutil.copy2(path, f"{path}.bak.{STAMP}")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


base = load(BASE)
base.setdefault("Jobs", {})["Enabled"] = True
base.setdefault("OrderNotifications", {})["Enabled"] = True
save(BASE, base)
print("appsettings.json: Jobs.Enabled=true, OrderNotifications.Enabled=true")

prod = load(PROD)
mp = prod.setdefault("Marketplace", {}).setdefault("MercadoPago", {})
mp["WebhookBaseUrl"] = "https://api.comunaclic.cl"
save(PROD, prod)
print("appsettings.Production.json: Marketplace.MercadoPago.WebhookBaseUrl=https://api.comunaclic.cl")
