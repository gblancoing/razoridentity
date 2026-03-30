BEGIN;

ALTER TABLE core.products
  ADD COLUMN IF NOT EXISTS country_id uuid NULL,
  ADD COLUMN IF NOT EXISTS region_id uuid NULL,
  ADD COLUMN IF NOT EXISTS comuna_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_core_products_country ON core.products (country_id);
CREATE INDEX IF NOT EXISTS ix_core_products_region ON core.products (region_id);
CREATE INDEX IF NOT EXISTS ix_core_products_comuna ON core.products (comuna_id);

ALTER TABLE core.services
  ADD COLUMN IF NOT EXISTS country_id uuid NULL,
  ADD COLUMN IF NOT EXISTS region_id uuid NULL,
  ADD COLUMN IF NOT EXISTS comuna_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_core_services_country ON core.services (country_id);
CREATE INDEX IF NOT EXISTS ix_core_services_region ON core.services (region_id);
CREATE INDEX IF NOT EXISTS ix_core_services_comuna ON core.services (comuna_id);

ALTER TABLE core.professionals
  ADD COLUMN IF NOT EXISTS country_id uuid NULL,
  ADD COLUMN IF NOT EXISTS region_id uuid NULL,
  ADD COLUMN IF NOT EXISTS comuna_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_core_professionals_country ON core.professionals (country_id);
CREATE INDEX IF NOT EXISTS ix_core_professionals_region ON core.professionals (region_id);
CREATE INDEX IF NOT EXISTS ix_core_professionals_comuna ON core.professionals (comuna_id);

COMMIT;
