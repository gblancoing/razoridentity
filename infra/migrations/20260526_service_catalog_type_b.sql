-- Catálogo tipo B: avisos reservables, staff y soft-delete
SET search_path TO core, public;

ALTER TABLE core.services
    ADD COLUMN IF NOT EXISTS is_bookable boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS requires_online_payment boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS image_url text,
    ADD COLUMN IF NOT EXISTS deleted_at timestamptz;

UPDATE core.services
SET is_bookable = (price > 0),
    requires_online_payment = (price > 0)
WHERE is_bookable = false AND price > 0;

ALTER TABLE core.professionals
    ADD COLUMN IF NOT EXISTS partner_id uuid REFERENCES core.partners(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_core_professionals_partner ON core.professionals(partner_id);

CREATE TABLE IF NOT EXISTS core.service_professionals (
    service_id uuid NOT NULL REFERENCES core.services(id) ON DELETE CASCADE,
    professional_id uuid NOT NULL REFERENCES core.professionals(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (service_id, professional_id)
);

CREATE INDEX IF NOT EXISTS ix_core_service_professionals_professional ON core.service_professionals(professional_id);

ALTER TABLE core.service_slots
    ADD COLUMN IF NOT EXISTS professional_id uuid REFERENCES core.professionals(id) ON DELETE SET NULL;

ALTER TABLE core.bookings
    ADD COLUMN IF NOT EXISTS professional_id uuid REFERENCES core.professionals(id) ON DELETE SET NULL;
