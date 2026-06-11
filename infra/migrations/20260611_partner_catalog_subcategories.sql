-- Subcategorías propias del local: jerarquía de 2 niveles en partner_catalog_categories.
-- Idempotente: se puede ejecutar más de una vez sin efectos.

ALTER TABLE core.partner_catalog_categories
    ADD COLUMN IF NOT EXISTS parent_id uuid;

ALTER TABLE core.partner_catalog_categories
    DROP CONSTRAINT IF EXISTS fk_partner_catalog_categories_parent;

ALTER TABLE core.partner_catalog_categories
    ADD CONSTRAINT fk_partner_catalog_categories_parent
    FOREIGN KEY (parent_id)
    REFERENCES core.partner_catalog_categories(id);

CREATE INDEX IF NOT EXISTS idx_partner_catalog_categories_parent
    ON core.partner_catalog_categories (partner_id, parent_id);
