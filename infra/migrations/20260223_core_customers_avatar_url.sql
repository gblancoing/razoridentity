-- Foto de perfil del comprador (URL https pública; sin almacenamiento de binarios en BD).
ALTER TABLE core.customers
  ADD COLUMN IF NOT EXISTS avatar_url text;

COMMENT ON COLUMN core.customers.avatar_url IS 'URL https de imagen de perfil del cliente (opcional).';
