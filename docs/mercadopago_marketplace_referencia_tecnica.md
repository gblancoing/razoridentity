# Integración Mercado Pago Marketplace (Split de Pagos) — ComunaClic

**Plataforma:** app.comunaclic.cl  
**Stack:** ASP.NET Core (Web API) + Blazor  
**Modelo de pago:** Mercado Pago — Split de Pagos / Marketplace  
**País:** Chile (moneda CLP)  
**Fecha:** Junio 2026

> Documento de referencia técnica. Las credenciales reales nunca deben incluirse en este archivo ni en el control de versiones.

---

## PARTE A — CONFIGURACIÓN DEL DUEÑO DE LA PLATAFORMA

### Paso 0 — Requisitos Previos
- Empresa constituida con inicio de actividades en el SII
- Cuenta de Mercado Pago verificada (KYC completo)
- Cuenta bancaria asociada a Mercado Pago
- Dominio con HTTPS activo

### Paso 1 — Crear Aplicación en Mercado Pago Developers
- Ingresar a mercadopago.cl/developers
- Crear aplicación: "ComunaClic Marketplace"
- Seleccionar "Pagos online → Checkout Pro"
- Indicar modelo "Marketplace / Split de pagos"
- Credenciales generadas: `CLIENT_ID`, `CLIENT_SECRET`, `ACCESS_TOKEN`, `PUBLIC_KEY`

### Paso 2 — Configurar URLs
- **Redirect URI:** `https://app.comunaclic.cl/oauth/mercadopago/callback`
- **Webhook URL:** `https://app.comunaclic.cl/api/webhooks/mercadopago`
- Suscribirse a eventos de tipo `payment`

### Paso 3 — Activar Split de Pagos
- Habilitar solución "Split de Pagos (Marketplace)"
- Mercado Pago descuenta su comisión primero
- Luego se descuenta comisión del marketplace
- El porcentaje se envía por transacción (parámetro `marketplace_fee` o `application_fee`)

### Paso 4 — Crear Credenciales de Prueba (Sandbox)
- Crear cuenta de prueba Vendedor
- Crear cuenta de prueba Comprador
- Generar tarjetas de prueba desde documentación

### Paso 5 — Onboarding de Vendedor (Recurrente)
- Vendedor accede a su panel
- Hace clic en "Conectar mi cuenta de Mercado Pago"
- Se redirige a Mercado Pago para autorizar
- ComunaClic guarda credenciales del vendedor cifradas
- Vendedor puede recibir pagos con split automático

> El vendedor debe iniciar sesión con su cuenta principal de Mercado Pago.

### Paso 6 — Paso a Producción
- Validar integración completa en Sandbox
- Activar modo productivo
- Reemplazar credenciales de prueba
- Verificar reparto correcto en el primer pago real

---

## PARTE B — RESUMEN EJECUTIVO TÉCNICO

### B.0 — Contexto del Modelo

ComunaClic es un marketplace donde:
- El **comprador paga el total**
- El **vendedor recibe su monto** en su cuenta de Mercado Pago
- **ComunaClic recibe comisión** (% configurable)
- **Mercado Pago hace el reparto automático** vía parámetro de comisión

> Cada cobro se hace usando el Access Token del vendedor (obtenido por OAuth) e incluye un campo de comisión del marketplace.

### B.1 — Endpoints de Mercado Pago a Usar

| Función | Método | Endpoint |
|---|---|---|
| Autorización OAuth | GET | `https://auth.mercadopago.cl/authorization` |
| Intercambio code → tokens | POST | `https://api.mercadopago.com/oauth/token` |
| Renovar token | POST | `https://api.mercadopago.com/oauth/token` |
| Crear preferencia | POST | `https://api.mercadopago.com/checkout/preferences` |
| Crear pago directo | POST | `https://api.mercadopago.com/v1/payments` |
| Consultar pago | GET | `https://api.mercadopago.com/v1/payments/{id}` |

### B.2 — Datos de Configuración

```json
{
  "MercadoPago": {
    "ClientId": "<CLIENT_ID de ComunaClic>",
    "ClientSecret": "<CLIENT_SECRET de ComunaClic>",
    "PlatformAccessToken": "<ACCESS_TOKEN propio de ComunaClic>",
    "RedirectUri": "https://app.comunaclic.cl/oauth/mercadopago/callback",
    "WebhookUrl": "https://app.comunaclic.cl/api/webhooks/mercadopago",
    "MarketplaceFeePercent": 10.0,
    "ApiBaseUrl": "https://api.mercadopago.com",
    "AuthBaseUrl": "https://auth.mercadopago.cl"
  }
}
```

### B.3 — Modelo de Datos

**Tabla: `SellerMercadoPagoAccount`**

| Campo | Tipo | Notas |
|---|---|---|
| Id | PK | |
| SellerId | FK | |
| MpUserId | string | |
| AccessToken | string | **Cifrado** con Data Protection API |
| RefreshToken | string | **Cifrado** con Data Protection API |
| PublicKey | string | |
| ExpiresAtUtc | DateTime | |
| LiveMode | bool | |
| CreatedAtUtc | DateTime | |
| UpdatedAtUtc | DateTime | |

### B.4 — Flujo OAuth (Vinculación del Vendedor)

**Paso 1 — Generar URL de Autorización:**
```
GET {AuthBaseUrl}/authorization
    ?client_id={ClientId}
    &response_type=code
    &platform_id=mp
    &redirect_uri={RedirectUri}
    &state={tokenAleatorioAntiCSRF}
```

**Paso 2 — Callback:**
- Mercado Pago redirige con `code` y `state`
- Validar que `state` coincide (protección CSRF)
- El `code` es válido solo **10 minutos**

**Paso 3 — Intercambiar code por tokens:**
```
POST https://api.mercadopago.com/oauth/token
Content-Type: application/x-www-form-urlencoded

client_id={ClientId}
client_secret={ClientSecret}
grant_type=authorization_code
code={AUTHORIZATION_CODE}
redirect_uri={RedirectUri}
```

**Respuesta:**
```json
{
  "access_token": "APP_USR-...",
  "token_type": "bearer",
  "expires_in": 15552000,
  "scope": "read write offline_access",
  "user_id": 241983636,
  "refresh_token": "TG-...",
  "public_key": "APP_USR-...",
  "live_mode": true
}
```

**Paso 4 — Refresh de Token:**
```
POST https://api.mercadopago.com/oauth/token
Content-Type: application/json

{
  "client_id": "{ClientId}",
  "client_secret": "{ClientSecret}",
  "grant_type": "refresh_token",
  "refresh_token": "{refresh_token_guardado}"
}
```

### B.5 — Crear el Cobro con Split

**Opción Recomendada: Checkout Pro**
```
POST https://api.mercadopago.com/checkout/preferences
Authorization: Bearer {ACCESS_TOKEN_DEL_VENDEDOR}
Content-Type: application/json

{
  "items": [
    {
      "id": "item-1234",
      "title": "Nombre del producto",
      "currency_id": "CLP",
      "quantity": 1,
      "unit_price": 10000
    }
  ],
  "marketplace_fee": 1000,
  "back_urls": {
    "success": "https://app.comunaclic.cl/pago/exito",
    "failure": "https://app.comunaclic.cl/pago/error",
    "pending": "https://app.comunaclic.cl/pago/pendiente"
  },
  "auto_return": "approved",
  "notification_url": "https://app.comunaclic.cl/api/webhooks/mercadopago"
}
```

**Alternativa: Checkout API (pago directo)**
```
POST https://api.mercadopago.com/v1/payments
Authorization: Bearer {ACCESS_TOKEN_DEL_VENDEDOR}
Content-Type: application/json

{
  "transaction_amount": 10000,
  "token": "{card_token}",
  "description": "Compra en ComunaClic",
  "installments": 1,
  "payment_method_id": "master",
  "payer": { "email": "comprador@correo.cl" },
  "application_fee": 1000
}
```

### B.6 — Webhook de Notificaciones

**Endpoint:** `POST /api/webhooks/mercadopago`

El handler debe:
1. Responder HTTP 200 inmediatamente
2. Consultar pago real: `GET /v1/payments/{id}`
3. Actualizar estado de orden según respuesta
4. Garantizar idempotencia (verificar si ya fue procesado)
5. Validar firma del webhook (header `x-signature`)

### B.7 — Componentes a Implementar

1. `MercadoPagoOptions` — clase de configuración tipada
2. `IMercadoPagoOAuthService` — autorización, intercambio code, refresh
3. `IMercadoPagoPaymentService` — crear preferencia/pago, consultar, calcular fee
4. `SellerMercadoPagoAccount` + repositorio EF Core
5. `OAuthController` — `GET /oauth/mercadopago/start` y callback
6. `CheckoutController` — crear preferencia/pago
7. `WebhooksController` — `POST /api/webhooks/mercadopago`
8. `TokenRefreshHostedService` (opcional) — refresh automático en background
9. Cifrado con `IDataProtector` (ASP.NET Core Data Protection API)
10. `IHttpClientFactory` tipado para llamadas a MP API

### B.8 — Reglas de Negocio

- **Split solo con dinero en cuenta MP:** la solución opera entre cuentas de Mercado Pago
- **Reembolsos:** descuento proporcional de vendedor y ComunaClic
- **Comisión:** primero descuenta MP, luego `marketplace_fee`/`application_fee`
- **Moneda:** siempre CLP, sin decimales (enteros)
- **Vencimiento:** access token ~180 días; code solo 10 minutos
- **Sandbox primero:** validar todo antes de producción

### B.9 — Orden de Implementación Sugerido

1. Configuración (`MercadoPagoOptions`) + secretos en `appsettings`
2. Entidad + migración EF Core
3. Servicio y controlador OAuth
4. Servicio y controlador Checkout
5. Webhook + actualización de estado
6. Refresh automático de tokens
7. Manejo de reembolsos
8. Paso a producción

### B.10 — Criterios de Aceptación

- Vendedor puede conectar cuenta de Mercado Pago desde su panel
- Credenciales se guardan cifradas en base de datos
- Compra genera pago con split: vendedor recibe su monto, ComunaClic su comisión
- Webhook actualiza estado de orden y es idempotente
- Tokens se refrescan automáticamente antes de vencer
- Flujo completo funciona en Sandbox antes de activar producción
