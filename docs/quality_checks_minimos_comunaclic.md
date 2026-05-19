# Quality Checks Minimos - ComunaClic

## Objetivo

Agregar una base de verificaciones automáticas mínimas para rutas críticas sin depender de `xunit` ni paquetes de testing externos, de modo que puedan correr incluso en entornos donde el restore o build completo están inestables.

## Proyecto

Archivo principal:

`src/ComunaClick.QualityChecks/Program.cs`

Proyecto:

`src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj`

## Qué cubre

La suite corre checks automáticos para:

- login
  - contrato inválido de `POST /v1/auth/login`
  - login real opcional con usuario QA
- register
  - contrato inválido de `POST /v1/auth/register`
  - registro real opcional con correo único si se habilita explícitamente
- categorías
  - `GET /v1/public/catalog/categories`
  - `GET /v1/public/catalog/discovery/{categoryCode}`
- tracking
  - protección de `GET /v1/public/orders/{id}` por `customerId`
  - protección de `GET /v1/public/bookings/{id}` por `customerId`
- ownership
  - `GET /v1/partners/mine` con token QA
  - acceso permitido a orders/bookings del partner propio si se entrega `QUALITY_PARTNER_ID`
  - acceso denegado a orders/bookings de partner ajeno si se entrega `QUALITY_FOREIGN_PARTNER_ID`
- módulo partner autenticado
  - `GET /v1/partners/{partnerId}`
  - `GET /v1/partners/{partnerId}/activation`
  - `GET /v1/partners/{partnerId}/products`
  - `GET /v1/partners/{partnerId}/services`
  - `GET /v1/partners/{partnerId}/professionals`
  - `GET /v1/partners/{partnerId}/leads`
  - `GET /v1/partners/{partnerId}/notifications`
  - `GET /v1/partners/{partnerId}/payouts`
  - `GET /api/sellers/{partnerId}/mercadopago/status`
  - `GET /api/sellers/{partnerId}/payments`
  - validación negativa opcional contra `QUALITY_FOREIGN_PARTNER_ID`
- buyer booking e2e (opcional con service real)
  - `GET /v1/public/services/{serviceId}` para contexto de tenant/partner
  - `POST /v1/buyer/customer/ensure` para asegurar perfil buyer
  - `POST /v1/bookings` para crear reserva real
  - `GET /v1/buyer/bookings/recent` para validar visibilidad en menú Estado
  - `POST /v1/orders` para crear order real desde producto público del partner
  - `GET /v1/public/orders/{id}?customerId=...` para tracking positivo de order
  - `GET /v1/public/bookings/{id}?customerId=...` para tracking público positivo
- lead workflow (opcional con ID real)
  - `PATCH /v1/leads/{id}/workflow` para mover a seguimiento
  - validación negativa: `lost` sin `outcomeReason` devuelve `400`
  - cierre perdido con motivo válido devuelve `200`
- publicación/base pública
  - home, login y register business del `app`
  - geo público
  - discovery público
  - validación opcional de un partner esperado visible en discovery

## Ejecución

Desde la raíz del repo:

```bash
dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj
```

Atajo RC autenticado (recomendado):

```bash
QUALITY_QA_EMAIL=... \
QUALITY_QA_PASSWORD=... \
QUALITY_BOOKING_FLOW_SERVICE_ID=... \
bash tmp_deploy_scripts/run_quality_checks_rc.sh
```

## Variables de entorno

Bases:

- `QUALITY_APP_BASE_URL`
- `QUALITY_ACL_BASE_URL`
- `QUALITY_API_BASE_URL`

Auth opcional:

- `QUALITY_QA_EMAIL`
- `QUALITY_QA_PASSWORD`
- `QUALITY_QA_RECAPTCHA_TOKEN`
- `QUALITY_STRICT_AUTH_CHECKS=true`

Ownership opcional:

- `QUALITY_PARTNER_ID`
- `QUALITY_FOREIGN_PARTNER_ID`
- `QUALITY_BOOKING_FLOW_SERVICE_ID` (si falta, usa `QUALITY_FAVORITE_SERVICE_ID`)

Partner module recomendado:

- `QUALITY_PARTNER_ID`
- `QUALITY_FOREIGN_PARTNER_ID`

Lead workflow opcional:

- `QUALITY_LEAD_ID`
- `QUALITY_LEAD_OWNER` (opcional)
- `QUALITY_LEAD_OUTCOME_REASON` (opcional)

Register real opcional:

- `QUALITY_ALLOW_REGISTER=true`

Publicación opcional:

- `QUALITY_EXPECTED_DISCOVERY_PARTNER_NAME`

## Ejemplos

Solo checks públicos/contractuales:

```bash
dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj
```

Con login QA:

```bash
QUALITY_QA_EMAIL=owner@comunaclic.test \
QUALITY_QA_PASSWORD=test123 \
dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj
```

Con ownership y tracking positivo:

```bash
QUALITY_QA_EMAIL=owner@comunaclic.test \
QUALITY_QA_PASSWORD=test123 \
QUALITY_PARTNER_ID=... \
QUALITY_FOREIGN_PARTNER_ID=... \
QUALITY_BOOKING_FLOW_SERVICE_ID=... \
QUALITY_LEAD_ID=... \
dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj
```

## Notas

- La suite usa únicamente librerías base de .NET 8.
- No reemplaza smoke test post-deploy ni QA manual completa.
- Está pensada como red de seguridad mínima para Fase 5 cuando no hay infraestructura de testing tradicional disponible.
- Para el módulo partner, la recomendación es correrla siempre con `QUALITY_PARTNER_ID` y, si existe, `QUALITY_FOREIGN_PARTNER_ID` para cubrir permisos positivos y negativos.
