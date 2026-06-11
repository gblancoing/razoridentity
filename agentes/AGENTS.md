# ComunaClic Agent Instructions

ComunaClic es una solución .NET 8 con un monolito modular de negocio, un servicio de ACL separado y un gateway de pagos aislado. Antes de tocar código, revisa [agent.md](agent.md) para la arquitectura base y [README.md](README.md) para la configuración local y de depuración.

## Cómo trabajar en este repo

- Usa el SDK fijado en [global.json](global.json): .NET 8.0.418.
- Prefiere builds seriales cuando valides cambios: `dotnet build --no-restore -m:1 -nr:false`.
- Valida cambios con `dotnet test tests/ComunaClick.Tests.Unit/ComunaClick.Tests.Unit.csproj` cuando aplique.
- Para smoke checks de negocio, usa `dotnet run --project src/ComunaClick.QualityChecks`.
- Si el cambio toca despliegue o publicación, consulta [docs/release_y_rollback_por_servicio_comunaclic.md](docs/release_y_rollback_por_servicio_comunaclic.md).

## Convenciones que debes respetar

- Mantén el tenant scoping: `TenantId` debe resolverse desde JWT o `X-Tenant-Id` validado, y las consultas multi-tenant deben filtrar por tenant.
- No llames a Transbank directamente desde la API de negocio; el flujo va por `Payments.Gateway.Api`.
- Trata `ComunaClick.Api` como monolito modular con fronteras internas claras, no como microservicios fragmentados.
- Si una regla ya está documentada, enlázala en vez de copiarla aquí.

## Documentación útil

- [docs/quality_checks_minimos_comunaclic.md](docs/quality_checks_minimos_comunaclic.md) para cobertura mínima de validación.
- [docs/diagnostico_build_publish_local_2026-04.md](docs/diagnostico_build_publish_local_2026-04.md) para problemas típicos de build y publish.
- [docs/objetivo_y_ramas_de_negocio_comunaclic.md](docs/objetivo_y_ramas_de_negocio_comunaclic.md) para el contexto funcional A/B/C.
- [docs/pasarela_pagos_multi_provider.md](docs/pasarela_pagos_multi_provider.md) para el diseño de pagos.
- [docs/empresa-tipo-a.md](docs/empresa-tipo-a.md) y [docs/empresa-tipo-b.md](docs/empresa-tipo-b.md) para reglas de catálogo por tipo de partner.
- [.agents/skills/ui-ux-pro-max/SKILL.md](.agents/skills/ui-ux-pro-max/SKILL.md) para tareas de UI, accesibilidad, responsive, animación y revisión visual.

## Skills de diseño disponibles

- `/stitch-design` — Skill end-to-end para Google Stitch. Genera pantallas desde texto o imágenes, edita y refina diseños existentes, gestiona design systems, genera variantes, y exporta assets a `.stitch/`. Punto de entrada único para todo el flujo de diseño con Stitch MCP. Instalada globalmente en `~/.claude/skills/stitch-design/SKILL.md`.

## Pistas de edición

- Revisa las reglas específicas en [.cursor/rules/empresa-tipo-a.mdc](.cursor/rules/empresa-tipo-a.mdc) y [.cursor/rules/empresa-tipo-b.mdc](.cursor/rules/empresa-tipo-b.mdc) antes de cambiar flujos de catálogo, productos o servicios.
- Mantén los cambios acotados al proyecto afectado y evita reordenar el repo o reformatear archivos ajenos al cambio.