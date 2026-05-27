# Regla de negocio: Empresa de comercio (Partner tipo A)

Documento de referencia para catálogo de **productos**, inventario, precios, publicación y **extensión opcional a servicios** en ComunaClic.

Complementa `docs/empresa-tipo-b.md` (empresas cuyo foco principal son avisos de servicio).

## Tipos de partner

| Tipo | Código | Catálogo principal | Puede extender |
|------|--------|--------------------|----------------|
| **Comercio** | **`A`** | **Productos con stock y precio de venta** (este documento) | **Sí → avisos de servicio** (modo híbrido) |
| Empresa de servicios | `B` | Avisos de servicio | Ver `empresa-tipo-b.md` |
| Profesionales / vitrina | `C` | Perfiles tipo equipo | — |

## Perfil de negocio (tipo A)

**Quién es:** pymes, retail, alimentos, ferreterías, emprendimientos con inventario físico o digital que **venden productos** en su barrio.

**Qué necesita en la plataforma:**

1. Organizar su tienda en **categorías propias** (secciones de vitrina), no solo el rubro global del registro.
2. Cargar **productos** con stock, fotos y descripción.
3. Registrar **precio de costo** (uso interno) y **precio de venta** (lo que ve y paga el comprador).
4. **Publicar** productos y el negocio (`IsActive` por ítem + `Partner.IsVisible` para la ficha).
5. Opcionalmente, si también presta servicios (ej. ferretería + instalación), **activar avisos de servicio** con las mismas reglas que tipo `B` sin cambiar el tipo principal del partner.
6. **Cuenta bancaria** en Ajuste de empresa para recibir liquidaciones de ventas.

## Archivos clave en el código (hoy)

| Área | Ruta |
|------|------|
| UI catálogo (productos + servicios según tipo) | `src/ComunaClick.SharedUI/Pages/Partner/Catalog.razor` |
| API productos | `src/ComunaClick.Api/Modules/Catalog/ProductsController.cs` |
| Entidad producto | `src/ComunaClick.Api/Persistence/Entities/Product.cs` |
| Inventario | `src/ComunaClick.Api/Persistence/Entities/ProductInventory.cs` |
| Servicio de stock (validación, reserva, descuento, alertas) | `src/ComunaClick.Api/Modules/Catalog/ProductInventoryService.cs` |
| Notificaciones stock bajo / agotado | `src/ComunaClick.Api/Modules/Catalog/StockNotificationService.cs` |
| Cumplimiento al pagar pedido | `src/ComunaClick.Api/Modules/Catalog/OrderInventoryFulfillment.cs` |
| Backfill inventario al arranque | `src/ComunaClick.Api/Persistence/ProductInventoryBackfill.cs` |
| Configuración umbrales | `appsettings.json` → sección `Inventory` (`InventoryOptions.cs`) |
| Carrito / pedidos (validación stock) | `CartController.cs`, `OrdersController.cs` |
| Checkout comprador (límite cantidad) | `src/ComunaClick.SharedUI/Pages/Buyer/Checkout.razor` |
| Bandeja alertas partner | `src/ComunaClick.SharedUI/Pages/Partner/Notifications.razor` |
| Categorías globales plataforma | `ProductCategory` / `ProductSubcategory` (`CatalogScope = commerce`) |
| Ajuste empresa / publicación | `Pages/Partner/CompanySettings.razor`, `PartnerPublishSection`, `PartnerBankSection` |
| Ficha pública comprador | `Pages/Buyer/Detail.razor`, `Components/Public/PartnerServiceStorefront.razor` |
| DTOs partner | `src/ComunaClick.Shared/Api/Partner/PartnerApiClient.cs` |
| Referencia servicios (híbrido) | `docs/empresa-tipo-b.md`, `ServicesController.cs` |

## Estado actual vs objetivo

| Capacidad | Estado actual | Objetivo |
|-----------|---------------|----------|
| CRUD productos en `/partner/catalog` | Parcial (crear, listar, editar básico) | CRUD completo + eliminar con reglas |
| Stock (`ProductInventory`) | **Implementado** (ver sección [Control de stock](#control-de-stock)) | Mantener; ajustes futuros: reserva con TTL, reportes valor inventario |
| Categorías en formulario producto | Select de **categorías globales** plataforma | **+ Categorías propias del partner** (secciones de tienda) |
| Precio de venta público | `Product.Price` | Mantener como único precio en checkout y ficha |
| Precio de costo | **No existe** | `CostPrice` solo panel partner (nunca en API pública) |
| Fotos producto | Subida desde celular/PC (cámara o galería), sin URL manual | Hasta 8 por producto (`ProductImageUpload`) |
| Publicar negocio | `Partner.IsVisible` + banner en catálogo | Igual que tipo B; checklist en Ajuste de empresa |
| Comercio que también vende servicios | **No**: `Partner.Type` es `A` **o** `B` | **Híbrido**: tipo `A` + flag `OffersServices` (o capacidades) |
| Textos localizados | Muchos strings sueltos en español en UI productos | `partner.catalog.a.*` en `LocaleService` |

## Objetivo de negocio

Para **empresas de comercio (tipo A)**, el catálogo es una **tienda en la ficha pública**: productos agrupados por **secciones/categorías del local**, con stock y precio de venta visible al comprador.

### Producto — datos obligatorios

| Campo | Visibilidad | Regla |
|-------|-------------|--------|
| Nombre | Pública | Obligatorio |
| Descripción | Pública | Recomendada |
| Categoría / sección tienda | Pública | Obligatoria (propia del partner o heredada de sección por defecto) |
| Precio de **venta** | Pública | Obligatorio para publicar; usado en carrito/checkout |
| Precio de **costo** | **Solo partner** | Opcional; para margen y reportes internos |
| Stock | Partner (+ disponibilidad pública) | Entero ≥ 0; **siempre** hay fila en `product_inventory`; descontar al marcar pedido **pagado**; reservar en pedidos pendientes |
| Fotos | Pública | Mínimo 0; recomendado ≥ 1; galería en ficha |
| `IsActive` | Partner | Publicado / pausado |

**Regla de precios:** lo que muestra y cobra ComunaClic al comprador es siempre `SalePrice` (hoy `Price`). `CostPrice` no debe aparecer en endpoints `AllowAnonymous`, ficha comprador ni listados públicos.

### Publicación (dos niveles)

1. **Producto** (`Product.IsActive`): el ítem aparece o no en la vitrina del local.
2. **Negocio** (`Partner.IsVisible`): la empresa aparece en búsqueda, mapa y categorías globales. Si hay productos activos pero el negocio no está publicado, mostrar banner (mismo patrón que tipo B).

### Modo híbrido — comercio + servicios

Un partner registrado como **tipo `A`** puede **también ofrecer servicios** (instalación, delivery premium, asesoría, etc.).

**Reglas:**

- El **tipo principal** sigue siendo `A` (métricas, registro, rubro comercio).
- Con capacidad `OffersServices = true` (nuevo campo o tabla de capacidades):
  - En `/partner/catalog` se muestran **dos pestañas o secciones**: **Productos** | **Servicios**.
  - Los servicios siguen **todas** las reglas de `empresa-tipo-b.md` (aviso sin precio / reservable, profesionales, agenda, pago).
- Un partner tipo `B` puro **no** gestiona productos (sin cambio).
- Búsqueda: partner `A` visible si tiene productos activos **o** servicios activos (si híbrido).

## Categorías: dos niveles

No confundir:

| Nivel | Ejemplo | Uso |
|-------|---------|-----|
| **Rubro plataforma** | “Alimentos”, “Ferretería” | Registro del negocio, mapa, descubrimiento (`Partner.SubcategoryId`) |
| **Sección de catálogo del local** | “Bebidas”, “Snacks”, “Ofertas” | Agrupa productos en la ficha del partner; **la crea el partner** |

**Objetivo técnico:** tabla `partner_catalog_categories` (nombre sugerido) con `PartnerId`, `Name`, `SortOrder`, `IsActive`; FK opcional en `Product.CategoryId` → sección local. Mantener string `Category` legacy o migrar a FK.

## Reglas de negocio obligatorias

1. Tipo `A`: catálogo **principal = productos**; servicios solo si híbrido habilitado.
2. **Costo vs venta:** `CostPrice` nunca expuesto al comprador; margen calculable en panel (`SalePrice - CostPrice`).
3. **Stock:** no permitir carrito/checkout/pedido si la cantidad supera el **disponible**; reservar unidades en pedidos `payment_pending` / `processing`; descontar inventario físico al pasar a **pagado**; alertar al partner en stock bajo o agotado (ver [Control de stock](#control-de-stock)).
4. **CRUD producto:** crear, editar, activar/desactivar, **eliminar** (soft-delete si hay pedidos históricos).
5. **Secciones:** el partner puede crear, renombrar, ordenar y desactivar categorías de tienda sin afectar el rubro global.
6. **Imágenes:** hasta 8 fotos por producto (mismo límite que avisos); primera = portada.
7. Textos vía `LocaleService` (`partner.catalog.a.*`), no strings sueltos en español.
8. No romper tipo `B` ni `C`; cambios acotados y flags de capacidad.

## Experiencia UI deseada (`/partner/catalog`, tipo A)

### Header

- Badge: “Catálogo de productos” / “Tu tienda”.
- Subtítulo: stock, precios y publicación en la ficha.
- CTA: “Crear producto”.
- Si híbrido: tabs **Productos** | **Servicios** (servicios reutiliza UI tipo B).

### Formulario crear/editar producto

- Nombre, descripción.
- **Sección de tienda** (select + “Nueva categoría” inline o modal).
- **Precio de costo** (opcional, etiqueta “Solo vos lo ves”).
- **Precio de venta** (obligatorio para publicar).
- Stock inicial / ajuste de stock.
- **Fotos** (`ProductImageUpload`, análogo a `ServiceImageUpload`).
- Toggle **Publicado** / Pausado.
- Preview (`PartnerOfferPreviewCard` con precio de venta).
- Ayuda: “El comprador solo ve el precio de venta.”

### Listado productos

- Agrupado por sección o tabla con columna categoría.
- Badges: Activo / Pausado / Sin stock.
- Acciones: Vista previa | Editar | Activar/Desactivar | Ajustar stock | **Eliminar** (confirmación).
- Columna opcional **Margen %** (solo partner, si hay costo).

### Métricas (cards superiores)

- Total productos publicados.
- Productos sin stock / stock bajo.
- Precio medio de venta (público).
- Estado publicación del negocio.

### Categorías de tienda (nueva subsección o `/partner/catalog/categories`)

- Listar secciones del local.
- Crear / editar nombre / orden / activar-desactivar.
- No eliminar sección con productos sin reasignar.

## Control de stock

Lógica implementada para empresas tipo **A** (productos con inventario).

### Modelo de datos

| Concepto | Detalle |
|----------|---------|
| Tabla | `core.product_inventory` (`ProductId` PK, `Quantity`, `UpdatedAt`) |
| Alta de producto | Siempre se crea fila de inventario, aunque el stock inicial sea **0** (`InitialStock` en `POST /v1/products`) |
| Productos legacy | Al arrancar la API, `ProductInventoryBackfill` crea fila con cantidad `0` si faltaba |
| Sin fila de inventario | Se trata como **0** unidades (ya no como “stock ilimitado”) |

### Cantidades: físico vs disponible

| Métrica | Significado | Quién la ve |
|---------|-------------|-------------|
| **Stock físico** (`Quantity` en inventario) | Unidades en bodega / registradas por el partner | Panel partner (catálogo, edición) |
| **Stock disponible** | Físico − unidades en pedidos que **reservan** stock | Comprador (ficha, checkout, API pública `AvailableQuantity` / `InStock`) |

**Estados de pedido que reservan** (no descontan físico aún): `payment_pending`, `processing` (configurable en `Inventory:ReservingOrderStatuses`).

**Descuento físico:** al confirmar venta — pedido pasa a `paid` (o pago Mercado Pago / webhook en estado `approved`). Servicio: `OrderInventoryFulfillment` + `ProductInventoryService.FulfillOrderAsync`.

### Validaciones (comprador)

| Punto | Comportamiento |
|-------|----------------|
| `POST /v1/cart/items` | Rechaza si `quantity` > disponible |
| `POST /v1/cart/checkout` / creación de pedido | Valida todas las líneas antes de persistir |
| `POST /v1/orders` | Igual validación |
| Checkout UI (`/buyer/checkout`) | Muestra stock disponible, `max` en cantidad, bloquea compra si `InStock = false` |

Mensajes API en español, ej.: *"Solo hay N unidad(es) disponibles de \"Producto\"."*

### API pública

En `ProductResponses.ToPublicResponse` y listados de vitrina:

- `InStock` = `availableQuantity > 0`
- `StockQuantity` = stock **físico** (on hand)
- `AvailableQuantity` = disponible para comprar ahora

### Alertas al administrador del negocio

Registradas en `interactions` (bandeja **`/partner/notifications`**):

| Tipo | Cuándo | Acción sugerida en UI |
|------|--------|------------------------|
| `stock_out` | Stock físico pasa de > 0 a **0** | Ir a Catálogo y reponer |
| `stock_low` | Stock físico cruza hacia **≤ umbral** (default **5**) | Reponer pronto |

- Payload incluye `message`, `productId`, `productName`, `quantity`, `threshold`.
- También se disparan al **ajustar stock manualmente** en catálogo (`PATCH .../inventory`) y al **descontar por venta pagada**.
- **Antispam:** no repite la misma alerta (mismo producto + tipo) en **6 horas**.
- Filtro “Acción” en notificaciones incluye tipos con `stock`.

Configuración en `appsettings.json`:

```json
"Inventory": {
  "LowStockThreshold": 5,
  "ReservingOrderStatuses": [ "payment_pending", "processing" ]
}
```

### Flujo resumido

```text
Partner crea producto (stock inicial N)
    → product_inventory.Quantity = N

Comprador agrega al carrito / crea pedido (payment_pending)
    → valida: cantidad ≤ disponible (N − reservas de otros pedidos pendientes)
    → reserva: pedido pendiente reduce disponible, NO reduce Quantity aún

Pedido pasa a paid (panel partner o pago aprobado)
    → Quantity -= cantidad vendida
    → si queda ≤ umbral o 0 → notificación stock_low / stock_out

Pedido cancelado (payment_pending)
    → deja de reservar; disponible sube de nuevo sin tocar Quantity
```

### Pendiente / mejoras futuras

- Liberación automática de reserva por timeout en pedidos abandonados (`payment_pending` antiguos).
- Reporte de valor de inventario a costo (`CostPrice × Quantity`).
- Email/WhatsApp además de bandeja in-app (hoy solo `Interaction`).

## Backend esperado

### Extender `Product`

```text
CostPrice          decimal?     // nullable, solo partner API
SalePrice          decimal      // renombrar conceptualmente desde Price (o alias)
PartnerCatalogCategoryId  Guid?  // FK sección local
// ImageUrls vía tabla product_images (espejo service_images)
```

### Nuevas entidades (sugerido)

- `PartnerCatalogCategory` — secciones propias del local.
- `ProductImage` — `ProductId`, `Url`, `SortOrder`.
- `PartnerCapabilities` o columnas en `Partner`:
  - `OffersServices` (bool, default false para A existentes).

### Endpoints

| Método | Ruta | Notas |
|--------|------|--------|
| CRUD | `/v1/partners/{id}/catalog-categories` | Solo staff del partner |
| POST | `/v1/products/{id}/images` | Multipart, como servicios |
| PATCH | `/v1/products/{id}/inventory` | Ajuste de stock; dispara alertas `stock_low` / `stock_out` |
| DELETE | `/v1/products/{id}` | Validar pedidos abiertos (`payment_pending`, `paid`, `processing`) |
| GET público | `/v1/public/products/{id}` | **Sin** `CostPrice`; `InStock`, `StockQuantity` (físico), `AvailableQuantity` |
| GET | `/v1/partners/{id}/notifications` | Incluye `stock_out`, `stock_low` entre `Interaction` |

### Híbrido

- `GET /v1/partners/me` incluye `OffersServices`.
- `Catalog.razor` consulta capacidad además de `Type == "A"`.

Migraciones en `infra/migrations/` + `DatabaseSchemaBootstrap` si aplica.

## Ficha pública comprador

- Listado de productos por **sección del local**.
- Detalle producto: galería ampliable (mismo lightbox que avisos), **solo precio de venta**, indicador “Sin stock” si aplica.
- CTA compra / carrito según flujo e-commerce existente.
- Si híbrido: en ficha del partner, bloque **Productos** y bloque **Servicios** (enlace a avisos tipo B).

## Criterios de aceptación

### Productos (tipo A)

- [x] Partner `A` ve catálogo centrado en productos (no avisos por defecto).
- [x] Crear producto con venta, stock y sección de tienda.
- [x] `CostPrice` guardado y visible solo en panel partner.
- [x] API pública y ficha comprador **nunca** muestran costo.
- [x] Activar/desactivar producto reflejado en vitrina.
- [x] Editar y eliminar con confirmaciones y errores claros.
- [x] Múltiples fotos por producto + galería en ficha.
- [x] Categorías de tienda creadas por el partner y asignables a productos.
- [x] ES/EN en `LocaleService` (`partner.catalog.a.*`).
- [x] Cuenta bancaria en Ajuste de empresa (`PartnerBankSection`) para recibir pagos.
- [x] Build OK; migraciones aplicables en dev.

### Control de stock (implementado)

- [x] Stock inicial al crear producto (incluye 0); fila `product_inventory` siempre existe.
- [x] No vender más unidades que el **stock disponible** (carrito, checkout, API pedidos).
- [x] Reserva en pedidos `payment_pending` / `processing` sin descontar físico hasta `paid`.
- [x] Descuento de inventario al marcar pedido pagado o al aprobar pago (Mercado Pago / webhook).
- [x] Ficha y vitrina pública: `InStock` según disponible, no según “sin registro de inventario”.
- [x] Alertas `stock_out` y `stock_low` en `/partner/notifications` con enlace a catálogo.
- [x] Checkout comprador limita cantidad y muestra unidades disponibles.
- [x] Configuración `Inventory:LowStockThreshold` y `ReservingOrderStatuses`.

### Híbrido (comercio + servicios)

- [x] Partner `A` con `OffersServices` ve pestaña/sección Servicios en catálogo.
- [x] Servicios del híbrido cumplen criterios de `empresa-tipo-b.md`.
- [x] Partner `B` sin productos no ve flujo de inventario.
- [x] Ficha pública muestra productos y servicios cuando correspondan.

## Fases de implementación sugeridas

1. **Modelo y API productos** — `CostPrice`, `product_images`, DELETE producto, DTOs sin costo en público.
2. **Categorías de tienda** — CRUD secciones partner + FK en producto.
3. **Catálogo partner UI (tipo A)** — formulario completo, listado, stock, localización, fotos.
4. **Ficha pública comprador** — secciones, galería, sin stock, solo precio venta.
5. **Capacidad híbrida** — `OffersServices`, tabs en `Catalog.razor`, reutilizar flujo B.
6. **Reportes internos** (opcional) — margen, valor inventario a costo.
7. **Stock** — reserva con TTL, conciliación pedidos abandonados (ver [Pendiente](#pendiente--mejoras-futuras) en control de stock).

## Restricciones

- Reutilizar `Catalog.razor`, `PartnerLayout`, `LocaleService`, `PartnerApiClient`, `CompanySettings`.
- Cambios mínimos; no romper tipo `B` ni `C`.
- Estilo visual: verde `#3b6700`, cards redondeadas (alineado a tipo B).
- Reutilizar patrones probados de tipo B: `ServiceImageUpload`, `ServiceOfferingDetail` (galería), publicación en Ajuste de empresa.

## Prompt corto para el agente (copiar al implementar)

```text
Implementá el perfil Partner tipo A (comercio) según docs/empresa-tipo-a.md:

- Catálogo de productos con secciones propias del partner, stock, precio de venta público y precio de costo solo interno.
- Subida múltiple de imágenes y galería en ficha comprador.
- Publicación por producto (IsActive) y por negocio (IsVisible).
- Modo híbrido: partner A puede tener OffersServices y gestionar avisos con las reglas de docs/empresa-tipo-b.md en la misma UI /partner/catalog.
- Localizar con partner.catalog.a.*; no romper tipo B ni C.
- Seguir fases del doc: modelo → categorías tienda → UI partner → ficha comprador → híbrido.
```

## Relación con tipo B

| | Tipo A (comercio) | Tipo B (servicios) |
|--|-------------------|---------------------|
| Ítem principal | Producto | Aviso de servicio |
| Precio público | Venta | Tarifa / “consultar” |
| Precio interno | Costo | — |
| Inventario | Stock (reserva + descuento al pagar + alertas) | — |
| Agenda / pago | Solo si híbrido con servicios | Según modo aviso |
| Categorías locales | Secciones de tienda | Secciones / rubro aviso |

Ver también: `.cursor/rules/empresa-tipo-a.mdc`, `.cursor/rules/empresa-tipo-b.mdc`.
