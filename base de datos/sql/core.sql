-- DROP SCHEMA core;

CREATE SCHEMA core AUTHORIZATION core_app_user;
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