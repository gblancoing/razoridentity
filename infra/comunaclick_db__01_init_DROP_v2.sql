-- =========================================================
-- comunaclick_db__01_init.sql
-- Unifica CORE + ACL en una sola base de datos (schemas: core, acl)
-- Recomendado ejecutar con un rol con permisos de DDL (ej: postgres)
--
-- Orden:
--  1) Ejecutar este archivo (crea/actualiza estructuras)
--  2) Ejecutar comunaclick_db__03_seed_geo_cl.sql (opcional)
--  3) Ejecutar comunaclick_db__04_seed_acl_admin.sql (opcional)
-- =========================================================

-- Extensiones
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS postgis;

-- Schemas

-- =========================================================
-- MODO REINSTALACIÓN (DESTRUCTIVO)
-- Esto elimina y recrea los schemas core y acl.
-- Úsalo solo si quieres una instalación limpia.
-- =========================================================
DROP SCHEMA IF EXISTS acl CASCADE;
DROP SCHEMA IF EXISTS core CASCADE;

CREATE SCHEMA IF NOT EXISTS core;


-- Función estándar para triggers updated_at
CREATE OR REPLACE FUNCTION core.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  NEW.updated_at := now();
  RETURN NEW;
END;
$$;
CREATE SCHEMA IF NOT EXISTS acl;

-- =========================
-- CORE (modelo base)
-- =========================
-- DROP SCHEMA core;

-- core.tenants definition

-- Drop table

-- DROP TABLE core.tenants;

CREATE TABLE core.tenants (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	"name" text NOT NULL,
	timezone text DEFAULT 'America/Santiago'::text NOT NULL,
	config_json jsonb DEFAULT '{}'::jsonb NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT tenants_name_key UNIQUE (name),
	CONSTRAINT tenants_pkey PRIMARY KEY (id)
);

-- NUEVO: bandera de activación de tenant (necesaria para vistas/índices)
ALTER TABLE core.tenants
  ADD COLUMN IF NOT EXISTS is_active boolean NOT NULL DEFAULT true;



-- core.customers definition

-- Drop table

-- DROP TABLE core.customers;

CREATE TABLE core.customers (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	email text NULL,
	phone text NULL,
	full_name text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT customers_pkey PRIMARY KEY (id),
	CONSTRAINT customers_tenant_id_email_key UNIQUE (tenant_id, email),
	CONSTRAINT customers_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_customers_phone ON core.customers USING btree (phone);
CREATE INDEX ix_core_customers_tenant ON core.customers USING btree (tenant_id);

-- Table Triggers

create trigger trg_core_customers_updated_at before
update
    on
    core.customers for each row execute function core.set_updated_at();


-- core.partners definition

-- Drop table

-- DROP TABLE core.partners;

CREATE TABLE core.partners (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	"type" bpchar(1) NOT NULL,
	"name" text NOT NULL,
	rut text NULL,
	address text NULL,
	phone text NULL,
	email text NULL,
	geo_point public.geography(point, 4326) NULL,
	is_visible bool DEFAULT false NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT partners_pkey PRIMARY KEY (id),
	CONSTRAINT partners_tenant_id_name_key UNIQUE (tenant_id, name),
	CONSTRAINT partners_type_check CHECK ((type = ANY (ARRAY['A'::bpchar, 'B'::bpchar, 'C'::bpchar]))),
	CONSTRAINT partners_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_partners_geo_gist ON core.partners USING gist (geo_point);
CREATE INDEX ix_core_partners_tenant ON core.partners USING btree (tenant_id);
CREATE INDEX ix_core_partners_type ON core.partners USING btree (type);
CREATE INDEX ix_core_partners_visible ON core.partners USING btree (is_visible);

-- Table Triggers

create trigger trg_core_partners_updated_at before
update
    on
    core.partners for each row execute function core.set_updated_at();


-- core.payments definition

-- Drop table

-- DROP TABLE core.payments;

CREATE TABLE core.payments (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	provider text DEFAULT 'transbank'::text NOT NULL,
	external_reference text NOT NULL,
	amount numeric(14, 2) NOT NULL,
	currency text DEFAULT 'CLP'::text NOT NULL,
	status text NOT NULL,
	gateway_intent_id uuid NULL,
	provider_token text NULL,
	last_event_id text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT payments_pkey PRIMARY KEY (id),
	CONSTRAINT payments_status_check CHECK ((status = ANY (ARRAY['pending'::text, 'approved'::text, 'rejected'::text, 'cancelled'::text, 'expired'::text]))),
	CONSTRAINT payments_tenant_id_provider_external_reference_key UNIQUE (tenant_id, provider, external_reference),
	CONSTRAINT payments_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_payments_external_ref ON core.payments USING btree (external_reference);
CREATE INDEX ix_core_payments_gateway_intent ON core.payments USING btree (gateway_intent_id);
CREATE INDEX ix_core_payments_status ON core.payments USING btree (status);

-- Table Triggers

create trigger trg_core_payments_updated_at before
update
    on
    core.payments for each row execute function core.set_updated_at();


-- core.payout_batches definition

-- Drop table

-- DROP TABLE core.payout_batches;

CREATE TABLE core.payout_batches (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	period_start date NOT NULL,
	period_end date NOT NULL,
	status text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT payout_batches_pkey PRIMARY KEY (id),
	CONSTRAINT payout_batches_status_check CHECK ((status = ANY (ARRAY['open'::text, 'processing'::text, 'paid'::text, 'failed'::text]))),
	CONSTRAINT payout_batches_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_payout_batches_tenant ON core.payout_batches USING btree (tenant_id);


-- core.payout_items definition

-- Drop table

-- DROP TABLE core.payout_items;

CREATE TABLE core.payout_items (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	batch_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	gross_amount numeric(14, 2) DEFAULT 0 NOT NULL,
	commission_amount numeric(14, 2) DEFAULT 0 NOT NULL,
	subscription_deduction numeric(14, 2) DEFAULT 0 NOT NULL,
	net_amount numeric(14, 2) DEFAULT 0 NOT NULL,
	currency text DEFAULT 'CLP'::text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT payout_items_batch_id_partner_id_key UNIQUE (batch_id, partner_id),
	CONSTRAINT payout_items_pkey PRIMARY KEY (id),
	CONSTRAINT payout_items_batch_id_fkey FOREIGN KEY (batch_id) REFERENCES core.payout_batches(id) ON DELETE CASCADE,
	CONSTRAINT payout_items_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE RESTRICT
);
CREATE INDEX ix_core_payout_items_partner ON core.payout_items USING btree (partner_id);


-- core.products definition

-- Drop table

-- DROP TABLE core.products;

CREATE TABLE core.products (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	"name" text NOT NULL,
	description text NULL,
	category text NULL,
	price numeric(14, 2) DEFAULT 0 NOT NULL,
	currency text DEFAULT 'CLP'::text NOT NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT products_pkey PRIMARY KEY (id),
	CONSTRAINT products_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE CASCADE,
	CONSTRAINT products_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_products_active ON core.products USING btree (is_active);
CREATE INDEX ix_core_products_partner ON core.products USING btree (partner_id);
CREATE INDEX ix_core_products_tenant ON core.products USING btree (tenant_id);


-- core.professionals definition

-- Drop table

-- DROP TABLE core.professionals;

CREATE TABLE core.professionals (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	"name" text NOT NULL,
	email text NULL,
	phone text NULL,
	specialty text NULL,
	bio text NULL,
	geo_point public.geography(point, 4326) NULL,
	is_verified bool DEFAULT false NOT NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT professionals_pkey PRIMARY KEY (id),
	CONSTRAINT professionals_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_professionals_active ON core.professionals USING btree (is_active);
CREATE INDEX ix_core_professionals_geo_gist ON core.professionals USING gist (geo_point);
CREATE INDEX ix_core_professionals_tenant ON core.professionals USING btree (tenant_id);


-- core.services definition

-- Drop table

-- DROP TABLE core.services;

CREATE TABLE core.services (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	"name" text NOT NULL,
	description text NULL,
	category text NULL,
	price numeric(14, 2) DEFAULT 0 NOT NULL,
	currency text DEFAULT 'CLP'::text NOT NULL,
	duration_minutes int4 DEFAULT 30 NOT NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT services_pkey PRIMARY KEY (id),
	CONSTRAINT services_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE CASCADE,
	CONSTRAINT services_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_services_partner ON core.services USING btree (partner_id);
CREATE INDEX ix_core_services_tenant ON core.services USING btree (tenant_id);


-- core.subscription_plans definition

-- Drop table

-- DROP TABLE core.subscription_plans;

CREATE TABLE core.subscription_plans (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NULL,
	code text NOT NULL,
	"name" text NOT NULL,
	monthly_price numeric(14, 2) DEFAULT 0 NOT NULL,
	currency text DEFAULT 'CLP'::text NOT NULL,
	commission_pct numeric(6, 3) DEFAULT 0 NOT NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT subscription_plans_pkey PRIMARY KEY (id),
	CONSTRAINT subscription_plans_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE SET NULL
);
CREATE INDEX ix_core_subscription_plans_active ON core.subscription_plans USING btree (is_active);
CREATE UNIQUE INDEX uq_subscription_plans_global_code ON core.subscription_plans USING btree (code) WHERE (tenant_id IS NULL);
CREATE UNIQUE INDEX uq_subscription_plans_tenant_code ON core.subscription_plans USING btree (tenant_id, code) WHERE (tenant_id IS NOT NULL);


-- core.subscriptions definition

-- Drop table

-- DROP TABLE core.subscriptions;

CREATE TABLE core.subscriptions (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	plan_id uuid NOT NULL,
	status text NOT NULL,
	current_period_start timestamptz DEFAULT now() NOT NULL,
	current_period_end timestamptz NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT subscriptions_partner_id_key UNIQUE (partner_id),
	CONSTRAINT subscriptions_pkey PRIMARY KEY (id),
	CONSTRAINT subscriptions_status_check CHECK ((status = ANY (ARRAY['active'::text, 'past_due'::text, 'suspended'::text, 'canceled'::text]))),
	CONSTRAINT subscriptions_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE CASCADE,
	CONSTRAINT subscriptions_plan_id_fkey FOREIGN KEY (plan_id) REFERENCES core.subscription_plans(id),
	CONSTRAINT subscriptions_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_subscriptions_period_end ON core.subscriptions USING btree (current_period_end);
CREATE INDEX ix_core_subscriptions_status ON core.subscriptions USING btree (status);
CREATE INDEX ix_core_subscriptions_tenant ON core.subscriptions USING btree (tenant_id);


-- core.customer_partner_links definition

-- Drop table

-- DROP TABLE core.customer_partner_links;

CREATE TABLE core.customer_partner_links (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	first_seen_at timestamptz DEFAULT now() NOT NULL,
	last_seen_at timestamptz DEFAULT now() NOT NULL,
	"source" text NULL,
	CONSTRAINT customer_partner_links_customer_id_partner_id_key UNIQUE (customer_id, partner_id),
	CONSTRAINT customer_partner_links_pkey PRIMARY KEY (id),
	CONSTRAINT customer_partner_links_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES core.customers(id) ON DELETE CASCADE,
	CONSTRAINT customer_partner_links_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE CASCADE,
	CONSTRAINT customer_partner_links_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_cpl_partner ON core.customer_partner_links USING btree (partner_id);
CREATE INDEX ix_core_cpl_tenant ON core.customer_partner_links USING btree (tenant_id);


-- core.interactions definition

-- Drop table

-- DROP TABLE core.interactions;

CREATE TABLE core.interactions (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	partner_id uuid NULL,
	"type" text NOT NULL,
	reference_id uuid NULL,
	payload jsonb DEFAULT '{}'::jsonb NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT interactions_pkey PRIMARY KEY (id),
	CONSTRAINT interactions_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES core.customers(id) ON DELETE CASCADE,
	CONSTRAINT interactions_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE SET NULL,
	CONSTRAINT interactions_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_interactions_created ON core.interactions USING btree (created_at);
CREATE INDEX ix_core_interactions_customer ON core.interactions USING btree (customer_id);
CREATE INDEX ix_core_interactions_partner ON core.interactions USING btree (partner_id);
CREATE INDEX ix_core_interactions_tenant ON core.interactions USING btree (tenant_id);


-- core.leads definition

-- Drop table

-- DROP TABLE core.leads;

CREATE TABLE core.leads (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	professional_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	status text NOT NULL,
	message text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT leads_pkey PRIMARY KEY (id),
	CONSTRAINT leads_status_check CHECK ((status = ANY (ARRAY['new'::text, 'contacted'::text, 'closed'::text, 'discarded'::text]))),
	CONSTRAINT leads_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES core.customers(id) ON DELETE CASCADE,
	CONSTRAINT leads_professional_id_fkey FOREIGN KEY (professional_id) REFERENCES core.professionals(id) ON DELETE CASCADE,
	CONSTRAINT leads_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_leads_professional ON core.leads USING btree (professional_id);
CREATE INDEX ix_core_leads_status ON core.leads USING btree (status);


-- core.orders definition

-- Drop table

-- DROP TABLE core.orders;

CREATE TABLE core.orders (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	status text NOT NULL,
	subtotal numeric(14, 2) DEFAULT 0 NOT NULL,
	delivery_fee numeric(14, 2) DEFAULT 0 NOT NULL,
	total_amount numeric(14, 2) DEFAULT 0 NOT NULL,
	currency text DEFAULT 'CLP'::text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT orders_pkey PRIMARY KEY (id),
	CONSTRAINT orders_status_check CHECK ((status = ANY (ARRAY['payment_pending'::text, 'paid'::text, 'preparing'::text, 'dispatched'::text, 'completed'::text, 'cancelled'::text, 'failed'::text]))),
	CONSTRAINT orders_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES core.customers(id) ON DELETE RESTRICT,
	CONSTRAINT orders_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE RESTRICT,
	CONSTRAINT orders_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_orders_created ON core.orders USING btree (created_at);
CREATE INDEX ix_core_orders_customer ON core.orders USING btree (customer_id);
CREATE INDEX ix_core_orders_partner ON core.orders USING btree (partner_id);
CREATE INDEX ix_core_orders_status ON core.orders USING btree (status);
CREATE INDEX ix_core_orders_tenant ON core.orders USING btree (tenant_id);

-- Table Triggers

create trigger trg_core_orders_updated_at before
update
    on
    core.orders for each row execute function core.set_updated_at();


-- core.partner_staff definition

-- Drop table

-- DROP TABLE core.partner_staff;

CREATE TABLE core.partner_staff (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	user_id uuid NOT NULL,
	"role" text DEFAULT 'staff'::text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT partner_staff_partner_id_user_id_key UNIQUE (partner_id, user_id),
	CONSTRAINT partner_staff_pkey PRIMARY KEY (id),
	CONSTRAINT partner_staff_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE CASCADE,
	CONSTRAINT partner_staff_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_partner_staff_tenant ON core.partner_staff USING btree (tenant_id);
CREATE INDEX ix_core_partner_staff_user ON core.partner_staff USING btree (user_id);


-- core.payment_events definition

-- Drop table

-- DROP TABLE core.payment_events;

CREATE TABLE core.payment_events (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	provider_event_id text NOT NULL,
	payment_id uuid NULL,
	payload jsonb DEFAULT '{}'::jsonb NOT NULL,
	received_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT payment_events_pkey PRIMARY KEY (id),
	CONSTRAINT payment_events_tenant_id_provider_event_id_key UNIQUE (tenant_id, provider_event_id),
	CONSTRAINT payment_events_payment_id_fkey FOREIGN KEY (payment_id) REFERENCES core.payments(id) ON DELETE SET NULL,
	CONSTRAINT payment_events_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);


-- core.product_inventory definition

-- Drop table

-- DROP TABLE core.product_inventory;

CREATE TABLE core.product_inventory (
	product_id uuid NOT NULL,
	quantity int4 DEFAULT 0 NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT product_inventory_pkey PRIMARY KEY (product_id),
	CONSTRAINT product_inventory_product_id_fkey FOREIGN KEY (product_id) REFERENCES core.products(id) ON DELETE CASCADE
);


-- core.service_slots definition

-- Drop table

-- DROP TABLE core.service_slots;

CREATE TABLE core.service_slots (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	service_id uuid NOT NULL,
	start_at timestamptz NOT NULL,
	end_at timestamptz NOT NULL,
	capacity int4 DEFAULT 1 NOT NULL,
	is_available bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT service_slots_pkey PRIMARY KEY (id),
	CONSTRAINT service_slots_service_id_start_at_key UNIQUE (service_id, start_at),
	CONSTRAINT service_slots_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE CASCADE,
	CONSTRAINT service_slots_service_id_fkey FOREIGN KEY (service_id) REFERENCES core.services(id) ON DELETE CASCADE,
	CONSTRAINT service_slots_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_service_slots_partner ON core.service_slots USING btree (partner_id);
CREATE INDEX ix_core_service_slots_start ON core.service_slots USING btree (start_at);


-- core.bookings definition

-- Drop table

-- DROP TABLE core.bookings;

CREATE TABLE core.bookings (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NOT NULL,
	service_id uuid NOT NULL,
	slot_id uuid NULL,
	customer_id uuid NOT NULL,
	status text NOT NULL,
	start_at timestamptz NOT NULL,
	end_at timestamptz NOT NULL,
	amount numeric(14, 2) DEFAULT 0 NOT NULL,
	currency text DEFAULT 'CLP'::text NOT NULL,
	cancellation_policy jsonb DEFAULT '{}'::jsonb NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT bookings_pkey PRIMARY KEY (id),
	CONSTRAINT bookings_status_check CHECK ((status = ANY (ARRAY['payment_pending'::text, 'confirmed'::text, 'attended'::text, 'no_show'::text, 'cancelled'::text, 'failed'::text]))),
	CONSTRAINT bookings_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES core.customers(id) ON DELETE RESTRICT,
	CONSTRAINT bookings_partner_id_fkey FOREIGN KEY (partner_id) REFERENCES core.partners(id) ON DELETE RESTRICT,
	CONSTRAINT bookings_service_id_fkey FOREIGN KEY (service_id) REFERENCES core.services(id) ON DELETE RESTRICT,
	CONSTRAINT bookings_slot_id_fkey FOREIGN KEY (slot_id) REFERENCES core.service_slots(id) ON DELETE SET NULL,
	CONSTRAINT bookings_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES core.tenants(id) ON DELETE CASCADE
);
CREATE INDEX ix_core_bookings_customer ON core.bookings USING btree (customer_id);
CREATE INDEX ix_core_bookings_partner ON core.bookings USING btree (partner_id);
CREATE INDEX ix_core_bookings_start ON core.bookings USING btree (start_at);
CREATE INDEX ix_core_bookings_status ON core.bookings USING btree (status);
CREATE INDEX ix_core_bookings_tenant ON core.bookings USING btree (tenant_id);

-- Table Triggers

create trigger trg_core_bookings_updated_at before
update
    on
    core.bookings for each row execute function core.set_updated_at();


-- core.deliveries definition

-- Drop table

-- DROP TABLE core.deliveries;

CREATE TABLE core.deliveries (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	order_id uuid NOT NULL,
	address text NOT NULL,
	notes text NULL,
	status text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT deliveries_order_id_key UNIQUE (order_id),
	CONSTRAINT deliveries_pkey PRIMARY KEY (id),
	CONSTRAINT deliveries_status_check CHECK ((status = ANY (ARRAY['pending'::text, 'assigned'::text, 'picked'::text, 'delivered'::text, 'failed'::text, 'cancelled'::text]))),
	CONSTRAINT deliveries_order_id_fkey FOREIGN KEY (order_id) REFERENCES core.orders(id) ON DELETE CASCADE
);


-- core.order_items definition

-- Drop table

-- DROP TABLE core.order_items;

CREATE TABLE core.order_items (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	order_id uuid NOT NULL,
	product_id uuid NOT NULL,
	quantity int4 NOT NULL,
	unit_price numeric(14, 2) DEFAULT 0 NOT NULL,
	total_price numeric(14, 2) DEFAULT 0 NOT NULL,
	CONSTRAINT order_items_pkey PRIMARY KEY (id),
	CONSTRAINT order_items_quantity_check CHECK ((quantity > 0)),
	CONSTRAINT order_items_order_id_fkey FOREIGN KEY (order_id) REFERENCES core.orders(id) ON DELETE CASCADE,
	CONSTRAINT order_items_product_id_fkey FOREIGN KEY (product_id) REFERENCES core.products(id) ON DELETE RESTRICT
);
CREATE INDEX ix_core_order_items_order ON core.order_items USING btree (order_id);



-- DROP FUNCTION core.set_updated_at();

CREATE OR REPLACE FUNCTION core.set_updated_at()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$function$
;

-- =========================
-- ACL (modelo base)
-- =========================
-- DROP SCHEMA acl;

-- acl.permissions definition

-- Drop table

-- DROP TABLE acl.permissions;

CREATE TABLE acl.permissions (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	code text NOT NULL,
	description text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT permissions_code_key UNIQUE (code),
	CONSTRAINT permissions_pkey PRIMARY KEY (id)
);


-- acl.roles definition

-- Drop table

-- DROP TABLE acl.roles;

CREATE TABLE acl.roles (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	"name" text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT roles_name_key UNIQUE (name),
	CONSTRAINT roles_pkey PRIMARY KEY (id)
);


-- acl.users definition

-- Drop table

-- DROP TABLE acl.users;

CREATE TABLE acl.users (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	email text NOT NULL,
	password_hash text NOT NULL,
	display_name text NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT users_email_key UNIQUE (email),
	CONSTRAINT users_pkey PRIMARY KEY (id)
);


-- acl.refresh_tokens definition

-- Drop table

-- DROP TABLE acl.refresh_tokens;

CREATE TABLE acl.refresh_tokens (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	user_id uuid NOT NULL,
	token_hash text NOT NULL,
	issued_at timestamptz DEFAULT now() NOT NULL,
	expires_at timestamptz NOT NULL,
	revoked_at timestamptz NULL,
	user_agent text NULL,
	ip_address text NULL,
	CONSTRAINT refresh_tokens_pkey PRIMARY KEY (id),
	CONSTRAINT refresh_tokens_token_hash_key UNIQUE (token_hash),
	CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES acl.users(id) ON DELETE CASCADE
);
CREATE INDEX ix_acl_refresh_tokens_expires ON acl.refresh_tokens USING btree (expires_at);
CREATE INDEX ix_acl_refresh_tokens_user ON acl.refresh_tokens USING btree (user_id);


-- acl.role_permissions definition

-- Drop table

-- DROP TABLE acl.role_permissions;

CREATE TABLE acl.role_permissions (
	role_id uuid NOT NULL,
	permission_id uuid NOT NULL,
	CONSTRAINT role_permissions_pkey PRIMARY KEY (role_id, permission_id),
	CONSTRAINT role_permissions_permission_id_fkey FOREIGN KEY (permission_id) REFERENCES acl.permissions(id) ON DELETE CASCADE,
	CONSTRAINT role_permissions_role_id_fkey FOREIGN KEY (role_id) REFERENCES acl.roles(id) ON DELETE CASCADE
);


-- acl.user_roles definition

-- Drop table

-- DROP TABLE acl.user_roles;

CREATE TABLE acl.user_roles (
	user_id uuid NOT NULL,
	role_id uuid NOT NULL,
	CONSTRAINT user_roles_pkey PRIMARY KEY (user_id, role_id),
	CONSTRAINT user_roles_role_id_fkey FOREIGN KEY (role_id) REFERENCES acl.roles(id) ON DELETE CASCADE,
	CONSTRAINT user_roles_user_id_fkey FOREIGN KEY (user_id) REFERENCES acl.users(id) ON DELETE CASCADE
);


-- acl.user_tenant_scope definition

-- Drop table

-- DROP TABLE acl.user_tenant_scope;

CREATE TABLE acl.user_tenant_scope (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	user_id uuid NOT NULL,
	tenant_id uuid NOT NULL,
	partner_id uuid NULL,
	scope_type text DEFAULT 'tenant'::text NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT user_tenant_scope_pkey PRIMARY KEY (id),
	CONSTRAINT user_tenant_scope_user_id_tenant_id_partner_id_scope_type_key UNIQUE (user_id, tenant_id, partner_id, scope_type),
	CONSTRAINT user_tenant_scope_user_id_fkey FOREIGN KEY (user_id) REFERENCES acl.users(id) ON DELETE CASCADE
);
CREATE INDEX ix_acl_user_tenant_scope_tenant ON acl.user_tenant_scope USING btree (tenant_id);
CREATE INDEX ix_acl_user_tenant_scope_user ON acl.user_tenant_scope USING btree (user_id);

-- =========================
-- Actualizaciones para multi-tenant por comuna + geografía + vistas selector
-- =========================
-- =========================================================
-- core_update.sql
-- Actualiza schema core para soportar:
-- - Catálogo geográfico (countries/regions/comunas) + georeferencia
-- - Tenant real = comuna (core.tenants.comuna_id)
-- Ejecutar en la DB: acl_db
-- =========================================================

-- (Opcional) extensiones si no existen
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Schema (no cambia owner si ya existe)
CREATE SCHEMA IF NOT EXISTS core;

-- =========================
-- 1) Catálogo geográfico
-- =========================
CREATE TABLE IF NOT EXISTS core.countries (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  code       text NOT NULL UNIQUE,   -- ej: CL
  name       text NOT NULL,
  is_active  boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS core.regions (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  country_id      uuid NOT NULL REFERENCES core.countries(id) ON DELETE RESTRICT,
  code            text NOT NULL,      -- ej: RM (o el que uses)
  name            text NOT NULL,
  legacy_region_id integer NULL,      -- id del dataset (ej gist)
  is_active       boolean NOT NULL DEFAULT true,
  created_at      timestamptz NOT NULL DEFAULT now(),
  UNIQUE (country_id, code)
);

CREATE INDEX IF NOT EXISTS ix_core_regions_country
  ON core.regions(country_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_core_regions_legacy
  ON core.regions(country_id, legacy_region_id)
  WHERE legacy_region_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS core.comunas (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  region_id         uuid NOT NULL REFERENCES core.regions(id) ON DELETE RESTRICT,
  code              text NOT NULL,          -- ej: 13101 (o code interno)
  name              text NOT NULL,
  legacy_comuna_id  integer NULL,           -- id del dataset (ej gist)
  latitude          double precision NULL,
  longitude         double precision NULL,
  is_active         boolean NOT NULL DEFAULT true,
  created_at        timestamptz NOT NULL DEFAULT now(),
  UNIQUE (region_id, code)
);

CREATE INDEX IF NOT EXISTS ix_core_comunas_region
  ON core.comunas(region_id);

CREATE INDEX IF NOT EXISTS ix_core_comunas_region_name
  ON core.comunas(region_id, name);

CREATE UNIQUE INDEX IF NOT EXISTS ux_core_comunas_legacy
  ON core.comunas(region_id, legacy_comuna_id)
  WHERE legacy_comuna_id IS NOT NULL;

-- =========================
-- 2) Tenant real = comuna
-- =========================
ALTER TABLE core.tenants
  ADD COLUMN IF NOT EXISTS comuna_id uuid NULL;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_core_tenants_comuna'
  ) THEN
    ALTER TABLE core.tenants
      ADD CONSTRAINT fk_core_tenants_comuna
      FOREIGN KEY (comuna_id)
      REFERENCES core.comunas(id)
      ON DELETE RESTRICT;
  END IF;
END$$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_core_tenants_comuna
  ON core.tenants(comuna_id)
  WHERE comuna_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_core_tenants_active_comuna
  ON core.tenants(is_active, comuna_id);

-- =========================
-- 3) Vista helper (opcional)
-- =========================
CREATE OR REPLACE VIEW core.v_tenant_geo AS
SELECT
  t.id          AS tenant_id,
  t.name        AS tenant_name,
  c.id          AS comuna_id,
  c.code        AS comuna_code,
  c.name        AS comuna_name,
  c.legacy_comuna_id,
  c.latitude,
  c.longitude,
  (c.latitude IS NOT NULL AND c.longitude IS NOT NULL) AS has_geo,
  r.id          AS region_id,
  r.code        AS region_code,
  r.name        AS region_name,
  r.legacy_region_id,
  co.id         AS country_id,
  co.code       AS country_code,
  co.name       AS country_name
FROM core.tenants t
LEFT JOIN core.comunas  c  ON c.id = t.comuna_id
LEFT JOIN core.regions  r  ON r.id = c.region_id
LEFT JOIN core.countries co ON co.id = r.country_id;


-- =========================================================
-- acl_update.sql
-- Actualiza schema acl para soportar:
-- - Super Admin global
-- - Accesos por tenant (comuna) / región / país + platform
-- - Vista para selector de comuna con georeferencia
-- Ejecutar en la DB: acl_db
-- =========================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE SCHEMA IF NOT EXISTS acl;

-- =========================
-- 1) Super Admin
-- =========================
ALTER TABLE acl.users
  ADD COLUMN IF NOT EXISTS is_super_admin boolean NOT NULL DEFAULT false;

CREATE INDEX IF NOT EXISTS ix_acl_users_is_super_admin
  ON acl.users(is_super_admin);

-- =========================
-- 2) Tablas de acceso (FK estrictas a core)
-- =========================

-- Acceso directo a tenants (comunas)
CREATE TABLE IF NOT EXISTS acl.user_tenant_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  tenant_id  uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_acl_user_tenant_access_user
  ON acl.user_tenant_access(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_user_tenant_access_tenant
  ON acl.user_tenant_access(tenant_id);

-- Acceso por región (expande a comunas/tenants)
CREATE TABLE IF NOT EXISTS acl.user_region_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  region_id  uuid NOT NULL REFERENCES core.regions(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, region_id)
);

CREATE INDEX IF NOT EXISTS ix_acl_user_region_access_user
  ON acl.user_region_access(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_user_region_access_region
  ON acl.user_region_access(region_id);

-- Acceso por país (expande a comunas/tenants)
CREATE TABLE IF NOT EXISTS acl.user_country_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  country_id uuid NOT NULL REFERENCES core.countries(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, country_id)
);

CREATE INDEX IF NOT EXISTS ix_acl_user_country_access_user
  ON acl.user_country_access(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_user_country_access_country
  ON acl.user_country_access(country_id);

-- Acceso platform (backoffice global sin tenant)
CREATE TABLE IF NOT EXISTS acl.user_platform_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id)
);

-- =========================
-- 3) Migración desde acl.user_tenant_scope (legacy)
--    - tenant -> user_tenant_access
--    - platform -> user_platform_access
-- =========================

-- Si existe tabla legacy, migramos (idempotente)
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema='acl' AND table_name='user_tenant_scope'
  ) THEN
    -- tenant
    INSERT INTO acl.user_tenant_access (user_id, tenant_id)
    SELECT uts.user_id, uts.tenant_id
    FROM acl.user_tenant_scope uts
    WHERE lower(uts.scope_type) = 'tenant'
    ON CONFLICT (user_id, tenant_id) DO NOTHING;

    -- platform
    INSERT INTO acl.user_platform_access (user_id)
    SELECT DISTINCT uts.user_id
    FROM acl.user_tenant_scope uts
    WHERE lower(uts.scope_type) = 'platform'
    ON CONFLICT (user_id) DO NOTHING;
  END IF;
END$$;

-- =========================
-- 4) Vista para selector de comuna (con geo)
-- =========================
CREATE OR REPLACE VIEW acl.v_user_accessible_tenants AS
WITH direct AS (
  SELECT uta.user_id, uta.tenant_id
  FROM acl.user_tenant_access uta
),
by_region AS (
  SELECT ura.user_id, t.id AS tenant_id
  FROM acl.user_region_access ura
  JOIN core.comunas c ON c.region_id = ura.region_id
  JOIN core.tenants t ON t.comuna_id = c.id
),
by_country AS (
  SELECT uca.user_id, t.id AS tenant_id
  FROM acl.user_country_access uca
  JOIN core.regions r ON r.country_id = uca.country_id
  JOIN core.comunas c ON c.region_id = r.id
  JOIN core.tenants t ON t.comuna_id = c.id
),
scoped AS (
  SELECT * FROM direct
  UNION
  SELECT * FROM by_region
  UNION
  SELECT * FROM by_country
)
SELECT DISTINCT
  u.id          AS user_id,

  t.id          AS tenant_id,
  t.name        AS tenant_name,

  c.id          AS comuna_id,
  c.code        AS comuna_code,
  c.name        AS comuna_name,
  c.legacy_comuna_id,
  c.latitude,
  c.longitude,
  (c.latitude IS NOT NULL AND c.longitude IS NOT NULL) AS has_geo,
  CASE
    WHEN c.latitude IS NOT NULL AND c.longitude IS NOT NULL
      THEN ('POINT(' || c.longitude::text || ' ' || c.latitude::text || ')')
    ELSE NULL
  END AS geo_wkt,

  r.id          AS region_id,
  r.code        AS region_code,
  r.name        AS region_name,
  r.legacy_region_id,

  co.id         AS country_id,
  co.code       AS country_code,
  co.name       AS country_name

FROM acl.users u
JOIN core.tenants t ON t.is_active = true
LEFT JOIN core.comunas  c  ON c.id = t.comuna_id
LEFT JOIN core.regions  r  ON r.id = c.region_id
LEFT JOIN core.countries co ON co.id = r.country_id
LEFT JOIN scoped s ON s.user_id = u.id AND s.tenant_id = t.id
WHERE u.is_active = true
  AND (
    u.is_super_admin = true
    OR s.tenant_id IS NOT NULL
  );

-- Helper para validación rápida (opcional)
CREATE OR REPLACE VIEW acl.v_user_can_access_tenant AS
SELECT user_id, tenant_id
FROM acl.v_user_accessible_tenants;

