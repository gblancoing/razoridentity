create table if not exists core.buyer_favorites (
    id uuid primary key default gen_random_uuid(),
    tenant_id uuid not null,
    customer_id uuid not null references core.customers(id) on delete cascade,
    type varchar(32) not null,
    target_id uuid not null,
    partner_id uuid null references core.partners(id) on delete set null,
    created_at timestamptz not null default now()
);

create unique index if not exists ux_buyer_favorites_scope
    on core.buyer_favorites(tenant_id, customer_id, type, target_id);

create index if not exists ix_buyer_favorites_customer_created_at
    on core.buyer_favorites(tenant_id, customer_id, created_at desc);

create index if not exists ix_buyer_favorites_partner
    on core.buyer_favorites(tenant_id, partner_id);
