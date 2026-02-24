BEGIN;

TRUNCATE TABLE
  acl.role_permissions,
  acl.user_roles,
  acl.user_tenant_scope,
  acl.user_tenant_access,
  acl.user_region_access,
  acl.user_country_access,
  acl.user_platform_access,
  acl.refresh_tokens,
  acl.password_reset_tokens,
  acl.permissions,
  acl.roles,
  acl.users
RESTART IDENTITY CASCADE;

COMMIT;
