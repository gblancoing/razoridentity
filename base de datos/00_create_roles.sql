-- =========================================================
-- 00_create_roles.sql
-- Ejecutar conectado a la DB "postgres" (o cualquier DB maintenance)
-- Requiere rol con permisos para CREATE ROLE.
-- =========================================================

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'core_app_user') THEN
    CREATE ROLE core_app_user LOGIN PASSWORD 'REPLACE_STRONG_PASSWORD';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'acl_app_user') THEN
    CREATE ROLE acl_app_user LOGIN PASSWORD 'REPLACE_STRONG_PASSWORD';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'payments_app_user') THEN
    CREATE ROLE payments_app_user LOGIN PASSWORD 'REPLACE_STRONG_PASSWORD';
  END IF;
END $$;
