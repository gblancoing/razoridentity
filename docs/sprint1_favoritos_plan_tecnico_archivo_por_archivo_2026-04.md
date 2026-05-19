# Sprint 1 - Favoritos Buyer

Fecha: 2026-04-19

## Objetivo técnico

Implementar favoritos para buyer autenticado (`customer`) con soporte para:

- `partner`
- `product`
- `service`
- `professional`

Incluye:

- persistencia en `core`
- endpoints autenticados `v1/buyer/favorites`
- cliente en `ComunaClick.Shared`
- UI en buyer para guardar/listar/remover
- quality checks mínimos del flujo

## Contrato API propuesto

### Endpoints

- `GET /v1/buyer/favorites?type={all|partner|product|service|professional}`
- `POST /v1/buyer/favorites`
- `DELETE /v1/buyer/favorites/{id}`

### Request `POST`

- `Type` (`partner|product|service|professional`)
- `TargetId` (`Guid`)
- `TenantId` opcional (`Guid?`) para reforzar contexto multi-tenant cuando aplique

### Response item

- `Id`
- `Type`
- `TargetId`
- `CustomerId`
- `PartnerId` opcional (si el target cuelga de partner)
- `Name`
- `Summary`
- `Category`
- `Price` opcional
- `Currency` opcional
- `ImageUrl` opcional/fallback
- `CreatedAt`

## Diseño de datos

Tabla nueva: `core.buyer_favorites`

Campos sugeridos:

- `id uuid pk`
- `tenant_id uuid not null`
- `customer_id uuid not null`
- `type varchar(32) not null`
- `target_id uuid not null`
- `partner_id uuid null`
- `created_at timestamptz not null default now()`

Índices sugeridos:

- `ux_buyer_favorites_scope` único en (`tenant_id`, `customer_id`, `type`, `target_id`)
- `ix_buyer_favorites_customer_created_at` en (`tenant_id`, `customer_id`, `created_at desc`)
- `ix_buyer_favorites_partner` en (`tenant_id`, `partner_id`)

## Plan archivo por archivo

## `api`

### Crear

- [infra/migrations/20260419_buyer_favorites.sql](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/infra/migrations/20260419_buyer_favorites.sql)
  - crear `core.buyer_favorites`
  - crear índices y constraint unique

- [src/ComunaClick.Api/Persistence/Entities/BuyerFavorite.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Api/Persistence/Entities/BuyerFavorite.cs)
  - entidad EF con campos de la tabla

- [src/ComunaClick.Api/Modules/Crm/Contracts/BuyerFavoriteCreateRequest.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Api/Modules/Crm/Contracts/BuyerFavoriteCreateRequest.cs)
  - DTO de entrada para `POST`

- [src/ComunaClick.Api/Modules/Crm/Contracts/BuyerFavoriteItemResponse.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Api/Modules/Crm/Contracts/BuyerFavoriteItemResponse.cs)
  - DTO de salida consolidada

- [src/ComunaClick.Api/Modules/Crm/BuyerFavoritesController.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Api/Modules/Crm/BuyerFavoritesController.cs)
  - `GET/POST/DELETE`
  - `[Authorize(Policy = "buyer.customer")]`
  - resolución de email claim + `customer` (igual patrón de [BuyerCustomerController.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Api/Modules/Crm/BuyerCustomerController.cs))
  - validación de ownership por tenant/customer
  - deduplicación por `(tenant, customer, type, target)`

### Modificar

- [src/ComunaClick.Api/Persistence/CoreDbContext.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Api/Persistence/CoreDbContext.cs)
  - agregar `DbSet<BuyerFavorite>`
  - query filter por tenant
  - mapeo tabla/índices/columnas en `OnModelCreating`

### Notas de implementación API

- Resolver metadata de favorito por tipo sin exponer datos sensibles:
  - `partner` desde `core.partners`
  - `product` desde `core.products`
  - `service` desde `core.services`
  - `professional` desde `core.professionals`
- Si target no existe o no está activo/visible:
  - rechazar creación con `400`
- `DELETE` debe validar que el favorito pertenezca al `customer` del token.

## `app` (incluye `ComunaClick.Shared` + `ComunaClick.SharedUI`)

### Crear

- [src/ComunaClick.SharedUI/Pages/Buyer/Favorites.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Pages/Buyer/Favorites.razor)
  - vista “Mis favoritos”
  - filtros por tipo
  - estados `loading`, `empty`, `error`
  - CTA a detalle por tipo

### Modificar

- [src/ComunaClick.Shared/Api/Buyer/BuyerApiClient.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Shared/Api/Buyer/BuyerApiClient.cs)
  - agregar métodos:
    - `GetFavoritesAsync(...)`
    - `CreateFavoriteAsync(...)`
    - `DeleteFavoriteAsync(...)`
  - agregar records DTO request/response

- [src/ComunaClick.SharedUI/Pages/CategoryDetail.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Pages/CategoryDetail.razor)
  - botón “Guardar” en cards de negocio/servicio/profesional
  - feedback inmediato por acción

- [src/ComunaClick.SharedUI/Pages/Buyer/Detail.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Pages/Buyer/Detail.razor)
  - acción principal secundaria “Guardar en favoritos”
  - estado toggled según si ya está guardado

- [src/ComunaClick.SharedUI/Layouts/BuyerLayout.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Layouts/BuyerLayout.razor)
  - acceso de navegación a `/buyer/favorites`

- [src/ComunaClick.SharedUI/Services/LocaleService.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Services/LocaleService.cs)
  - claves ES/EN:
    - títulos
    - estados vacíos
    - éxito/error al guardar/remover

### Opcional recomendado

- [src/ComunaClick.SharedUI/Pages/Buyer/Profile.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Pages/Buyer/Profile.razor)
  - shortcut a favoritos recientes

## `acl`

Sprint 1 no requiere cambios de código ACL si se mantiene policy API actual:

- `buyer.customer` ya definida en [Program.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.Api/Program.cs).

### Verificación obligatoria

- Confirmar que token buyer de QA trae rol `customer`.
- Confirmar que sesión partner no pasa policy `buyer.customer`.

Si falla alguna verificación, revisar seeds/config ACL en:

- [infra/seeds/acl_seed.sql](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/infra/seeds/acl_seed.sql)
- [base de datos/sql/acl_access.sql](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/base%20de%20datos/sql/acl_access.sql)

## `admin`

Sin cambios funcionales en Sprint 1.

### Tarea mínima de preparación

- Registrar ticket para Sprint 8:
  - filtro por `customerId`/`targetId` en soporte interno de favoritos.

## Pruebas y validación

### Quality checks automáticos

- Modificar [src/ComunaClick.QualityChecks/Program.cs](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.QualityChecks/Program.cs)
  - agregar bloque autenticado:
    - crear favorito `service/product`
    - listar favoritos y validar presencia
    - borrar favorito y validar remoción

### Smoke manual

- Buyer login (QA)
- Guardar favorito desde:
  - `CategoryDetail`
  - `Buyer/Detail`
- Verificar listado en `/buyer/favorites`
- Eliminar favorito desde listado
- Repetir en al menos dos tipos de target

### No-regresión mínima

- `dotnet build` `ComunaClick.Api` y `ComunaClick.SharedUI` con receta estable (`-m:1 -nr:false`)
- smoke post-deploy público existente sin degradación

## Secuencia sugerida de implementación (orden real)

1. `infra/migrations` + entidad + `CoreDbContext`.
2. `BuyerFavoritesController` + contratos API.
3. `BuyerApiClient` (métodos + records).
4. UI: `Favorites.razor` + botones en `CategoryDetail` y `Buyer/Detail`.
5. textos en `LocaleService`.
6. `QualityChecks` autenticados para favoritos.
7. deploy `api` + `app`, smoke y validación QA.

## Criterio de cierre Sprint 1

- Buyer autenticado puede guardar, listar y eliminar favoritos en producción.
- No hay acceso cruzado entre buyers.
- El flujo funciona para al menos `partner` y `service` (idealmente los 4 tipos).
- Quality checks de favoritos pasan con credenciales QA.
