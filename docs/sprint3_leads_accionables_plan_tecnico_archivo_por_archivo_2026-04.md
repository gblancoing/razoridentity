# Sprint 3 - Leads Accionables (Plan Tecnico Archivo por Archivo)

Fecha: 2026-04-19

## Objetivo del sprint

Cerrar el flujo de leads operativos para partner con estados comerciales reales:

- `contacted`
- `in_follow_up`
- `won`
- `lost` (con motivo obligatorio)

Y validar el comportamiento con checks automáticos opcionales en entorno QA.

## Alcance implementado

### API

- [x] Normalización y validación de estados de lead.
- [x] Restricción para evitar cierres `won/lost` en endpoint rápido de status.
- [x] Validación de negocio: `OutcomeReason` obligatorio cuando status final es `lost`.
- [x] Validación de prioridad permitida (`high`, `normal`, `low`).

### APP (SharedUI)

- [x] Acciones rápidas en bandeja:
  - `Contactado`
  - `Seguimiento`
  - `Ganado`
  - `Perdido (motivo)`
- [x] Formulario de edición workflow con estado comercial explícito.
- [x] Validación local para exigir motivo al guardar `lost`.
- [x] Filtro/copies/etiquetas adaptadas a `ganado/perdido`.

### QualityChecks

- [x] Bloque opcional de validación E2E para workflow de leads autenticado.
- [x] Caso negativo: `lost` sin motivo debe fallar (`400`).
- [x] Caso positivo: `lost` con motivo debe pasar (`200`).

## Archivos tocados

### API

1. `src/ComunaClick.Api/Modules/Leads/LeadsController.cs`
   - Reglas de normalización de status (`follow_up -> in_follow_up`, `closed -> won`).
   - Validaciones de status/prioridad.
   - En `PATCH /v1/leads/{id}/status` se bloquea `won/lost` y se redirige a workflow.
   - En `PATCH /v1/leads/{id}/workflow` se exige `OutcomeReason` para `lost`.

### APP

1. `src/ComunaClick.SharedUI/Pages/Partner/Leads.razor`
   - Nuevos CTA de outcome (`Ganado`, `Perdido`).
   - Nuevo campo de `Estado comercial` en draft de workflow.
   - Validación UI para `lost` con motivo obligatorio.
   - Mapeo visual y de filtros para estados `won/lost`.

### Quality checks

1. `src/ComunaClick.QualityChecks/Program.cs`
   - Nuevas variables:
     - `QUALITY_LEAD_ID`
     - `QUALITY_LEAD_OWNER`
     - `QUALITY_LEAD_OUTCOME_REASON`
   - Nueva rutina `CheckLeadWorkflowAsync(token)` con checks de flujo.

2. `docs/quality_checks_minimos_comunaclic.md`
   - Documentación actualizada de cobertura y variables de lead workflow.

## Validacion recomendada (release candidate)

1. Build local:
   - `dotnet build src/ComunaClick.Api/ComunaClick.Api.csproj --no-restore -m:1 -nr:false`
   - `dotnet build src/ComunaClick.SharedUI/ComunaClick.SharedUI.csproj --no-restore -m:1 -nr:false`
   - `dotnet build src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj --no-restore -m:1 -nr:false`

2. Deploy:
   - `publish_and_deploy_comunaclic.sh api app`

3. Post deploy:
   - `bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh`
   - `dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj --no-build`
   - Opcional lead workflow:
     - exportar `QUALITY_QA_EMAIL`, `QUALITY_QA_PASSWORD`, `QUALITY_LEAD_ID`
     - re-ejecutar quality checks.

## Pendiente siguiente (Bloque 2)

Tras cerrar este sprint de leads, el siguiente frente natural es Agenda operativa:

- confirmar/reprogramar/completada/no-show
- nota interna por reserva
- acciones rápidas en panel partner
