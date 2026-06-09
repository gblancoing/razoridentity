-- Coordenadas exactas por aviso (servicio), independientes de la comuna.
SET search_path TO core, public;

ALTER TABLE core.services
    ADD COLUMN IF NOT EXISTS latitude double precision NULL,
    ADD COLUMN IF NOT EXISTS longitude double precision NULL;

CREATE INDEX IF NOT EXISTS ix_core_services_latitude ON core.services (latitude);
CREATE INDEX IF NOT EXISTS ix_core_services_longitude ON core.services (longitude);
