-- Categoría Inmuebles + subcategorías (tipo de bien × modalidad).
-- Ejecutar contra la BD core (schema core).

BEGIN;

SET search_path TO core;

INSERT INTO product_categories (id, code, name, sort_order, is_active)
VALUES ('7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles', 'Inmuebles', 50, true)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;

INSERT INTO product_subcategories (id, category_id, code, name, sort_order, is_active) VALUES
('7200aaaa-0001-4000-8000-000000000002'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-agricolas-arriendo', 'Agrícolas — Arriendo', 1, true),
('7200aaaa-0001-4000-8000-000000000003'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-agricolas-venta', 'Agrícolas — Venta', 2, true),
('7200aaaa-0001-4000-8000-000000000004'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-bodegas-arriendo', 'Bodegas — Arriendo', 3, true),
('7200aaaa-0001-4000-8000-000000000005'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-bodegas-venta', 'Bodegas — Venta', 4, true),
('7200aaaa-0001-4000-8000-000000000006'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-casas-arriendo', 'Casas — Arriendo', 5, true),
('7200aaaa-0001-4000-8000-000000000007'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-casas-arriendo-temporal', 'Casas — Arriendo temporal', 6, true),
('7200aaaa-0001-4000-8000-000000000008'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-casas-venta', 'Casas — Venta', 7, true),
('7200aaaa-0001-4000-8000-000000000009'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-departamentos-arriendo', 'Departamentos — Arriendo', 8, true),
('7200aaaa-0001-4000-8000-00000000000a'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-departamentos-arriendo-temporal', 'Departamentos — Arriendo temporal', 9, true),
('7200aaaa-0001-4000-8000-00000000000b'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-departamentos-venta', 'Departamentos — Venta', 10, true),
('7200aaaa-0001-4000-8000-00000000000c'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-estacionamientos-arriendo', 'Estacionamientos — Arriendo', 11, true),
('7200aaaa-0001-4000-8000-00000000000d'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-estacionamientos-venta', 'Estacionamientos — Venta', 12, true),
('7200aaaa-0001-4000-8000-00000000000e'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-industriales-arriendo', 'Industriales — Arriendo', 13, true),
('7200aaaa-0001-4000-8000-00000000000f'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-industriales-venta', 'Industriales — Venta', 14, true),
('7200aaaa-0001-4000-8000-000000000010'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-locales-arriendo', 'Locales — Arriendo', 15, true),
('7200aaaa-0001-4000-8000-000000000011'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-locales-venta', 'Locales — Venta', 16, true),
('7200aaaa-0001-4000-8000-000000000012'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-loteos-arriendo', 'Loteos — Arriendo', 17, true),
('7200aaaa-0001-4000-8000-000000000013'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-loteos-venta', 'Loteos — Venta', 18, true),
('7200aaaa-0001-4000-8000-000000000014'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-lotes-cementerio-venta', 'Lotes de cementerio — Venta', 19, true),
('7200aaaa-0001-4000-8000-000000000015'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-oficinas-arriendo', 'Oficinas — Arriendo', 20, true),
('7200aaaa-0001-4000-8000-000000000016'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-oficinas-venta', 'Oficinas — Venta', 21, true),
('7200aaaa-0001-4000-8000-000000000017'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-otros-arriendo', 'Otros inmuebles — Arriendo', 22, true),
('7200aaaa-0001-4000-8000-000000000018'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-otros-arriendo-temporal', 'Otros inmuebles — Arriendo temporal', 23, true),
('7200aaaa-0001-4000-8000-000000000019'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-otros-venta', 'Otros inmuebles — Venta', 24, true),
('7200aaaa-0001-4000-8000-00000000001a'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-parcelas-arriendo', 'Parcelas — Arriendo', 25, true),
('7200aaaa-0001-4000-8000-00000000001b'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-parcelas-arriendo-temporal', 'Parcelas — Arriendo temporal', 26, true),
('7200aaaa-0001-4000-8000-00000000001c'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-parcelas-venta', 'Parcelas — Venta', 27, true),
('7200aaaa-0001-4000-8000-00000000001d'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-sitios-arriendo', 'Sitios — Arriendo', 28, true),
('7200aaaa-0001-4000-8000-00000000001e'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-sitios-venta', 'Sitios — Venta', 29, true),
('7200aaaa-0001-4000-8000-00000000001f'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-terrenos-arriendo', 'Terrenos — Arriendo', 30, true),
('7200aaaa-0001-4000-8000-000000000020'::uuid, '7200aaaa-0001-4000-8000-000000000001'::uuid, 'inmuebles-terrenos-venta', 'Terrenos — Venta', 31, true)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active,
    category_id = EXCLUDED.category_id;

COMMIT;
