-- Foto de perfil circular del profesional (avatar en ficha pública)
ALTER TABLE core.professionals
  ADD COLUMN IF NOT EXISTS profile_photo_url TEXT;
