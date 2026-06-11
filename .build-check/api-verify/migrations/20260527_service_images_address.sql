ALTER TABLE core.services
    ADD COLUMN IF NOT EXISTS service_address text;

CREATE TABLE IF NOT EXISTS core.service_images (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    service_id uuid NOT NULL REFERENCES core.services(id) ON DELETE CASCADE,
    url text NOT NULL,
    sort_order int NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_service_images_service
    ON core.service_images (service_id, sort_order);
