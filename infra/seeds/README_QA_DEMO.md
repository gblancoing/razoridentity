# QA / Demo seed data for ComunaCLick

## File
- `infra/seeds/core_seed_qa_demo.sql`

## What it creates
- 1 tenant demo in Alhué
- 1 partner tipo A eligible
  - subcategoría
  - 3 productos activos
  - 1 producto inactivo
- 1 partner tipo B eligible
  - contacto y dirección
  - 3 servicios activos
- 1 partner tipo C eligible
  - 2 profesionales activos/verificados
  - 1 profesional incompleto
- 2 customers válidos
- 1 customer nuevo/incompleto
- 2 órdenes demo
- 2 reservas demo
- 2 leads demo
- 1 partner no elegible/no visible para validar discovery

## Intended QA checks
1. Discovery público
   - tipo A visible con productos activos
   - tipo B visible con servicios activos
   - tipo C visible con profesionales verificados
   - partner pausado no debe aparecer
   - producto inactivo no debe aparecer
   - profesional incompleto/no verificado no debe aparecer

2. Partner activation
   - A cumple checklist
   - B cumple checklist
   - C cumple checklist con los 2 profesionales válidos

3. Funnel buyer
   - checkout usando customer válido
   - booking usando customer válido
   - contact/lead usando customer válido
   - customer incompleto para probar fricción y errores guiados

## How to run
Apply against the same PostgreSQL database used by `ComunaClick.Api`:

```bash
psql "$CORE_DB_CONNECTION" -f infra/seeds/core_seed_qa_demo.sql
```

Or from pgAdmin/Query Tool:
- open the file
- execute the full script in the `core` database/schema context

## Notes
- Uses fixed UUIDs for repeatable demos and QA scripts.
- Uses `ON CONFLICT DO NOTHING` (or upsert style where needed) so it is safe-ish to rerun.
- Assumes the current schema already includes:
  - geo tables
  - partner subcategory support
  - product_categories / product_subcategories
