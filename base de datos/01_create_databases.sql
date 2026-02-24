-- =========================================================
-- 01_create_databases.sql
-- Ejecutar conectado a la DB "postgres" (o cualquier DB maintenance)
-- Requiere rol con permisos para CREATE DATABASE.
--
-- NOTA: PostgreSQL NO soporta CREATE DATABASE IF NOT EXISTS.
-- Si ya existen, este script fallará en esas líneas.
-- =========================================================

CREATE DATABASE core_db;
CREATE DATABASE acl_db;
CREATE DATABASE payments_db;

GRANT CONNECT ON DATABASE core_db TO core_app_user;
GRANT CONNECT ON DATABASE acl_db TO acl_app_user;
GRANT CONNECT ON DATABASE payments_db TO payments_app_user;
