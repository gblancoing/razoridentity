ALTER TABLE core.partners
    ADD COLUMN IF NOT EXISTS latitude double precision NULL,
    ADD COLUMN IF NOT EXISTS longitude double precision NULL;

CREATE INDEX IF NOT EXISTS ix_core_partners_latitude ON core.partners (latitude);
CREATE INDEX IF NOT EXISTS ix_core_partners_longitude ON core.partners (longitude);
