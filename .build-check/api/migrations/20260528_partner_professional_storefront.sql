ALTER TABLE core.partners
    ADD COLUMN IF NOT EXISTS banner_url text,
    ADD COLUMN IF NOT EXISTS storefront_tagline text,
    ADD COLUMN IF NOT EXISTS storefront_about text,
    ADD COLUMN IF NOT EXISTS storefront_highlight_1 text,
    ADD COLUMN IF NOT EXISTS storefront_highlight_2 text,
    ADD COLUMN IF NOT EXISTS storefront_highlight_3 text;

ALTER TABLE core.professionals
    ADD COLUMN IF NOT EXISTS banner_url text,
    ADD COLUMN IF NOT EXISTS profile_headline text;
