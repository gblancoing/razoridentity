-- Marca de descuento de stock por orden (fulfillment idempotente).
-- Las órdenes ya pagadas se marcan como descontadas para no volver a descontar stock.
ALTER TABLE core.orders
    ADD COLUMN IF NOT EXISTS inventory_fulfilled_at timestamptz NULL;

UPDATE core.orders
SET inventory_fulfilled_at = updated_at
WHERE inventory_fulfilled_at IS NULL
  AND lower(status) IN ('paid', 'approved');
