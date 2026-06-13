BEGIN;

ALTER TABLE core.products
    ADD COLUMN IF NOT EXISTS image_url text;

COMMIT;
