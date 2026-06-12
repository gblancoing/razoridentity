-- Módulo de delivery con tracking GPS en vivo.
-- Idempotente: se puede ejecutar más de una vez sin efectos.

-- Datos de envío en la orden. La dirección de destino reutiliza delivery_address.
ALTER TABLE core.orders
    ADD COLUMN IF NOT EXISTS delivery_type text,
    ADD COLUMN IF NOT EXISTS delivery_status text,
    ADD COLUMN IF NOT EXISTS courier_id uuid,
    ADD COLUMN IF NOT EXISTS courier_token_key text,
    ADD COLUMN IF NOT EXISTS origin_lat double precision,
    ADD COLUMN IF NOT EXISTS origin_lng double precision,
    ADD COLUMN IF NOT EXISTS origin_address text,
    ADD COLUMN IF NOT EXISTS destination_lat double precision,
    ADD COLUMN IF NOT EXISTS destination_lng double precision;

-- Repartidores propios de cada negocio (pueden pertenecer a empresas externas).
CREATE TABLE IF NOT EXISTS core.couriers (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    partner_id uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
    name text NOT NULL,
    phone text NOT NULL,
    company text,
    is_available boolean NOT NULL DEFAULT true,
    current_lat double precision,
    current_lng double precision,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_couriers_partner
    ON core.couriers (partner_id);

-- Historial de posiciones GPS y cambios de estado del envío.
CREATE TABLE IF NOT EXISTS core.delivery_tracking (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id uuid NOT NULL REFERENCES core.orders(id) ON DELETE CASCADE,
    courier_id uuid NOT NULL REFERENCES core.couriers(id) ON DELETE CASCADE,
    lat double precision NOT NULL,
    lng double precision NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_delivery_tracking_order_created
    ON core.delivery_tracking (order_id, created_at DESC);

ALTER TABLE core.orders
    DROP CONSTRAINT IF EXISTS fk_orders_courier;

ALTER TABLE core.orders
    ADD CONSTRAINT fk_orders_courier
    FOREIGN KEY (courier_id)
    REFERENCES core.couriers(id)
    ON DELETE SET NULL;
