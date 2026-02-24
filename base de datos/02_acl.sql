-- =========================================================
-- 02_acl.sql
-- Ejecutar conectado a la DB: acl_db
-- =========================================================

-- Extensiones
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Schema + permisos
CREATE SCHEMA IF NOT EXISTS acl AUTHORIZATION acl_app_user;

GRANT USAGE, CREATE ON SCHEMA acl TO acl_app_user;
ALTER ROLE acl_app_user IN DATABASE acl_db SET search_path = acl, public;

ALTER DEFAULT PRIVILEGES IN SCHEMA acl
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO acl_app_user;

ALTER DEFAULT PRIVILEGES IN SCHEMA acl
GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO acl_app_user;

-- =========================
-- TABLAS
-- =========================

CREATE TABLE IF NOT EXISTS acl.users (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  email            text NOT NULL UNIQUE,
  password_hash    text NOT NULL,
  display_name     text NULL,
  is_active        boolean NOT NULL DEFAULT true,
  created_at       timestamptz NOT NULL DEFAULT now(),
  updated_at       timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS acl.roles (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name        text NOT NULL UNIQUE,
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS acl.permissions (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  code        text NOT NULL UNIQUE,
  description text NULL,
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS acl.user_roles (
  user_id uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  role_id uuid NOT NULL REFERENCES acl.roles(id) ON DELETE CASCADE,
  PRIMARY KEY (user_id, role_id)
);

CREATE TABLE IF NOT EXISTS acl.role_permissions (
  role_id       uuid NOT NULL REFERENCES acl.roles(id) ON DELETE CASCADE,
  permission_id uuid NOT NULL REFERENCES acl.permissions(id) ON DELETE CASCADE,
  PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE IF NOT EXISTS acl.user_tenant_scope (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  tenant_id  uuid NOT NULL,
  partner_id uuid NULL,
  scope_type text NOT NULL DEFAULT 'tenant', -- tenant|partner|platform
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (user_id, tenant_id, partner_id, scope_type)
);

CREATE INDEX IF NOT EXISTS ix_acl_user_tenant_scope_user
  ON acl.user_tenant_scope(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_user_tenant_scope_tenant
  ON acl.user_tenant_scope(tenant_id);

CREATE TABLE IF NOT EXISTS acl.refresh_tokens (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id     uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  token_hash  text NOT NULL UNIQUE,
  issued_at   timestamptz NOT NULL DEFAULT now(),
  expires_at  timestamptz NOT NULL,
  revoked_at  timestamptz NULL,
  user_agent  text NULL,
  ip_address  text NULL
);

CREATE INDEX IF NOT EXISTS ix_acl_refresh_tokens_user
  ON acl.refresh_tokens(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_refresh_tokens_expires
  ON acl.refresh_tokens(expires_at);

CREATE TABLE IF NOT EXISTS acl.password_reset_tokens (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id     uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  token_hash  text NOT NULL UNIQUE,
  issued_at   timestamptz NOT NULL DEFAULT now(),
  expires_at  timestamptz NOT NULL,
  used_at     timestamptz NULL,
  user_agent  text NULL,
  ip_address  text NULL
);

CREATE INDEX IF NOT EXISTS ix_acl_password_reset_tokens_user
  ON acl.password_reset_tokens(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_password_reset_tokens_expires
  ON acl.password_reset_tokens(expires_at);
