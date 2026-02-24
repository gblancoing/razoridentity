BEGIN;

SET search_path TO core;

INSERT INTO countries (id, code, name, is_active, created_at)
VALUES
  ('10000000-0000-0000-0000-000000000001', 'CL', 'Chile', true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO regions (id, country_id, code, name, legacy_region_id, is_active, created_at)
VALUES
  ('10000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'RM', 'Región Metropolitana', 13, true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO comunas (id, region_id, code, name, legacy_comuna_id, latitude, longitude, is_active, created_at)
VALUES
  ('10000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000002', 'SCL', 'Santiago', 13101, -33.4489, -70.6693, true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO tenants (id, comuna_id, "name", timezone, config_json, is_active, created_at, updated_at)
VALUES
  ('11111111-1111-1111-1111-111111111111', '10000000-0000-0000-0000-000000000003', 'ComunaClic Demo', 'America/Santiago', '{}'::jsonb, true, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO partners (id, tenant_id, "type", "name", rut, address, phone, email, is_visible, created_at, updated_at)
VALUES
  ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'B', 'Panaderia Central', '76.123.456-7', 'Av. Central 123', '+56 9 5555 0000', 'contacto@panaderia.test', true, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO partner_staff (id, tenant_id, partner_id, user_id, "role", created_at)
VALUES
  ('14141414-1414-1414-1414-141414141414', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', '21212121-2121-2121-2121-212121212121', 'owner', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO subscription_plans (id, tenant_id, code, "name", monthly_price, currency, commission_pct, is_active, created_at)
VALUES
  ('12121212-1212-1212-1212-121212121212', null, 'starter', 'Starter', 9900, 'CLP', 0.050, true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO subscriptions (id, tenant_id, partner_id, plan_id, status, current_period_start, current_period_end, created_at, updated_at)
VALUES
  ('13131313-1313-1313-1313-131313131313', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', '12121212-1212-1212-1212-121212121212', 'active', now(), now() + interval '30 days', now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO professionals (id, tenant_id, "name", email, phone, specialty, bio, is_verified, is_active, created_at)
VALUES
  ('33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'Ana Torres', 'ana@comunaclic.test', '+56 9 4444 1111', 'Carpinteria', 'Profesional verificada en obras menores.', true, true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO services (id, tenant_id, partner_id, "name", description, category, price, currency, duration_minutes, is_active, created_at, updated_at)
VALUES
  ('66666666-6666-6666-6666-666666666666', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222',
   'Instalacion de repisas', 'Servicio de instalacion en domicilio', 'Hogar', 15000, 'CLP', 60, true, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO service_slots (id, tenant_id, partner_id, service_id, start_at, end_at, capacity, is_available, created_at)
VALUES
  ('77777777-7777-7777-7777-777777777777', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222',
   '66666666-6666-6666-6666-666666666666', now() + interval '1 day', now() + interval '1 day' + interval '1 hour', 1, true, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, tenant_id, partner_id, "name", description, category, price, currency, is_active, created_at, updated_at)
VALUES
  ('55555555-5555-5555-5555-555555555555', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222',
   'Pan masa madre', 'Pan artesanal de larga fermentacion', 'Panaderia', 3500, 'CLP', true, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_inventory (product_id, quantity, updated_at)
VALUES
  ('55555555-5555-5555-5555-555555555555', 120, now())
ON CONFLICT (product_id) DO NOTHING;

INSERT INTO customers (id, tenant_id, email, phone, full_name, created_at, updated_at)
VALUES
  ('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 'cliente@comunaclic.test', '+56 9 3333 2222', 'Cliente Demo', now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO customer_partner_links (id, tenant_id, customer_id, partner_id, first_seen_at, last_seen_at, "source")
VALUES
  ('16161616-1616-1616-1616-161616161616', '11111111-1111-1111-1111-111111111111', '44444444-4444-4444-4444-444444444444', '22222222-2222-2222-2222-222222222222', now(), now(), 'seed')
ON CONFLICT (id) DO NOTHING;

INSERT INTO orders (id, tenant_id, partner_id, customer_id, status, subtotal, delivery_fee, total_amount, currency, created_at, updated_at)
VALUES
  ('88888888-8888-8888-8888-888888888888', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', '44444444-4444-4444-4444-444444444444',
   'payment_pending', 7000, 1000, 8000, 'CLP', now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO order_items (id, order_id, product_id, quantity, unit_price, total_price)
VALUES
  ('99999999-9999-9999-9999-999999999999', '88888888-8888-8888-8888-888888888888', '55555555-5555-5555-5555-555555555555', 2, 3500, 7000)
ON CONFLICT (id) DO NOTHING;

INSERT INTO bookings (id, tenant_id, partner_id, service_id, slot_id, customer_id, status, start_at, end_at, amount, currency, cancellation_policy, created_at, updated_at)
VALUES
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222',
   '66666666-6666-6666-6666-666666666666', '77777777-7777-7777-7777-777777777777', '44444444-4444-4444-4444-444444444444',
   'confirmed', now() + interval '1 day', now() + interval '1 day' + interval '1 hour', 15000, 'CLP', '{}'::jsonb, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO leads (id, tenant_id, professional_id, customer_id, status, message, created_at, updated_at)
VALUES
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333', '44444444-4444-4444-4444-444444444444', 'new', 'Necesito instalacion de repisas.', now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO payments (id, tenant_id, provider, external_reference, amount, currency, status, gateway_intent_id, provider_token, last_event_id, created_at, updated_at)
VALUES
  ('cccccccc-cccc-cccc-cccc-cccccccccccc', '11111111-1111-1111-1111-111111111111', 'transbank', 'order-88888888', 8000, 'CLP', 'pending', '17171717-1717-1717-1717-171717171717', null, 'evt-001', now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO payment_events (id, tenant_id, provider_event_id, payment_id, payload, received_at)
VALUES
  ('dddddddd-dddd-dddd-dddd-dddddddddddd', '11111111-1111-1111-1111-111111111111', 'evt-001', 'cccccccc-cccc-cccc-cccc-cccccccccccc', '{}'::jsonb, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO payout_batches (id, tenant_id, period_start, period_end, status, created_at)
VALUES
  ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', '11111111-1111-1111-1111-111111111111', current_date - 7, current_date, 'open', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO payout_items (id, batch_id, partner_id, gross_amount, commission_amount, subscription_deduction, net_amount, currency, created_at)
VALUES
  ('ffffffff-ffff-ffff-ffff-ffffffffffff', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', '22222222-2222-2222-2222-222222222222',
   8000, 400, 0, 7600, 'CLP', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO interactions (id, tenant_id, customer_id, partner_id, "type", reference_id, payload, created_at)
VALUES
  ('15151515-1515-1515-1515-151515151515', '11111111-1111-1111-1111-111111111111', '44444444-4444-4444-4444-444444444444', '22222222-2222-2222-2222-222222222222',
   'notification', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '{"title":"Nuevo lead","message":"Cliente solicita presupuesto."}'::jsonb, now())
ON CONFLICT (id) DO NOTHING;

COMMIT;
