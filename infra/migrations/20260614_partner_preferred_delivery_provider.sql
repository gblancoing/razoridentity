-- Transportista preferido del comercio para sus despachos.
-- null = la plataforma asigna automático por zona (comuna > región > global).
ALTER TABLE core.partners
    ADD COLUMN IF NOT EXISTS preferred_delivery_provider_id uuid;

COMMENT ON COLUMN core.partners.preferred_delivery_provider_id IS
    'FK lógica a core.delivery_providers; null = transportista automático por zona.';
