# ComunaClic — Objetivo del Proyecto y Ramas de Negocio

> Documento maestro que une la **visión estratégica** del proyecto con el **estado real implementado** en la plataforma (Web/App Blazor + API .NET 8 + PostgreSQL, arquitectura multi-tenant por comuna).
>
> Sirve como referencia para completar y mejorar la idea, alinear objetivos y priorizar próximos pasos por cada tipo de usuario.

---

## 1. Objetivo del Proyecto

**ComunaClic** es una plataforma digital (Web/App) de **hiper-localización** diseñada para ser la solución integral de digitalización de **Pequeñas y Medianas Empresas (PyMES)** y **Profesionales Independientes** que operan dentro de una comunidad geográfica específica en Chile y Latinoamérica.

Cada instancia de la plataforma se configura para una **comuna o ciudad** (modelo multi-tenant), generando un **efecto de red hiper-local**: a mayor cantidad de socios y transacciones en un territorio, mayor atracción de nuevos usuarios y menor costo de adquisición.

### 1.1 Problemas que resuelve

| # | Problema estructural | Cómo lo aborda ComunaClic |
|---|----------------------|---------------------------|
| 1 | **Invisibilidad digital** de comercios locales frente a grandes plataformas nacionales | Descubrimiento hiper-local por comuna, búsqueda y mapa por categoría, ficha pública por negocio/producto/servicio/profesional |
| 2 | **Ausencia de pago digital integrado** para PyMES de bajo volumen | Integración con pasarela (Mercado Pago / multi-provider), checkout y reservas con pago anticipado, base de payouts/liquidaciones |
| 3 | **Gestión ineficiente de reputación y "boca a boca"** local | CRM básico, mensajería interna (buzón), leads y handoff a WhatsApp |
| 4 | Comisiones altas (20%–35%) de grandes apps de delivery | Comisiones competitivas 10%–18% (productos) y 3%–5% (servicios) |
| 5 | Falta de una solución que **integre productos + servicios + directorio** | Tres segmentos (A, B, C) en una sola aplicación |

### 1.2 Propuesta de valor central

La plataforma centraliza en una sola aplicación tres flujos de negocio hoy fragmentados:

| Segmento | A — PyMES de Productos | B — PyMES de Servicios | C — Profesionales Independientes |
|----------|------------------------|------------------------|----------------------------------|
| **Núcleo** | Marketplace local con delivery | Agenda con pago anticipado | Directorio premium |
| **Modelo de ingreso** | Comisión **5%–18%** | Comisión **3%–5%** | Suscripción, **0% comisión** |
| **Capacidad clave** | Catálogo + stock + checkout | Avisos + slots + reservas pagadas | Perfil + generación de leads |

---

## 2. Mercado y Escalabilidad (resumen estratégico)

| Dimensión | Detalle |
|-----------|---------|
| Mercado principal | PyMES y profesionales independientes de Chile (98,3% del total de empresas) |
| Mercado potencial | Expansión a Latinoamérica (Perú, Colombia, México) |
| E-commerce Chile | > USD $11.500 millones, CAGR proyectado > 11% hasta 2030 |
| Ventaja competitiva | Comisiones 10%–18% vs. 20%–35% de grandes apps |

### Modelo de densidad progresiva

| Plazo | Foco | Socios meta | Proyección |
|-------|------|-------------|------------|
| Corto (0–12 m) | Validación y densidad en 1–2 comunas piloto | 150–250 | USD -12.000 anual (inversión) |
| Mediano (12–36 m) | Expansión a 5–10 comunas/ciudades | 1.500–2.500 | USD +660.000 anual |
| Largo (36+ m) | Expansión nacional + Latinoamérica | 5.000+ | USD +1.740.000 anual |

---

## 3. Objetivos del Proyecto

### 3.1 Objetivo general

Desarrollar, validar y escalar una plataforma digital hiper-local que integre **marketplace de productos, reserva de servicios y directorio profesional** en una sola aplicación, posicionando a ComunaClic como el ecosistema digital de referencia para PyMES y profesionales independientes en Chile, con visión de expansión latinoamericana.

### 3.2 Objetivos estratégicos

| ID | Objetivo | Meta |
|----|----------|------|
| **OE1** | Validación de modelo y densidad local | 150–250 socios activos en comuna(s) piloto en 12 meses (≈40% A, 30% B, 30% C) |
| **OE2** | Punto de equilibrio financiero | Break-even al superar 2.000 socios; utilidad bruta anual > USD $660.000 |
| **OE3** | Escalabilidad geográfica replicable | Arquitectura + go-to-market que replique por comuna con mínimo costo fijo incremental |
| **OE4** | Posicionamiento competitivo por comisión | 10%–18% (A) y 3%–5% (B) para incentivar migración desde plataformas caras |
| **OE5** | Efecto red hiper-local | Masa crítica por comuna que reduzca CAC y acelere crecimiento orgánico |

---

## 4. Ramas de Negocio por Tipo de Usuario

Resumen de los tres segmentos de socios (partners) y el comprador/usuario final.

| Código | Segmento | Catálogo principal | Modelo de ingreso | Documento detalle |
|--------|----------|--------------------|-------------------|-------------------|
| **A** | Empresa de venta de productos | Productos con stock y precio | Comisión 5%–18% | `docs/empresa-tipo-a.md` |
| **B** | Empresa de servicios | Avisos de servicio (con/sin reserva) | Comisión 3%–5% | `docs/empresa-tipo-b.md` |
| **C** | Profesional independiente | Perfil/directorio | Suscripción, 0% comisión | (este documento) |
| — | Comprador / usuario final | — | — | — |

> **Nota:** un usuario puede combinar roles. Una **empresa** también puede comprar como cliente, y un **profesional** mantiene su rol comprador. Los **buzones de mensajería están separados** por contexto (ver §6).

---

### 4.1 Segmento A — Empresa de venta de productos

**Quién es:** pymes, retail, alimentos, ferreterías, emprendimientos con inventario físico o digital que **venden productos** en su barrio.

**Objetivo de negocio:** que el catálogo funcione como una **tienda en la ficha pública**, con productos agrupados por secciones del local, stock y precio de venta visible al comprador, con checkout y delivery local.

#### Estado implementado

| Capacidad | Estado |
|-----------|--------|
| CRUD de productos en `/partner/catalog` | ✅ Crear, listar, editar, activar/desactivar, eliminar |
| Stock real (`product_inventory`) con reserva y descuento al pagar | ✅ Implementado |
| Alertas `stock_low` / `stock_out` en notificaciones | ✅ Implementado |
| Precio de venta público + precio de costo solo interno | ✅ Implementado |
| Múltiples fotos por producto + galería en ficha | ✅ Implementado (subida desde cámara/galería) |
| Categorías propias del local (secciones de tienda) | ✅ Implementado |
| Multi-categoría marketplace + geo por producto | ✅ Implementado |
| Modo híbrido (producto + servicios con `OffersServices`) | ✅ Implementado |
| Cuenta bancaria para liquidaciones (`PartnerBankSection`) | ✅ Implementado |
| Checkout comprador con límite de stock | ✅ Implementado |

#### Por mejorar / completar

- Liberación automática de reservas por timeout (pedidos `payment_pending` abandonados).
- Reporte de valor de inventario a costo (`CostPrice × Quantity`).
- Alertas de stock por email/WhatsApp además de la bandeja in-app.
- Profundizar métricas de ventas y márgenes en el dashboard.

---

### 4.2 Segmento B — Empresa de servicios

**Quién es:** peluquerías, estética, consultoras, empresas con atención por turnos o por proyecto. El catálogo es una **vitrina editorial tipo ficha de empresa**.

**Objetivo de negocio:** publicar **avisos de servicio** en dos modos y, cuando corresponda, activar **agenda con reserva pagada** para reducir No-Shows.

#### Modos de aviso

| Modo | Descripción | Agenda/Pago |
|------|-------------|-------------|
| **Sin precio** | Publicación informativa; el cliente contacta por inbox/WhatsApp | No activa agenda |
| **Con precio (reservable)** | Precio + duración + profesionales asignados | Activa slots y **exige pago en línea** para confirmar |

#### Estado implementado

| Capacidad | Estado |
|-----------|--------|
| Catálogo de avisos en `/partner/catalog` (tipo B) | ✅ Implementado |
| Imágenes, dirección y geo por servicio | ✅ Implementado |
| Asignación de profesionales a aviso reservable (capacidad paralela) | ✅ Implementado |
| Cuenta bancaria para recibir pagos de reservas | ✅ Implementado |
| Geo del servicio y "usar mi ubicación" | ✅ Implementado |
| Storefront / vitrina pública del negocio | ✅ Implementado |

#### Por mejorar / completar

- Slots por profesional con **disponibilidad real** y bloqueo de doble booking del mismo profesional.
- Checkout obligatorio de reserva (modo reservable) end-to-end y reagendo post-pago según política.
- Calendario al comprador en modo reservable.
- Profundizar módulo **Agenda** con estados accionables y filtros.
- Cobertura ES/EN completa de strings restantes.

---

### 4.3 Segmento C — Profesional independiente

**Quién es:** profesionales que ofrecen su perfil de forma estructurada (consultores, oficios, especialistas) buscando **leads de alto valor**, con modelo de **suscripción y 0% comisión**.

**Objetivo de negocio:** directorio profesional de prestigio donde el profesional gestiona su perfil, visibilidad (activo/pausado) y **recibe consultas separadas** de su actividad como comprador.

#### Estado implementado

| Capacidad | Estado |
|-----------|--------|
| Perfil profesional en `/account/professional` (especialidad, bio, teléfono, redes) | ✅ Implementado |
| Activar / pausar perfil (visibilidad en directorio) | ✅ Implementado (`IsActive`) |
| Estado verificado / pendiente | ✅ Implementado |
| Pestaña "Profesional" en menú y sidebar del centro de cuenta | ✅ Implementado |
| **Buzón profesional separado** (`/account/professional/messages`) | ✅ Implementado |
| API de consultas al perfil (`GET /v1/inbox/threads/as-professional`) | ✅ Implementado |
| Detección de perfil profesional para mostrar pestañas | ✅ Implementado |

#### Por mejorar / completar

- **Suscripción / plan premium** como fuente de ingreso (modelo C aún sin cobro activo).
- Programa "Destacado en ComunaClic" como ingreso adicional.
- Métricas de leads recibidos y conversión por perfil.
- Vista previa pública del perfil profesional desde la edición.
- Verificación/validación de identidad profesional.

---

### 4.4 Comprador / usuario final

**Quién es:** vecino que busca productos, servicios o profesionales cercanos.

#### Estado implementado

- Registro y login (cuenta personal, OAuth Google), sesión con JWT + refresh.
- Navegación pública por categorías, detalle de oferta, búsqueda hiper-local con contexto de comuna.
- Compra de productos (carrito + checkout), reserva de servicios y contacto/lead.
- **Buzón de mensajería del comprador** (`/account/messages`) con handoff a WhatsApp.
- Favoritos, perfil con dirección de despacho, foto de perfil.

#### Por mejorar / completar

- Reseñas / reputación de negocios y profesionales.
- Historial de compras y reservas más completo.
- Notificaciones push / email de seguimiento.

---

## 5. Multi-perfil: una cuenta, varios roles

Un mismo usuario puede ser **comprador** y, a la vez, **empresa** o **profesional**. El sistema separa los contextos:

| Rol | Acceso | Mensajería |
|-----|--------|------------|
| Comprador | `/account/profile`, `/account/buyer` | `/account/messages` (consultas que **envía**) |
| Empresa (A/B) | Menú/sidebar **Empresa** → `/partner/settings` o `/account/company`; panel `/partner` | `/partner/messages` (consultas del negocio) |
| Profesional (C) | Sidebar **Perfil profesional** → `/account/professional` | `/account/professional/messages` (consultas que **recibe**) |

**Detección de rol:** combinación de rol en `localStorage` (`commerce` / `professional`), negocios asociados (`ListMyPartnersAsync`), `IsPartnerAccount` (JWT con `PartnerId`) y existencia de perfil profesional en API.

---

## 6. Separación de mensajería (buzones)

| Buzón | Ruta | Contenido | API |
|-------|------|-----------|-----|
| Comprador | `/account/messages` | Consultas que el usuario **envía** como cliente | `GET /v1/inbox/threads/mine` |
| Profesional | `/account/professional/messages` | Consultas que **recibe** en su perfil | `GET /v1/inbox/threads/as-professional` |
| Negocio | `/partner/messages` | Consultas del panel comercial (partner + profesionales vinculados) | `GET /v1/inbox/threads/partner/{id}` |

Las consultas de **compras no se mezclan** con las de **ventas/servicios** ni con las del **perfil profesional**. El buzón es responsive (vista tipo app en móvil) y permite handoff a WhatsApp.

---

## 7. Estado general de la plataforma

### Implementado y en producción

- Autenticación y autorización (ACL dedicado, roles `platform_admin`, `tenant_admin`, `partner_owner`, `partner_staff`, `customer`).
- Onboarding de negocios A/B/C con checklist de activación.
- Gestión multi-negocio por usuario y contexto de partner activo.
- Catálogo de productos (con stock), servicios (con profesionales) y perfiles profesionales.
- Publicación en dos niveles (ítem `IsActive` + negocio `IsVisible`).
- Búsqueda pública hiper-local, mapa por categoría con fallback de geolocalización.
- Pedidos, reservas y leads.
- Mensajería interna separada por rol + WhatsApp.
- Base de pagos (Mercado Pago / multi-provider) y payouts.
- CRM básico e interacciones.
- Dashboard partner con métricas base.
- Multi-tenant por comuna; UI Web Blazor + base MAUI Blazor Hybrid.

### Próximas mejoras priorizadas (transversal)

1. **Pagos end-to-end** robustos en productos y reservas + liquidaciones automáticas a socios.
2. **Agenda real** por profesional (disponibilidad, anti doble-booking, reagendo).
3. **Suscripción Segmento C** y programa de destacados como ingresos.
4. **Reputación / reseñas** para activar el "boca a boca" digital.
5. **CRM Pro** como ingreso complementario (mediano plazo).
6. Seguridad: rotación de secretos productivos y verificación antiabuso (ver `docs/pendientes_por_fases_comunaclic.md`).
7. Integración futura con **facturación electrónica SII** (largo plazo).

---

## 8. Documentos relacionados

| Tema | Archivo |
|------|---------|
| Reglas empresa tipo A (productos) | `docs/empresa-tipo-a.md` |
| Reglas empresa tipo B (servicios) | `docs/empresa-tipo-b.md` |
| Capacidades y beneficios | `docs/capacidades_y_beneficios_comunaclic.md` |
| Pendientes por fases | `docs/pendientes_por_fases_comunaclic.md` |
| Registro tres perfiles | `docs/pruebas_manuales_registro_tres_perfiles.md` |
| Integración Mercado Pago | `docs/plan_etapas_integracion_mercadopago_comunaclic.md` |
| Release y rollback | `docs/release_y_rollback_por_servicio_comunaclic.md` |

---

_Última actualización: 31 de mayo de 2026._
