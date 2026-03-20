BEGIN;

SET search_path TO acl;

-- Demo business user for /my-businesses -> partner session flow
INSERT INTO users (id, email, password_hash, display_name, is_active, is_super_admin, created_at, updated_at)
VALUES (
  '41414141-4141-4141-4141-414141414141',
  'negocio.alhue.demo@comunaclic.test',
  'test123',
  'Negocio Alhué Demo',
  true,
  false,
  now(),
  now()
)
ON CONFLICT (email) DO UPDATE
SET display_name = EXCLUDED.display_name,
    is_active = true,
    updated_at = now();

INSERT INTO user_roles (user_id, role_id)
VALUES
  ('41414141-4141-4141-4141-414141414141', 'c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1')
ON CONFLICT DO NOTHING;

-- Tenant-level scope that allows business selection within the tenant
INSERT INTO user_tenant_scope (id, user_id, tenant_id, partner_id, scope_type, created_at)
VALUES
  ('42424242-4242-4242-4242-424242424242', '41414141-4141-4141-4141-414141414141', '7b352be0-36f6-4ee5-9898-de8d3918858e', null, 'tenant', now())
ON CONFLICT (user_id, tenant_id, partner_id, scope_type) DO NOTHING;

INSERT INTO user_tenant_access (user_id, tenant_id)
VALUES
  ('41414141-4141-4141-4141-414141414141', '7b352be0-36f6-4ee5-9898-de8d3918858e')
ON CONFLICT DO NOTHING;

COMMIT;
