# ComunaClic

Marketplace y panel de gestión para negocios locales. Stack principal: .NET 8 + Blazor + APIs.

## Estructura del repo
- `src/ComunaClick` — Blazor Web App (UI).
- `src/ComunaClick.SharedUI` — componentes UI compartidos.
- `src/ComunaClick.Shared` — DTOs + clientes HTTP.
- `src/ComunaClick.Common` — tipos comunes y helpers (Auth, Result, Money).
- `src/ComunaClick.Acl` — API de autenticación/autorización (JWT).
- `src/ComunaClick.Api` — API de negocio (Core).
- `src/ComunaClick.Mobile` — MAUI Blazor Hybrid (iOS/Android).
- `src/Payments.Common` — contratos de pagos.
- `infra/` — scripts SQL, seeds, certs locales.

## Requisitos
- .NET 8 SDK
- PostgreSQL (DB única `comunaclick_db` con schemas `acl`, `core`, `payments`)
- (Opcional) Xcode para iOS, Android SDK para Android

## Configuración rápida (dev)
1) Variables y connection strings
   - `src/ComunaClick.Acl/appsettings.json`
   - `src/ComunaClick.Api/appsettings.json`
   - `src/ComunaClick/appsettings.Development.json`

2) Certificado HTTPS local (opcional)
   - Cert propio en `infra/certs/localhost.pfx` (password: `a12050939K`)
   - Importar y confiar en macOS:
     `sudo security add-trusted-cert -d -r trustRoot -k ~/Library/Keychains/login.keychain-db infra/certs/localhost.crt`

3) Ejecutar
```bash
dotnet run --project src/ComunaClick.Acl
dotnet run --project src/ComunaClick.Api
dotnet run --project src/ComunaClick
```

## Endpoints locales
- App: `https://localhost:7224`
- ACL: `http://localhost:5135/swagger`
- API: `http://localhost:5277/swagger`

## Producción AWS
- Dominio raíz: `https://comunaclic.cl`
- App: `https://app.comunaclic.cl`
- ACL: `https://acl.comunaclic.cl`
- API: `https://api.comunaclic.cl`
- Proxy interno en EC2:
  - App -> `127.0.0.1:5103`
  - ACL -> `127.0.0.1:5102`
  - API -> `127.0.0.1:5101`

Nota:
- En producción no usar `localhost:5135` ni `localhost:5277` para clientes HTTP del frontend o la API.
- El repo ya incluye overrides de producción:
  - `src/ComunaClick/appsettings.Production.json`
  - `src/ComunaClick.Api/appsettings.Production.json`

## Mejoras recientes
- Se estabilizó la publicación en AWS con script dedicado de `publish + deploy` hacia el EC2 `3.92.248.0`.
- Se corrigieron referencias de producción que estaban apuntando a `localhost`, reemplazándolas por `acl.comunaclic.cl` y `api.comunaclic.cl`.
- Se emitió y configuró certificado Let's Encrypt válido para:
  - `comunaclic.cl`
  - `app.comunaclic.cl`
  - `acl.comunaclic.cl`
  - `api.comunaclic.cl`
- Se arregló la resolución Razor de layouts/componentes en `SharedUI`, lo que permitió volver a publicar la app web sin errores de compilación.
- Se actualizó el branding:
  - logo principal del sitio
  - imagen dedicada para loaders
- Se incorporó loader visual con branding en páginas de carga del frontend.
- Se agregaron tolerancias a errores HTTP (`403`, `404`, etc.) en varias páginas partner para evitar que Blazor Server corte el circuito completo.
- Se corrigieron mapeos EF/PostgreSQL en `CoreDbContext` para entidades con columnas `snake_case`.
- Se agregó migración SQL para columnas geográficas de catálogo (`country_id`, `region_id`, `comuna_id`) en productos, servicios y profesionales.
- En login, el CTA `¿No tienes cuenta? Regístrate gratis` ya navega al flujo real de `/register` en vez de quedar en `#`.

## Multi-tenant por comuna (resumen)
- Tenant real = comuna (`core.tenants.comuna_id`).
- Catálogo geográfico: `core.countries`, `core.regions`, `core.comunas`.
- ACL: `users.is_super_admin` y tablas `user_*_access` para scopes.
- Vistas: `core.v_tenant_geo`, `acl.v_user_accessible_tenants`, `acl.v_user_can_access_tenant`.

## Seeds y reset
Scripts en `infra/seeds/`:
- `core_reset.sql`, `core_seed.sql`
- `acl_reset.sql`, `acl_seed.sql`
- `payments_reset.sql`, `payments_seed.sql`

Nota: si ya tienes datos reales de país/región/comuna, no ejecutes los inserts de geografía.

## Login demo
- Email: `owner@comunaclic.test`
- Password: `test123`
- Tenant: `11111111-1111-1111-1111-111111111111`
- Partner: `22222222-2222-2222-2222-222222222222`

## UI pública
Incluye Home, Login, Registro de negocio, Centro de ayuda, Privacidad, Términos y páginas Discover.

Estado actual:
- `/login` ofrece rutas separadas para cuenta personal y registro de negocio.
- `/register` crea cuenta real en ACL y usa `intent=buyer|partner` para dirigir el siguiente paso.
- `POST /v1/auth/register` ya está expuesto en ACL y conectado al frontend.
- El onboarding de negocio continúa en `/register/business` para partners.

## Notas
Para más contexto del estado del proyecto, revisar `agent.md`.
