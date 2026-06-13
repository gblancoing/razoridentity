BEGIN;

SET search_path TO payments;

INSERT INTO payment_intents (id, external_reference, amount, currency, status, provider, provider_token, authorization_code, raw_response, created_at, updated_at)
VALUES
  ('17171717-1717-1717-1717-171717171717', 'order-88888888', 8000, 'CLP', 'pending', 'transbank', null, null, '{}'::jsonb, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO customer_tokens (id, customer_id, provider_ref, status, raw_response, created_at, revoked_at)
VALUES
  ('18181818-1818-1818-1818-181818181818', 'cust-44444444', 'token-demo', 'active', '{}'::jsonb, now(), null)
ON CONFLICT (id) DO NOTHING;

INSERT INTO charges (id, customer_token_id, intent_id, amount, currency, status, provider_ref, authorization_code, raw_response, created_at)
VALUES
  ('19191919-1919-1919-1919-191919191919', '18181818-1818-1818-1818-181818181818', '17171717-1717-1717-1717-171717171717',
   8000, 'CLP', 'pending', null, null, '{}'::jsonb, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO provider_events (id, provider_event_id, intent_id, event_type, payload, received_at)
VALUES
  ('20202020-2020-2020-2020-202020202020', 'evt-001', '17171717-1717-1717-1717-171717171717', 'payment.pending', '{}'::jsonb, now())
ON CONFLICT (id) DO NOTHING;

-- Suscripciones de ejemplo (tabla real payments.subscriptions)
INSERT INTO subscriptions (id, customer_id, customer_token_id, external_reference, plan_name, provider, amount, currency, billing_interval, status, next_charge_at, created_at, updated_at)
VALUES
  ('21212121-2121-2121-2121-212121212121', 'cust-44444444', '18181818-1818-1818-1818-181818181818',
   'subscription:demo-mensual', 'Plan Comercio Mensual', 'transbank', 9990, 'CLP', 'monthly', 'active', now() + interval '20 days', now() - interval '40 days', now()),
  ('22222222-3232-3232-3232-323232323232', 'cust-55555555', null,
   'subscription:demo-anual', 'Plan Comercio Anual', 'mercadopago', 99990, 'CLP', 'yearly', 'paused', null, now() - interval '90 days', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO subscription_attempts (id, subscription_id, intent_id, charge_id, status, error_message, attempted_at)
VALUES
  ('23232323-2323-2323-2323-232323232323', '21212121-2121-2121-2121-212121212121',
   '17171717-1717-1717-1717-171717171717', '19191919-1919-1919-1919-191919191919', 'approved', null, now() - interval '10 days'),
  ('24242424-2424-2424-2424-242424242424', '21212121-2121-2121-2121-212121212121',
   null, null, 'rejected', 'Fondos insuficientes', now() - interval '40 days')
ON CONFLICT (id) DO NOTHING;

COMMIT;
