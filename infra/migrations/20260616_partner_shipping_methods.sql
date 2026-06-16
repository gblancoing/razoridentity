-- Métodos de envío configurables por comercio (tipo A).
-- El comercio puede activar una o más opciones combinables.
ALTER TABLE core.partners
    ADD COLUMN IF NOT EXISTS shipping_courier_paid_enabled     boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS shipping_free_over_amount_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS shipping_free_over_amount         numeric(12,2),
    ADD COLUMN IF NOT EXISTS shipping_delivery_zone_enabled    boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS shipping_free_enabled             boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN core.partners.shipping_courier_paid_enabled     IS 'El cliente paga y coordina el courier (ej: BluExpress).';
COMMENT ON COLUMN core.partners.shipping_free_over_amount_enabled IS 'Envío gratis cuando el pedido supera un monto mínimo.';
COMMENT ON COLUMN core.partners.shipping_free_over_amount         IS 'Monto mínimo (CLP) para envío gratis; null cuando la opción está desactivada.';
COMMENT ON COLUMN core.partners.shipping_delivery_zone_enabled    IS 'Usa el sistema de repartidores de la plataforma para zonas cercanas.';
COMMENT ON COLUMN core.partners.shipping_free_enabled             IS 'El comercio absorbe siempre el costo de envío.';
