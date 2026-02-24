BEGIN;

TRUNCATE TABLE
  payments.provider_events,
  payments.charges,
  payments.customer_tokens,
  payments.payment_intents
RESTART IDENTITY CASCADE;

COMMIT;
