create table if not exists core.delivery_providers (
    id uuid primary key default gen_random_uuid(),
    tenant_id uuid null,
    region_id uuid null,
    comuna_id uuid null,
    name varchar(160) not null,
    contact_name varchar(120) null,
    contact_phone varchar(64) null,
    contact_email varchar(160) null,
    base_fee numeric(14,2) not null default 0,
    estimated_minutes int null,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists ix_delivery_providers_scope
    on core.delivery_providers(tenant_id, region_id, comuna_id, is_active);

create table if not exists core.shopping_carts (
    id uuid primary key default gen_random_uuid(),
    tenant_id uuid not null,
    partner_id uuid not null,
    customer_id uuid not null,
    status varchar(32) not null default 'active',
    subtotal numeric(14,2) not null default 0,
    delivery_fee numeric(14,2) not null default 0,
    total_amount numeric(14,2) not null default 0,
    currency varchar(8) not null default 'CLP',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists ix_shopping_carts_lookup
    on core.shopping_carts(tenant_id, customer_id, partner_id, status);

create table if not exists core.shopping_cart_items (
    id uuid primary key default gen_random_uuid(),
    cart_id uuid not null references core.shopping_carts(id) on delete cascade,
    product_id uuid not null references core.products(id),
    quantity int not null,
    unit_price numeric(14,2) not null,
    total_price numeric(14,2) not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_shopping_cart_items_cart_product
    on core.shopping_cart_items(cart_id, product_id);

alter table core.orders
    add column if not exists delivery_provider_id uuid null,
    add column if not exists delivery_provider_name varchar(160) null,
    add column if not exists delivery_address varchar(300) null;
