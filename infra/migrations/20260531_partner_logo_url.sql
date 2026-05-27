-- Logo / foto de perfil del negocio (marca en fichas públicas)
ALTER TABLE core.partners
  ADD COLUMN IF NOT EXISTS logo_url TEXT;
