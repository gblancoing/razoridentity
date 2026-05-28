-- Ubicación por producto y categorías marketplace para descubrimiento.
SET search_path TO core, public;

ALTER TABLE core.products
    ADD COLUMN IF NOT EXISTS product_address text NULL,
    ADD COLUMN IF NOT EXISTS latitude double precision NULL,
    ADD COLUMN IF NOT EXISTS longitude double precision NULL;

CREATE INDEX IF NOT EXISTS ix_core_products_latitude ON core.products (latitude);
CREATE INDEX IF NOT EXISTS ix_core_products_longitude ON core.products (longitude);

CREATE TABLE IF NOT EXISTS core.product_discovery_subcategories (
    product_id uuid NOT NULL REFERENCES core.products (id) ON DELETE CASCADE,
    subcategory_id uuid NOT NULL REFERENCES core.product_subcategories (id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (product_id, subcategory_id)
);

CREATE INDEX IF NOT EXISTS ix_core_product_discovery_subcategories_subcategory
    ON core.product_discovery_subcategories (subcategory_id);
