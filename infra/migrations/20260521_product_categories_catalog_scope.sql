-- Catálogo separado: comercio (tipo A) vs servicios (tipo B)
ALTER TABLE core.product_categories
    ADD COLUMN IF NOT EXISTS catalog_scope text NOT NULL DEFAULT 'commerce';

UPDATE core.product_categories
SET catalog_scope = 'commerce'
WHERE catalog_scope IS NULL OR btrim(catalog_scope) = '';

CREATE INDEX IF NOT EXISTS ix_product_categories_catalog_scope
    ON core.product_categories (catalog_scope);

-- Categorías de servicios (empresas tipo B) — alineadas al hub público
INSERT INTO core.product_categories (id, code, name, catalog_scope, sort_order, is_active)
VALUES
    ('81000000-0000-0000-0000-000000000001', 'svc-farmacias', 'Farmacias y salud', 'service', 10, true),
    ('81000000-0000-0000-0000-000000000002', 'svc-agronegocios', 'Agronegocios y campo', 'service', 20, true),
    ('81000000-0000-0000-0000-000000000003', 'svc-automotriz', 'Automotriz y mecánica', 'service', 30, true),
    ('81000000-0000-0000-0000-000000000004', 'svc-clinicas-salud', 'Clínicas y salud', 'service', 40, true),
    ('81000000-0000-0000-0000-000000000005', 'svc-construccion', 'Construcción y obras', 'service', 50, true),
    ('81000000-0000-0000-0000-000000000006', 'svc-peluquerias', 'Peluquerías y estética', 'service', 60, true),
    ('81000000-0000-0000-0000-000000000007', 'svc-alimentos-bar', 'Alimentos y bar', 'service', 70, true),
    ('81000000-0000-0000-0000-000000000008', 'svc-educacion', 'Educación y cursos', 'service', 80, true),
    ('81000000-0000-0000-0000-000000000009', 'svc-tecnologia', 'Tecnología y computación', 'service', 90, true),
    ('81000000-0000-0000-0000-00000000000a', 'svc-hogar-reparacion', 'Hogar y reparación', 'service', 100, true),
    ('81000000-0000-0000-0000-00000000000b', 'svc-limpieza', 'Limpieza y mantención', 'service', 110, true),
    ('81000000-0000-0000-0000-00000000000c', 'svc-inmobiliaria', 'Inmobiliaria e inmuebles', 'service', 120, true)
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    catalog_scope = EXCLUDED.catalog_scope,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;

INSERT INTO core.product_subcategories (id, category_id, code, name, sort_order, is_active)
VALUES
    ('81010000-0000-0000-0000-000000000001', '81000000-0000-0000-0000-000000000001', 'svc-farmacias-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000002', '81000000-0000-0000-0000-000000000002', 'svc-agronegocios-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000003', '81000000-0000-0000-0000-000000000003', 'svc-automotriz-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000004', '81000000-0000-0000-0000-000000000004', 'svc-clinicas-salud-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000005', '81000000-0000-0000-0000-000000000005', 'svc-construccion-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000006', '81000000-0000-0000-0000-000000000006', 'svc-peluquerias-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000007', '81000000-0000-0000-0000-000000000007', 'svc-alimentos-bar-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000008', '81000000-0000-0000-0000-000000000008', 'svc-educacion-general', 'General', 1, true),
    ('81010000-0000-0000-0000-000000000009', '81000000-0000-0000-0000-000000000009', 'svc-tecnologia-general', 'General', 1, true),
    ('81010000-0000-0000-0000-00000000000a', '81000000-0000-0000-0000-00000000000a', 'svc-hogar-reparacion-general', 'General', 1, true),
    ('81010000-0000-0000-0000-00000000000b', '81000000-0000-0000-0000-00000000000b', 'svc-limpieza-general', 'General', 1, true),
    ('81010000-0000-0000-0000-00000000000c', '81000000-0000-0000-0000-00000000000c', 'svc-inmobiliaria-general', 'General', 1, true)
ON CONFLICT (code) DO UPDATE
SET
    category_id = EXCLUDED.category_id,
    name = EXCLUDED.name,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;
