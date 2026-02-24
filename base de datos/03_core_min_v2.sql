-- =========================================================
-- 03_core_min.sql  (versión "robusta")
-- Ejecutar conectado a la DB: core_db
--
-- Motivo: evita sentencias que a veces fallan en RDS/herramientas
-- (AUTHORIZATION en CREATE SCHEMA, ALTER ROLE ... IN DATABASE, ALTER DEFAULT PRIVILEGES).
-- =========================================================

-- Extensiones (requiere usuario admin con permisos en RDS)
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Schema + ownership (más compatible)
CREATE SCHEMA IF NOT EXISTS core;
ALTER SCHEMA core OWNER TO core_app_user;

GRANT USAGE, CREATE ON SCHEMA core TO core_app_user;

-- =========================
-- TENANTS / PARTNERS
-- =========================

CREATE TABLE IF NOT EXISTS core.tenants (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name        text NOT NULL UNIQUE,
  timezone    text NOT NULL DEFAULT 'America/Santiago',
  config_json jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS core.partners (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  type        char(1) NOT NULL CHECK (type IN ('A','B','C')),
  name        text NOT NULL,
  rut         text NULL,
  address     text NULL,
  phone       text NULL,
  email       text NULL,
  geo_point   geography(Point,4326) NULL,
  is_visible  boolean NOT NULL DEFAULT false,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, name)
);

CREATE INDEX IF NOT EXISTS ix_core_partners_tenant ON core.partners(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_partners_type ON core.partners(type);
CREATE INDEX IF NOT EXISTS ix_core_partners_visible ON core.partners(is_visible);
CREATE INDEX IF NOT EXISTS ix_core_partners_geo_gist ON core.partners USING GIST (geo_point);

CREATE TABLE IF NOT EXISTS core.partner_staff (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id  uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  partner_id uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
  user_id    uuid NOT NULL,
  role       text NOT NULL DEFAULT 'staff',
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (partner_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_core_partner_staff_tenant ON core.partner_staff(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_partner_staff_user ON core.partner_staff(user_id);

-- =========================
-- SUBSCRIPCIONES
-- =========================

CREATE TABLE IF NOT EXISTS core.subscription_plans (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id          uuid NULL REFERENCES core.tenants(id) ON DELETE SET NULL, -- NULL => global
  code              text NOT NULL,
  name              text NOT NULL,
  monthly_price     numeric(14,2) NOT NULL DEFAULT 0,
  currency          text NOT NULL DEFAULT 'CLP',
  commission_pct    numeric(6,3) NOT NULL DEFAULT 0,
  is_active         boolean NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now()
);

-- Un código no se puede repetir dentro de un mismo tenant (solo para planes tenant-scoped)
CREATE UNIQUE INDEX IF NOT EXISTS uq_subscription_plans_tenant_code
  ON core.subscription_plans (tenant_id, code)
  WHERE tenant_id IS NOT NULL;

-- Un código global (tenant_id NULL) no se puede repetir entre planes globales
CREATE UNIQUE INDEX IF NOT EXISTS uq_subscription_plans_global_code
  ON core.subscription_plans (code)
  WHERE tenant_id IS NULL;

CREATE INDEX IF NOT EXISTS ix_core_subscription_plans_active
  ON core.subscription_plans(is_active);

CREATE TABLE IF NOT EXISTS core.subscriptions (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id            uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  partner_id           uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
  plan_id              uuid NOT NULL REFERENCES core.subscription_plans(id),
  status               text NOT NULL CHECK (status IN ('active','past_due','suspended','canceled')),
  current_period_start timestamptz NOT NULL DEFAULT now(),
  current_period_end   timestamptz NOT NULL,
  created_at           timestamptz NOT NULL DEFAULT now(),
  updated_at           timestamptz NOT NULL DEFAULT now(),
  UNIQUE (partner_id)
);

CREATE INDEX IF NOT EXISTS ix_core_subscriptions_tenant ON core.subscriptions(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_subscriptions_status ON core.subscriptions(status);
CREATE INDEX IF NOT EXISTS ix_core_subscriptions_period_end ON core.subscriptions(current_period_end);

-- =========================
-- CUSTOMERS / CRM
-- =========================

CREATE TABLE IF NOT EXISTS core.customers (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id  uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  email      text NULL,
  phone      text NULL,
  full_name  text NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, email)
);

CREATE INDEX IF NOT EXISTS ix_core_customers_tenant ON core.customers(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_customers_phone ON core.customers(phone);

CREATE TABLE IF NOT EXISTS core.customer_partner_links (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id     uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  customer_id   uuid NOT NULL REFERENCES core.customers(id) ON DELETE CASCADE,
  partner_id    uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
  first_seen_at timestamptz NOT NULL DEFAULT now(),
  last_seen_at  timestamptz NOT NULL DEFAULT now(),
  source        text NULL,
  UNIQUE (customer_id, partner_id)
);

CREATE INDEX IF NOT EXISTS ix_core_cpl_tenant ON core.customer_partner_links(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_cpl_partner ON core.customer_partner_links(partner_id);

CREATE TABLE IF NOT EXISTS core.interactions (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  customer_id  uuid NOT NULL REFERENCES core.customers(id) ON DELETE CASCADE,
  partner_id   uuid NULL REFERENCES core.partners(id) ON DELETE SET NULL,
  type         text NOT NULL,
  reference_id uuid NULL,
  payload      jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_interactions_tenant ON core.interactions(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_interactions_customer ON core.interactions(customer_id);
CREATE INDEX IF NOT EXISTS ix_core_interactions_partner ON core.interactions(partner_id);
CREATE INDEX IF NOT EXISTS ix_core_interactions_created ON core.interactions(created_at);

-- =========================
-- CATALOGO A (PRODUCTOS)
-- =========================

CREATE TABLE IF NOT EXISTS core.products (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  partner_id  uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
  name        text NOT NULL,
  description text NULL,
  category    text NULL,
  price       numeric(14,2) NOT NULL DEFAULT 0,
  currency    text NOT NULL DEFAULT 'CLP',
  is_active   boolean NOT NULL DEFAULT true,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_products_tenant ON core.products(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_products_partner ON core.products(partner_id);
CREATE INDEX IF NOT EXISTS ix_core_products_active ON core.products(is_active);

CREATE TABLE IF NOT EXISTS core.product_inventory (
  product_id uuid PRIMARY KEY REFERENCES core.products(id) ON DELETE CASCADE,
  quantity   integer NOT NULL DEFAULT 0,
  updated_at timestamptz NOT NULL DEFAULT now()
);

-- =========================
-- CATALOGO B (SERVICIOS / AGENDA)
-- =========================

CREATE TABLE IF NOT EXISTS core.services (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id        uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  partner_id       uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
  name             text NOT NULL,
  description      text NULL,
  category         text NULL,
  price            numeric(14,2) NOT NULL DEFAULT 0,
  currency         text NOT NULL DEFAULT 'CLP',
  duration_minutes int NOT NULL DEFAULT 30,
  is_active        boolean NOT NULL DEFAULT true,
  created_at       timestamptz NOT NULL DEFAULT now(),
  updated_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_services_tenant ON core.services(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_services_partner ON core.services(partner_id);

CREATE TABLE IF NOT EXISTS core.service_slots (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  partner_id   uuid NOT NULL REFERENCES core.partners(id) ON DELETE CASCADE,
  service_id   uuid NOT NULL REFERENCES core.services(id) ON DELETE CASCADE,
  start_at     timestamptz NOT NULL,
  end_at       timestamptz NOT NULL,
  capacity     int NOT NULL DEFAULT 1,
  is_available boolean NOT NULL DEFAULT true,
  created_at   timestamptz NOT NULL DEFAULT now(),
  UNIQUE (service_id, start_at)
);

CREATE INDEX IF NOT EXISTS ix_core_service_slots_partner ON core.service_slots(partner_id);
CREATE INDEX IF NOT EXISTS ix_core_service_slots_start ON core.service_slots(start_at);

-- =========================
-- CATALOGO C (PROFESIONALES / LEADS)
-- =========================

CREATE TABLE IF NOT EXISTS core.professionals (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  name        text NOT NULL,
  email       text NULL,
  phone       text NULL,
  specialty   text NULL,
  bio         text NULL,
  geo_point   geography(Point,4326) NULL,
  is_verified boolean NOT NULL DEFAULT false,
  is_active   boolean NOT NULL DEFAULT true,
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_professionals_tenant ON core.professionals(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_professionals_geo_gist ON core.professionals USING GIST (geo_point);
CREATE INDEX IF NOT EXISTS ix_core_professionals_active ON core.professionals(is_active);

CREATE TABLE IF NOT EXISTS core.leads (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id        uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  professional_id  uuid NOT NULL REFERENCES core.professionals(id) ON DELETE CASCADE,
  customer_id      uuid NOT NULL REFERENCES core.customers(id) ON DELETE CASCADE,
  status           text NOT NULL CHECK (status IN ('new','contacted','closed','discarded')),
  message          text NULL,
  created_at       timestamptz NOT NULL DEFAULT now(),
  updated_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_leads_professional ON core.leads(professional_id);
CREATE INDEX IF NOT EXISTS ix_core_leads_status ON core.leads(status);

-- =========================
-- TRANSACCIONES A (ORDERS)
-- =========================

CREATE TABLE IF NOT EXISTS core.orders (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  partner_id   uuid NOT NULL REFERENCES core.partners(id) ON DELETE RESTRICT,
  customer_id  uuid NOT NULL REFERENCES core.customers(id) ON DELETE RESTRICT,
  status       text NOT NULL CHECK (status IN ('payment_pending','paid','preparing','dispatched','completed','cancelled','failed')),
  subtotal     numeric(14,2) NOT NULL DEFAULT 0,
  delivery_fee numeric(14,2) NOT NULL DEFAULT 0,
  total_amount numeric(14,2) NOT NULL DEFAULT 0,
  currency     text NOT NULL DEFAULT 'CLP',
  created_at   timestamptz NOT NULL DEFAULT now(),
  updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_orders_tenant ON core.orders(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_orders_partner ON core.orders(partner_id);
CREATE INDEX IF NOT EXISTS ix_core_orders_customer ON core.orders(customer_id);
CREATE INDEX IF NOT EXISTS ix_core_orders_status ON core.orders(status);
CREATE INDEX IF NOT EXISTS ix_core_orders_created ON core.orders(created_at);

CREATE TABLE IF NOT EXISTS core.order_items (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  order_id    uuid NOT NULL REFERENCES core.orders(id) ON DELETE CASCADE,
  product_id  uuid NOT NULL REFERENCES core.products(id) ON DELETE RESTRICT,
  quantity    int NOT NULL CHECK (quantity > 0),
  unit_price  numeric(14,2) NOT NULL DEFAULT 0,
  total_price numeric(14,2) NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_core_order_items_order ON core.order_items(order_id);

CREATE TABLE IF NOT EXISTS core.deliveries (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  order_id   uuid NOT NULL UNIQUE REFERENCES core.orders(id) ON DELETE CASCADE,
  address    text NOT NULL,
  notes      text NULL,
  status     text NOT NULL CHECK (status IN ('pending','assigned','picked','delivered','failed','cancelled')),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

-- =========================
-- TRANSACCIONES B (BOOKINGS)
-- =========================

CREATE TABLE IF NOT EXISTS core.bookings (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  partner_id  uuid NOT NULL REFERENCES core.partners(id) ON DELETE RESTRICT,
  service_id  uuid NOT NULL REFERENCES core.services(id) ON DELETE RESTRICT,
  slot_id     uuid NULL REFERENCES core.service_slots(id) ON DELETE SET NULL,
  customer_id uuid NOT NULL REFERENCES core.customers(id) ON DELETE RESTRICT,
  status      text NOT NULL CHECK (status IN ('payment_pending','confirmed','attended','no_show','cancelled','failed')),
  start_at    timestamptz NOT NULL,
  end_at      timestamptz NOT NULL,
  amount      numeric(14,2) NOT NULL DEFAULT 0,
  currency    text NOT NULL DEFAULT 'CLP',
  cancellation_policy jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_bookings_tenant ON core.bookings(tenant_id);
CREATE INDEX IF NOT EXISTS ix_core_bookings_partner ON core.bookings(partner_id);
CREATE INDEX IF NOT EXISTS ix_core_bookings_customer ON core.bookings(customer_id);
CREATE INDEX IF NOT EXISTS ix_core_bookings_status ON core.bookings(status);
CREATE INDEX IF NOT EXISTS ix_core_bookings_start ON core.bookings(start_at);

-- =========================
-- PAGOS (ORQUESTACIÓN EN CORE)
-- =========================

CREATE TABLE IF NOT EXISTS core.payments (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id           uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  provider            text NOT NULL DEFAULT 'transbank',
  external_reference  text NOT NULL,
  amount              numeric(14,2) NOT NULL,
  currency            text NOT NULL DEFAULT 'CLP',
  status              text NOT NULL CHECK (status IN ('pending','approved','rejected','cancelled','expired')),
  gateway_intent_id   uuid NULL,
  provider_token      text NULL,
  last_event_id       text NULL,
  created_at          timestamptz NOT NULL DEFAULT now(),
  updated_at          timestamptz NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, provider, external_reference)
);

CREATE INDEX IF NOT EXISTS ix_core_payments_status ON core.payments(status);
CREATE INDEX IF NOT EXISTS ix_core_payments_gateway_intent ON core.payments(gateway_intent_id);
CREATE INDEX IF NOT EXISTS ix_core_payments_external_ref ON core.payments(external_reference);

CREATE TABLE IF NOT EXISTS core.payment_events (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id         uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  provider_event_id text NOT NULL,
  payment_id        uuid NULL REFERENCES core.payments(id) ON DELETE SET NULL,
  payload           jsonb NOT NULL DEFAULT '{}'::jsonb,
  received_at       timestamptz NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, provider_event_id)
);

-- =========================
-- LIQUIDACIONES
-- =========================

CREATE TABLE IF NOT EXISTS core.payout_batches (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id    uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  period_start date NOT NULL,
  period_end   date NOT NULL,
  status       text NOT NULL CHECK (status IN ('open','processing','paid','failed')),
  created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_core_payout_batches_tenant ON core.payout_batches(tenant_id);

CREATE TABLE IF NOT EXISTS core.payout_items (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  batch_id               uuid NOT NULL REFERENCES core.payout_batches(id) ON DELETE CASCADE,
  partner_id             uuid NOT NULL REFERENCES core.partners(id) ON DELETE RESTRICT,
  gross_amount           numeric(14,2) NOT NULL DEFAULT 0,
  commission_amount      numeric(14,2) NOT NULL DEFAULT 0,
  subscription_deduction numeric(14,2) NOT NULL DEFAULT 0,
  net_amount             numeric(14,2) NOT NULL DEFAULT 0,
  currency               text NOT NULL DEFAULT 'CLP',
  created_at             timestamptz NOT NULL DEFAULT now(),
  UNIQUE (batch_id, partner_id)
);

CREATE INDEX IF NOT EXISTS ix_core_payout_items_partner ON core.payout_items(partner_id);
