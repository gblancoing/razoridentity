# Plan por Etapas - Integración, Uso y Revisión de Mercado Pago en ComunaClic

## Objetivo

Implementar Mercado Pago de forma segura y operativa en ComunaClic, con trazabilidad end-to-end (creación de pago, callback, conciliación, monitoreo y mejora continua).

## Etapa 0 - Preparación y alcance

### Tareas
- Confirmar alcance: checkout único, suscripciones y panel operativo.
- Definir ambientes (`sandbox` y `production`) y responsables.
- Alinear nomenclatura de referencias (`external_reference`, `provider_token`, `intent_id`).

### Entregables
- Documento de alcance aprobado.
- Matriz de responsabilidades (App, Gateway, Operaciones).

### Criterio de salida
- Todos los equipos acuerdan flujo y límites de integración.

---

## Etapa 1 - Configuración técnica base

### Tareas
- Configurar `PaymentProviders:MercadoPago` en `Payments.App` y `Payments.Gateway.Api`.
- Mantener secretos solo en backend (token privado fuera de frontend).
- Validar rutas oficiales:
  - `POST /checkout/preferences`
  - `GET /v1/payments/{id}`

### Entregables
- Configuración por ambiente lista.
- Checklist de variables de entorno y secretos.

### Criterio de salida
- La app y el gateway leen configuración correctamente sin exponer secretos.

---

## Etapa 2 - Flujo de pago end-to-end (sandbox)

### Tareas
- Crear preferencia de pago desde `Payments.Gateway.Api`.
- Persistir `PaymentIntent` con proveedor `mercadopago`.
- Redirigir al checkout y recibir callbacks/webhooks.
- Actualizar estado local (`pending`, `captured`, `failed`, `canceled`).

### Entregables
- Flujo E2E funcional en sandbox.
- Registro de `ProviderEvents` por cada callback.

### Criterio de salida
- Se completa el ciclo pago->callback->estado en al menos 10 pruebas válidas.

---

## Etapa 3 - Seguridad y hardening

### Tareas
- Implementar validación de firma/origen de webhook.
- Aplicar rate limiting en endpoints de callback.
- Agregar idempotencia para callbacks repetidos.
- Revisar logs para no exponer datos sensibles.

### Entregables
- Webhook endurecido.
- Política de seguridad documentada.

### Criterio de salida
- No hay doble procesamiento y los callbacks no autenticados se rechazan.

---

## Etapa 4 - Uso operativo en Payments.App

### Tareas
- Mostrar estado de integración Mercado Pago en dashboard.
- Exponer métricas mínimas: pagos capturados, pendientes, fallidos, por proveedor.
- Habilitar filtros por proveedor/estado/rango de fecha.

### Entregables
- Dashboard operativo para soporte y finanzas.
- Vista de transacciones y eventos con trazabilidad.

### Criterio de salida
- Operaciones puede diagnosticar pagos sin revisar base de datos manualmente.

---

## Etapa 5 - QA funcional y técnica

### Tareas
- Ejecutar casos de prueba:
  - pago aprobado
  - pago pendiente
  - pago rechazado
  - callback duplicado
  - callback tardío
- Validar conciliación entre `PaymentIntent`, `Charge` y `ProviderEvent`.

### Entregables
- Evidencia de QA (resultados por caso).
- Lista de defectos y correcciones.

### Criterio de salida
- Casos críticos aprobados sin bloqueantes.

---

## Etapa 6 - Paso a producción controlado

### Tareas
- Activar integración por feature flag o tenant piloto.
- Monitorear 72 horas iniciales.
- Preparar plan de rollback (desactivar proveedor sin caída total).

### Entregables
- Go-live parcial y luego general.
- Acta de estabilidad post-lanzamiento.

### Criterio de salida
- Operación estable con métricas dentro de umbrales definidos.

---

## Etapa 7 - Revisión continua y optimización

### Tareas
- Revisión semanal de KPIs:
  - tasa de aprobación
  - tiempo de confirmación
  - tasa de error callback
- Ajustar UX de retorno y mensajes de estado.
- Planificar siguientes mejoras (refunds, conciliación automática avanzada, suscripciones full).

### Entregables
- Informe de mejora continua mensual.
- Backlog priorizado de evolución de pagos.

### Criterio de salida
- Mejora medible de estabilidad y conversión mes a mes.

---

## KPIs sugeridos de seguimiento

- Tasa de pagos aprobados (%).
- Tiempo promedio entre creación de intención y confirmación final.
- Porcentaje de callbacks duplicados procesados idempotentemente.
- Error rate por endpoint de integración (4xx/5xx).
- % de transacciones conciliadas automáticamente.

---

## Checklist rápido de inicio

- [ ] Configuración Mercado Pago por ambiente cargada.
- [ ] Token privado en backend y no expuesto en frontend.
- [ ] Callback y webhook con seguridad mínima aplicada.
- [ ] Dashboard con visibilidad de estado por proveedor.
- [ ] Pruebas sandbox E2E completadas.
