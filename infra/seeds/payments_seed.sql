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

COMMIT;
