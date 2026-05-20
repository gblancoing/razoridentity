-- Dirección de despacho del comprador (perfil buyer).
ALTER TABLE core.customers
  ADD COLUMN IF NOT EXISTS country_id uuid NULL REFERENCES core.countries(id),
  ADD COLUMN IF NOT EXISTS region_id uuid NULL REFERENCES core.regions(id),
  ADD COLUMN IF NOT EXISTS comuna_id uuid NULL REFERENCES core.comunas(id),
  ADD COLUMN IF NOT EXISTS address text NULL,
  ADD COLUMN IF NOT EXISTS latitude double precision NULL,
  ADD COLUMN IF NOT EXISTS longitude double precision NULL;

CREATE INDEX IF NOT EXISTS ix_core_customers_comuna ON core.customers(comuna_id);

COMMENT ON COLUMN core.customers.address IS 'Calle, número, depto. u otras referencias para despacho.';
COMMENT ON COLUMN core.customers.latitude IS 'Latitud del punto en mapa (opcional).';
COMMENT ON COLUMN core.customers.longitude IS 'Longitud del punto en mapa (opcional).';
