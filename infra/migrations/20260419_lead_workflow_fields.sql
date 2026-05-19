alter table core.leads
    add column if not exists priority varchar(32) null,
    add column if not exists owner varchar(120) null,
    add column if not exists next_follow_up_at timestamptz null,
    add column if not exists internal_note text null,
    add column if not exists outcome_reason varchar(240) null;

create index if not exists ix_leads_priority
    on core.leads(tenant_id, priority);

create index if not exists ix_leads_next_follow_up_at
    on core.leads(tenant_id, next_follow_up_at);
