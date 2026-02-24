-- =========================================================
-- comunaclick_db__04_seed_acl_admin_pgadmin.sql
-- PGADMIN-FRIENDLY
--
-- Ejecutar en Query Tool (conectado a comunaclick_db).
-- Reemplaza los 3 valores marcados:
--   <<ADMIN_EMAIL>>
--   <<ADMIN_DISPLAY>>
--   <<ADMIN_PASSWORD_HASH>>
--
-- NOTA: El hash debe venir desde tu app (BCrypt/Argon2/etc).
-- =========================================================

BEGIN;

INSERT INTO acl.users (email, password_hash, display_name, is_active, is_super_admin)
VALUES ('<<ADMIN_EMAIL>>', '<<ADMIN_PASSWORD_HASH>>', '<<ADMIN_DISPLAY>>', true, true)
ON CONFLICT (email)
DO UPDATE SET
  password_hash  = EXCLUDED.password_hash,
  display_name   = EXCLUDED.display_name,
  is_active      = true,
  is_super_admin = true,
  updated_at     = now();

INSERT INTO acl.user_platform_access (user_id)
SELECT id FROM acl.users WHERE email='<<ADMIN_EMAIL>>'
ON CONFLICT (user_id) DO NOTHING;

COMMIT;

-- Verificación:
-- SELECT id, email, is_super_admin, is_active FROM acl.users WHERE email='<<ADMIN_EMAIL>>';
-- SELECT * FROM acl.user_platform_access WHERE user_id=(SELECT id FROM acl.users WHERE email='<<ADMIN_EMAIL>>');
