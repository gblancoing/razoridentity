-- =========================================================
-- 01_core_geo.sql
-- Ejecutar conectado a la DB: acl_db
-- =========================================================

-- Asegura schema
CREATE SCHEMA IF NOT EXISTS core AUTHORIZATION core_app_user;

-- =========================
-- 1) Catálogo geográfico
-- =========================

CREATE TABLE IF NOT EXISTS core.countries (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  code       text NOT NULL UNIQUE,  -- ej: CL
  name       text NOT NULL,
  is_active  boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS core.regions (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  country_id uuid NOT NULL REFERENCES core.countries(id) ON DELETE RESTRICT,
  code       text NOT NULL,          -- ej: RM
  name       text NOT NULL,
  is_active  boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (country_id, code)
);

CREATE INDEX IF NOT EXISTS ix_core_regions_country
  ON core.regions(country_id);

CREATE TABLE IF NOT EXISTS core.comunas (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  region_id  uuid NOT NULL REFERENCES core.regions(id) ON DELETE RESTRICT,
  code       text NOT NULL,          -- ej: 13101 (si usas código oficial)
  name       text NOT NULL,
  is_active  boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (region_id, code)
);

CREATE INDEX IF NOT EXISTS ix_core_comunas_region
  ON core.comunas(region_id);

-- =========================
-- 2) Tenant real = comuna
--    (agrega comuna_id a core.tenants)
-- =========================

ALTER TABLE core.tenants
  ADD COLUMN IF NOT EXISTS comuna_id uuid NULL;

-- FK (no cascades para evitar borrados accidentales)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'fk_core_tenants_comuna'
  ) THEN
    ALTER TABLE core.tenants
      ADD CONSTRAINT fk_core_tenants_comuna
      FOREIGN KEY (comuna_id)
      REFERENCES core.comunas(id)
      ON DELETE RESTRICT;
  END IF;
END$$;

-- 1 tenant por comuna (único cuando comuna_id no es NULL)
CREATE UNIQUE INDEX IF NOT EXISTS ux_core_tenants_comuna
  ON core.tenants(comuna_id)
  WHERE comuna_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_core_tenants_comuna
  ON core.tenants(comuna_id);

-- =========================
-- 3) (Opcional) Vista para resolver geografía de un tenant
-- =========================
CREATE OR REPLACE VIEW core.v_tenant_geo AS
SELECT
  t.id          AS tenant_id,
  t.name        AS tenant_name,
  c.id          AS comuna_id,
  c.name        AS comuna_name,
  r.id          AS region_id,
  r.name        AS region_name,
  co.id         AS country_id,
  co.name       AS country_name
FROM core.tenants t
LEFT JOIN core.comunas  c  ON c.id = t.comuna_id
LEFT JOIN core.regions  r  ON r.id = c.region_id
LEFT JOIN core.countries co ON co.id = r.country_id;
