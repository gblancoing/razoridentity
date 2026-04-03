# Informe de correcciones e implementación pendiente

Documento base revisado: `/Users/alexolave/Downloads/ComunaClic_Analisis_Disenio_2026.md`

Fecha de revisión: 2026-04-03

## Resumen ejecutivo

El proyecto ya avanzó en flujo buyer/partner, publicación, seguridad básica, geolocalización, vistas públicas de detalle y operación inicial de catálogo. Sin embargo, todavía quedan brechas de diseño/UX, limpieza de textos técnicos visibles en frontend, mejoras funcionales en módulos partner, y tareas de seguridad operativa y QA para cerrar una versión más sólida.

La recomendación es ejecutar lo pendiente en 5 etapas, priorizando primero correcciones de UI visibles al usuario final y luego endurecimiento operativo, pruebas y documentación.

## Estado general contra el análisis de diseño

## Avance ejecutado en esta pasada

- Se eliminaron códigos técnicos de categoría visibles en tarjetas y encabezados públicos de buyer/categorías.
- Se rediseñó `Buyer/Profile.razor` para dejar de exponer `CustomerId` como input manual y presentar una vista más clara de cuenta personal.
- Se mejoró la jerarquía visual de `PublicNav`, `PublicFooter`, `Home` y `Login`.
- Se reemplazó el copy `Editorial Service` por una bajada más alineada al producto en el layout partner.
- Se quitaron estados crudos en `Partner/Agenda` y `Partner/Leads`, reemplazándolos por etiquetas legibles y badges visuales.
- Se actualizó `README.md` para reflejar que `/register` y `POST /v1/auth/register` ya son parte del flujo real.
- Se validó `rate limiting` + `reCAPTCHA` en producción:
  - 12 intentos `POST /v1/auth/login` sin token reCAPTCHA devolvieron `10 x 400` y luego `2 x 429`.
  - `https://app.comunaclic.cl/login` está cargando `window.comunaclicRecaptchaEnabled = true` y el script `https://www.google.com/recaptcha/api.js`.

Pendiente técnico de esta pasada:

- La validación local con `dotnet build src/ComunaClick/ComunaClick.App.csproj --no-restore` volvió a quedarse sin salida útil en este entorno, así que la compilación completa de `app` quedó pendiente de confirmar por publish/deploy controlado.

### Ya resuelto o parcialmente resuelto

- Navegación partner con estado activo visual y botón de logout ya implementado en layout.
- Ficha pública buyer de producto, servicio y profesional mejorada con hero visual, datos clave y CTA.
- Mapa por categoría con geolocalización, fallback y clustering de marcadores.
- Registro buyer/partner separado por intención y returnUrl.
- reCAPTCHA v3 integrado en login y registro.
- Endpoints públicos de tracking endurecidos con `customerId`.
- Password plain-text fallback deshabilitado por defecto.

### Aún pendiente de corregir o implementar

- El frontend buyer aún muestra códigos técnicos de categoría en varias vistas públicas.
- Agenda partner sigue exponiendo texto de placeholder y estados backend crudos.
- Leads partner aún muestra `Status` técnico sin traducción visual.
- Perfil buyer sigue siendo muy básico y todavía expone `CustomerId` como dato editable/visible.
- Sidebar partner mantiene copy `Editorial Service`, que no calza con el producto final.
- Navbar pública no diferencia visualmente bien `Registrar negocio` como acción secundaria.
- Footer público usa íconos genéricos en redes sociales, no marcas/redes reales.
- Home todavía tiene tags de noticias sin estilo tipo badge y hero con imagen genérica externa.
- Login todavía no está completamente alineado al estilo propuesto y `¿Olvidaste tu contraseña?` no tiene flujo funcional.
- `README.md` contiene secciones desactualizadas sobre `/register` y ACL.
- Falta rotación real de secretos fuera del repositorio.
- Falta validar rate limiting y reCAPTCHA en escenarios controlados de abuso.
- Faltan pruebas end-to-end automáticas y una pasada QA completa.

## Etapas recomendadas

## Etapa 1 - Corrección UI/UX pública y buyer

Objetivo: eliminar fricción visual y datos técnicos visibles para usuarios finales.

Tareas:

- Reemplazar slugs/códigos técnicos de categoría por nombres legibles en:
  - `src/ComunaClick.SharedUI/Pages/Buyer/BuyerHome.razor`
  - `src/ComunaClick.SharedUI/Pages/Categories.razor`
  - `src/ComunaClick.SharedUI/Pages/CategoryDetail.razor`
- Rediseñar `src/ComunaClick.SharedUI/Pages/Buyer/Profile.razor` para:
  - ocultar `CustomerId`
  - mostrar bienvenida, avatar/iniciales, datos personales y bloque de preferencias
  - dejar el identificador técnico solo como dato interno, no como input principal
- Ajustar navbar pública en `src/ComunaClick.SharedUI/Components/PublicNav.razor`:
  - `Iniciar sesión` como acción primaria
  - `Registrar negocio` como botón secundario/outline
- Ajustar footer social en `src/ComunaClick.SharedUI/Components/PublicFooter.razor` para usar íconos/redes reales.
- Mejorar hero y tarjetas de novedades en `src/ComunaClick.SharedUI/Pages/Home.razor`:
  - badges visuales para tags de noticias
  - imagen hero más representativa del contexto local
- Alinear campos visuales de login en `src/ComunaClick.SharedUI/Pages/Login.razor` al sistema visual definido.
- Definir e implementar flujo real de `¿Olvidaste tu contraseña?`.

Criterio de término:

- La navegación pública y buyer ya no muestra identificadores técnicos al usuario final.
- Login, perfil buyer, home y navegación pública quedan consistentes visualmente con el diseño objetivo.

Servicios a publicar:

- `app`

## Etapa 2 - Corrección UI/UX partner y módulos operativos

Objetivo: convertir Agenda, Leads, Payouts y Notifications en vistas operativas más claras y menos “placeholder”.

Tareas:

- Reemplazar copy `Editorial Service` por una bajada de producto más coherente en `src/ComunaClick.SharedUI/Layouts/PartnerLayout.razor`.
- En `src/ComunaClick.SharedUI/Pages/Partner/Agenda.razor`:
  - eliminar texto `placeholder`
  - mapear `booking.Status` a etiquetas legibles y badges visuales
  - mejorar estructura de próximas reservas/calendario si aún no hay vista calendario real
- En `src/ComunaClick.SharedUI/Pages/Partner/Leads.razor`:
  - mapear `lead.Status` a estados legibles
  - agregar badge de estado y prioridad
  - mejorar empty state y acciones disponibles
- En `src/ComunaClick.SharedUI/Pages/Partner/Payouts.razor`:
  - reforzar visualización de estado de liquidación, montos y periodos
  - revisar si faltan CTAs o contexto operacional
- En `src/ComunaClick.SharedUI/Pages/Partner/Notifications.razor`:
  - mejorar jerarquía visual y estado leído/no leído
  - revisar si falta acción operacional por notificación
- Actualizar textos de `partner.agenda.placeholder` y estados en `src/ComunaClick.SharedUI/Services/LocaleService.cs`.

Criterio de término:

- Ninguna vista partner principal presenta texto placeholder visible.
- Agenda, Leads, Payouts y Notifications muestran estados legibles, badges y empty states claros.

Servicios a publicar:

- `app`
- `api` si algún estado/traducción requiere contrato adicional

## Etapa 3 - Seguridad operativa y control de acceso

Objetivo: cerrar riesgos técnicos y dejar controles verificables en producción.

Tareas:

- Ejecutar rotación real de secretos documentada en `docs/secret_rotation_checklist.md`:
  - credenciales de base de datos
  - JWT signing key
  - refresh/reset peppers
  - webhook/internal keys
- Mover secretos fuera de `appsettings.json` hacia variables de entorno/secret manager.
- Validar en entorno controlado:
  - rate limiting en login, register, endpoints públicos y webhooks
  - reCAPTCHA v3 en login/register con score esperado
  - protección efectiva de `UsersController`, `RolesController`, `PermissionsController`
- Revisar headers y hardening en runtime con pruebas HTTP.
- Revisar logs para confirmar que bloqueos por 429/401/403 quedan trazables sin filtrar datos sensibles.

Criterio de término:

- Los secretos productivos ya no quedan hardcodeados en el repo.
- Rate limiting, reCAPTCHA y autorización administrativa quedan probados con evidencia mínima.

Servicios a publicar:

- `acl`
- `api`
- `app` si cambia configuración frontend de reCAPTCHA

## Etapa 4 - Pruebas end-to-end, QA y datos

Objetivo: validar el flujo completo buyer/partner y reducir regresiones.

Tareas:

- Ejecutar smoke test manual completo partner:
  - registrar partner
  - crear negocio
  - cargar producto/servicio/profesional
  - publicar
  - editar ubicación exacta
  - verificar aparición pública
- Ejecutar smoke test manual completo buyer:
  - registro cuenta personal
  - login
  - selección de categoría
  - vista mapa cercano
  - detalle de producto/servicio/profesional
  - compra/reserva/contacto
  - seguimiento de orden/reserva
- Implementar pruebas automáticas mínimas para:
  - auth/register/login
  - publicación partner
  - discovery público por categoría
  - tracking público con `customerId`
  - autorización partner/admin
- Revisar y limpiar datos de prueba antiguos o duplicados si afectan demostraciones.

Criterio de término:

- Existe una evidencia reproducible del flujo E2E buyer/partner.
- Hay al menos una base mínima de pruebas automatizadas para rutas críticas.

Servicios a publicar:

- según cambios: `api`, `acl`, `app`

## Etapa 5 - Limpieza documental y consistencia de repositorio

Objetivo: dejar el proyecto más mantenible y con documentación alineada al estado real.

Tareas:

- Actualizar `README.md` para reflejar que `/register` y `POST /v1/auth/register` ya están implementados.
- Mantener `docs/pendientes_por_fases_comunaclic.md` sincronizado con lo realmente cerrado.
- Registrar decisiones relevantes en `memory/YYYY-MM-DD.md`.
- Revisar y decidir destino de archivos locales sueltos no versionados:
  - `.DS_Store`
  - PNGs temporales
  - `appsettings.Development.json`
- Revisar textos de producto aún mezclados con copy “editorial” si ya no representan la propuesta final.

Criterio de término:

- Documentación principal y backlog quedan consistentes con la versión desplegada.
- El working tree queda limpio o con excepciones locales claramente intencionales.

Servicios a publicar:

- ninguno, salvo cambios en `app/api/acl`

## Orden de ejecución sugerido

1. Etapa 1 - correcciones públicas/buyer más visibles.
2. Etapa 2 - módulos partner aún con deuda de UX y placeholders.
3. Etapa 3 - rotación de secretos y validación de controles de abuso/autorización.
4. Etapa 4 - QA E2E y automatización mínima.
5. Etapa 5 - limpieza documental y orden del repositorio.

## Nota de riesgo

Antes de rotar secretos productivos, conviene coordinar una ventana de despliegue y validar qué servicios leen esos valores por archivo, variable de entorno o systemd/nginx, para evitar cortar login/API en producción durante el cambio.
