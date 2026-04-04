# Pasarela de pagos multi-provider

## Objetivo

Iniciar una pasarela de pagos unificada sobre `Payments.Common` y `Payments.Gateway.Api`, soportando primero checkout redirigido para:

- Transbank Webpay Plus
- Khipu
- Mercado Pago Checkout Pro

`Oneclick` y tokenización recurrente quedan para una segunda fase.

## Documentación oficial revisada

- Transbank Webpay Plus:
  - https://www.transbankdevelopers.cl/documentacion/webpay-plus
- Khipu API v3:
  - https://docs.khipu.com/portal/es/khipu-api/payment-api-v3
- Mercado Pago Checkout Pro / Preferences:
  - https://www.mercadopago.cl/developers/es/reference/preferences/_checkout_preferences/post

## Patrón común de integración

Los 3 proveedores permiten modelar el inicio de pago como:

1. Recibir una orden local `ExternalReference`, `Amount`, `Currency`, `ReturnUrl`.
2. Crear una intención de pago contra el proveedor.
3. Persistir:
   - `Provider`
   - `ProviderToken` o identificador externo de checkout
   - `RedirectUrl`
   - `RawResponse`
   - `Status`
4. Redirigir al comprador a la URL del proveedor.
5. Confirmar/actualizar estado vía retorno/webhook y registrar un `ProviderEvent`.

## Mapeo por proveedor

## Transbank Webpay Plus

Según documentación oficial, el inicio de Webpay Plus crea una transacción y retorna `token` + `url`.

Mapeo propuesto:

- `PaymentIntent.Provider = "transbank"`
- `PaymentIntent.ProviderToken = token`
- `PaymentIntent.RawResponse = payload completo de creación`
- `PaymentIntentResponse.RedirectUrl = url + token_ws`

Notas:

- En integración productiva, Transbank requiere credenciales `Tbk-Api-Key-Id` y `Tbk-Api-Key-Secret`.
- El retorno/commit de transacción debe implementarse en una siguiente fase con un endpoint explícito de confirmación.

## Khipu

Según documentación oficial de Khipu API v3, la creación de pago se realiza con `POST /v3/payments` y retorna `payment_id` y `payment_url`.

Mapeo propuesto:

- `PaymentIntent.Provider = "khipu"`
- `PaymentIntent.ProviderToken = payment_id`
- `PaymentIntent.RawResponse = payload completo de creación`
- `PaymentIntentResponse.RedirectUrl = payment_url`

Notas:

- La notificación asíncrona debe apuntar a un webhook propio del gateway.
- Si todavía no hay credenciales reales, se puede operar en `Simulate=true` para validar flujo interno.

## Mercado Pago Checkout Pro

Según la referencia oficial de Preferences, la creación de preferencia se realiza con `POST /checkout/preferences` y retorna `id`, `init_point` y `sandbox_init_point`.

Mapeo propuesto:

- `PaymentIntent.Provider = "mercadopago"`
- `PaymentIntent.ProviderToken = preference.id`
- `PaymentIntent.RawResponse = payload completo de creación`
- `PaymentIntentResponse.RedirectUrl = init_point` o `sandbox_init_point`

Notas:

- El request debe incluir `external_reference`, `items` y `back_urls`.
- Webhook/notifications y validación de pago confirmado quedan para la siguiente fase.

## Implementación aplicada en esta primera fase

- `Payments.Common` ahora expone contratos de creación de pago por proveedor:
  - `PaymentProviderCreateRequest`
  - `PaymentProviderCreateResponse`
  - `IPaymentProvider.CreatePaymentAsync(...)`
- `Payments.Gateway.Api` ahora tiene:
  - `TransbankPaymentProvider`
  - `KhipuPaymentProvider`
  - `MercadoPagoPaymentProvider`
  - `PaymentProviderResolver`
- `PaymentIntentsController.Create(...)` ya no genera siempre un token local fijo, sino que delega la creación al proveedor seleccionado.
- `appsettings.json` incorpora secciones `PaymentProviders:*` con `Simulate=true` por defecto.
- Se agregaron callbacks/webhooks por proveedor:
  - `GET|POST /v1/payment-callbacks/{provider}/return`
  - `POST /v1/payment-callbacks/{provider}/webhook`
- Cada callback:
  - delega la interpretación/confirmación al adapter del proveedor
  - busca la `PaymentIntent` por `Provider + ProviderToken` o `ExternalReference`
  - actualiza `Status`, `AuthorizationCode`, `RawResponse`
  - registra idempotente un `ProviderEvent`
  - notifica a ComunaClic Core mediante `IComunaClicNotifier`

## Próximas etapas recomendadas

## Etapa 1 - Checkout redirigido real

- Definir credenciales sandbox por proveedor fuera de `appsettings.json`.
- Conectar credenciales reales sandbox en `PaymentProviders:*`.
- Probar callbacks reales:
  - Transbank commit por `token_ws`
  - Khipu notificación por `payment_id`
  - Mercado Pago notificación/consulta por `id` y `data.id`
- Definir una página de resultado/retorno en `app` para mostrar estado al comprador cuando vuelva desde el proveedor.

## Etapa 2 - Webhooks firmados y seguridad

- Agregar rate limiting y headers de seguridad también a `Payments.Gateway.Api`.
- Validar autenticidad/origen de webhooks según cada proveedor.
- Proteger endpoints internos de cambio de estado con llave interna o firma.
- Evitar guardar datos sensibles de tarjetas; persistir solo identificadores/tokens de proveedor.

## Etapa 3 - Oneclick/tokenización

- Separar claramente qué proveedores soportan pago oneclick/tokenizado en este gateway.
- Completar flujos de inscripción, charge y baja de token.
- Alinear `CustomerToken.ProviderRef` con identificadores reales de cada proveedor.

## Etapa 4 - QA y conciliación

- Agregar pruebas unitarias por provider adapter en modo simulado.
- Agregar pruebas de integración sobre `POST /v1/payment-intents`.
- Probar idempotencia de `ExternalReference + Provider + pending`.
- Revisar conciliación entre `PaymentIntent`, `Charge`, `ProviderEvent` y notificaciones hacia ComunaClic Core.

## Riesgo/decisión pendiente

Para Transbank, Khipu y Mercado Pago el retorno/browser redirect y el webhook no son idénticos. La recomendación es **no forzar un único endpoint genérico sin metadata**, sino crear callbacks por proveedor o un callback común que reciba `provider` y luego delegue al adapter correspondiente.
