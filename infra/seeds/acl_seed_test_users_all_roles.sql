-- Usuarios de prueba por rol (contraseña para todos: test123).
-- Requiere haber aplicado antes acl_seed.sql (roles, permisos base) o que existan los mismos UUID de roles.
-- Hash PBKDF2 generado con ComunaClick.Acl Security.PasswordHasher para "test123".

BEGIN;

SET search_path TO acl;

-- Mismo hash que en acl_seed.sql para test123
-- pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=

INSERT INTO roles (id, name, created_at)
VALUES ('e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2', 'customer', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO users (id, email, password_hash, display_name, is_active, is_super_admin, created_at, updated_at)
VALUES
  -- Admin de plataforma (mismo rol que admin@comunaclic.test; usuario alterno)
  ('a0a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a0', 'platform.admin@comunaclic.test',
   'pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=',
   'QA Platform Admin', true, true, now(), now()),
  -- Administrador de tenant (sin super admin)
  ('a1a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a1', 'tenant.admin@comunaclic.test',
   'pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=',
   'QA Tenant Admin', true, false, now(), now()),
  -- Staff del partner (no owner)
  ('b0b0b0b0-b0b0-b0b0-b0b0-b0b0b0b0b0b0', 'partner.staff@comunaclic.test',
   'pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=',
   'QA Partner Staff', true, false, now(), now()),
  -- Comprador (rol customer — exigido por la API buyer.customer)
  ('c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0', 'buyer@comunaclic.test',
   'pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=',
   'QA Buyer', true, false, now(), now()),
  -- Rol buyer legado (si quieres probar claims con nombre "buyer")
  ('d0d0d0d0-d0d0-d0d0-d0d0-d0d0d0d0d0d0', 'legacy.buyer@comunaclic.test',
   'pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=',
   'QA Legacy Buyer Role', true, false, now(), now())
ON CONFLICT (id) DO UPDATE SET
  password_hash = EXCLUDED.password_hash,
  display_name = EXCLUDED.display_name,
  is_active = EXCLUDED.is_active,
  is_super_admin = EXCLUDED.is_super_admin,
  updated_at = now();

INSERT INTO user_roles (user_id, role_id)
VALUES
  ('a0a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a0', 'a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1'),
  ('a1a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a1', 'b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1'),
  ('b0b0b0b0-b0b0-b0b0-b0b0-b0b0b0b0b0b0', 'd1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1'),
  ('c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0', 'e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2'),
  ('d0d0d0d0-d0d0-d0d0-d0d0-d0d0d0d0d0d0', 'e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1')
ON CONFLICT DO NOTHING;

INSERT INTO user_platform_access (user_id)
VALUES ('a0a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a0')
ON CONFLICT DO NOTHING;

INSERT INTO user_tenant_access (user_id, tenant_id)
VALUES
  ('a1a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a1', '11111111-1111-1111-1111-111111111111'),
  ('b0b0b0b0-b0b0-b0b0-b0b0-b0b0b0b0b0b0', '11111111-1111-1111-1111-111111111111'),
  ('c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0', '11111111-1111-1111-1111-111111111111'),
  ('d0d0d0d0-d0d0-d0d0-d0d0-d0d0d0d0d0d0', '11111111-1111-1111-1111-111111111111')
ON CONFLICT DO NOTHING;

INSERT INTO user_tenant_scope (id, user_id, tenant_id, partner_id, scope_type, created_at)
VALUES
  ('a1a1a1a0-a1a1-a1a1-a1a1-a1a1a1a1a1a0', 'a1a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a1',
   '11111111-1111-1111-1111-111111111111', null, 'tenant', now()),
  ('b1b1b1b0-b1b1-b1b1-b1b1-b1b1b1b1b1b0', 'b0b0b0b0-b0b0-b0b0-b0b0-b0b0b0b0b0b0',
   '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', 'partner', now()),
  ('c1c1c1c0-c1c1-c1c1-c1c1-c1c1c1c1c1c0', 'c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0',
   '11111111-1111-1111-1111-111111111111', null, 'tenant', now()),
  ('d1d1d1d0-d1d1-d1d1-d1d1-d1d1d1d1d1d0', 'd0d0d0d0-d0d0-d0d0-d0d0-d0d0d0d0d0d0',
   '11111111-1111-1111-1111-111111111111', null, 'tenant', now())
ON CONFLICT (id) DO NOTHING;

COMMIT;
