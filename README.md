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

Requisitos: el usuario debe existir en `acl.users` (por ejemplo aplicando `infra/seeds/acl_seed.sql`). La columna `password_hash` debe ser un hash **PBKDF2** como genera el registro en ACL, no la contraseña en texto plano; si tu base fue sembrada con el seed antiguo (`test123` literal), el login devolvía **401** porque el verificador rechazaba texto plano. Solución: vuelve a ejecutar el seed actualizado o, en desarrollo, deja `PasswordHashing:AllowPlainText` en `true` en `ComunaClick.Acl/appsettings.Development.json` (solo local).

### Cargar usuarios y datos ACL en PostgreSQL (lo que sí debes usar)

Los datos **no** se importan con `Install-Package` ni con PowerShell “normal” usando `-Project`. Ese comando es **solo** de la **Consola del Administrador de Paquetes** dentro de **Visual Studio** (menú *Herramientas → Administrador de paquetes NuGet → Consola del administrador de paquetes*). En **Windows PowerShell** o **Terminal** estándar, `Install-Package` es otro cmdlet y **no** tiene el parámetro `-Project`.

Para **crear/importar** filas en la base debes ejecutar los scripts `.sql` con el cliente **`psql`** (viene con PostgreSQL) o pegar el contenido en **pgAdmin** (Query Tool).

**Opción A — Script del repo (recomendado)**  
Desde la raíz del repositorio (`ComunaClic_Actualizado`):

```powershell
cd C:\Users\gblanco\source\repos\ComunaClic_Actualizado
.\infra\seeds\Apply-AclSeeds.ps1
```

Si **no tienes `psql` instalado** (error “no se reconoce el término psql”), el mismo script pasa automáticamente a **`dotnet run`** con la herramienta `tools/RunSqlSeeds` (Npgsql). Solo necesitas **.NET 8 SDK** y que PostgreSQL esté accesible en red con la cadena que uses abajo.

Si tu contraseña de `postgres` no es `admin123`:

```powershell
.\infra\seeds\Apply-AclSeeds.ps1 -Password "tu_clave"
```

Solo el seed base (sin usuarios extra por rol):

```powershell
.\infra\seeds\Apply-AclSeeds.ps1 -SkipExtraUsers
```

**Opción B — `psql` manual** (la ruta `-f` debe ser absoluta o estar ejecutándote desde la raíz del repo):

```powershell
$env:PGPASSWORD = "admin123"
psql -h localhost -p 5432 -U postgres -d comunaclick_db -v ON_ERROR_STOP=1 -f infra/seeds/acl_seed.sql
psql -h localhost -p 5432 -U postgres -d comunaclick_db -v ON_ERROR_STOP=1 -f infra/seeds/acl_seed_test_users_all_roles.sql
Remove-Item Env:PGPASSWORD
```

**Opción C — pgAdmin**  
Abre la base `comunaclick_db` → *Tools → Query Tool* → *Open File* → elige `infra/seeds/acl_seed.sql` → Execute; repite con `acl_seed_test_users_all_roles.sql`.

**Opción D — Solo .NET (sin `psql`)**  
Desde la raíz del repo; la cadena debe coincidir con tu servidor (misma que en `appsettings.Development.json`):

```powershell
$env:COMUNACLICK_DB = "Host=localhost;Port=5432;Database=comunaclick_db;Username=postgres;Password=admin123"
dotnet run --project tools/RunSqlSeeds -c Release -- infra/seeds/acl_seed.sql infra/seeds/acl_seed_test_users_all_roles.sql
Remove-Item Env:COMUNACLICK_DB
```

Si omites los dos archivos al final, la herramienta ejecuta por defecto esos mismos dos scripts bajo `infra/seeds/`.

Orden: primero `acl_seed.sql` (roles y usuarios base), después `acl_seed_test_users_all_roles.sql` (usuarios extra). El esquema `acl` y tablas deben existir (scripts de init del repo, p. ej. `infra/comunaclick_db__01_init.sql`, si es base nueva).

### Visual Studio / NuGet (opcional, no carga la base)

Si en el futuro usas EF migrations desde Visual Studio:

```powershell
# Solo dentro de: Visual Studio → Consola del Administrador de Paquetes
Install-Package Microsoft.EntityFrameworkCore.Design -Project ComunaClick.Acl
```

En terminal normal, para la herramienta global:

```powershell
dotnet tool update --global dotnet-ef
```

### Usuarios de prueba adicionales (`acl_seed_test_users_all_roles.sql`)

Todos con contraseña **`test123`** (mismo hash PBKDF2 que el seed base).

| Email | Rol principal | Notas |
|-------|----------------|--------|
| `owner@comunaclic.test` | `partner_owner` | Ya en `acl_seed.sql`; tenant/partner demo |
| `admin@comunaclic.test` | `platform_admin` | Super admin |
| `platform.admin@comunaclic.test` | `platform_admin` | Super admin alterno |
| `tenant.admin@comunaclic.test` | `tenant_admin` | Alcance tenant demo |
| `partner.staff@comunaclic.test` | `partner_staff` | Partner demo `2222…` |
| `buyer@comunaclic.test` | `customer` | Comprador (recomendado para API `buyer.customer`) |
| `legacy.buyer@comunaclic.test` | `buyer` | Rol legado en seeds |

## UI pública
Incluye Home, Login, Registro de negocio, Centro de ayuda, Privacidad, Términos y páginas Discover.

Estado actual:
- `/login` ofrece rutas separadas para cuenta personal y registro de negocio.
- `/register` crea cuenta real en ACL y usa `intent=buyer|partner` para dirigir el siguiente paso.
- `POST /v1/auth/register` ya está expuesto en ACL y conectado al frontend.
- El onboarding de negocio continúa en `/register/business` para partners.

## Notas
Para más contexto del estado del proyecto, revisar `agent.md`.
