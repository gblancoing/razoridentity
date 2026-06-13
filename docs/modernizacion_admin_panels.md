# Modernización de Payments.App y ComunaClick.Admin

**Fecha:** 2026-06-12  
**Branch:** `feat/rama_comunaclic`

---

## Contexto

El proyecto había avanzado sustancialmente (delivery con repartidores y liquidaciones, Mercado Pago marketplace con OAuth/split payments, carrito multi-local, payouts, leads), pero los dos paneles de administración quedaron atrás:

- **Payments.App**: sin suscripciones reales, sin export CSV, sin reintentos de notificación, sin conciliación.
- **ComunaClick.Admin**: mezclaba MudBlazor (solo MainLayout + Dashboard) con CSS custom, y no reflejaba ninguna de las funcionalidades nuevas.

---

## Decisiones de arquitectura

| Decisión | Detalle |
|---|---|
| UI unificada | CSS design tokens propios (sin MudBlazor), RCL compartida `ComunaClick.AdminUI` |
| Alcance full-stack | Nuevos endpoints admin en ComunaClick.Api y Payments.Gateway.Api |
| SSO preservado | Ambos paneles comparten `localStorage["payments.tokens"]` — no se tocó |
| Sin migraciones EF | Gateway usa DDL idempotente (`CREATE TABLE IF NOT EXISTS`); schema bootstrap al arranque |

---

## Fase 0 — RCL ComunaClick.AdminUI

Nueva Razor Class Library presentacional (`src/ComunaClick.AdminUI/`).

### CSS
- `wwwroot/css/admin-tokens.css` — design tokens MD3: `--color-primary:#2d9e4f`, superficies `#fffdf7`, Inter/Manrope, radios, sombras, badges de estado.
- `wwwroot/css/admin.css` — layout completo: admin-shell, sidebar off-canvas (≤1024px, verificado a 375px), data-table con colapso a cards en mobile, status badges, filter-bar, paginator, dialogs, botones (`btn`, `btn-primary`, `btn-ghost`, `btn-danger`).

### Componentes
| Componente | Descripción |
|---|---|
| `AdminShell` | Layout topbar + sidebar parametrizado con `AdminNavItem(Href, Icon, Label)` |
| `DataTable<TItem>` | Genérico con paginación cliente, slots filter/header/row, `KeySelector` |
| `PageHeader` | Eyebrow + título + descripción + slot Actions |
| `KpiCard` | Tarjeta de indicador con etiqueta, valor y footnote |
| `StatusBadge` | Badge de estado con mapeo automático de colores |
| `ConfirmDialog` | Modal CSS puro con `@bind-IsOpen`, `Danger`, `IsBusy` |
| `JsonViewer` | `<details>` con JSON pretty-print colapsable |
| `BarList` | Gráfico de barras CSS puro |
| `EmptyState` | Estado vacío con ícono, título y descripción |
| `LoadingPanel` | Indicador de carga |
| `ErrorPanel` | Panel de error con botón retry |
| `Paginator` | Paginación con números de página |

---

## Fase 1 — Backend ComunaClick.Api

Nuevos controllers en `src/ComunaClick.Api/Modules/Admin/`:

### `AdminOrdersController`
- `GET /v1/admin/orders` — paginado, filtros: status/tenant/partner/fechas/search
- `GET /v1/admin/orders/{id}` — detalle con items, pago MP y settlement
- `GET /v1/admin/orders/export.csv` — hasta 2000 filas

### `AdminCouriersController`
- `GET /v1/admin/couriers` — estado MP payee (via `Courier.Id == Seller.Id`), settlements pendientes

### `AdminMarketplaceController` (extendido)
- `GET /v1/admin/sellers` — vendedores con estado MP, comisiones, conteo de pagos
- `POST /v1/admin/marketplace/payments/{id}/sync` — delega a `MercadoPagoWebhookService`

### `PayoutsController` (extendido)
- `GET /v1/admin/payouts/batches` — listado global entre todos los tenants (`platform.admin`)

**DTOs** en `AdminOperationsContracts.cs`: `AdminPagedResult<T>`, `AdminOrderListItemDto`, `AdminOrderDetailDto`, `AdminCourierListItemDto`, `AdminSellerListItemDto`, `AdminPayoutBatchListItemDto`.

---

## Fase 2 — Backend Payments.Gateway.Api

### Esquema SQL (idempotente en `base de datos/04_payments.sql`)
```sql
CREATE TABLE IF NOT EXISTS payments.subscriptions (...)
CREATE TABLE IF NOT EXISTS payments.subscription_attempts (...)
ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS review_status text;
ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS review_note text;
ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS core_notified_at timestamptz;
ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS core_notify_attempts int;
```

### Bootstrap
`PaymentsSchemaBootstrap.cs` — aplica DDL idempotente al arranque, tolerante a errores de permisos.

### Nuevos endpoints

#### `AdminSubscriptionsController` (`v1/admin/subscriptions`)
- `GET /` — suscripciones reales (reemplaza heurística "SUBS")
- `GET /{id}` — detalle con historial de intentos
- `POST /{id}/cancel|pause|resume`

#### `AdminPaymentsController` (reescrito)
- `GET /dashboard` — KPIs día/semana/mes + distribución por proveedor
- `GET /alerts` — conteos de alertas para dashboard
- `GET /payment-intents` — listado con filtros
- `GET /payment-intents/export.csv`
- `GET /payment-intents/{id}` — detalle con eventos y estado de notificación a Core
- `POST /payment-intents/{id}/notify-core` — reenvía notificación; crea `ProviderEvent` sintético `core.notify.retry`
- `POST /payment-intents/{id}/review` — marca flagged/resolved
- `GET /reconciliation` — intents estancados, sin eventos, cargos sin auth, marcados para revisión

---

## Fase 3 — Frontend Payments.App

### Páginas reescritas/creadas

| Página | Ruta | Novedades |
|---|---|---|
| `Dashboard.razor` | `/` | KPIs día/semana/mes, panel de alertas, BarList de proveedores |
| `Transactions.razor` | `/transacciones` | FilterBar (proveedor/estado/fechas/referencia), DataTable, export CSV autenticado |
| `TransactionDetail.razor` | `/transacciones/{id}` | Timeline de eventos, JsonViewer de payloads, reintentar notificación Core, marcar para revisión |
| `Subscriptions.razor` | `/suscripciones` | Datos reales del gateway, filtros estado/búsqueda |
| `SubscriptionDetail.razor` | `/suscripciones/{id}` | Historial de intentos, cancel/pause/resume con ConfirmDialog |
| `Reconciliation.razor` | `/conciliacion` | Discrepancias del gateway (stuck_pending, sin eventos, cargo sin auth, flagged) |

### Helpers JS (`wwwroot/app.js`)
```js
window.paymentsApp.downloadWithAuth(url, token, filename)
// fetch() + blob + anchor programático para descargas con JWT
```

---

## Fase 4 — Frontend ComunaClick.Admin

### Páginas nuevas

| Página | Ruta | Descripción |
|---|---|---|
| `Orders.razor` | `/ordenes` | Listado global con filtros + export CSV |
| `OrderDetail.razor` | `/ordenes/{id}` | Items, pago MP, slip de liquidación |
| `DeliverySettlements.razor` | `/liquidaciones` | Slips de repartidores con "Marcar liquidado" + ConfirmDialog |
| `Couriers.razor` | `/couriers` | Estado MP payee, cuenta portal, liquidaciones pendientes |
| `Marketplace.razor` | `/marketplace` | Vendedores, estado OAuth, comisiones, sync MP |
| `Payouts.razor` | `/payouts` | Batches globales por tenant y periodo |

### Páginas restyled con AdminUI

Tenants, Partners, Categories, Users, Audit (con `JsonViewer` para valores anteriores/nuevos), DeliveryProviders, Content, Settings.  
Reemplazados: `<section class="page-header">` → `<PageHeader>`, estados de carga manuales → `<LoadingPanel>` / `<ErrorPanel>`, botones `primary-link` → `btn btn-primary/ghost`.

### Helper JS (`wwwroot/app.js`)
```js
window.comunaclicAdmin.downloadWithAuth(url, token, filename)
```

### Servicios extendidos
- `AdminApiClient.cs` — `AccessToken`, `GetOrdersAsync`, `BuildOrdersExportUrl`, `GetSettlementsAsync`, `SettleAsync`, `GetCouriersAsync`, `GetSellersAsync`, `SyncMarketplacePaymentAsync`, `GetPayoutBatchesAsync`
- `Models/AdminDtos.cs` — DTOs espejo de todos los contratos nuevos

---

## Fase 5 — Roles granulares Payments.App

### Seed SQL (`infra/seeds/acl_payments_roles.sql`)
```sql
INSERT INTO acl.roles (name, created_at)
VALUES
  ('payments.viewer',   now()),
  ('payments.operator', now()),
  ('payments.admin',    now())
ON CONFLICT (name) DO NOTHING;
```

### Políticas en gateway (`Program.cs`)
```
payments.viewer   ⊃ payments.operator ⊃ payments.admin
```
`tenant_admin` y `platform_admin` satisfacen todos los niveles (backwards compatibility).

### Asignación por endpoint
| Endpoint | Política mínima |
|---|---|
| GETs (listados, detalle, export, dashboard, alertas, conciliación) | `payments.viewer` |
| POST notify-core, POST review | `payments.operator` |
| POST cancel/pause/resume suscripción | `payments.operator` |

### `PaymentsAuthStateService`
```csharp
bool IsViewer   // viewer + operator + admin + tenant_admin + platform_admin
bool IsOperator // operator + admin + tenant_admin + platform_admin
bool IsAdmin    // admin + tenant_admin + platform_admin
bool IsPaymentsAdmin => IsViewer // alias backwards-compat
```

`MainLayout` requiere `IsViewer`. Botones de mutación en `TransactionDetail` y `SubscriptionDetail` se ocultan si `!AuthState.IsOperator`.

---

## Proyectos afectados y resultado de build

| Proyecto | Build |
|---|---|
| `ComunaClick.AdminUI` | ✅ 0 errores, 0 advertencias |
| `ComunaClick.Admin` | ✅ 0 errores, 0 advertencias |
| `Payments.App` | ✅ 0 errores, 0 advertencias |
| `Payments.Gateway.Api` | ✅ 0 errores, 0 advertencias |
| `ComunaClick.Api` | ✅ 0 errores, 0 advertencias |

> `ComunaClick.Mobile` tiene errores preexistentes (NETSDK1135, CS0234) no relacionados con estos cambios.

---

## Notas operativas

- **No se modificó** `localStorage["payments.tokens"]` ni el shape de `PaymentsAuthTokens` — SSO entre paneles preservado.
- **Bootstrap de esquema** del gateway es tolerante: si el usuario de BD tiene permisos limitados, loga warning y continúa.
- **Export CSV** usa `fetch()` + blob + anchor programático — no `<a href>` directo (que no envía JWT).
- **Iconos**: Material Symbols (fuente ya vinculada en ambos `App.razor`).
