-- =========================================================
-- Etapa 1.1 - Geo explícita operativa + backfill desde tenant
-- Compatibilidad: mantiene tenant_id como legacy/transición
-- =========================================================

CREATE SCHEMA IF NOT EXISTS core;

-- 1) Agregar columnas geo explícitas a entidades operativas clave
ALTER TABLE core.partners       ADD COLUMN IF NOT EXISTS country_id uuid NULL;
ALTER TABLE core.partners       ADD COLUMN IF NOT EXISTS region_id  uuid NULL;
ALTER TABLE core.partners       ADD COLUMN IF NOT EXISTS comuna_id  uuid NULL;

ALTER TABLE core.products       ADD COLUMN IF NOT EXISTS country_id uuid NULL;
ALTER TABLE core.products       ADD COLUMN IF NOT EXISTS region_id  uuid NULL;
ALTER TABLE core.products       ADD COLUMN IF NOT EXISTS comuna_id  uuid NULL;

ALTER TABLE core.services       ADD COLUMN IF NOT EXISTS country_id uuid NULL;
ALTER TABLE core.services       ADD COLUMN IF NOT EXISTS region_id  uuid NULL;
ALTER TABLE core.services       ADD COLUMN IF NOT EXISTS comuna_id  uuid NULL;

ALTER TABLE core.professionals  ADD COLUMN IF NOT EXISTS country_id uuid NULL;
ALTER TABLE core.professionals  ADD COLUMN IF NOT EXISTS region_id  uuid NULL;
ALTER TABLE core.professionals  ADD COLUMN IF NOT EXISTS comuna_id  uuid NULL;

-- 2) FKs defensivas
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_partners_country') THEN
    ALTER TABLE core.partners ADD CONSTRAINT fk_core_partners_country FOREIGN KEY (country_id) REFERENCES core.countries(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_partners_region') THEN
    ALTER TABLE core.partners ADD CONSTRAINT fk_core_partners_region FOREIGN KEY (region_id) REFERENCES core.regions(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_partners_comuna') THEN
    ALTER TABLE core.partners ADD CONSTRAINT fk_core_partners_comuna FOREIGN KEY (comuna_id) REFERENCES core.comunas(id) ON DELETE RESTRICT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_products_country') THEN
    ALTER TABLE core.products ADD CONSTRAINT fk_core_products_country FOREIGN KEY (country_id) REFERENCES core.countries(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_products_region') THEN
    ALTER TABLE core.products ADD CONSTRAINT fk_core_products_region FOREIGN KEY (region_id) REFERENCES core.regions(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_products_comuna') THEN
    ALTER TABLE core.products ADD CONSTRAINT fk_core_products_comuna FOREIGN KEY (comuna_id) REFERENCES core.comunas(id) ON DELETE RESTRICT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_services_country') THEN
    ALTER TABLE core.services ADD CONSTRAINT fk_core_services_country FOREIGN KEY (country_id) REFERENCES core.countries(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_services_region') THEN
    ALTER TABLE core.services ADD CONSTRAINT fk_core_services_region FOREIGN KEY (region_id) REFERENCES core.regions(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_services_comuna') THEN
    ALTER TABLE core.services ADD CONSTRAINT fk_core_services_comuna FOREIGN KEY (comuna_id) REFERENCES core.comunas(id) ON DELETE RESTRICT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_professionals_country') THEN
    ALTER TABLE core.professionals ADD CONSTRAINT fk_core_professionals_country FOREIGN KEY (country_id) REFERENCES core.countries(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_professionals_region') THEN
    ALTER TABLE core.professionals ADD CONSTRAINT fk_core_professionals_region FOREIGN KEY (region_id) REFERENCES core.regions(id) ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_professionals_comuna') THEN
    ALTER TABLE core.professionals ADD CONSTRAINT fk_core_professionals_comuna FOREIGN KEY (comuna_id) REFERENCES core.comunas(id) ON DELETE RESTRICT;
  END IF;
END $$;

-- 3) Índices para lectura geo-first
CREATE INDEX IF NOT EXISTS ix_core_partners_country ON core.partners(country_id);
CREATE INDEX IF NOT EXISTS ix_core_partners_region  ON core.partners(region_id);
CREATE INDEX IF NOT EXISTS ix_core_partners_comuna  ON core.partners(comuna_id);

CREATE INDEX IF NOT EXISTS ix_core_products_country ON core.products(country_id);
CREATE INDEX IF NOT EXISTS ix_core_products_region  ON core.products(region_id);
CREATE INDEX IF NOT EXISTS ix_core_products_comuna  ON core.products(comuna_id);

CREATE INDEX IF NOT EXISTS ix_core_services_country ON core.services(country_id);
CREATE INDEX IF NOT EXISTS ix_core_services_region  ON core.services(region_id);
CREATE INDEX IF NOT EXISTS ix_core_services_comuna  ON core.services(comuna_id);

CREATE INDEX IF NOT EXISTS ix_core_professionals_country ON core.professionals(country_id);
CREATE INDEX IF NOT EXISTS ix_core_professionals_region  ON core.professionals(region_id);
CREATE INDEX IF NOT EXISTS ix_core_professionals_comuna  ON core.professionals(comuna_id);

-- 4) Backfill seguro desde tenant -> comuna -> region -> country
WITH tenant_geo AS (
  SELECT
    t.id AS tenant_id,
    c.id AS comuna_id,
    r.id AS region_id,
    co.id AS country_id
  FROM core.tenants t
  LEFT JOIN core.comunas c ON c.id = t.comuna_id
  LEFT JOIN core.regions r ON r.id = c.region_id
  LEFT JOIN core.countries co ON co.id = r.country_id
)
UPDATE core.partners p
SET comuna_id  = COALESCE(p.comuna_id, tg.comuna_id),
    region_id  = COALESCE(p.region_id, tg.region_id),
    country_id = COALESCE(p.country_id, tg.country_id)
FROM tenant_geo tg
WHERE tg.tenant_id = p.tenant_id
  AND (p.comuna_id IS NULL OR p.region_id IS NULL OR p.country_id IS NULL);

WITH tenant_geo AS (
  SELECT
    t.id AS tenant_id,
    c.id AS comuna_id,
    r.id AS region_id,
    co.id AS country_id
  FROM core.tenants t
  LEFT JOIN core.comunas c ON c.id = t.comuna_id
  LEFT JOIN core.regions r ON r.id = c.region_id
  LEFT JOIN core.countries co ON co.id = r.country_id
)
UPDATE core.products p
SET comuna_id  = COALESCE(p.comuna_id, tg.comuna_id),
    region_id  = COALESCE(p.region_id, tg.region_id),
    country_id = COALESCE(p.country_id, tg.country_id)
FROM tenant_geo tg
WHERE tg.tenant_id = p.tenant_id
  AND (p.comuna_id IS NULL OR p.region_id IS NULL OR p.country_id IS NULL);

WITH tenant_geo AS (
  SELECT
    t.id AS tenant_id,
    c.id AS comuna_id,
    r.id AS region_id,
    co.id AS country_id
  FROM core.tenants t
  LEFT JOIN core.comunas c ON c.id = t.comuna_id
  LEFT JOIN core.regions r ON r.id = c.region_id
  LEFT JOIN core.countries co ON co.id = r.country_id
)
UPDATE core.services s
SET comuna_id  = COALESCE(s.comuna_id, tg.comuna_id),
    region_id  = COALESCE(s.region_id, tg.region_id),
    country_id = COALESCE(s.country_id, tg.country_id)
FROM tenant_geo tg
WHERE tg.tenant_id = s.tenant_id
  AND (s.comuna_id IS NULL OR s.region_id IS NULL OR s.country_id IS NULL);

WITH tenant_geo AS (
  SELECT
    t.id AS tenant_id,
    c.id AS comuna_id,
    r.id AS region_id,
    co.id AS country_id
  FROM core.tenants t
  LEFT JOIN core.comunas c ON c.id = t.comuna_id
  LEFT JOIN core.regions r ON r.id = c.region_id
  LEFT JOIN core.countries co ON co.id = r.country_id
)
UPDATE core.professionals p
SET comuna_id  = COALESCE(p.comuna_id, tg.comuna_id),
    region_id  = COALESCE(p.region_id, tg.region_id),
    country_id = COALESCE(p.country_id, tg.country_id)
FROM tenant_geo tg
WHERE tg.tenant_id = p.tenant_id
  AND (p.comuna_id IS NULL OR p.region_id IS NULL OR p.country_id IS NULL);
