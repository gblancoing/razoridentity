ALTER TABLE core.partners
    ADD COLUMN IF NOT EXISTS offers_services boolean NOT NULL DEFAULT false;

ALTER TABLE core.products
    ADD COLUMN IF NOT EXISTS cost_price numeric(14,2),
    ADD COLUMN IF NOT EXISTS partner_catalog_category_id uuid;

CREATE TABLE IF NOT EXISTS core.partner_catalog_categories (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    partner_id uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
    name text NOT NULL,
    sort_order int NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_partner_catalog_categories_partner
    ON core.partner_catalog_categories (partner_id, sort_order);

ALTER TABLE core.products
    DROP CONSTRAINT IF EXISTS fk_products_partner_catalog_category;

ALTER TABLE core.products
    ADD CONSTRAINT fk_products_partner_catalog_category
    FOREIGN KEY (partner_catalog_category_id)
    REFERENCES core.partner_catalog_categories(id)
    ON DELETE SET NULL;

CREATE TABLE IF NOT EXISTS core.product_images (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    product_id uuid NOT NULL REFERENCES core.products(id) ON DELETE CASCADE,
    url text NOT NULL,
    sort_order int NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_product_images_product
    ON core.product_images (product_id, sort_order);
