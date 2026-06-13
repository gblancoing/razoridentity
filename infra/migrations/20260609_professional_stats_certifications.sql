-- Contador de visualizaciones del perfil público
ALTER TABLE core.professionals
  ADD COLUMN IF NOT EXISTS profile_view_count BIGINT NOT NULL DEFAULT 0;

-- Certificaciones del profesional en formato JSON (array de {id,name,institution,year,url})
ALTER TABLE core.professionals
  ADD COLUMN IF NOT EXISTS certifications_json TEXT;
