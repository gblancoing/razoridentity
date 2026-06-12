-- Liquidación del transporte (split al transportista) + tipo de transportista.
-- Idempotente: se aplica desde DatabaseSchemaBootstrap al arrancar el API.

-- Tipo de transportista (courier | taxi | ...), extensible a futuro.
ALTER TABLE core.couriers
    ADD COLUMN IF NOT EXISTS kind text NOT NULL DEFAULT 'courier';

-- Slip/comprobante de liquidación del envío: desglose auditable de
-- bruto cliente − fee MercadoPago (prorrateado) − comisión ComunaClic = neto transportista.
CREATE TABLE IF NOT EXISTS core.delivery_settlements (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    order_id uuid NOT NULL REFERENCES core.orders(id) ON DELETE CASCADE,
    partner_id uuid NOT NULL,
    courier_id uuid NULL REFERENCES core.couriers(id) ON DELETE SET NULL,
    payment_id uuid NULL,
    gross_amount numeric(14,2) NOT NULL DEFAULT 0,
    platform_fee_amount numeric(14,2) NOT NULL DEFAULT 0,
    mercadopago_fee_amount numeric(14,2) NULL,
    net_to_courier_amount numeric(14,2) NOT NULL DEFAULT 0,
    currency text NOT NULL DEFAULT 'CLP',
    status text NOT NULL DEFAULT 'pending',
    settled_at timestamptz NULL,
    notes text NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_delivery_settlements_order
    ON core.delivery_settlements (order_id);
CREATE INDEX IF NOT EXISTS ix_delivery_settlements_partner
    ON core.delivery_settlements (partner_id);
CREATE INDEX IF NOT EXISTS ix_delivery_settlements_courier
    ON core.delivery_settlements (courier_id);
