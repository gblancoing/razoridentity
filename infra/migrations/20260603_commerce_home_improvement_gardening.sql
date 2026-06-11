-- Nueva categoría comercio: Mejoramiento del hogar y jardinería
-- Schema: core (Postgres)

BEGIN;

SET search_path TO core;

INSERT INTO product_categories (id, code, name, image_url, catalog_scope, sort_order, is_active)
VALUES (
    gen_random_uuid(),
    'home-improvement-gardening',
    'Mejoramiento del hogar y jardinería',
    '_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png',
    'commerce',
    25,
    true
)
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    image_url = EXCLUDED.image_url,
    catalog_scope = EXCLUDED.catalog_scope,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;

COMMIT;

