# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ComunaClic is a marketplace + management panel for local businesses in Chile. Multi-tenant, where **a tenant = a comuna** (`core.tenants.comuna_id`). Stack: .NET 8 + Blazor (Server) for web, MAUI Blazor Hybrid for mobile, PostgreSQL, Mercado Pago for payments.

## Common commands

Run from repo root. Solution file is `ComunaClick.sln`.

```bash
# Run the three-service dev stack (each in its own terminal)
dotnet run --project src/ComunaClick.Acl                       # auth API   -> http://localhost:5135/swagger
dotnet run --project src/ComunaClick.Api                       # business API -> http://localhost:5277/swagger
dotnet run --project src/ComunaClick/ComunaClick.App.csproj    # Blazor web  -> https://localhost:7224

# Build / tests
dotnet build ComunaClick.sln
dotnet test tests/ComunaClick.Tests.Unit                       # xUnit + EF InMemory
dotnet test tests/ComunaClick.Tests.Unit --filter "FullyQualifiedName~Orders"   # single class/namespace
dotnet test tests/ComunaClick.Tests.Unit --filter "DisplayName~<test name>"     # single test
```

In Visual Studio, open `ComunaClick.sln` and use the **solution launch profile** "ComunaClic — local (ACL + API + App)" (`ComunaClick.slnLaunch`), not a single startup project. F5.

**Do not** run two copies of the repo under debug at once — they share ports (5135, 5277, 7224).

The `global.json` pins SDK `8.0.418` with `rollForward: latestMajor`. Web/API/tests are `net8.0`; **only `src/ComunaClick.Mobile` targets `net10.0`** (android/ios/maccatalyst/windows) — building it needs the .NET 10 SDK and MAUI workloads, and it is not part of the normal dev stack.

## Database

Single PostgreSQL DB `comunaclick_db` with three schemas: `acl` (auth), `core` (business), `payments`. `CoreDbContext` uses `HasDefaultSchema("core")` and maps entities to **snake_case** table/column names — when adding entities, mirror the existing explicit `ToTable`/column mappings; EF's default PascalCase will not match the DB.

Seeds live in `infra/seeds/`. Apply ACL users via `infra/seeds/Apply-AclSeeds.ps1` (falls back to `tools/RunSqlSeeds` via Npgsql if `psql` is absent). Order matters: `acl_seed.sql` then `acl_seed_test_users_all_roles.sql`. All seeded test users use password `test123` and must have a **PBKDF2** `password_hash` (not plaintext) or login returns 401. For local-only plaintext, set `PasswordHashing:AllowPlainText: true` in `ComunaClick.Acl/appsettings.Development.json`.

See `README.md` for the full seed/import matrix (psql, pgAdmin, dotnet-only).

## Architecture

Three deployables + shared libraries:

- **`ComunaClick.Acl`** — auth/authz API. Issues JWTs. Handles Google Sign-In. Owns the `acl` schema (`users.is_super_admin`, `user_*_access` scope tables).
- **`ComunaClick.Api`** — business/core API. Organized by **feature module** under `Modules/<Feature>/` (Catalog, Cart, Orders, Checkout, Bookings, Payments, Delivery, Marketplace, Notifications, Inbox, Onboarding, etc.). Each module holds its own `*Controller.cs`, services, and `Contracts/` DTOs. `Persistence/` has `CoreDbContext` + `PaymentsDbContext` and `Entities/`. Add new features as a new module folder, not by extending unrelated controllers.
- **`ComunaClick` (`ComunaClick.App`)** — Blazor Server web host. Thin — nearly all UI lives in `ComunaClick.SharedUI`.
- **`ComunaClick.SharedUI`** — the real UI: Razor `Pages/` (grouped `Buyer/`, `Partner/`, public), `Layouts/`, `Components/`, `Services/` (incl. `LocaleService` for i18n), `Http/`. Referenced by **both** the web app and mobile — keep it host-agnostic.
- **`ComunaClick.Mobile`** — MAUI Blazor Hybrid; reuses `SharedUI` + `Shared`.
- **`ComunaClick.Shared`** — DTOs + typed HTTP clients (e.g. `Api/Partner/PartnerApiClient.cs`) the UI uses to call the APIs.
- **`ComunaClick.Common`** — cross-cutting primitives: `Auth`, `Results` (Result type), `Types` (Money), `Errors`, `Funnel`.
- **`Payments.*`** — `Payments.Gateway.Api` (Mercado Pago marketplace gateway, port 5207, only in the extended launch profile), `Payments.Common` contracts, `Payments.App`.

Frontend HTTP clients are configured from `appsettings` — `appsettings.Development.json` points at local API ports; `appsettings.Production.json` at `acl.comunaclic.cl` / `api.comunaclic.cl`. Never hardcode `localhost` for production clients.

### Partner types (business rules)

Partners have a type that changes catalog/checkout behavior. When touching catalog, cart, orders, bookings, or the partner/buyer pages, respect these — full specs in `docs/empresa-tipo-a.md` and `docs/empresa-tipo-b.md`, summarized in `.cursor/rules/*.mdc`:

- **Type A (comercio / goods):** product catalog with stock. Available stock = physical − reservations (`payment_pending`/`processing`); validate in cart/orders, decrement on `paid`; `stock_low`/`stock_out` alerts. `CostPrice` is partner-panel only — **never** expose in public API or buyer views. Can be hybrid (`OffersServices`) with a Services tab following Type B rules.
- **Type B (servicios):** service listings only, no product inventory. Priced service (> 0) requires the booking/agenda engine with **mandatory online payment** to confirm; unpriced service is informational (contact via inbox/WhatsApp). Availability/bookings are **per professional**.

Changes must not break the other types; keep them scoped. Localize UI strings via `LocaleService` (`partner.catalog.*` keys).

## Conventions

- Do not edit source files with PowerShell `Set-Content`/`Out-File` — it corrupts Spanish accents (mojibake). Use the Edit tool; if verifying, grep for `Ã` to detect corruption.
- Final change summaries for this user are written in Spanish.
- Frontend changes must be verified at mobile width (~375px); the project has an active mobile-first redesign.
- Payment/marketplace specifics: see `README_MERCADOPAGO_MARKETPLACE.md`.
