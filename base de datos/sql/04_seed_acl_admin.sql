-- =========================================================
-- 04_seed_acl_admin.sql
-- Crea un Super Admin inicial (ACL) + acceso platform.
-- Ejecutar en psql conectado a acl_db.
--
-- USO (recomendado):
--   \set admin_email 'admin@comunaclick.cl'
--   \set admin_display 'Super Admin'
--   \set admin_password_hash '<<PEGA-AQUI-TU-HASH>>'
--   \i 04_seed_acl_admin.sql
--
-- NOTA:
-- - Este script NO genera el hash de la contraseña (depende de tu app).
-- - Si tu app usa BCrypt/Argon2/etc., calcula el hash en la app y pégalo aquí.
-- =========================================================

BEGIN;

-- =========================
-- 1) Crear/actualizar usuario Super Admin
-- =========================
INSERT INTO acl.users (email, password_hash, display_name, is_active, is_super_admin)
VALUES (:'admin_email', :'admin_password_hash', :'admin_display', true, true)
ON CONFLICT (email)
DO UPDATE SET
  password_hash   = EXCLUDED.password_hash,
  display_name    = EXCLUDED.display_name,
  is_active       = true,
  is_super_admin  = true,
  updated_at      = now();

-- Obtener user_id
WITH u AS (
  SELECT id FROM acl.users WHERE email = :'admin_email'
)
-- =========================
-- 2) Acceso platform (backoffice global)
-- =========================
INSERT INTO acl.user_platform_access (user_id)
SELECT id FROM u
ON CONFLICT (user_id) DO NOTHING;

COMMIT;

-- =========================
-- Verificación
-- =========================
-- SELECT id, email, is_super_admin, is_active FROM acl.users WHERE email = :'admin_email';
-- SELECT * FROM acl.user_platform_access WHERE user_id = (SELECT id FROM acl.users WHERE email = :'admin_email');
