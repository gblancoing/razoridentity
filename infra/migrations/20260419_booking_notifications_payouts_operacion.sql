alter table core.bookings
    add column if not exists internal_note text null,
    add column if not exists outcome_reason varchar(240) null;

create index if not exists ix_bookings_partner_start_at
    on core.bookings(tenant_id, partner_id, start_at);

create index if not exists ix_bookings_partner_status
    on core.bookings(tenant_id, partner_id, status);

alter table core.interactions
    add column if not exists read_at timestamptz null,
    add column if not exists archived_at timestamptz null;

create index if not exists ix_interactions_partner_created_at
    on core.interactions(tenant_id, partner_id, created_at desc);

create index if not exists ix_interactions_partner_archive_read
    on core.interactions(tenant_id, partner_id, archived_at, read_at);
