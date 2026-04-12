# Payments.App - Configuración Mercado Pago

Documento de configuración para `Payments.App` usando referencias oficiales de Mercado Pago:

- References API: https://www.mercadopago.cl/developers/es/reference
- Preferences (Checkout Pro): https://www.mercadopago.cl/developers/es/reference/preferences/_checkout_preferences/post
- Payments: https://www.mercadopago.cl/developers/es/reference/payments/_payments_id/get

## Sección `appsettings`

```json
"PaymentProviders": {
  "MercadoPago": {
    "Enabled": true,
    "Environment": "sandbox",
    "PublicKey": "APP_USR-...",
    "CreatePreferenceUrl": "https://api.mercadopago.com/checkout/preferences",
    "GetPaymentUrl": "https://api.mercadopago.com/v1/payments",
    "WebhookTopic": "payment",
    "ValidateWebhookSignature": false
  }
}
```

## Campos

- `Enabled`: habilita visualmente la integración en el panel.
- `Environment`: `sandbox` o `production`.
- `PublicKey`: clave pública para componentes front cuando se habilite checkout embebido.
- `CreatePreferenceUrl`: endpoint para crear preferencias Checkout Pro.
- `GetPaymentUrl`: endpoint para consultar estado de pago por id.
- `WebhookTopic`: tópico esperado para notificaciones de pago.
- `ValidateWebhookSignature`: indica si se valida firma de webhook.

## Nota de seguridad

- No guardar `AccessToken` secreto en `Payments.App`.
- El `AccessToken` debe quedar en `Payments.Gateway.Api` o variable de entorno del backend.
