BEGIN;

SET search_path TO acl;

-- password_hash: PBKDF2-SHA256 para la contraseña demo "test123" (mismo algoritmo que PasswordHasher en ACL).
INSERT INTO users (id, email, password_hash, display_name, is_active, is_super_admin, created_at, updated_at)
VALUES
  ('21212121-2121-2121-2121-212121212121', 'owner@comunaclic.test', 'pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=', 'Owner Demo', true, false, now(), now()),
  ('99999999-0000-0000-0000-000000000001', 'admin@comunaclic.test', 'pbkdf2:100000:fawH1t3ZKttmoUdvpSMGww==:ErGBBTDvYkOeSuN9I/cVV6DPBDNaqV0wVFAl6jtEVao=', 'Super Admin', true, true, now(), now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO roles (id, name, created_at)
VALUES
  ('a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1', 'platform_admin', now()),
  ('b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1', 'tenant_admin', now()),
  ('c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1', 'partner_owner', now()),
  ('d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1', 'partner_staff', now()),
  ('e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1', 'buyer', now()),
  ('e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2', 'customer', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO permissions (id, code, description, created_at)
VALUES
  ('f1f1f1f1-f1f1-f1f1-f1f1-f1f1f1f1f1f1', 'manage_users', 'Manage users and roles', now()),
  ('f2f2f2f2-f2f2-f2f2-f2f2-f2f2f2f2f2f2', 'manage_catalog', 'Manage catalog items', now()),
  ('f3f3f3f3-f3f3-f3f3-f3f3-f3f3f3f3f3f3', 'view_orders', 'View orders and bookings', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
VALUES
  ('c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1', 'f2f2f2f2-f2f2-f2f2-f2f2-f2f2f2f2f2f2'),
  ('c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1', 'f3f3f3f3-f3f3-f3f3-f3f3-f3f3f3f3f3f3')
ON CONFLICT DO NOTHING;

INSERT INTO user_roles (user_id, role_id)
VALUES
  ('21212121-2121-2121-2121-212121212121', 'c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1'),
  ('99999999-0000-0000-0000-000000000001', 'a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1')
ON CONFLICT DO NOTHING;

INSERT INTO user_tenant_scope (id, user_id, tenant_id, partner_id, scope_type, created_at)
VALUES
  ('31313131-3131-3131-3131-313131313131', '21212121-2121-2121-2121-212121212121', '11111111-1111-1111-1111-111111111111', null, 'tenant', now()),
  ('32323232-3232-3232-3232-323232323232', '21212121-2121-2121-2121-212121212121', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', 'partner', now())
ON CONFLICT DO NOTHING;

INSERT INTO user_tenant_access (user_id, tenant_id)
VALUES
  ('21212121-2121-2121-2121-212121212121', '11111111-1111-1111-1111-111111111111')
ON CONFLICT DO NOTHING;

INSERT INTO user_platform_access (user_id)
VALUES
  ('99999999-0000-0000-0000-000000000001')
ON CONFLICT DO NOTHING;

COMMIT;
