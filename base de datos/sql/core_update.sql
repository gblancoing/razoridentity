-- =========================================================
-- core_update.sql
-- Actualiza schema core para soportar:
-- - Catálogo geográfico (countries/regions/comunas) + georeferencia
-- - Tenant real = comuna (core.tenants.comuna_id)
-- Ejecutar en la DB: acl_db
-- =========================================================

-- (Opcional) extensiones si no existen
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Schema (no cambia owner si ya existe)
CREATE SCHEMA IF NOT EXISTS core;

-- =========================
-- 1) Catálogo geográfico
-- =========================
CREATE TABLE IF NOT EXISTS core.countries (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  code       text NOT NULL UNIQUE,   -- ej: CL
  name       text NOT NULL,
  is_active  boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS core.regions (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  country_id      uuid NOT NULL REFERENCES core.countries(id) ON DELETE RESTRICT,
  code            text NOT NULL,      -- ej: RM (o el que uses)
  name            text NOT NULL,
  legacy_region_id integer NULL,      -- id del dataset (ej gist)
  is_active       boolean NOT NULL DEFAULT true,
  created_at      timestamptz NOT NULL DEFAULT now(),
  UNIQUE (country_id, code)
);

CREATE INDEX IF NOT EXISTS ix_core_regions_country
  ON core.regions(country_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_core_regions_legacy
  ON core.regions(country_id, legacy_region_id)
  WHERE legacy_region_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS core.comunas (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  region_id         uuid NOT NULL REFERENCES core.regions(id) ON DELETE RESTRICT,
  code              text NOT NULL,          -- ej: 13101 (o code interno)
  name              text NOT NULL,
  legacy_comuna_id  integer NULL,           -- id del dataset (ej gist)
  latitude          double precision NULL,
  longitude         double precision NULL,
  is_active         boolean NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now(),
  UNIQUE (region_id, code)
);

CREATE INDEX IF NOT EXISTS ix_core_comunas_region
  ON core.comunas(region_id);

CREATE INDEX IF NOT EXISTS ix_core_comunas_region_name
  ON core.comunas(region_id, name);

CREATE UNIQUE INDEX IF NOT EXISTS ux_core_comunas_legacy
  ON core.comunas(region_id, legacy_comuna_id)
  WHERE legacy_comuna_id IS NOT NULL;

-- =========================
-- 2) Tenant real = comuna
-- =========================
ALTER TABLE core.tenants
  ADD COLUMN IF NOT EXISTS comuna_id uuid NULL;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_tenants_comuna'
  ) THEN
    ALTER TABLE core.tenants
      ADD CONSTRAINT fk_core_tenants_comuna
      FOREIGN KEY (comuna_id)
      REFERENCES core.comunas(id)
      ON DELETE RESTRICT;
  END IF;
END$$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_core_tenants_comuna
  ON core.tenants(comuna_id)
  WHERE comuna_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_core_tenants_active_comuna
  ON core.tenants(is_active, comuna_id);

-- =========================
-- 3) Vista helper (opcional)
-- =========================
CREATE OR REPLACE VIEW core.v_tenant_geo AS
SELECT
  t.id          AS tenant_id,
  t.name        AS tenant_name,
  c.id          AS comuna_id,
  c.code        AS comuna_code,
  c.name        AS comuna_name,
  c.legacy_comuna_id,
  c.latitude,
  c.longitude,
  (c.latitude IS NOT NULL AND c.longitude IS NOT NULL) AS has_geo,
  r.id          AS region_id,
  r.code        AS region_code,
  r.name        AS region_name,
  r.legacy_region_id,
  co.id         AS country_id,
  co.code       AS country_code,
  co.name       AS country_name
FROM core.tenants t
LEFT JOIN core.comunas  c  ON c.id = t.comuna_id
LEFT JOIN core.regions  r  ON r.id = c.region_id
LEFT JOIN core.countries co ON co.id = r.country_id;
