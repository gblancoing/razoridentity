# ComunaClic — Manual global de módulos por perfil de usuario

> **Última actualización:** 2026-06-12
> Documento maestro: describe la plataforma, sus aplicaciones, y los módulos
> disponibles para cada tipo de perfil de usuario. Para el detalle del flujo de
> delivery ver [`proceso_compra_venta_delivery.md`](proceso_compra_venta_delivery.md).

---

## 1. Visión general

ComunaClic es un marketplace local multi-negocio con pagos vía Mercado Pago
(modelo marketplace: cada negocio cobra a su propia cuenta y ComunaClic retiene
una comisión como `application_fee`) y servicio de **delivery** integrado
(cotización por distancia, seguimiento en vivo y liquidación al repartidor).

### Aplicaciones y dominios

| App | Dominio | Para quién |
|-----|---------|-----------|
| **Portal público + cuentas** | app.comunaclic.cl | Compradores, comercios, profesionales, repartidores |
| **Autenticación (ACL)** | acl.comunaclic.cl | Login/registro (backend; sin UI propia para el usuario final) |
| **API** | api.comunaclic.cl | Backend de datos y lógica (no se navega directo) |
| **Panel de administración** | admin.comunaclic.cl | Administradores de plataforma |

### Perfiles de usuario

| Perfil | Rol técnico | Se registra como | Entidad que crea |
|--------|-------------|------------------|------------------|
| **Comprador** | customer | "Personal" (`/register?role=natural`) | Customer |
| **Comercio** | partner_owner | "Comercio" (`/register?role=commerce`) | Partner tipo A |
| **Profesional** | partner_owner | "Profesional" (`/register?role=professional`) | Partner tipo C + Professional |
| **Repartidor** | customer + vínculo Courier | "Repartidor" (`/register?role=courier`) | reclama un Courier por email |
| **Administrador** | platform_admin | (creado por seed / otro admin) | — |

> Nota: el repartidor usa una cuenta normal (rol customer); su condición de
> repartidor se deriva de los perfiles `Courier` que el comercio registró con su
> correo. No requiere cambios en el sistema de roles.

---

## 2. Módulos transversales (todo el portal público)

Disponibles para cualquier visitante (con o sin sesión):

- **Inicio / Home** (`/`) — vitrina y accesos.
- **Comercio / Categorías** (`/categorias`) — catálogo por categoría.
- **Servicios** (`/services`, `/services-near`) — avisos de servicios y profesionales.
- **Profesionales** (`/professionals`).
- **Ofertas** (`/ofertas`).
- **Ficha de detalle** (`/buyer/detail/{tipo}/{id}`) — producto, servicio, profesional o negocio.
- **Carrito** (`/buyer/cart`) — ícono con contador en el header, multi-negocio.
- **Idioma** ES/EN, conmutable en el header.

---

## 3. Comprador (perfil personal)

**Registro:** `/register` → tarjeta "Personal". **Menú:** Centro de cuenta (`/account`).

### Módulos
- **Comprador** (`/account/buyer`, `/buyer/orders`) — pedidos y seguimiento.
- **Carrito y checkout** (`/buyer/cart`, `/buyer/checkout`):
  - Carrito **multi-negocio** (un pedido + un pago por cada comercio).
  - Checkout con **cotización de envío en vivo** (a domicilio / retiro / por pagar).
  - Pago con **Mercado Pago** (total con envío incluido).
- **Seguimiento en vivo** (`/track/{orderId}`) — mapa con local, destino y repartidor.
- **Favoritos** (`/buyer/favorites`) — zonas y negocios guardados.
- **Buzón de mensajes** (`/account/messages`) — consultas con negocios y profesionales.
- **Perfil** (`/account/profile`, `/buyer/profile`) — datos y ubicación.
- **Reservas** (`/buyer/booking`) — para servicios con agenda.

---

## 4. Comercio (Partner tipo A)

**Registro:** `/register` → "Comercio" → onboarding de negocio.
**Panel:** `/partner` (layout propio con sidebar).

### Módulos (sidebar del panel)
- **Dashboard** (`/partner`) — control de empresa, KPIs.
- **Catálogo** (`/partner/catalog`) — productos y servicios, stock.
- **Mi página / Storefront** (`/partner/storefront`) — banner, textos, calidad del perfil.
- **Agenda** (`/partner/agenda`) — reservas con horario (si ofrece servicios reservables).
- **Pedidos** (`/partner/orders`) — **núcleo de ventas y delivery**:
  - Ventas, estado de pago, confirmar/cancelar.
  - **Mis repartidores**: alta de repartidores (nombre, teléfono, **correo**), con
    badges "Cuenta creada" y "MP conectado/sin conectar".
  - **Asignación de repartidor** + link seguro por WhatsApp.
  - **Trazabilidad del envío** (línea de tiempo) + **mapa en vivo**.
  - **Agradecer entrega por WhatsApp** (mensaje programado).
  - **Liquidaciones de envío** (tabla): desglose producto vs transporte, comisiones,
    neto al repartidor, y pago vía Mercado Pago o manual.
- **Leads** (`/partner/leads`) — contactos/consultas accionables.
- **Payouts** (`/partner/payouts`) — liquidaciones/retiros del negocio.
- **Mercado Pago** (`/partner/mercadopago`) — vinculación de la cuenta de cobro del negocio.
- **Notificaciones** (`/partner/notifications`).
- **Buzón de mensajes** (`/partner/messages`).
- **Ajuste de empresa** (`/partner/settings`) — datos organizacionales, cuenta bancaria,
  **transportista de despacho preferido**, equipo y publicación.

---

## 5. Profesional (Partner tipo C)

**Registro:** `/register` → "Profesional". **Menú:** Centro de cuenta.

### Módulos
- **Panel profesional** (`/account/professional`) — perfil, especialidad, bio,
  foto, banner, certificaciones, enlaces web/redes, contador de vistas.
- **Mensajes del profesional** (`/account/professional/messages`).
- Comparte la base de Partner (puede tener panel de negocio si ofrece catálogo/agenda).

---

## 6. Repartidor (Courier) — portal propio

**Registro:** `/register` → "Repartidor" con el **mismo correo** que el comercio registró.
**Menú:** Centro de cuenta → **Panel repartidor** (`/account/courier`).

### Módulos
- **Mis viajes** — entregas activas y completadas, estado y neto por viaje.
- **Mis ganancias** — Pendiente de pago / Pagado / Pago directo + movimientos recientes.
- **Cuenta Mercado Pago** — **vincular / desvincular** su cuenta para cobrar los envíos.
- **Vista de entrega activa** (`/courier/delivery/{orderId}` vía link por WhatsApp) —
  mapa de contexto, navegación a Google Maps/Waze, GPS en vivo y botones de estado
  (recogí / entregué). No requiere cuenta para esta vista (opera por token seguro).

---

## 7. Administrador de plataforma

**Acceso:** admin.comunaclic.cl (login propio, rol `platform_admin`).

### Módulos (panel Admin)
- **Dashboard** (`/`) — resumen de KPIs.
- **Usuarios** (`/usuarios`) — activar/desactivar y resetear clave de cuentas ACL.
- **Tenants** (`/tenants`) — comunas/territorios.
- **Partners** (`/partners`) — gestión de negocios.
- **Categorías** (`/categorias`) — categorías del catálogo.
- **Transportistas de delivery** (`/delivery-providers`) — **alta de transportistas
  por zona** (comuna/región/global) con su tarifa base; define el costo de envío.
- **Liquidaciones de envío** (admin) — marcar como "liquidada" los pagos manuales.
- **Contenido** (`/contenido`) — contenido público del sitio.
- **Pagos** (`/pagos`) — dashboard de pasarela Mercado Pago.
- **Auditoría** (`/auditoria`) — registro de eventos.
- **Configuración** (`/configuracion`) — ajustes de plataforma.

---

## 8. Módulo de Delivery (resumen)

| Etapa | Quién | Dónde |
|-------|-------|-------|
| Definir tarifas por zona | Admin | `/delivery-providers` |
| Elegir transportista preferido | Comercio | `/partner/settings` → Despacho |
| Cotizar envío | Comprador | Checkout (automático) |
| Pagar (productos + envío) | Comprador | Mercado Pago |
| Asignar repartidor + tracking | Comercio | `/partner/orders` |
| Seguir la entrega en vivo | Comprador y Comercio | mapa |
| Cobrar el envío | Repartidor | `/account/courier` (vincula su MP) |
| Pagar el envío al repartidor | Comercio | `/partner/orders` → Liquidaciones |
| Liquidar pagos manuales | Admin | Admin → liquidaciones |

Detalle completo: [`proceso_compra_venta_delivery.md`](proceso_compra_venta_delivery.md).

---

## 9. Pagos y comisiones (Mercado Pago marketplace)

- El **comprador paga una vez** al comercio (productos + envío). El comercio recibe el
  neto del producto; ComunaClic retiene su comisión como `application_fee`.
- Tras la entrega, el **comercio paga el envío al repartidor**: el neto cae en la cuenta
  MP del repartidor y ComunaClic retiene su comisión del transporte. Si el repartidor
  no tiene MP conectado, el pago es **manual** (transferencia/efectivo) y un admin lo
  marca liquidado.
- **Reserva y expiración:** un pedido sin pagar reserva stock y expira a los 5 minutos
  (libera el stock); el pago confirmado siempre gana ante la expiración.
