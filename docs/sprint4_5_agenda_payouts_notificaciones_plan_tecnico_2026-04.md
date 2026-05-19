# Sprint 4-5 - Agenda, Payouts y Notificaciones (Plan Tecnico)

Fecha: 2026-04-19

## Objetivo

Continuar Bloque 2 (operación partner accionable) con tres frentes:

1. Agenda operativa accionable.
2. Payouts con filtros y exportación CSV simple.
3. Notificaciones accionables (marcar leída/archivar + salto al recurso).

## Cambios implementados

### Agenda operativa

- API:
  - nuevo endpoint `PATCH /v1/bookings/{id}/workflow`
  - validación de estados operativos permitidos
  - validación de ventana horaria (`EndAt > StartAt`)
  - motivo obligatorio para `cancelled` y `no_show`
  - endpoint rápido `PATCH /status` restringido para evitar cierres sin motivo
- UI partner:
  - acciones rápidas: confirmar, completar, no-show, cancelar
  - edición workflow: estado, inicio/fin, nota interna, motivo resultado
  - actualización inline de la tarjeta tras guardar

### Payouts

- API:
  - `GET /v1/partners/{partnerId}/payouts` con filtros por:
    - `from`
    - `to`
    - `batchStatus`
  - respuesta enriquecida con `batchStatus`
  - endpoint de exportación:
    - `GET /v1/partners/{partnerId}/payouts/export.csv`
- UI partner:
  - filtros de rango y estado de batch
  - badge visual de estado de liquidación
  - exportación CSV simple desde la vista (texto generado para uso directo)

### Notificaciones

- API:
  - listado con `includeArchived`
  - `PATCH /v1/notifications/{id}/read`
  - `PATCH /v1/notifications/{id}/archive`
- UI partner:
  - toggle para ver archivadas
  - acciones por tarjeta:
    - marcar leída
    - archivar
    - ir al recurso relacionado (agenda/leads/payouts)

## Migración requerida

Archivo:

- `infra/migrations/20260419_booking_notifications_payouts_operacion.sql`

Aplica:

- nuevas columnas en `core.bookings`:
  - `internal_note`
  - `outcome_reason`
- nuevas columnas en `core.interactions`:
  - `read_at`
  - `archived_at`
- índices de soporte para agenda y bandeja.

## Archivos tocados

- `src/ComunaClick.Api/Persistence/Entities/Booking.cs`
- `src/ComunaClick.Api/Persistence/Entities/Interaction.cs`
- `src/ComunaClick.Api/Persistence/CoreDbContext.cs`
- `src/ComunaClick.Api/Modules/Bookings/BookingsController.cs`
- `src/ComunaClick.Api/Modules/Bookings/Contracts/BookingWorkflowUpdateRequest.cs`
- `src/ComunaClick.Api/Modules/Payouts/PayoutsController.cs`
- `src/ComunaClick.Api/Modules/Payouts/Contracts/PartnerPayoutItemResponse.cs`
- `src/ComunaClick.Api/Modules/Notifications/NotificationsController.cs`
- `src/ComunaClick.Shared/Api/Partner/PartnerApiClient.cs`
- `src/ComunaClick.SharedUI/Pages/Partner/Agenda.razor`
- `src/ComunaClick.SharedUI/Pages/Partner/Payouts.razor`
- `src/ComunaClick.SharedUI/Pages/Partner/Notifications.razor`

## Validación técnica

Builds completados:

- `ComunaClick.Api` OK
- `ComunaClick.SharedUI` OK

Warnings no bloqueantes previos:

- nullability en `CategoryVisualService`

## Secuencia recomendada de release

1. Aplicar migración SQL.
2. Deploy `api` y `app`.
3. Smoke post-deploy.
4. Quality checks mínimos.
