# Backlog Ejecutable Por Sprint - ComunaClic

Fecha: 2026-04-19

## Objetivo

Traducir el roadmap funcional de `plan_nuevas_funcionalidades_comunaclic_2026-04.md` a un backlog ejecutable por sprint, con tareas concretas separadas por servicio:

- `api`
- `app`
- `acl`
- `admin`

## Supuestos de planificación

- Base actual ya validada públicamente en producción para `app`, `api` y `acl`.
- Los checks autenticados QA todavía no están cerrados al 100%, por lo que cada sprint debe dejar smoke funcional mínimo y pruebas manuales guiadas.
- La prioridad es construir valor de producto sin romper el flujo buyer/partner ya desplegado.

## Sprint 1 - Favoritos Buyer

Objetivo: permitir que buyer guarde y recupere ofertas relevantes.

Plan técnico detallado:

- [sprint1_favoritos_plan_tecnico_archivo_por_archivo_2026-04.md](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/docs/sprint1_favoritos_plan_tecnico_archivo_por_archivo_2026-04.md)

### `api`

- Crear modelo de favoritos buyer para:
  - partner
  - producto
  - servicio
  - profesional
- Definir endpoints:
  - `GET /v1/buyer/favorites`
  - `POST /v1/buyer/favorites`
  - `DELETE /v1/buyer/favorites/{id}`
- Validar ownership por `customerId` y usuario autenticado.
- Evitar duplicados por tipo de recurso.
- Incluir metadata mínima para render rápido en app:
  - nombre
  - tipo
  - imagen principal/fallback
  - partnerId o resourceId
  - comuna/categoría si existe

### `app`

- Agregar CTA “Guardar” o “Favorito” en:
  - fichas públicas
  - cards de discovery
  - detalle de producto/servicio/profesional
- Crear vista buyer “Mis favoritos”.
- Agregar estados visuales:
  - guardado
  - removido
  - vacío
  - error
- Agregar acceso rápido desde perfil buyer o navegación autenticada.

### `acl`

- Confirmar que el scope/rol `customer` puede operar favoritos autenticados sin exponer endpoints a otros roles por accidente.
- Ajustar permisos si la convención actual exige política explícita.

### `admin`

- Sin cambios funcionales obligatorios en este sprint.
- Solo dejar considerado un futuro filtro de soporte para favoritos si más adelante hiciera falta.

### Criterio de cierre

- buyer autenticado puede guardar, listar y quitar favoritos desde la UI.

### Publicación esperada

- `api`
- `app`
- `acl` solo si cambia policy

## Sprint 2 - Reseñas y Confianza

Objetivo: agregar prueba social y señales de credibilidad.

### `api`

- Definir entidad de reseñas con:
  - rating
  - comentario
  - tipo de recurso reseñado
  - referencia a compra/reserva/lead permitido
  - estado moderable
- Definir regla de elegibilidad:
  - compra completada
  - reserva completada
  - opcionalmente lead cerrado, si negocio lo requiere
- Crear endpoints:
  - `GET` reseñas públicas por recurso
  - `POST` reseña autenticada
  - `GET` resumen agregado de rating
- Agregar flags de confianza derivados:
  - cantidad de reseñas
  - rating promedio
  - negocio verificado si ya existe bandera disponible

### `app`

- Mostrar summary de reseñas en cards y fichas públicas.
- Crear formulario de reseña post compra/reserva elegible.
- Agregar bloque de reseñas en detalle público.
- Agregar badges de confianza visibles sin ruido:
  - verificado
  - respuesta rápida
  - mejor evaluado

### `acl`

- Verificar que solo buyer autenticado elegible pueda emitir reseña.
- Revisar que partner/admin no puedan suplantar reseñas buyer.

### `admin`

- Crear backlog técnico de moderación futura:
  - listar reseñas
  - ocultar/publicar
  - marcar abuso
- Si alcanza el sprint, exponer un listado simple solo interno.

### Criterio de cierre

- reseñas visibles en público y creación controlada para buyer elegible.

### Publicación esperada

- `api`
- `app`
- `admin` opcional

## Sprint 3 - Leads Accionables

Objetivo: convertir leads en flujo de trabajo real para el partner.

Plan técnico detallado:

- [sprint3_leads_accionables_plan_tecnico_archivo_por_archivo_2026-04.md](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/docs/sprint3_leads_accionables_plan_tecnico_archivo_por_archivo_2026-04.md)

### `api`

- Extender entidad/contrato de leads con:
  - estado comercial
  - prioridad
  - responsable
  - fecha de próximo seguimiento
  - notas internas
  - motivo de cierre ganado/perdido
- Crear endpoints de acción:
  - cambiar estado
  - asignar responsable
  - guardar nota
  - agendar seguimiento
- Mantener ownership estricto por partner.
- Exponer agregados simples para KPIs de leads.

### `app`

- Agregar acciones rápidas en [Leads.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Pages/Partner/Leads.razor:1):
  - `Contactar`
  - `Seguimiento`
  - `Ganado`
  - `Perdido`
- Crear drawer/modal liviano para editar estado, responsable y nota.
- Agregar filtros por:
  - estado
  - prioridad
  - responsable
  - vencimiento de seguimiento
- Mejorar KPIs del módulo con:
  - abiertos
  - sin seguimiento
  - ganados
  - perdidos

### `acl`

- Revisar acceso para `partner_owner` y `partner_staff` según política vigente.
- Confirmar que un partner no pueda leer ni editar leads ajenos.

### `admin`

- Sin cambios funcionales obligatorios.
- Dejar gancho para auditoría futura de cambios de estado si se requiere.

### Criterio de cierre

- partner puede mover un lead por etapas sin salir de ComunaClic.

### Publicación esperada

- `api`
- `app`
- `acl` si cambian policies

## Sprint 4 - Agenda Operativa

Objetivo: permitir gestión diaria de reservas desde la agenda.

Plan técnico detallado (incluye avance de payouts/notificaciones del mismo bloque):

- [sprint4_5_agenda_payouts_notificaciones_plan_tecnico_2026-04.md](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/docs/sprint4_5_agenda_payouts_notificaciones_plan_tecnico_2026-04.md)

### `api`

- Extender acciones de booking:
  - confirmar
  - reprogramar
  - completar
  - marcar `no_show`
  - cancelar con motivo
- Guardar notas internas por reserva.
- Exponer historial mínimo de cambios si el contrato ya lo permite sin mucha fricción.

### `app`

- Agregar acciones operativas en [Agenda.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Pages/Partner/Agenda.razor:1).
- Crear flujo de reprogramación simple con selección de nueva fecha/hora.
- Mejorar lectura temporal:
  - hoy
  - próximas 24h
  - esta semana
- Agregar estados vacíos y errores por acción.
- Si el diseño lo soporta, agregar una vista calendario liviana o timeline semanal.

### `acl`

- Verificar permisos por partner para mutar bookings propios.

### `admin`

- Sin cambios funcionales obligatorios.

### Criterio de cierre

- partner puede gestionar el ciclo básico de una reserva desde agenda.

### Publicación esperada

- `api`
- `app`

## Sprint 5 - Payouts y Exportación

Objetivo: mejorar claridad financiera y capacidad de conciliación básica.

### `api`

- Exponer desglose por payout:
  - orden/reserva asociada
  - bruto
  - comisión
  - neto
  - fecha
  - estado
- Soportar filtros por rango de fechas y estado.
- Agregar endpoint de exportación CSV o payload apto para export client-side.

### `app`

- Mejorar [Payouts.razor](/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick/src/ComunaClick.SharedUI/Pages/Partner/Payouts.razor:1) con:
  - tabla o lista detallada
  - filtros por estado/fecha
  - resumen por periodo
  - descarga CSV
- Reforzar empty states y explicación de estados financieros.

### `acl`

- Confirmar acceso solo a datos financieros del partner autenticado.

### `admin`

- Considerar necesidad de vista de conciliación interna futura, sin implementarla aún.

### Criterio de cierre

- partner puede entender qué se liquidó, qué falta y exportarlo.

### Publicación esperada

- `api`
- `app`

## Sprint 6 - Embudo y Salud del Negocio

Objetivo: que el partner vea resultados, no solo operación.

### `api`

- Crear agregados analíticos básicos:
  - vistas
  - contactos
  - reservas
  - compras
  - conversión simple
- Agregar consultas por:
  - periodo
  - tipo de oferta
  - partner
- Exponer alertas derivadas:
  - leads sin seguimiento
  - caída de conversión
  - baja actividad reciente

### `app`

- Crear dashboard partner con embudo simple.
- Mostrar alertas accionables.
- Agregar comparativa básica:
  - últimos 7 días
  - últimos 30 días

### `acl`

- Sin cambios funcionales probables.

### `admin`

- Considerar vista agregada platform-side en sprint posterior.

### Criterio de cierre

- partner puede entender actividad y resultado reciente desde un tablero básico.

### Publicación esperada

- `api`
- `app`

## Sprint 7 - Promociones y Cupones Simples

Objetivo: habilitar un primer motor liviano de activación comercial.

### `api`

- Crear entidad de promoción/cupón con:
  - código
  - tipo de descuento
  - valor
  - vigencia
  - recurso aplicable
  - partner owner
- Validar uso por buyer y reglas mínimas.
- Integrar cálculo en flujo que corresponda:
  - compra
  - reserva, si aplica

### `app`

- Crear gestión partner de promociones simples.
- Mostrar cupones activos en fichas públicas elegibles.
- Permitir ingreso de cupón en checkout o reserva si ya existe paso adecuado.

### `acl`

- Verificar scopes para creación/edición de promociones partner.

### `admin`

- Sin cambios obligatorios; dejar pendiente moderación de campañas abusivas.

### Criterio de cierre

- partner puede crear una promo básica y buyer puede usarla.

### Publicación esperada

- `api`
- `app`

## Sprint 8 - Moderación y Soporte Admin

Objetivo: dar herramientas internas para operar el marketplace.

### `api`

- Exponer endpoints administrativos para:
  - moderar reseñas
  - pausar publicación
  - marcar negocio verificado
  - buscar partner/customer/order/booking
- Agregar auditoría mínima en acciones sensibles.

### `app`

- Sin cambios funcionales obligatorios, salvo badges derivados si admin verifica negocio.

### `acl`

- Endurecer permisos de `platform_admin` y scopes asociados a moderación.

### `admin`

- Implementar vistas base:
  - listado de reseñas
  - búsqueda operacional
  - verificación de negocio
  - pausa de publicación

### Criterio de cierre

- equipo interno puede moderar y asistir sin tocar base de datos manualmente.

### Publicación esperada

- `api`
- `acl`
- `admin`
- `app` opcional si cambia badge público

## Tareas transversales por sprint

Cada sprint debería incluir además:

- smoke manual buyer/partner del flujo afectado
- actualización de quality checks si el nuevo flujo lo amerita
- actualización de `docs/pendientes_por_fases_comunaclic.md`
- bitácora en `memory/YYYY-MM-DD.md`
- publicación solo de servicios realmente cambiados

## Priorización recomendada

### Prioridad alta inmediata

- Sprint 1 - Favoritos Buyer
- Sprint 2 - Reseñas y Confianza
- Sprint 3 - Leads Accionables
- Sprint 4 - Agenda Operativa

### Prioridad media

- Sprint 5 - Payouts y Exportación
- Sprint 6 - Embudo y Salud del Negocio

### Prioridad posterior

- Sprint 7 - Promociones y Cupones
- Sprint 8 - Moderación y Soporte Admin

## Paquete recomendado para el siguiente ciclo

Si se quiere arrancar con un ciclo corto de alto impacto, recomiendo ejecutar este paquete:

1. Sprint 1
2. Sprint 3
3. Sprint 4

Eso deja una mejora clara para ambos lados del marketplace:

- buyer guarda oferta y vuelve
- partner mueve leads reales
- partner gestiona reservas desde agenda
