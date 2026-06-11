# Regla de negocio: Empresa de servicios (Partner tipo B)

Documento de referencia para catálogo, avisos, agendamiento y reservas pagadas en ComunaClic.

## Tipos de partner

| Tipo | Código | Catálogo |
|------|--------|----------|
| Comercio | `A` | Productos con stock y precio — ver **`docs/empresa-tipo-a.md`** (puede sumar servicios en modo híbrido) |
| **Empresa de servicios** | **`B`** | **Avisos de servicio (este documento)** |
| Profesionales / vitrina | `C` | Perfiles tipo equipo profesional |

> Un comercio tipo `A` con capacidad **OffersServices** también publica avisos siguiendo este documento; el catálogo de productos e **inventario/stock** se define en **`docs/empresa-tipo-a.md`** (sección [Control de stock](empresa-tipo-a.md#control-de-stock)).

## Inventario y stock (qué aplica y qué no)

| Ámbito | Tipo `B` puro | Tipo `A` híbrido (`OffersServices`) |
|--------|---------------|-------------------------------------|
| **Avisos de servicio** | Sin `product_inventory`. Capacidad = **profesionales + slots + reservas** (este documento). | Igual que `B` en pestaña **Servicios**. |
| **Productos** | **No aplica** — sin pestaña productos ni stock. | Pestaña **Productos** con reglas de **`empresa-tipo-a.md`**: stock físico, disponible, reserva en pedidos pendientes, descuento al pagar, alertas `stock_low` / `stock_out`. |
| **Notificaciones** (`/partner/notifications`) | Pedidos, reservas, leads, pagos (según integraciones). | Lo anterior **+** alertas de stock del catálogo de productos. |

**Regla:** no implementar ni documentar lógica de `ProductInventory` en avisos tipo `B`. Si el agente trabaja en `Catalog.razor`, distinguir pestaña **Productos** (tipo A / híbrido) vs **Servicios** (tipo B).

## Archivos clave en el código

- UI catálogo: `src/ComunaClick.SharedUI/Pages/Partner/Catalog.razor`
- Ajuste empresa / cuenta bancaria: `Pages/Partner/CompanySettings.razor`, `PartnerBankSection.razor`
- API servicios: `src/ComunaClick.Api/Modules/Catalog/ServicesController.cs`
- Entidad servicio: `src/ComunaClick.Api/Persistence/Entities/Service.cs`
- Slots: `src/ComunaClick.Api/Persistence/Entities/ServiceSlot.cs`
- Reservas: `src/ComunaClick.Api/Persistence/Entities/Booking.cs`
- Profesionales: `src/ComunaClick.Api/Persistence/Entities/Professional.cs`
- DTOs: `src/ComunaClick.Shared/Api/Partner/PartnerApiClient.cs`
- Bandeja notificaciones (compartida con tipo A): `src/ComunaClick.SharedUI/Pages/Partner/Notifications.razor`
- Stock de productos (**solo tipo A / híbrido**, no avisos B): ver archivos en `docs/empresa-tipo-a.md` § Archivos clave

## Objetivo de negocio

Para **empresas de servicios (tipo B)**, el catálogo es una **vitrina editorial tipo ficha de empresa** (estilo LinkedIn): solo pueden crear **avisos/publicaciones de servicios** ligados a su actividad, organizados en **secciones o categorías** visibles en la ficha pública.

Muchas empresas operan como **locales con agenda por hora** (peluquería, uñas, estética, consultoría con turnos). El sistema debe soportar **dos modos de aviso**:

### Modo A — Aviso sin precio

- Publicación informativa en la ficha.
- **No** activa motor de agendamiento ni control de horas.
- El cliente contacta por mensajería u otros canales (inbox / WhatsApp).
- Debe poder **activarse/desactivarse**, **editarse** y **eliminarse**.

### Modo B — Aviso con precio (reservable)

- Precio definido por la empresa + duración del servicio.
- **Activa automáticamente** agendamiento + disponibilidad horaria.
- **Reservar hora exige pago en línea** (Mercado Pago / flujo de pagos existente).
- Si el cliente llega tarde o necesita cambio, puede **reagendar** porque **ya pagó** (respetar `CancellationPolicy` en `Booking`).
- Debe poder **activarse/desactivarse**, **editarse** y **eliminarse** (con reglas si hay reservas futuras pagadas).
- **Profesionales asignados (obligatorio):** al publicar un aviso reservable, la empresa debe marcar **qué profesional(es)** lo atienden. Cada uno seleccionado representa **capacidad paralela** a la misma hora (ej. 3 profesionales = hasta 3 turnos a las 10:00). Tabla `service_professionals`; UI en `/partner/catalog`.

**Regla derivada:** precio = 0 o null → sin agendamiento; precio > 0 → agendamiento + pago obligatorio para confirmar reserva + al menos un profesional del equipo asignado.

## Múltiples profesionales por local

Un mismo local puede tener **varios profesionales** en paralelo (ej. a las 10:00 una estilista ocupada y otra libre).

**Implicaciones técnicas:**

1. Vincular **servicio ↔ profesional(es)** y/o **slot ↔ ProfessionalId** (hoy `ServiceSlot` y `Booking` no tienen profesional asignado).
2. Disponibilidad por **profesional**, no solo capacidad global del slot.
3. UI partner: sección **Equipo** en `/partner/catalog` (registrar profesionales) + checkboxes por aviso reservable.
4. UI comprador: elegir profesional o “cualquiera disponible” según reglas del negocio.

Reutilizar `Professional` donde tenga sentido; no confundir catálogo tipo `B` (avisos) con tipo `C` (solo perfiles).

## Reglas de negocio obligatorias

1. Tipo `B`: **solo avisos de servicios** — sin flujo de productos ni inventario (`ProductInventory`); comercio con productos = tipo `A` (ver `empresa-tipo-a.md`).
2. Transición sin precio ↔ con precio: advertencia explícita en UI (impacto en reservas).
3. **Publicación:** `IsActive` = en línea / bajado (Activar/Desactivar).
4. **CRUD completo:** crear, editar, **eliminar** (soft-delete si hay historial; hard-delete solo sin dependencias).
5. **Secciones/categorías:** agrupar avisos en ficha pública (evitar `"CATEGORIA PENDIENTE"` permanente).
6. Banner si `Partner.IsVisible == false` pero hay avisos activos.
7. Textos vía `LocaleService` (`partner.catalog.*`), no strings sueltos en español.

## Experiencia UI deseada (`/partner/catalog`, tipo B)

**Header:** “Catálogo de servicios” / “Avisos de tu empresa”; subtítulo vitrina + reservas pagadas; CTA “Crear aviso de servicio”. Si el partner es **híbrido (`A` + `OffersServices`)**, la misma página muestra pestaña **Productos** (stock, ver tipo A) y pestaña **Servicios** (este documento).

**Formulario crear/editar:** nombre, descripción, categoría/sección, imagen, duración (si con precio), precio opcional; modo “Solo publicación” vs “Reservable con pago en línea”; si reservable: profesionales asignados + política reagendo; preview (`PartnerOfferPreviewCard`).

**Listado:** badges Activo/Pausado/Reservable/Solo consulta; acciones Vista previa | Editar | Activar/Desactivar | **Eliminar** (confirmación).

**Métricas:** total avisos, reservables, duración media (solo reservables), estado operación.

## Backend esperado

Extender modelo `Service` (o relacionado):

- `PricingMode` o equivalente: `ListingOnly` | `BookablePaid`
- `RequiresOnlinePayment` cuando sea reservable
- Tabla puente **Service ↔ Professionals**
- Categoría/sección con FK si aplica

Extender `ServiceSlot` / `Booking` con `ProfessionalId` opcional.

Endpoints: `DELETE` servicio con validación de reservas pagadas futuras; disponibilidad por servicio + profesional + fechas; booking confirmado solo tras pago.

Migraciones en `infra/migrations/` + `DatabaseSchemaBootstrap` si aplica.

## Criterios de aceptación

- [ ] Partner `B` solo ve avisos de servicio (no productos ni campos de stock).
- [ ] Partner `B` puro no recibe alertas `stock_out` / `stock_low` (solo aplica a productos tipo A).
- [ ] Partner `A` híbrido: avisos cumplen criterios B; productos cumplen criterios de stock en `empresa-tipo-a.md`.
- [ ] Sin precio: ficha visible, sin calendario al comprador.
- [ ] Con precio: calendario, slots por profesional, checkout obligatorio.
- [x] Al publicar reservable: asignar ≥1 profesional del equipo (capacidad paralela = cantidad asignada).
- [ ] No doble booking del mismo profesional en el mismo horario; sí paralelo entre profesionales.
- [ ] Activar/desactivar reflejado en ficha pública.
- [ ] Editar y eliminar con confirmaciones y errores claros.
- [ ] Reagendo post-pago según política.
- [ ] ES/EN en `LocaleService`.
- [x] Cuenta bancaria en Ajuste de empresa para recibir pagos de reservas.
- [ ] Build OK; migración aplicable en dev.

## Fases de implementación sugeridas

1. **Modelo y API** — migración, flags bookable, servicio–profesional, DELETE.
2. **Catálogo partner UI** — formulario dual, listado, eliminar, copy.
3. **Disponibilidad y reservas** — slots por profesional, checkout, reagendo.
4. **Ficha pública comprador** — secciones; CTA Consultar vs Reservar y pagar.

## Restricciones

- Reutilizar `Catalog.razor`, `PartnerLayout`, `LocaleService`, `PartnerApiClient`.
- Cambios mínimos; no romper tipos `A` ni `C`.
- Estilo visual: verde `#3b6700`, cards redondeadas.

## Relación con tipo A (comercio)

| | Tipo B (servicios) | Tipo A (productos) |
|--|-------------------|---------------------|
| Ítem de catálogo | Aviso (`Service`) | Producto (`Product`) |
| “Capacidad” | Slots × profesionales asignados | Stock (`product_inventory`) |
| Pago comprador | Reserva de turno (modo reservable) | Pedido / checkout producto |
| Alertas operativas | Reservas, leads, pedidos según flujo | **+** `stock_low`, `stock_out` en notificaciones |
| Documento | Este archivo | `docs/empresa-tipo-a.md` |

Ver también: `.cursor/rules/empresa-tipo-b.mdc`, `.cursor/rules/empresa-tipo-a.mdc`.
