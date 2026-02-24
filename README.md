# ComunaClic

Marketplace y panel de gestión para negocios locales. Stack principal: .NET 8 + Blazor + APIs.

## Estructura del repo
- `src/ComunaClick.App` — Blazor Web App (UI).
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
   - `src/ComunaClick.App/appsettings.Development.json`

2) Certificado HTTPS local (opcional)
   - Cert propio en `infra/certs/localhost.pfx` (password: `a12050939K`)
   - Importar y confiar en macOS:
     `sudo security add-trusted-cert -d -r trustRoot -k ~/Library/Keychains/login.keychain-db infra/certs/localhost.crt`

3) Ejecutar
```bash
dotnet run --project src/ComunaClick.Acl
dotnet run --project src/ComunaClick.Api
dotnet run --project src/ComunaClick.App
```

## Endpoints locales
- App: `https://localhost:7224`
- ACL: `http://localhost:5135/swagger`
- API: `http://localhost:5277/swagger`

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

## Notas
Para más contexto del estado del proyecto, revisar `agent.md`.
