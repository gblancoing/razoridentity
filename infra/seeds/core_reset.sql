BEGIN;

TRUNCATE TABLE
  core.payment_events,
  core.payments,
  core.payout_items,
  core.payout_batches,
  core.bookings,
  core.service_slots,
  core.services,
  core.leads,
  core.order_items,
  core.orders,
  core.product_inventory,
  core.products,
  core.customer_partner_links,
  core.customers,
  core.partner_staff,
  core.partners,
  core.subscriptions,
  core.subscription_plans,
  core.interactions,
  core.professionals,
  core.tenants,
  core.comunas,
  core.regions,
  core.countries
RESTART IDENTITY CASCADE;

COMMIT;
