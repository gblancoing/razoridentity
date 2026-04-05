# Requerimiento: Payment.App - Dashboard de transacciones por suscripción

## 1. Objetivo

Crear una aplicación administrativa **Payment.App** que permita visualizar, auditar y operar las transacciones generadas por **botones de pago de suscripción**, integrándose con `Payments.Gateway.Api` y el esquema `payments`.

El foco inicial no es procesar el pago en sí, sino **dar visibilidad operacional y trazabilidad** sobre:

- intenciones de pago
- confirmaciones/callbacks/webhooks
- estado de suscripciones
- montos, proveedor y comercio asociado
- errores o pagos pendientes de revisión

## 2. Alcance funcional

## 2.1 Dashboard principal

La pantalla inicial debe mostrar un resumen ejecutivo con:

- total transado del día, semana y mes
- cantidad de pagos exitosos, pendientes, fallidos y cancelados
- distribución por proveedor: Transbank, Khipu, Mercado Pago
- cantidad de suscripciones activas, pausadas, vencidas y canceladas
- últimas transacciones recibidas
- alertas operativas: webhooks fallidos, pagos duplicados, intentos sin confirmación

## 2.2 Listado de transacciones

Debe existir una vista tabular con filtros y búsqueda por:

- rango de fechas
- proveedor
- estado de la intención: `pending`, `captured`, `failed`, `canceled`
- `ExternalReference`
- `ProviderToken`
- email/identificador de cliente si aplica
- negocio/partner asociado si la suscripción está vinculada a ComunaClic

La tabla debe mostrar como mínimo:

- fecha de creación
- proveedor
- monto y moneda
- estado
- referencia externa
- token/id del proveedor
- código de autorización
- botón de detalle

## 2.3 Detalle de transacción

Al seleccionar una transacción, se debe mostrar:

- datos base de `PaymentIntent`
- línea de tiempo de eventos `ProviderEvent`
- payload técnico recibido desde cada callback/webhook en formato JSON legible
- resultado de notificación hacia Core
- trazabilidad de `Charge` si corresponde
- acciones operativas según permisos:
  - reintentar notificación interna
  - marcar para revisión manual
  - exportar detalle

## 2.4 Gestión de suscripciones

Debe existir una vista específica para suscripciones creadas desde botones de pago recurrente:

- listado de planes/suscripciones
- estado actual de cada suscripción
- último pago asociado
- próxima fecha estimada de cobro
- proveedor de pago usado
- cliente y negocio relacionado
- detalle histórico de intentos de cobro

Estados sugeridos:

- `active`
- `trial`
- `past_due`
- `paused`
- `canceled`
- `expired`

## 2.5 Reportería y exportación

El usuario operador debe poder:

- exportar transacciones filtradas a CSV
- descargar reporte mensual por proveedor
- descargar conciliación básica: `PaymentIntent` + `ProviderEvent` + `Charge`

## 3. Alcance no funcional

- UI web responsive, priorizando escritorio y vista tablet
- autenticación obligatoria
- control de acceso por rol administrativo
- paginación server-side para tablas
- auditoría de acciones manuales
- no exponer secretos ni credenciales de proveedor en frontend
- no mostrar datos sensibles de tarjeta; solo identificadores/tokens del proveedor

## 4. Arquitectura propuesta

## 4.1 Proyecto nuevo

Crear un proyecto web independiente:

- `src/Payments.App/Payments.App.csproj`

Opciones sugeridas:

- **Blazor Server** si se quiere seguir el patrón de `ComunaClick.App`
- **Blazor Web App** con SSR + interactividad server si se busca consistencia con el frontend actual

Recomendación práctica: **Blazor Server / Blazor Web App server-side**, para avanzar rápido reutilizando estilo, layout, auth y patrón Razor.

## 4.2 Fuente de datos

`Payment.App` no debería conectarse directamente a la base en una primera fase si se quiere encapsular lógica.

Patrón recomendado:

- `Payment.App` consume endpoints administrativos de `Payments.Gateway.Api`
- `Payments.Gateway.Api` expone endpoints de consulta/operación con autorización

Ejemplos de endpoints a agregar:

- `GET /v1/admin/payment-intents`
- `GET /v1/admin/payment-intents/{id}`
- `GET /v1/admin/subscriptions`
- `GET /v1/admin/subscriptions/{id}`
- `POST /v1/admin/payment-intents/{id}/retry-core-notify`
- `GET /v1/admin/reports/transactions.csv`

## 5. Modelo de datos mínimo requerido

Hoy el gateway ya maneja:

- `payments.payment_intents`
- `payments.provider_events`
- `payments.customer_tokens`
- `payments.charges`

Para cubrir suscripciones de forma más clara, se sugiere evaluar una tabla nueva:

## 5.1 `payments.subscriptions`

Campos sugeridos:

- `id`
- `customer_id`
- `partner_id`
- `plan_code`
- `provider`
- `provider_subscription_ref`
- `status`
- `amount`
- `currency`
- `billing_cycle`
- `current_period_start`
- `current_period_end`
- `next_billing_at`
- `last_payment_intent_id`
- `raw_metadata`
- `created_at`
- `updated_at`

## 5.2 `payments.subscription_events`

Campos sugeridos:

- `id`
- `subscription_id`
- `event_type`
- `provider_event_id`
- `payload`
- `received_at`

## 6. Permisos y roles

Roles mínimos sugeridos:

- `payments.viewer`: puede ver dashboard, listados y detalles
- `payments.operator`: puede reintentar notificaciones, marcar revisión, exportar reportes
- `payments.admin`: puede administrar configuración operativa y ver trazabilidad completa

Reglas:

- ninguna vista debe ser pública
- todos los endpoints admin de `Payments.Gateway.Api` deben exigir token y policy
- registrar usuario, fecha y acción cuando se ejecuten operaciones manuales

## 7. Diseño de pantallas sugerido

## 7.1 Layout general

- Sidebar con:
  - Dashboard
  - Transacciones
  - Suscripciones
  - Reportes
  - Alertas
- Topbar con:
  - usuario conectado
  - selector rápido de rango de fechas
  - botón logout

## 7.2 Pantalla Dashboard

Bloques:

- cards KPI superiores
- gráfico de volumen transado por día
- gráfico/tabla por proveedor
- últimas transacciones
- alertas operativas

## 7.3 Pantalla Transacciones

- filtros arriba
- tabla paginada
- badge visual de estado
- badge de proveedor
- acción `Ver detalle`

## 7.4 Pantalla Detalle

- resumen de monto/estado/proveedor
- datos de referencia
- timeline de eventos
- bloque JSON técnico
- acciones operativas controladas por rol

## 7.5 Pantalla Suscripciones

- tabla de suscripciones
- filtros por estado, plan, proveedor
- detalle de una suscripción con historial de cobros

## 8. Fases de implementación

## Fase 1 - Base visible del backoffice

- crear proyecto `Payments.App`
- crear layout administrativo
- crear dashboard inicial con KPIs mockeados o datos reales mínimos
- crear página de listado de transacciones consumiendo endpoint admin
- crear página de detalle de transacción

## Fase 2 - Endpoints administrativos en Payments.Gateway.Api

- exponer `GET /v1/admin/payment-intents`
- exponer `GET /v1/admin/payment-intents/{id}`
- exponer resumen KPI de dashboard
- implementar paginación/filtros server-side
- proteger endpoints con políticas de admin/operador

## Fase 3 - Suscripciones

- diseñar y crear `payments.subscriptions`
- registrar relación entre botón de suscripción y `PaymentIntent`
- implementar vista de suscripciones
- implementar detalle e historial de cobros

## Fase 4 - Operación y reportería

- reintento manual de notificación a Core
- exportación CSV
- tablero de alertas
- auditoría de acciones manuales

## Fase 5 - Seguridad, QA y despliegue

- rate limiting y headers de seguridad en `Payments.Gateway.Api`
- autenticación y autorización en `Payments.App`
- smoke tests de dashboard, listado, detalle y exportación
- agregar target `payments-app` al script de deploy si se publica como servicio separado

## 9. Criterios de aceptación

- un operador autenticado puede entrar a `Payment.App` y ver resumen de transacciones
- se puede filtrar y abrir el detalle de una transacción real
- el detalle muestra eventos del proveedor y payload técnico
- se puede distinguir rápidamente pagos exitosos, pendientes, fallidos y cancelados
- se puede ver una suscripción y su historial de cobros
- no existe acceso anónimo al dashboard
- no se exponen secretos ni datos de tarjeta en la UI

## 10. Riesgos y decisiones pendientes

- definir si `Payment.App` comparte ACL de ComunaClic o usa autenticación propia
- definir si será una app interna solo de administración o también visible para partners
- definir si una suscripción representa membresía del negocio, plan SaaS, o plan recurrente del buyer
- confirmar qué proveedor soportará realmente cobros recurrentes en la primera versión

## 11. Recomendación de implementación inmediata

Para no frenar el avance, la primera entrega debería ser:

1. crear `Payments.App`
2. agregar dashboard con KPIs + listado de últimas transacciones
3. agregar detalle de `PaymentIntent` + `ProviderEvents`
4. exponer solo los endpoints admin de lectura desde `Payments.Gateway.Api`
5. dejar suscripciones y acciones operativas para una segunda iteración
