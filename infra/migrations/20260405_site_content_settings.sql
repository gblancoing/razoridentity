create table if not exists core.site_content_settings (
    id uuid primary key,
    section varchar(64) not null,
    content_json jsonb not null default '{}'::jsonb,
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_site_content_settings_section
    on core.site_content_settings(section);
