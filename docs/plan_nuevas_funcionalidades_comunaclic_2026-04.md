# Plan de Nuevas Funcionalidades - ComunaClic

Fecha: 2026-04-19

## Objetivo

Definir una siguiente hoja de ruta de producto basada en el estado actual del sistema: marketplace local ya publicable, operación partner inicial, smoke/release reproducible y validación pública en producción.

La idea no es seguir agregando pantallas aisladas, sino priorizar funcionalidades que aumenten:

- conversión buyer
- retención buyer
- operación partner
- monetización
- control comercial y analítico

## Punto de partida

Hoy ComunaClic ya cuenta con:

- registro/login y separación `acl` + `api` + `app`
- onboarding y publicación básica de negocio
- catálogo de productos, servicios y profesionales
- discovery público por comuna/categoría
- compras, reservas y leads en base funcional
- panel partner con agenda, leads, payouts y notificaciones mejorados
- smoke tests y quality checks mínimos
- flujo de publish/deploy estable para `app`, `api` y `acl`

La siguiente etapa recomendable es pasar de “marketplace funcional” a “plataforma que ayuda a vender, retener y operar mejor”.

## Principios de priorización

1. Priorizar funcionalidades que impacten flujo real buyer-partner antes que features cosméticas.
2. Favorecer piezas reutilizables en `api` y `SharedUI` para no duplicar lógica por vertical.
3. Evitar features que exijan alta complejidad operativa sin antes tener trazabilidad y métricas.
4. Diseñar cada bloque para publicarse por etapas cortas y verificables.

## Roadmap propuesto

## Bloque 1 - Confianza y conversión buyer

Objetivo: hacer más fácil decidir, comparar y volver a una oferta.

### Funcionalidades

- Favoritos buyer para negocios, productos, servicios y profesionales.
- Historial reciente de vistas y accesos rápidos a “seguir explorando”.
- Reseñas y calificaciones post compra/reserva/contacto validado.
- Señales públicas de confianza:
  - negocio verificado
  - cantidad de reseñas
  - tiempo promedio de respuesta
  - disponibilidad o última actividad

### Impacto esperado

- más retorno de usuarios
- mejor decisión de compra
- más credibilidad para partners buenos

### Servicios probables

- `api`
- `app`

### Dependencias

- definir ownership de reseñas
- decidir si las reseñas requieren transacción confirmada o también lead cerrado

### Criterio de cierre

- buyer puede guardar favoritos y volver a ellos
- buyer puede dejar review en escenarios permitidos
- vistas públicas muestran confianza sin exponer datos sensibles

## Bloque 2 - Operación partner accionable

Objetivo: convertir el panel partner en una consola de trabajo, no solo consulta.

### Funcionalidades

- Acciones rápidas sobre leads:
  - cambiar estado
  - asignar responsable
  - registrar próximo seguimiento
  - marcar como ganado/perdido
- Agenda con acciones operativas:
  - confirmar
  - reprogramar
  - marcar completada/no show
  - notas internas por reserva
- Notificaciones accionables:
  - marcar como leída
  - archivar
  - saltar directo al recurso relacionado
- Payouts con trazabilidad:
  - detalle por orden/reserva
  - estado de liquidación
  - rango de fechas
  - exportación simple CSV

### Impacto esperado

- menos operación fuera de plataforma
- mejor seguimiento comercial
- más claridad financiera para partners

### Servicios probables

- `api`
- `app`

### Dependencias

- revisar contratos actuales de `lead`, `booking`, `notification` y `payout`
- decidir si la exportación se hace server-side o client-side

### Criterio de cierre

- partner puede ejecutar tareas diarias desde ComunaClic sin depender de planillas externas para lo básico

## Bloque 3 - Retención y remarketing liviano

Objetivo: recuperar demanda sin convertir ComunaClic en un CRM pesado.

### Funcionalidades

- Recordatorios buyer:
  - reservas próximas
  - pagos pendientes
  - negocios favoritos con novedades
- Campañas simples partner:
  - cupón de bienvenida
  - descuento por re-compra
  - reactivación de leads fríos
- Centro de promociones visibles en fichas y dashboard partner.
- Métrica de efectividad por promoción/cupón.

### Impacto esperado

- más recompra
- reactivación de usuarios dormidos
- incentivo concreto para partners

### Servicios probables

- `api`
- `app`
- eventualmente `acl` si hay scopes nuevos

### Dependencias

- definir motor simple de reglas antes de automatizaciones más avanzadas
- definir si los cupones aplican solo a productos o también a servicios

### Criterio de cierre

- partner puede crear una promoción básica
- buyer puede verla y usarla en el flujo correspondiente
- existe trazabilidad mínima de uso y resultado

## Bloque 4 - Analítica y salud del negocio

Objetivo: ayudar al partner a entender qué está funcionando.

### Funcionalidades

- Dashboard partner con embudo:
  - vistas
  - contactos
  - reservas
  - compras
- Métricas por oferta/categoría/comuna.
- Alertas de salud:
  - caída de visibilidad
  - alta tasa de no show
  - leads sin seguimiento
  - baja conversión reciente
- Resumen semanal para partner en dashboard y/o notificación.

### Impacto esperado

- decisiones más informadas
- mejor percepción de valor del panel
- base para monetización futura por planes

### Servicios probables

- `api`
- `app`

### Dependencias

- consolidar eventos y timestamps confiables
- definir si basta con agregación transaccional o si hace falta tabla/materialización analítica

### Criterio de cierre

- partner ve al menos un tablero útil que conecte visibilidad con resultados reales

## Bloque 5 - Monetización y planes

Objetivo: abrir ingresos recurrentes y diferenciar niveles de servicio.

### Funcionalidades

- Planes partner:
  - gratuito
  - destacado
  - pro
- Límites o beneficios por plan:
  - cantidad de publicaciones
  - prioridad en discovery
  - analytics avanzados
  - promociones habilitadas
- Suscripción y estado de plan en dashboard.
- Integración gradual con `payments` y `payments-app` para cobro recurrente.

### Impacto esperado

- monetización más clara
- incentivo a usar más la plataforma
- marco comercial para upsell

### Servicios probables

- `api`
- `app`
- `payments`
- `payments-app`

### Dependencias

- primero validar valor de analytics/promociones
- definir modelo comercial y política de downgrade

### Criterio de cierre

- partner puede ver su plan actual, beneficios y ruta de upgrade
- existe al menos un flujo básico de suscripción o intención de contratación

## Bloque 6 - Admin y soporte operativo

Objetivo: dar herramientas internas para escalar sin operar a mano.

### Funcionalidades

- Panel admin para moderación de partners, ofertas y reseñas.
- Verificación de negocio y badges administrables.
- Vista de incidentes:
  - abuso
  - fraude
  - conflictos buyer-partner
- Herramientas de soporte:
  - buscar por partner/customer/order/booking
  - reenviar notificación
  - bloquear o pausar publicación

### Impacto esperado

- menos operación manual por base de datos
- mejor gobernanza del marketplace
- más seguridad operacional

### Servicios probables

- `api`
- `admin`

### Dependencias

- endurecer permisos administrativos
- trazar auditoría básica de acciones internas

### Criterio de cierre

- el equipo puede moderar y asistir sin tocar datos productivos directamente

## Orden recomendado de ejecución

1. Bloque 1 - Confianza y conversión buyer
2. Bloque 2 - Operación partner accionable
3. Bloque 4 - Analítica y salud del negocio
4. Bloque 3 - Retención y remarketing liviano
5. Bloque 6 - Admin y soporte operativo
6. Bloque 5 - Monetización y planes

## Por qué este orden

- Bloques 1 y 2 mejoran la propuesta base sin exigir infraestructura nueva compleja.
- Bloque 4 aprovecha mejor los datos generados por 1 y 2.
- Bloque 3 rinde más cuando ya existen señales de comportamiento y operación.
- Bloque 6 ordena el crecimiento interno antes de escalar soporte.
- Bloque 5 conviene después de validar qué beneficios realmente mueven adopción y conversión.

## Entregables sugeridos por sprint

### Sprint A

- favoritos buyer
- historial reciente
- badges de confianza iniciales

### Sprint B

- acciones rápidas en leads
- agenda con confirmación/reprogramación
- notificaciones accionables

### Sprint C

- embudo partner
- alertas operativas básicas
- payouts con exportación y filtros avanzados

### Sprint D

- cupones/promociones simples
- recordatorios buyer
- trazabilidad de campañas

### Sprint E

- admin de moderación
- verificación de negocio
- soporte interno básico

### Sprint F

- planes y beneficios
- integración de suscripción
- upgrade/downgrade inicial

## Riesgos a vigilar

- abrir demasiados flujos nuevos sin cerrar QA autenticado real
- crear deuda de permisos si admin y partner ganan acciones nuevas sin ownership claro
- diseñar promociones/suscripciones antes de tener métricas útiles
- recargar el panel partner con ruido en vez de acciones concretas

## Recomendación práctica inmediata

Si hay que elegir un solo siguiente frente, recomiendo abrir primero un mini-roadmap compuesto por:

1. favoritos + reseñas buyer
2. leads accionables + agenda operativa
3. dashboard partner con embudo simple

Ese paquete tiene el mejor equilibrio entre valor visible, impacto comercial y complejidad razonable.
