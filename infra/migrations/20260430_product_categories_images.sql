ALTER TABLE core.product_categories
    ADD COLUMN IF NOT EXISTS image_url text;

INSERT INTO core.product_categories (id, code, name, image_url, sort_order, is_active)
VALUES
    (gen_random_uuid(), 'ALIMENTOS', 'Alimentos', '_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png', 1, true),
    (gen_random_uuid(), 'food-beverages', 'Alimentos y Bebidas', '_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png', 10, true),
    (gen_random_uuid(), 'food-drink', 'Alimentos y Bebidas', '_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png', 10, true),
    (gen_random_uuid(), 'home-cleaning', 'Hogar y Limpieza', '_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png', 20, true),
    (gen_random_uuid(), 'health-personal-care', 'Salud y Cuidado Personal', '_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png', 30, true),
    (gen_random_uuid(), 'health-care', 'Salud y Cuidado Personal', '_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png', 30, true),
    (gen_random_uuid(), 'babies-kids', 'Bebés y Niños', '_content/ComunaClick.SharedUI/category-images/regalos.png', 40, true),
    (gen_random_uuid(), 'fashion-accessories', 'Moda y Accesorios', '_content/ComunaClick.SharedUI/category-images/moda.png', 50, true),
    (gen_random_uuid(), 'tech-accessories', 'Tecnología y Accesorios', '_content/ComunaClick.SharedUI/category-images/tecnologia.png', 60, true),
    (gen_random_uuid(), 'gifts-celebrations', 'Regalos y Celebraciones', '_content/ComunaClick.SharedUI/category-images/regalos.png', 70, true),
    (gen_random_uuid(), 'sports-outdoors', 'Deportes y Aire Libre', '_content/ComunaClick.SharedUI/category-images/deporte.png', 80, true),
    (gen_random_uuid(), 'stationery-office', 'Librería y Oficina', '_content/ComunaClick.SharedUI/category-images/libreria.png', 90, true),
    (gen_random_uuid(), 'crafts-entrepreneurs', 'Artesanía y Emprendimientos', '_content/ComunaClick.SharedUI/category-images/artesania.png', 100, true)
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    image_url = EXCLUDED.image_url,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;
