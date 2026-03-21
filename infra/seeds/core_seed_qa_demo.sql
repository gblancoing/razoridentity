BEGIN;

SET search_path TO core;

-- Defensive compatibility for current main schema/state
CREATE TABLE IF NOT EXISTS product_categories (
    id uuid PRIMARY KEY,
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    sort_order integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS product_subcategories (
    id uuid PRIMARY KEY,
    category_id uuid NOT NULL REFERENCES product_categories(id),
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    sort_order integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true
);

ALTER TABLE partners ADD COLUMN IF NOT EXISTS subcategory_id uuid NULL;
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_partners_subcategory_id'
    ) THEN
        ALTER TABLE partners
            ADD CONSTRAINT fk_partners_subcategory_id
            FOREIGN KEY (subcategory_id) REFERENCES product_subcategories(id);
    END IF;
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

-- Demo tenant/comuna base
INSERT INTO countries (id, code, name, is_active, created_at)
VALUES ('10000000-0000-0000-0000-000000000001', 'CL', 'Chile', true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO regions (id, country_id, code, name, legacy_region_id, is_active, created_at)
VALUES ('10000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'RM', 'Región Metropolitana', 13, true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO comunas (id, region_id, code, name, legacy_comuna_id, latitude, longitude, is_active, created_at)
VALUES ('216f98f0-541a-47e1-b586-ed530fddf50d', '10000000-0000-0000-0000-000000000002', 'ALHUE', 'Alhué', 13502, -34.0333, -71.1000, true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO tenants (id, comuna_id, name, timezone, config_json, is_active, created_at, updated_at)
VALUES ('70000000-0000-0000-0000-000000000001', '216f98f0-541a-47e1-b586-ed530fddf50d', 'ComunaClic QA Demo Alhué', 'America/Santiago', '{}'::jsonb, true, now(), now())
ON CONFLICT (comuna_id) DO UPDATE
SET name = EXCLUDED.name,
    timezone = EXCLUDED.timezone,
    config_json = EXCLUDED.config_json,
    is_active = EXCLUDED.is_active,
    updated_at = now();

-- Catalog taxonomy for tipo A
INSERT INTO product_categories (id, code, name, sort_order, is_active)
VALUES ('71000000-0000-0000-0000-000000000001', 'ALIMENTOS', 'Alimentos', 1, true)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_subcategories (id, category_id, code, name, sort_order, is_active)
VALUES ('71000000-0000-0000-0000-000000000002', '71000000-0000-0000-0000-000000000001', 'PANADERIA', 'Panadería', 1, true)
ON CONFLICT (id) DO NOTHING;

-- Partners A/B/C + one paused/non-eligible
INSERT INTO partners (id, tenant_id, country_id, region_id, comuna_id, subcategory_id, type, name, rut, address, phone, email, is_visible, created_at, updated_at)
VALUES
('72000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', '71000000-0000-0000-0000-000000000002', 'A', 'Panadería Los Naranjos', '76.111.111-1', 'Camino Principal 101, Alhué', '+56 9 7000 0001', 'hola@naranjos.demo', true, now(), now()),
('72000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', null, 'B', 'Kine Alhué Centro', '76.222.222-2', 'Av. Salud 202, Alhué', '+56 9 7000 0002', 'agenda@kinealhue.demo', true, now(), now()),
('72000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', null, 'C', 'Red Profesional Alhué', '76.333.333-3', 'Pasaje Oficios 303, Alhué', '+56 9 7000 0003', 'contacto@redprofesional.demo', true, now(), now()),
('72000000-0000-0000-0000-000000000004', '70000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', '71000000-0000-0000-0000-000000000002', 'A', 'Local Pausado QA', '76.444.444-4', 'Esquina Test 404, Alhué', '+56 9 7000 0004', 'pausado@qa.demo', false, now(), now())
ON CONFLICT (id) DO NOTHING;

-- Customers: 2 valid + 1 incomplete QA
INSERT INTO customers (id, tenant_id, email, phone, full_name, created_at, updated_at)
VALUES
('73000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', 'compradora1@demo.cl', '+56 9 7111 1111', 'Camila Pérez', now(), now()),
('73000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', 'comprador2@demo.cl', '+56 9 7222 2222', 'Jorge Soto', now(), now()),
('73000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000001', null, null, null, now(), now())
ON CONFLICT (id) DO NOTHING;

-- Products tipo A: 3 active + 1 inactive
INSERT INTO products (id, tenant_id, partner_id, country_id, region_id, comuna_id, name, description, category, price, currency, is_active, created_at, updated_at)
VALUES
('74000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Marraqueta 1kg', 'Pan fresco del día', 'Panadería', 2500, 'CLP', true, now(), now()),
('74000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Kuchen de nuez', 'Kuchen familiar artesanal', 'Pastelería', 8900, 'CLP', true, now(), now()),
('74000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Empanada de pino', 'Empanada horneada individual', 'Panadería', 2200, 'CLP', true, now(), now()),
('74000000-0000-0000-0000-000000000004', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Producto inactivo QA', 'No debe salir en discovery', 'QA', 999, 'CLP', false, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_inventory (product_id, quantity, updated_at)
VALUES
('74000000-0000-0000-0000-000000000001', 80, now()),
('74000000-0000-0000-0000-000000000002', 20, now()),
('74000000-0000-0000-0000-000000000003', 60, now()),
('74000000-0000-0000-0000-000000000004', 0, now())
ON CONFLICT (product_id) DO UPDATE SET quantity = EXCLUDED.quantity, updated_at = EXCLUDED.updated_at;

-- Services tipo B: 3 active
INSERT INTO services (id, tenant_id, partner_id, country_id, region_id, comuna_id, name, description, category, price, currency, duration_minutes, is_active, created_at, updated_at)
VALUES
('75000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Evaluación kinésica', 'Primera evaluación funcional', 'Salud', 18000, 'CLP', 45, true, now(), now()),
('75000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Sesión de rehabilitación', 'Atención kinésica en box', 'Salud', 22000, 'CLP', 60, true, now(), now()),
('75000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Masoterapia 45 min', 'Sesión de descarga muscular', 'Bienestar', 25000, 'CLP', 45, true, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO service_slots (id, tenant_id, partner_id, service_id, start_at, end_at, capacity, is_available, created_at)
VALUES
('75100000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000002', '75000000-0000-0000-0000-000000000001', now() + interval '1 day', now() + interval '1 day' + interval '45 minutes', 1, true, now()),
('75100000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000002', '75000000-0000-0000-0000-000000000002', now() + interval '2 day', now() + interval '2 day' + interval '60 minutes', 1, true, now())
ON CONFLICT (id) DO NOTHING;

-- Professionals tipo C: 2 complete + 1 incomplete
INSERT INTO professionals (id, tenant_id, country_id, region_id, comuna_id, name, email, phone, specialty, bio, is_verified, is_active, created_at)
VALUES
('76000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Daniela Rojas', 'daniela@redprofesional.demo', '+56 9 7333 3331', 'Psicología clínica', 'Atención de adultos y adolescentes.', true, true, now()),
('76000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'Felipe Soto', 'felipe@redprofesional.demo', '+56 9 7333 3332', 'Terapia ocupacional', 'Intervención funcional y acompañamiento.', true, true, now()),
('76000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', '216f98f0-541a-47e1-b586-ed530fddf50d', 'QA incompleto', null, null, null, 'No debe calificar para discovery/activación.', false, true, now())
ON CONFLICT (id) DO NOTHING;

-- Orders: 2 demo
INSERT INTO orders (id, tenant_id, partner_id, customer_id, status, subtotal, delivery_fee, total_amount, currency, created_at, updated_at)
VALUES
('77000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', '73000000-0000-0000-0000-000000000001', 'paid', 4700, 1500, 6200, 'CLP', now(), now()),
('77000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', '73000000-0000-0000-0000-000000000002', 'payment_pending', 8900, 0, 8900, 'CLP', now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO order_items (id, order_id, product_id, quantity, unit_price, total_price)
VALUES
('77100000-0000-0000-0000-000000000001', '77000000-0000-0000-0000-000000000001', '74000000-0000-0000-0000-000000000001', 1, 2500, 2500),
('77100000-0000-0000-0000-000000000002', '77000000-0000-0000-0000-000000000001', '74000000-0000-0000-0000-000000000003', 1, 2200, 2200),
('77100000-0000-0000-0000-000000000003', '77000000-0000-0000-0000-000000000002', '74000000-0000-0000-0000-000000000002', 1, 8900, 8900)
ON CONFLICT (id) DO NOTHING;

-- Bookings: 2 demo
INSERT INTO bookings (id, tenant_id, partner_id, service_id, slot_id, customer_id, status, start_at, end_at, amount, currency, cancellation_policy, created_at, updated_at)
VALUES
('78000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000002', '75000000-0000-0000-0000-000000000001', '75100000-0000-0000-0000-000000000001', '73000000-0000-0000-0000-000000000001', 'confirmed', now() + interval '1 day', now() + interval '1 day' + interval '45 minutes', 18000, 'CLP', '{}'::jsonb, now(), now()),
('78000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000002', '75000000-0000-0000-0000-000000000002', '75100000-0000-0000-0000-000000000002', '73000000-0000-0000-0000-000000000002', 'created', now() + interval '2 day', now() + interval '2 day' + interval '60 minutes', 22000, 'CLP', '{}'::jsonb, now(), now())
ON CONFLICT (id) DO NOTHING;

-- Leads: 2 demo
INSERT INTO leads (id, tenant_id, professional_id, customer_id, status, message, created_at, updated_at)
VALUES
('79000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '76000000-0000-0000-0000-000000000001', '73000000-0000-0000-0000-000000000001', 'new', 'Quiero agendar una primera orientación.', now(), now()),
('79000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '76000000-0000-0000-0000-000000000002', '73000000-0000-0000-0000-000000000002', 'contacted', 'Necesito apoyo ocupacional para rehabilitación.', now(), now())
ON CONFLICT (id) DO NOTHING;

-- Optional links/interactions useful for demos
INSERT INTO customer_partner_links (id, tenant_id, customer_id, partner_id, first_seen_at, last_seen_at, source)
VALUES
('7a100000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000001', '73000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', now(), now(), 'qa_seed'),
('7a100000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000001', '73000000-0000-0000-0000-000000000002', '72000000-0000-0000-0000-000000000002', now(), now(), 'qa_seed')
ON CONFLICT (id) DO NOTHING;

COMMIT;
