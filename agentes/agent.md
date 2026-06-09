# agent.md — Blueprint ComunaClic (.NET 8) + ACL + Payments (Transbank Webpay Plus + Oneclick)

> Objetivo: Este documento permite que un agente (p. ej. GPT en VS Code) cree, configure y entienda la solución completa.
> Incluye arquitectura, estructura de repositorio, comandos de scaffolding, contratos, módulos, persistencia, jobs e infraestructura local.

---

## 0) Resumen ejecutivo

La plataforma **ComunaClic** es un marketplace hiper-local con 3 verticales:
- **A**: Productos/delivery (orden, pago, despacho).
- **B**: Servicios con agenda (reserva, recordatorios, pago anticipado, no-show/cancelación).
- **C**: Directorio profesional (leads, contacto y trazabilidad).

Se implementa en **.NET 8** con:
- **ComunaClick.App**: UI (Blazor Web App) para Panel Socio + Admin Comunal + Admin Plataforma.
- **ComunaClick.Api**: API de negocio (catálogo, búsqueda, órdenes, reservas, leads, CRM, liquidación).
- **ComunaClick.Acl**: Servicio de autenticación/autorización (JWT, refresh, RBAC/Permisos, multi-tenant).
- **ComunaClick.Common**: Kernel común (contratos, errores, middleware, paginación, observabilidad).
- **Payments.Gateway.Api**: Servicio independiente de pagos con **Transbank** (Webpay Plus + Oneclick).

Infra local recomendada:
- **PostgreSQL + PostGIS** (geo queries)
- **Redis** (cache / rate limiting / idempotencia / state ephemeral)
- **Hangfire** (jobs: recordatorios, liquidaciones, reintentos)

---

## 1) Arquitectura y decisiones

### 1.1 Patrón general
- **Modular Monolith** para `ComunaClick.Api` (módulos internos con fronteras claras).
- Servicio separado para `ComunaClick.Acl` (auth/acl).
- Servicio separado para `Payments.Gateway.Api` (pasarela).

**Motivo:** independencia real del gateway de pagos y del stack de identidad, y un core de negocio escalable sin sobrecargar microservicios.

### 1.2 Multi-tenant (por Comuna)
- Cada request a `ComunaClick.Api` opera dentro de un **TenantId** (comuna).
- El `TenantId` se resuelve desde:
  - Claim JWT `tenant_id` (preferido) y/o
  - Header `X-Tenant-Id` (solo si la policy lo permite y se valida).
- En DB, todas las entidades “multi-tenant” tienen columna `tenant_id`.
- EF Core: aplicar filtros globales por `tenant_id`.

### 1.3 Autorización (ACL)
- JWT Access Token (corto) + Refresh Token (revocable).
- **Policy-based authorization** por permisos.
- Roles sugeridos:
  - `platform_admin`
  - `tenant_admin`
  - `partner_owner`
  - `partner_staff`
  - `customer`
  - `professional`

#### 1.3.1 ACL (autenticación + autorización) bien aterrizado
**Estrategia**
- JWT Access Token (corto).
- Refresh Token (persistido y revocable).
- Autorización policy-based por permisos (no solo roles).

**Roles típicos**
- `platform_admin`
- `tenant_admin` (admin comunal)
- `partner_owner`
- `partner_staff`
- `customer`
- `professional`

**Permisos (ejemplos)**
- `partner.read`, `partner.write`
- `catalog.products.write`
- `orders.manage`
- `bookings.manage`
- `payouts.view`
- `crm.view`

**Regla multi-tenant (clave)**
- Toda request debe resolver:
  - `TenantId` desde JWT (claim) y/o header `X-Tenant-Id` validado.
  - `PartnerId` cuando corresponda (panel socio).
- En la API: aplicar filtro global (EF Core global query filters) o repos con “TenantScope”.

### 1.4 Pagos (Transbank) — Independencia
`Payments.Gateway.Api`:
- Implementa Webpay Plus (redirect) y Oneclick (inscripción + cargos).
- Exposición pública solo para callbacks de Transbank.
- Notificación interna hacia `ComunaClick.Api` por webhook interno autenticado (API Key / mTLS).

`ComunaClick.Api`:
- **Nunca** habla directo con Transbank.
- Solo consume el Gateway (HTTP) y recibe notificaciones internas.

---

## 2) Estructura del repositorio

```
/src
  /ComunaClick.Common
    /Contracts
    /Errors
    /Middleware
    /Observability
    /Tenant
  /ComunaClick.Acl
    /Domain
    /Persistence
    /Endpoints
    /Services
  /ComunaClick.Api
    /Modules
      /Onboarding
      /Search
      /Catalog
      /Orders
      /Bookings
      /Leads
      /PaymentsOrchestration
      /Payouts
      /Crm
    /Persistence
    /Integrations
    /Jobs
  /ComunaClick.App
    /UI (Blazor Web App)
    /ApiClients
  /ComunaClick.Shared
    /Auth
    /Dtos
    /Validation
  /ComunaClick.SharedUI
    /Pages
    /Components
    /Services
  /ComunaClick.Mobile
    /Platforms (iOS/Android/MacCatalyst)
    /Resources
    /wwwroot
  /Payments.Common
    /Contracts
    /Abstractions
  /Payments.Gateway.Api
    /Providers/Transbank
    /Persistence
    /Endpoints
    /Security
/tests
  /ComunaClick.Tests.Unit
  /ComunaClick.Tests.Integration
/infra
  initdb (sql)
```

> Nota: `Payments.Common` puede quedar dentro del repo o ser repo separado si será usado por más soluciones.

---

## 2.1 ComunaClick.Api — propósito

API de negocio del marketplace ComunaClick. Orquesta toda la experiencia del usuario final y socios:

- Multi-tenant (por comuna/tenant).
- Expone funcionalidades de catálogo/agenda/profesionales.
- Maneja transacciones (orders/bookings/leads).
- Orquesta pagos (pero no integra directamente Transbank; eso va en Payments.Gateway.Api).
- Liquida/paga a socios (payouts).
- Mantiene CRM mínimo (clientes/interacciones).

Dependencias externas directas:
- ComunaClick.Acl para identidad/autorización (JWT).
- Payments.Gateway.Api para iniciar/confirmar pagos y recibir notificaciones internas.
- Proveedores mensajería (WhatsApp/SMS/Email) vía capa Integrations.

Estructura propuesta:
```
/ComunaClick.Api
  /Modules
    /Onboarding
    /Search
    /Catalog
    /Orders
    /Bookings
    /Leads
    /Payments
    /Payouts
    /Crm
  /Persistence
  /Integrations
```

---

## 2.3 Web + Mobile (Blazor + MAUI Hybrid)

### Objetivo
Reutilizar UI y lógica entre web y mobile con un solo stack C#.

### Proyectos
- **ComunaClick.SharedUI**: páginas Razor + componentes comunes + servicios UI (i18n, auth state).
- **ComunaClick.Shared**: DTOs, contratos, validaciones, auth helpers y stores.
- **ComunaClick.App**: host web (Blazor Web App) que consume SharedUI.
- **ComunaClick.Mobile**: host MAUI Blazor Hybrid que consume SharedUI.

### Routing compartido
- Las rutas viven en `ComunaClick.SharedUI/Pages` (ej.: `/`, `/login`, `/register`).
- Los hosts (web y mobile) agregan el assembly de SharedUI al router.

### I18n (ES/EN)
- `LocaleService` en SharedUI.
- Persistencia en `localStorage`/`app.js` (web) y disponible en mobile por WebView.
- Selector de idioma: `LanguageToggle` (reutilizable).

### Auth scaffolding
- `ComunaClick.Shared/Auth`: `AuthTokens`, `LoginRequest`, `RegisterRequest`, `ExternalLoginRequest`.
- `ITokenStore` con implementación `InMemoryTokenStore` (placeholder).
- `AuthStateService` en SharedUI para estado de sesión.

### Notas MAUI
- TargetFrameworks: `net8.0-android;net8.0-ios;net8.0-maccatalyst`.
- `ComunaClick.Mobile/wwwroot/index.html` incluye Tailwind CDN + fonts.
- Reemplazar `InMemoryTokenStore` por SecureStorage en mobile (fase auth real).

---

## 3) Quick start (Web + Mobile)

### Requisitos
- .NET SDK 8.x
- Xcode (para iOS) + Simulador instalado
- Workloads MAUI:
  - `android`
  - `wasm-tools-net8`

### Instalar workloads MAUI
```
sudo dotnet workload install maui android wasm-tools-net8
```

### Web (Blazor Web App)
```
dotnet build src/ComunaClick.App/ComunaClick.App.csproj
dotnet run --project src/ComunaClick.App
```

### Mobile (MAUI Blazor Hybrid)
#### Android (emulador o dispositivo)
```
dotnet build src/ComunaClick.Mobile/ComunaClick.Mobile.csproj -f net8.0-android
dotnet run --project src/ComunaClick.Mobile/ComunaClick.Mobile.csproj -f net8.0-android
```

#### iOS (simulador)
```
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -license accept
dotnet build src/ComunaClick.Mobile/ComunaClick.Mobile.csproj -t:Run -f net8.0-ios -p:_DeviceName="iPhone 15"
```

---

## 4) Conexión Web/Mobile con ComunaClick.Api + ComunaClick.Acl

### Configuración por entorno (Web)
Configurar en `appsettings.json` / `appsettings.Development.json`:
```
"Api": {
  "ApiBaseUrl": "http://localhost:5277",
  "AclBaseUrl": "http://localhost:5135",
  "DefaultPartnerId": ""
}
```

### Configuración (Mobile)
Registrar `ApiOptions` en `MauiProgram.cs` con las base URLs.

### Clientes HTTP (Shared)
- `ComunaClick.Shared.Auth.Acl.AclAuthClient`
- `ComunaClick.Shared.Api.Buyer.BuyerApiClient`
- `ComunaClick.Shared.Api.Partner.PartnerApiClient`

### Auth
- `AuthStateService` inicializa en `LanguageInitializer`.
- Tokens guardados en `ITokenStore`:
  - Web: `WebTokenStore` (localStorage)
  - Mobile: `SecureTokenStore` (MAUI SecureStorage)

### Endpoints sociales (ACL)
Agregar:
- `POST /v1/auth/google`
- `POST /v1/auth/apple`

Payload recomendado:
```
{
  "idToken": "JWT",
  "accessToken": "optional",
  "authorizationCode": "optional",
  "tenantId": "optional-guid",
  "partnerId": "optional-guid",
  "deviceId": "optional",
  "userAgent": "optional"
}
```

### Estado actual de social login (ACL)
- Endpoints creados, pero retornan `501 Not Implemented` (placeholder).
- Falta validar `idToken` con Google/Apple y emitir JWT real.

### Multi-tenant (decisión)
- Autenticado: tenant/partner desde JWT/AuthResponse (NO usar `X-Tenant-Id`).
- Anónimo: `X-Tenant-Id` permitido en endpoints públicos (ej. `/v1/search`, `/v1/partners`).

---

## 2.2 Módulos ComunaClick.Api (responsabilidades)

### /Modules/Onboarding
Responsabilidad

Flujo de alta y configuración inicial de:

- Tenants (comunas) (si lo administras desde esta API).
- Socios (Partners A/B/C) y su visibilidad (is_visible).
- Staff del socio (link a user_id del ACL).
- Suscripciones/planes (plan asignado al socio, estado, periodos).

Endpoints típicos

- POST /v1/tenants (admin plataforma) — crear comuna
- POST /v1/partners — registrar socio (A/B/C)
- POST /v1/partners/{id}/staff — vincular usuario ACL como staff
- POST /v1/subscriptions — asignar plan al socio
- PATCH /v1/partners/{id}/visibility — habilitar/deshabilitar publicación

Persistencia

Tablas: core.tenants, core.partners, core.partner_staff, core.subscription_plans, core.subscriptions.

Reglas clave

- is_visible = true solo si cumple onboarding (y si aplica, suscripción/pago al día).
- Todo filtrado por tenant_id desde JWT/headers.

### /Modules/Search
Responsabilidad

Motor de búsqueda unificado:

- Búsqueda por ubicación (PostGIS) y texto.
- Filtrado por categoría, tipo de socio (A/B/C), disponibilidad (B), verificación (C).
- Ranking simple (distancia + rating futuro + actividad).

Endpoints típicos

- GET /v1/search?query=&lat=&lng=&radius=&type=A|B|C&category=
- GET /v1/search/partners
- GET /v1/search/professionals

Persistencia

Consulta principalmente: core.partners, core.products, core.services, core.professionals, core.service_slots.

Reglas clave

- Solo devuelve entidades is_visible=true (partners) y is_active=true.
- Geofiltro con ST_DWithin.

### /Modules/Catalog
Responsabilidad

Gestión de catálogo:

- Productos (A) y stock.
- Servicios (B), duración, precio.
- Profesionales (C) como “oferta” (perfil y especialidad).

Endpoints típicos

Productos

- GET /v1/partners/{partnerId}/products
- POST /v1/products
- PATCH /v1/products/{id}
- PATCH /v1/products/{id}/inventory

Servicios

- GET /v1/partners/{partnerId}/services
- POST /v1/services
- PATCH /v1/services/{id}
- POST /v1/services/{id}/slots (si generas slots desde API)

Profesionales

- GET /v1/professionals
- POST /v1/professionals
- PATCH /v1/professionals/{id}

Persistencia

core.products, core.product_inventory, core.services, core.service_slots, core.professionals.

Reglas clave

- Solo socios del tenant pueden administrar su catálogo.
- Validar scopes: tenant_id + partner_id (si es staff).

### /Modules/Orders
Responsabilidad

Flujo de compra de productos (A):

- Carrito/checkout (MVP: crear orden + items).
- Estados: payment_pending → paid → preparing → dispatched → completed.
- Delivery info.
- Integración con Payments (crear intent/orquestación).

Endpoints típicos

- POST /v1/orders (crea orden + items) → status payment_pending
- GET /v1/orders/{id}
- GET /v1/partners/{partnerId}/orders (panel socio)
- PATCH /v1/orders/{id}/status (preparing/dispatched/completed)
- POST /v1/orders/{id}/cancel

Persistencia

core.orders, core.order_items, core.deliveries, core.payments (link por external_reference=order:{id}).

Reglas clave

- Recalcular totales server-side.
- No permitir cambios si pago aprobado y estado ya avanzó.

### /Modules/Bookings
Responsabilidad

Reservas de servicios (B):

- Reservar slot, confirmar disponibilidad.
- Estados: payment_pending → confirmed → attended|no_show|cancelled.
- Política de cancelación (json simple por ahora).
- Integración con Payments.

Endpoints típicos

- GET /v1/services/{serviceId}/availability?from=&to=
- POST /v1/bookings (slot_id o start/end directo) → payment_pending
- GET /v1/bookings/{id}
- GET /v1/partners/{partnerId}/bookings
- POST /v1/bookings/{id}/cancel

Persistencia

core.bookings, core.service_slots (bloqueo/ocupación), core.payments (external_reference=booking:{id}).

Reglas clave

- Bloqueo de concurrencia: si usas slots, marcar is_available=false al crear booking (ideal con transacción).
- Si pago expira/rechaza, liberar slot.

### /Modules/Leads
Responsabilidad

Leads para profesionales (C):

- Cliente crea lead con mensaje.
- Profesional gestiona estado del lead.
- Se alimenta CRM (interactions).

Endpoints típicos

- POST /v1/leads (cliente)
- GET /v1/professionals/{id}/leads
- PATCH /v1/leads/{id}/status (contacted/closed/discarded)

Persistencia

core.leads, core.customers, core.professionals, core.interactions.

Reglas clave

- Un lead no es pago (MVP). Si a futuro se cobra comisión, se conecta a Payments.

### /Modules/Payments (orquestación en Core)
Responsabilidad

NO habla con Transbank directo. Orquesta:

- Crear registro core.payments y pedir al gateway que cree el intent.
- Actualizar órdenes/reservas cuando el gateway notifica resultado.
- Idempotencia de eventos recibidos.

Endpoints típicos (internos)

- POST /v1/payments/intents (desde Orders/Bookings) → crea core.payments + llama Gateway
- POST /v1/payments/provider-notify (webhook interno desde Payments.Gateway.Api)

Headers: X-Internal-Key

Body: intent_id, external_reference, status, provider_event_id, raw

Persistencia

core.payments, core.payment_events + actualización de core.orders / core.bookings.

Reglas clave

- Idempotencia: core.payment_events unique por (tenant_id, provider_event_id).
- Cambios de estado:
  - approved → order/bookings a paid/confirmed
  - rejected/expired → order/bookings a failed/cancelled (según política)
- No aceptar notify sin X-Internal-Key.

### /Modules/Payouts
Responsabilidad

Liquidaciones a socios:

- Calcular gross, comisión, deducciones (suscripción) y net.
- Generar batch por periodo.
- Exportar reporte (CSV) (opcional).
- Marcar batch como pagado/failed.

Endpoints típicos

- POST /v1/payouts/batches (admin) {period_start, period_end}
- GET /v1/payouts/batches/{id}
- POST /v1/payouts/batches/{id}/close
- POST /v1/payouts/batches/{id}/mark-paid
- GET /v1/partners/{partnerId}/payouts

Persistencia

core.payout_batches, core.payout_items y lectura de core.orders, core.bookings, core.payments, core.subscriptions.

Reglas clave

- Solo incluir transacciones paid/confirmed dentro del periodo.
- Comisión configurable (por plan o tenant config).

### /Modules/Crm
Responsabilidad

Vista y trazabilidad de clientes:

- Perfil de customer.
- Timeline de interacciones (orders/bookings/leads/mensajes).
- Enlaces customer ↔ partner.

Endpoints típicos

- GET /v1/customers/{id}
- GET /v1/customers/{id}/timeline
- GET /v1/partners/{partnerId}/customers

Persistencia

core.customers, core.customer_partner_links, core.interactions.

Reglas clave

- Cada order/booking/lead debe “emitir” una interaction.

---

## 2.3 Persistence (EF Core + Postgres)

Qué incluye

- CoreDbContext (solo core_db).
- Configuración por schema: modelBuilder.HasDefaultSchema("core").
- Migraciones (si decides EF Migrations) o ejecutar scripts SQL (ya tenemos scripts).

Interceptores

- TenantSaveChangesInterceptor para setear tenant_id.
- Soft-tenant filter global query filter (si aplica).

Concurrency

- Para slots/stock: usar transacciones + SELECT ... FOR UPDATE (ideal) o rowversion equivalente (Postgres: xmin o columna).

Convención multi-tenant

- tenant_id se obtiene del JWT (claim tenant_id) y se inyecta en un TenantContext.
- Repositorios/queries siempre filtran por tenant.

---

## 2.4 Integrations

### Integrations/PaymentGateway
Responsable de hablar con Payments.Gateway.Api:

- CreateIntentAsync(external_reference, amount, return_url)
- GetIntentStatusAsync(intent_id)
- NotifyInternalAsync (si se requiere desde core hacia gateway, normalmente no)

Nunca se integra Transbank aquí.

### Integrations/WhatsAppSms
Proveedor abstracto:

- SendOrderConfirmation(customer, order)
- SendBookingConfirmation(customer, booking)
- SendLeadNotification(professional, lead)

Implementaciones:

- DummyProvider (dev)
- (futuro) Twilio / WhatsApp Business / AWS SNS

---

## 2.5 Notas de seguridad/ACL para todos los módulos

- JWT emitido por ComunaClick.Acl.
- Policies:
  - tenant.admin
  - partner.owner
  - partner.staff
  - platform.admin
- Todos los endpoints deben validar:
  - tenant_id del token
  - si aplica partner_id (scope)

## 2.6 Estado actual del repositorio (código existente)

Este repo ya tiene endpoints y persistencia básica en **ComunaClick.Api** y CRUD base en **ComunaClick.Acl**. Lo siguiente refleja lo implementado hoy:

### 2.6.1 ComunaClick.Api (implementado)

**Autenticación/JWT**
- `AddAuthentication().AddJwtBearer()` configurado en `Program.cs` con `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`.
- Policies existentes: `platform.admin`, `tenant.admin`, `partner.owner`, `partner.staff`.
- Resolución de roles basada en claims `role` y/o `roles`.

**Multi-tenant**
- `TenantResolutionMiddleware` usa claims `tenant_id` / `partner_id` y fallback a headers `X-Tenant-Id` / `X-Partner-Id`.
- Headers configurables vía `Tenant:HeaderName` y `Tenant:PartnerHeaderName` (opcionales).
- `CoreDbContext` aplica `HasQueryFilter` por `tenant_id` en la mayoría de entidades.

**Endpoints actualmente disponibles**

Onboarding
- `GET /v1/tenants`, `GET /v1/tenants/{id}`, `POST /v1/tenants`, `PATCH /v1/tenants/{id}`
- `GET /v1/partners`, `GET /v1/partners/{id}`, `POST /v1/partners`, `PATCH /v1/partners/{id}`
- `PATCH /v1/partners/{id}/visibility`, `POST /v1/partners/{id}/staff`

Catalog
- Productos: `GET /v1/products/{id}`, `GET /v1/partners/{partnerId}/products`, `POST /v1/products`, `PATCH /v1/products/{id}`, `PATCH /v1/products/{id}/inventory`
- Servicios: `GET /v1/services/{id}`, `GET /v1/partners/{partnerId}/services`, `POST /v1/services`, `PATCH /v1/services/{id}`
- Slots: `GET /v1/services/{serviceId}/slots`, `POST /v1/services/{serviceId}/slots`, `PATCH /v1/service-slots/{id}`
- Profesionales: `GET /v1/professionals`, `GET /v1/professionals/{id}`, `POST /v1/professionals`, `PATCH /v1/professionals/{id}`

Orders
- `GET /v1/orders/{id}`, `GET /v1/partners/{partnerId}/orders`, `POST /v1/orders`, `PATCH /v1/orders/{id}/status`, `POST /v1/orders/{id}/cancel`

Bookings
- `GET /v1/bookings/{id}`, `GET /v1/partners/{partnerId}/bookings`, `POST /v1/bookings`, `PATCH /v1/bookings/{id}/status`, `POST /v1/bookings/{id}/cancel`

Leads
- `GET /v1/leads/{id}`, `GET /v1/professionals/{professionalId}/leads`, `POST /v1/leads` (AllowAnonymous), `PATCH /v1/leads/{id}/status`

Notifications
- `GET /v1/partners/{partnerId}/notifications` (usa `core.interactions` filtrado por partner)

Payments (core)
- `GET /v1/payments/{id}`, `POST /v1/payments`
- `POST /v1/payments/provider-notify` (AllowAnonymous + `X-Internal-Key`)
- Config extra usada pero no documentada: `Payments:InternalWebhookKey`

Payouts
- `POST /v1/payouts/batches`, `GET /v1/payouts/batches/{id}`, `POST /v1/payouts/batches/{id}/close`, `POST /v1/payouts/batches/{id}/mark-paid`
- `GET /v1/partners/{partnerId}/payouts`

CRM
- `GET /v1/customers/{id}`, `POST /v1/customers`, `PATCH /v1/customers/{id}`
- `GET /v1/customers/{id}/timeline`, `GET /v1/partners/{partnerId}/customers`

**Notas**
- `Search` ya cuenta con endpoints básicos (texto + filtros simples).
- Existe un `JobsHostedService` con toggle por `Jobs:Enabled` (skeleton).

### 2.6.2 ComunaClick.Acl (implementado)

Auth y CRUD base:
- Auth: `POST /v1/auth/login`, `POST /v1/auth/refresh`
- Password reset: `POST /v1/auth/password-reset`, `POST /v1/auth/password-reset/confirm`
- Users: `GET /v1/users`, `GET /v1/users/{id}`, `POST /v1/users`, `PUT /v1/users/{id}`, `DELETE /v1/users/{id}`
- Roles: `GET /v1/roles`, `GET /v1/roles/{id}`, `POST /v1/roles`, `PUT /v1/roles/{id}`, `DELETE /v1/roles/{id}`
- Permissions: `GET /v1/permissions`, `GET /v1/permissions/{id}`, `POST /v1/permissions`, `PUT /v1/permissions/{id}`, `DELETE /v1/permissions/{id}`
- Asignaciones: `/v1/users/{id}/roles`, `/v1/roles/{id}/permissions`, `/v1/users/{id}/scopes`

**Notas**
- Password hashing PBKDF2 en create/update/reset (no se recibe `PasswordHash` plano).
- `PasswordReset:ReturnToken` controla si el endpoint devuelve el token (true solo para pruebas).

### 2.6.3 Payments.Gateway.Api (estado actual)

- API con endpoints mínimos:
  - `POST /v1/payment-intents`
  - `GET /v1/payment-intents/{id}`
  - `POST /v1/payment-intents/{id}/status` (simula callback + notifica a Core)
  - `POST /v1/oneclick/enrollments`
  - `POST /v1/oneclick/charges`
- Persistencia EF Core alineada con `04_payments.sql`.

### 2.6.4 ComunaClick.App (estado actual)

- Blazor Web App con UI real (Home público, Buyer y Partner).
- Enrutamiento compartido desde `ComunaClick.SharedUI`.
- Internacionalización ES/EN con `LocaleService` y persistencia en localStorage/SecureStorage.
- Logo largo `logo-largo.png` usado en navs/footers.

Páginas públicas agregadas:
- `/discover`, `/services-near`, `/top-rated`, `/new-businesses`
- `/for-businesses`, `/add-business`, `/advertising`, `/business-tools`
- `/support` (formulario real), `/help` (plantilla centro de ayuda), `/privacy`, `/terms`

Soporte (Core API):
- `POST /v1/support/tickets`
- Guarda `Interaction` con `Type = "support_ticket"` y payload `{name,email,topic,message}`.
- Requiere `TenantId` (JWT o `X-Tenant-Id`).

### 2.6.5 Common libs (estado real)

- `ComunaClick.Common` tiene un set mínimo útil:
  - `Errors/Error`, `Results/Result` y `Result<T>`
  - `Types/Money`, `Types/Pagination`
  - `Auth/AuthConstants` (claims)
- `Payments.Common` tiene un set mínimo útil:
  - `Models/PaymentStatus`, `PaymentProvider`, `PaymentEvent`
  - `Interfaces/IPaymentProvider`

Uso actual:
- `AuthConstants` se usa en ACL (JWT) y API (TenantResolution + policies).
- `Money` se usa en `OrdersController` y `PaymentsController` (normalización currency).

## 3) Scaffolding (comandos) — .NET 8

### 3.1 Crear solución
```bash
mkdir comunaclick && cd comunaclick
dotnet new sln -n ComunaClick
mkdir src tests infra
```

### 3.2 Proyectos base
```bash
# Common
dotnet new classlib -n ComunaClick.Common -o src/ComunaClick.Common

# ACL
dotnet new webapi -n ComunaClick.Acl -o src/ComunaClick.Acl

# Core API
dotnet new webapi -n ComunaClick.Api -o src/ComunaClick.Api

# App (Blazor Web App)
dotnet new blazor -n ComunaClick.App -o src/ComunaClick.App

# Payments contracts
dotnet new classlib -n Payments.Common -o src/Payments.Common

# Payments gateway API
dotnet new webapi -n Payments.Gateway.Api -o src/Payments.Gateway.Api
```

### 3.3 Agregar a la solución
```bash
dotnet sln ComunaClick.sln add \
  src/ComunaClick.Common/ComunaClick.Common.csproj \
  src/ComunaClick.Acl/ComunaClick.Acl.csproj \
  src/ComunaClick.Api/ComunaClick.Api.csproj \
  src/ComunaClick.App/ComunaClick.App.csproj \
  src/Payments.Common/Payments.Common.csproj \
  src/Payments.Gateway.Api/Payments.Gateway.Api.csproj
```

### 3.4 Referencias entre proyectos
```bash
# Common referenciado por todos
dotnet add src/ComunaClick.Acl reference src/ComunaClick.Common/ComunaClick.Common.csproj
dotnet add src/ComunaClick.Api reference src/ComunaClick.Common/ComunaClick.Common.csproj
dotnet add src/ComunaClick.App reference src/ComunaClick.Common/ComunaClick.Common.csproj

# Payments contracts
dotnet add src/Payments.Gateway.Api reference src/Payments.Common/Payments.Common.csproj

# ComunaClic consume contracts si quieres tipado fuerte
dotnet add src/ComunaClick.Api reference src/Payments.Common/Payments.Common.csproj
```

---

## 4) Paquetes NuGet recomendados

### 4.1 ComunaClick.Api / ComunaClick.Acl / Payments.Gateway.Api
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Design`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` (PostGIS)
- `FluentValidation.AspNetCore` (o `FluentValidation`)
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `Swashbuckle.AspNetCore`
- `Hangfire.AspNetCore`
- `Hangfire.PostgreSql`

### 4.2 Payments.Gateway.Api (Transbank)
- SDK oficial (si se usa): `Transbank.Webpay` / `transbank-sdk-dotnet` (según paquete disponible en NuGet para tu versión)
- Alternativa: HTTP directo (HttpClient) con headers `Tbk-Api-Key-Id` y `Tbk-Api-Key-Secret`.

---

## 5) Infra y ejecución sin Docker (AWS RDS)

### 5.1 Dependencias
- **AWS RDS PostgreSQL** (única dependencia obligatoria por ahora)
- (Opcional a futuro) Redis para cache/idempotencia/rate-limit
- (Opcional) Hangfire con storage en PostgreSQL

> En esta etapa no se usa Docker. Todo corre local con `dotnet run` y la base está en AWS.

---

## 6) Configuración por ambiente (appsettings + variables)

### 6.1 ComunaClick.Acl (ejemplo)
Variables:
- `ConnectionStrings__AclDb`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey` (secreto)
- `Jwt__AccessTokenMinutes`
- `Jwt__RefreshTokenDays`
- `PasswordReset__TokenMinutes`
- `PasswordReset__TokenPepper` (secreto)
- `PasswordReset__ReturnToken` (true solo para pruebas)

### 6.2 ComunaClick.Api
Variables:
- `ConnectionStrings__CoreDb`
- `Tenant__HeaderName` (por defecto `X-Tenant-Id`)
- `Acl__BaseUrl` (URL ACL)
- `Acl__IntrospectionKey` (si se usa)
- `Payments__GatewayBaseUrl`
- `Payments__InternalWebhookKey` (para validar webhooks internos desde Payments)

### 6.3 Payments.Gateway.Api
Variables:
- `ConnectionStrings__PaymentsDb`
- `Transbank__CommerceCode`
- `Transbank__ApiKeyId`
- `Transbank__ApiKeySecret`
- `Transbank__Environment` = `Integration|Production`
- `ComunaClic__InternalWebhookUrl`
- `ComunaClic__InternalWebhookKey`

---

## 7) Modelos de datos — mínimo viable

### 7.1 Multi-tenant y ACL (AclDb)
**users**
- id (uuid), email, password_hash, is_active, created_at

**roles**
- id, name

**permissions**
- id, code

**user_roles**
- user_id, role_id

**role_permissions**
- role_id, permission_id

**user_tenant_scope**
- user_id, tenant_id, partner_id (nullable), scope_type

**refresh_tokens**
- id, user_id, token_hash, expires_at, revoked_at

### 7.2 Core (CoreDb)
**tenants**
- id, name, timezone, config_json

**partners**
- id, tenant_id, type (A|B|C), name, rut, address, geo_point (geography), is_visible

**subscriptions**
- id, partner_id, plan_id, status, current_period_end

**products / services / professionals**
- básicos según vertical

**orders / bookings / leads**
- `orders`: id, tenant_id, partner_id, customer_id, total_amount, status
- `bookings`: id, tenant_id, partner_id, customer_id, start_at, status
- `leads`: id, tenant_id, professional_id, customer_id, status

**payments (orquestación en ComunaClic)**
- id, tenant_id, external_reference, amount, status, provider, created_at
- gateway_intent_id (nullable), last_update_at

**payout_batches / payout_items**
- para liquidación

**customers, interactions, customer_partner_links**
- CRM mínimo

### 7.3 Payments (PaymentsDb)
**payment_intents**
- id (uuid), external_reference, amount, currency, status
- provider, provider_token, authorization_code
- raw_response_json, created_at, updated_at

**provider_events** (idempotencia)
- id, provider_event_id (unique), intent_id, event_type, received_at

**customer_tokens** (Oneclick)
- id, customer_id, provider_ref (tbk_user/username u otro), status, created_at

**charges**
- id, customer_token_id, intent_id, amount, status, provider_ref, created_at

---

## 8) Contratos (DTOs) — Payments.Common

### 8.1 Estados estándar
- `Pending`
- `Approved`
- `Rejected`
- `Cancelled`
- `Expired`

### 8.2 DTOs sugeridos

**CreatePaymentIntentRequest**
- `externalReference` (string) — OrderId/BookingId/etc.
- `amount` (decimal)
- `currency` (string, default `CLP`)
- `customerId` (string|null)
- `returnUrl` (string) — URL donde vuelve el usuario
- `metadata` (dict/string-json) — opcional

**CreatePaymentIntentResponse**
- `intentId` (Guid)
- `provider` (`transbank`)
- `redirectUrl` (string|null)
- `providerToken` (string|null)
- `status` (enum)

**PaymentNotification**
- `providerEventId` (string)
- `intentId` (Guid)
- `externalReference` (string)
- `status` (enum)
- `amount` (decimal)
- `occurredAt` (DateTimeOffset)
- `raw` (json/string)

**StartEnrollmentRequest** (Oneclick)
- `customerId` (string)
- `returnUrl` (string)
- `email` (string|null)
- `metadata` (json|null)

**StartEnrollmentResponse**
- `enrollmentId` (Guid)
- `enrollmentUrl` (string)
- `status` (Pending)

**FinishEnrollmentResponse**
- `customerTokenId` (Guid)
- `status` (Approved|Rejected)

**ChargeWithTokenRequest**
- `customerTokenId` (Guid)
- `externalReference` (string)
- `amount` (decimal)
- `currency` (string)

**ChargeResponse**
- `chargeId` (Guid)
- `intentId` (Guid)
- `status` (Approved|Rejected|Pending)
- `authorizationCode` (string|null)

---

## 9) Endpoints — Payments.Gateway.Api (Transbank)

### 9.1 Webpay Plus
- `POST /v1/webpayplus/intents`
  - input: `CreatePaymentIntentRequest`
  - output: `CreatePaymentIntentResponse` (con `redirectUrl` y `providerToken`)

- `POST /v1/webpayplus/callback` (PÚBLICO — Transbank retorna aquí o se usa en returnUrl)
  - recibe token (p. ej. `token_ws`)
  - hace **commit/confirmación** con Transbank
  - actualiza `payment_intents`
  - dispara webhook interno a ComunaClic

- `GET /v1/intents/{intentId}`
  - estado del intent

### 9.2 Oneclick
- `POST /v1/oneclick/inscriptions/start`
  - inicia inscripción (enrolamiento)
  - retorna URL para enrolar

- `POST /v1/oneclick/inscriptions/finish`
  - finaliza inscripción con datos retornados por Transbank
  - guarda `customer_tokens`

- `POST /v1/oneclick/charges`
  - realiza cargo con token enrolado

- `DELETE /v1/oneclick/tokens/{customerTokenId}`
  - revoca/elimina inscripción

### 9.3 Webhook interno hacia ComunaClic
- `POST {ComunaClic__InternalWebhookUrl}` con header `X-Internal-Key: {ComunaClic__InternalWebhookKey}`
- body: `PaymentNotification`

---

## 10) Endpoints — ComunaClick.Api (orquestación pagos)

### 10.1 Crear intent de pago (Order/Booking)
- `POST /v1/payments/intent`
  - input: `type = order|booking`, `referenceId`, `amount`, `returnUrl`
  - crea Payment interno (Pending)
  - llama a `Payments.Gateway.Api` (webpayplus/intents o oneclick/charges según caso)
  - devuelve `redirectUrl` si corresponde

### 10.2 Webhook interno desde Payments
- `POST /v1/payments/provider-notify` (INTERNAL)
  - valida `X-Internal-Key`
  - aplica idempotencia (providerEventId)
  - actualiza Payment interno
  - transiciona Order/Booking:
    - Approved => Confirmar orden/reserva
    - Rejected/Cancelled => marcar fallida

---

## 11) Search geo (PostGIS) — Módulo Search

### 11.1 Campos
- `partners.geo_point` tipo `geography(Point,4326)`

### 11.2 Query por radio
Usar ST_DWithin / distancia:
- “Socios dentro de X km de (lat,lng)”

Index:
- GIST sobre `geo_point`.

---

## 12) Jobs (Hangfire) — ComunaClick.Api

### 12.1 Recordatorios (B)
- Job recurrente que:
  - busca bookings próximas (T-24h, T-2h)
  - envía notificación (WhatsApp/SMS/email)
  - registra interacción/resultado

### 12.2 Liquidación
- Job diario/semanal:
  - agrupa transacciones aprobadas por socio
  - calcula comisión
  - descuenta suscripción si aplica
  - genera `payout_batch` y `payout_items`

### 12.3 Reintentos / outbox
- Job para reintentar envíos fallidos (webhooks internos, notificaciones).
- Opcional: Outbox table + publisher.

---

## 13) Observabilidad y errores (Common)

### 13.1 Correlation ID
- Middleware que:
  - lee `X-Correlation-Id` o genera uno
  - lo agrega a logs y responses

### 13.2 Error model estándar
Estructura recomendada:
```json
{
  "traceId": "…",
  "code": "validation_error|unauthorized|not_found|conflict|internal_error",
  "message": "…",
  "details": [ ... ]
}
```

### 13.3 Logging
- Serilog + consola en dev
- OpenTelemetry (traces) opcional

---

## 14) Seguridad

### 14.1 ACL JWT
Claims recomendados:
- `sub` (userId)
- `tenant_id`
- `role` o `roles`
- `permissions` (si se emiten en token) o introspección contra ACL

### 14.2 Comunicación interna
Entre servicios:
- API Key (`X-Internal-Key`) o mTLS.
- Permitir IPs/ingress control en producción.

### 14.3 Idempotencia
- En Payments: `provider_event_id` UNIQUE.
- En ComunaClic: `provider_event_id` UNIQUE por recepción.

---

## 15) Convenciones de código (para el agente)

- Preferir **Minimal APIs** o Controllers, pero consistente en todos los proyectos.
- Validación: FluentValidation.
- DTOs en `*.Common`/`Payments.Common`, entidades en Domain.
- No mezclar modelos EF con DTOs de API.
- Repositorios o DbContext directo con “service layer” por módulo.
- Mapear estados de provider -> estados internos (enum común).

---

## 16) Tareas mínimas de implementación (orden sugerido)

1) Infra local (Postgres+PostGIS, Redis).
2) ACL:
   - Users, Roles, Permissions, JWT/Refresh.
3) Common:
   - Middleware de errores, correlation id, Result type.
4) Core API:
   - Tenants, Partners, Search geo.
5) Payments.Gateway.Api:
   - Webpay Plus: intent + callback/commit + notify interno.
   - Oneclick: start/finish inscription + charge + delete.
6) Core API:
   - Orquestación: PaymentIntent interno + webhook internal.
7) App:
   - Login, panel socio básico, búsqueda/landing.

---
## 17) Ejecución local sin Docker

### 17.1 Requisitos
- .NET SDK 8 instalado
- Acceso de red a RDS PostgreSQL (Security Group permite tu IP o VPN)
- Certificados/SSL: en RDS normalmente se usa `SSL Mode=Require`

### 17.2 Puertos sugeridos (sin Docker)
- ComunaClick.Acl → `https://localhost:7001`
- ComunaClick.Api → `https://localhost:7002`
- Payments.Gateway.Api → `https://localhost:7003`
- ComunaClick.App → `https://localhost:7004`

### 17.3 Configuración (appsettings.Development.json)
**ACL**
```json
{
  "ConnectionStrings": {
    "AclDb": "Host=YOUR_RDS_ENDPOINT;Port=5432;Database=acl_db;Username=acl_app_user;Password=***;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

**Core API**
```json
{
  "ConnectionStrings": {
    "CoreDb": "Host=YOUR_RDS_ENDPOINT;Port=5432;Database=core_db;Username=core_app_user;Password=***;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Acl": { "BaseUrl": "https://localhost:7001" },
  "Payments": {
    "GatewayBaseUrl": "https://localhost:7003",
    "InternalWebhookKey": "CHANGE_ME"
  }
}
```

**Payments Gateway**
```json
{
  "ConnectionStrings": {
    "PaymentsDb": "Host=YOUR_RDS_ENDPOINT;Port=5432;Database=payments_db;Username=payments_app_user;Password=***;SSL Mode=Require;Trust Server Certificate=true"
  },
  "ComunaClic": {
    "InternalWebhookUrl": "https://localhost:7002/v1/payments/provider-notify",
    "InternalWebhookKey": "CHANGE_ME"
  },
  "Transbank": {
    "Environment": "Integration",
    "CommerceCode": "REPLACE_ME",
    "ApiKeyId": "REPLACE_ME",
    "ApiKeySecret": "REPLACE_ME"
  }
}
```

### 17.4 Ejecutar (4 terminales)
```bash
dotnet run --project src/ComunaClick.Acl
dotnet run --project src/ComunaClick.Api
dotnet run --project src/Payments.Gateway.Api
dotnet run --project src/ComunaClick.App
```

### 17.5 Nota AWS (Security Groups)
- RDS debe estar en subnets privadas; tu acceso local típicamente será vía **VPN/Bastion/SSM**.
- Para un MVP rápido, si lo expones públicamente, restringe el inbound de PostgreSQL (5432) a **tu IP** y usa SSL.
---

## 18) Checklist de “Definition of Done” (MVP)

- [ ] Login/refresh funcionando (ACL)
- [ ] Tenant scope aplicado en Core API
- [ ] Search por radio con PostGIS
- [ ] Webpay Plus: pago end-to-end con commit y notificación interna
- [ ] Oneclick: inscripción end-to-end + cargo con token
- [ ] Idempotencia en webhooks
- [ ] Jobs Hangfire base (recordatorio + liquidación)
- [ ] Swagger documentado en los 3 APIs

---

## 19) Notas específicas Transbank

- Webpay Plus: flujo típico “create → redirect → return → commit”.
- Oneclick: requiere “start inscription → finish inscription → authorize charge”; además soporta revocación de inscripción.
- Mantener **separadas** credenciales de integración vs producción.
- Nunca loguear secretos (`ApiKeySecret`).

---

## 20) Implementación sugerida de “Provider Layer” (Payments)

Diseño interno:
- `ITransbankWebpayPlusProvider`
- `ITransbankOneclickProvider`
- `PaymentIntentService`
- `NotificationService` (envío a ComunaClic)

Permite:
- añadir otro provider a futuro (ej. MercadoPago) sin cambiar contratos hacia ComunaClic.

---

---

## 21) Modelo de Base de Datos en PostgreSQL (AWS RDS) — scripts listos

### 21.1 Estrategia recomendada (1 instancia RDS, 3 bases)
Para el MVP en AWS se usará **una instancia** de PostgreSQL (RDS) con **tres databases** separadas (mejor que schemas en una sola DB):
- `core_db`  → **ComunaClick.Api**
- `acl_db`   → **ComunaClick.Acl**
- `payments_db` → **Payments.Gateway.Api**

Esto mantiene independencia lógica y permisos mínimos por servicio, y permite separar a futuro en instancias distintas sin rediseñar.

### 21.2 Scripts generados (orden de ejecución)
Se incluyen scripts separados (ejecutar con usuario admin/master en RDS):

1) Crear roles (usuarios de aplicación)
- `00_create_roles.sql`

2) Crear databases
- `01_create_databases.sql`
> Nota: PostgreSQL no soporta `CREATE DATABASE IF NOT EXISTS`; si ya existen, este script fallará en esas líneas.

3) Crear tablas ACL
- `02_acl.sql` (ejecutar conectado a `acl_db`)

4) Crear tablas CORE (PostGIS + negocio)
- `03_core_min_v2.sql` (ejecutar conectado a `core_db`)
> Este es el script "robusto" para RDS/DBeaver. Reemplaza variantes previas y corrige el UNIQUE de `subscription_plans` usando índices parciales.

5) Crear tablas PAYMENTS (Gateway)
- `04_payments.sql` (ejecutar conectado a `payments_db`)

6) Seeds mínimos (para ver operatividad)
- `infra/seeds/acl_seed.sql`
- `infra/seeds/core_seed.sql`
- `infra/seeds/payments_seed.sql`

7) Reset rápido (limpieza)
- `infra/seeds/acl_reset.sql`
- `infra/seeds/core_reset.sql`
- `infra/seeds/payments_reset.sql`

### 21.5 IDs demo (seed)
- Tenant: `11111111-1111-1111-1111-111111111111`
- Partner: `22222222-2222-2222-2222-222222222222`
- Professional: `33333333-3333-3333-3333-333333333333`
- Customer: `44444444-4444-4444-4444-444444444444`
- Product: `55555555-5555-5555-5555-555555555555`
- Service: `66666666-6666-6666-6666-666666666666`
- ServiceSlot: `77777777-7777-7777-7777-777777777777`
- Order: `88888888-8888-8888-8888-888888888888`
- Booking: `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`
- Payment (core): `cccccccc-cccc-cccc-cccc-cccccccccccc`
- Payment Intent (payments): `17171717-1717-1717-1717-171717171717`
- Lead: `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb`

### 21.3 Ejecución con psql (ejemplo)
```bash
# 1) Roles + DBs (en postgres)
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=postgres sslmode=require" -f 00_create_roles.sql
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=postgres sslmode=require" -f 01_create_databases.sql

# 2) ACL
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=acl_db sslmode=require" -f 02_acl.sql

# 3) CORE
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=core_db sslmode=require" -f 03_core_min_v2.sql

# 4) PAYMENTS
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=payments_db sslmode=require" -f 04_payments.sql

# 5) Seeds (datos demo)
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=acl_db sslmode=require" -f infra/seeds/acl_seed.sql
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=core_db sslmode=require" -f infra/seeds/core_seed.sql
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=payments_db sslmode=require" -f infra/seeds/payments_seed.sql

# 6) Reset (limpieza rápida)
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=acl_db sslmode=require" -f infra/seeds/acl_reset.sql
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=core_db sslmode=require" -f infra/seeds/core_reset.sql
psql "host=YOUR_RDS_ENDPOINT user=ADMIN dbname=payments_db sslmode=require" -f infra/seeds/payments_reset.sql
```

### 21.4 Notas de compatibilidad (RDS + DBeaver)
- Si aparece `must be able to SET ROLE "acl_app_user"` al crear owners/schemas, otorgar membresía del rol al admin:
  ```sql
  GRANT acl_app_user TO <TU_USUARIO_ADMIN>;
  GRANT core_app_user TO <TU_USUARIO_ADMIN>;
  GRANT payments_app_user TO <TU_USUARIO_ADMIN>;
  ```
- En DBeaver usa **Execute SQL Script** (no solo la selección) y valida `SELECT current_database();`.
- Para reintentar sin datos:
  ```sql
  DROP SCHEMA IF EXISTS core CASCADE;
  ```

---

## 22) Modelo para que el agente genere los proyectos (.NET 8)

### 22.1 Objetivo del generador
El agente debe generar una solución .NET 8 con estos proyectos y responsabilidades:

- `ComunaClick.Common` (classlib): contratos comunes, middleware, errores, tenant context, observabilidad.
- `ComunaClick.Acl` (webapi): autenticación y autorización (JWT + Refresh), RBAC/Permisos, scopes por tenant/partner.
- `ComunaClick.Api` (webapi): módulos de negocio (Search, Catalog, Orders, Bookings, Leads, PaymentsOrchestration, Payouts, CRM).
- `ComunaClick.App` (blazor web app): UI para Panel Socio + Admin + Customer (MVP).
- `Payments.Common` (classlib): DTOs neutrales para pasarela.
- `Payments.Gateway.Api` (webapi): integración Transbank (Webpay Plus + Oneclick), callbacks públicos, webhooks internos hacia Core.

### 22.2 Comandos de creación (scaffold)
```bash
mkdir comunaclick && cd comunaclick
dotnet new sln -n ComunaClick
mkdir src tests infra

dotnet new classlib -n ComunaClick.Common -o src/ComunaClick.Common
dotnet new webapi   -n ComunaClick.Acl    -o src/ComunaClick.Acl
dotnet new webapi   -n ComunaClick.Api    -o src/ComunaClick.Api
dotnet new blazor   -n ComunaClick.App    -o src/ComunaClick.App
dotnet new classlib -n Payments.Common    -o src/Payments.Common
dotnet new webapi   -n Payments.Gateway.Api -o src/Payments.Gateway.Api

dotnet sln ComunaClick.sln add   src/ComunaClick.Common/ComunaClick.Common.csproj   src/ComunaClick.Acl/ComunaClick.Acl.csproj   src/ComunaClick.Api/ComunaClick.Api.csproj   src/ComunaClick.App/ComunaClick.App.csproj   src/Payments.Common/Payments.Common.csproj   src/Payments.Gateway.Api/Payments.Gateway.Api.csproj

dotnet add src/ComunaClick.Acl reference src/ComunaClick.Common/ComunaClick.Common.csproj
dotnet add src/ComunaClick.Api reference src/ComunaClick.Common/ComunaClick.Common.csproj
dotnet add src/ComunaClick.App reference src/ComunaClick.Common/ComunaClick.Common.csproj
dotnet add src/Payments.Gateway.Api reference src/Payments.Common/Payments.Common.csproj
dotnet add src/ComunaClick.Api reference src/Payments.Common/Payments.Common.csproj
```m

### 22.3 Puntos de integración obligatorios
- `ComunaClick.Acl` emite JWT con `tenant_id` y roles/permisos.
- `ComunaClick.Api` valida JWT y aplica filtro por tenant.
- `Payments.Gateway.Api` no depende de `ComunaClick.Api` (solo notifica por webhook interno).
- `ComunaClick.Api` orquesta pagos: crea `core.payments` (Pending) y recibe notificaciones internas.

### 22.4 Connection strings (DB única por schemas)
- DB: `comunaclick_db` con schemas `acl`, `core`, `payments`.
- ACL: `Database=comunaclick_db;Username=acl_app_user;Password=...`
- CORE: `Database=comunaclick_db;Username=core_app_user;Password=...`
- PAYMENTS: `Database=comunaclick_db;Username=payments_app_user;Password=...`

### 22.5 Multi-tenant por comuna (resumen)
- Core: `countries`, `regions`, `comunas` + `tenants.comuna_id` (1:1) + `is_active`.
- ACL: `users.is_super_admin`, tablas `user_*_access`, vistas `v_user_accessible_tenants` y `v_user_can_access_tenant`.


Fin.
