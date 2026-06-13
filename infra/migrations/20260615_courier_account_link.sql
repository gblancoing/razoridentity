-- Vincula el repartidor a una cuenta de usuario (portal self-service).
-- El comercio registra al repartidor con su correo; al crear una cuenta con
-- ese correo, el portal reclama la fila escribiendo user_id.
-- Idempotente: se aplica desde DatabaseSchemaBootstrap al arrancar el API.
ALTER TABLE core.couriers
    ADD COLUMN IF NOT EXISTS email text NULL,
    ADD COLUMN IF NOT EXISTS user_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_couriers_email_lower ON core.couriers (lower(email));
CREATE INDEX IF NOT EXISTS ix_couriers_user_id ON core.couriers (user_id);

COMMENT ON COLUMN core.couriers.email IS 'Correo del repartidor (lo registra el negocio); habilita el reclamo del portal.';
COMMENT ON COLUMN core.couriers.user_id IS 'Usuario que reclamó este perfil de repartidor (FK lógica a ACL users).';
