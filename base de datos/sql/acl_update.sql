-- =========================================================
-- acl_update.sql
-- Actualiza schema acl para soportar:
-- - Super Admin global
-- - Accesos por tenant (comuna) / región / país + platform
-- - Vista para selector de comuna con georeferencia
-- Ejecutar en la DB: acl_db
-- =========================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE SCHEMA IF NOT EXISTS acl;

-- =========================
-- 1) Super Admin
-- =========================
ALTER TABLE acl.users
  ADD COLUMN IF NOT EXISTS is_super_admin boolean NOT NULL DEFAULT false;

CREATE INDEX IF NOT EXISTS ix_acl_users_is_super_admin
  ON acl.users(is_super_admin);

-- =========================
-- 2) Tablas de acceso (FK estrictas a core)
-- =========================

-- Acceso directo a tenants (comunas)
CREATE TABLE IF NOT EXISTS acl.user_tenant_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  tenant_id  uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_acl_user_tenant_access_user
  ON acl.user_tenant_access(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_user_tenant_access_tenant
  ON acl.user_tenant_access(tenant_id);

-- Acceso por región (expande a comunas/tenants)
CREATE TABLE IF NOT EXISTS acl.user_region_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  region_id  uuid NOT NULL REFERENCES core.regions(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, region_id)
);

CREATE INDEX IF NOT EXISTS ix_acl_user_region_access_user
  ON acl.user_region_access(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_user_region_access_region
  ON acl.user_region_access(region_id);

-- Acceso por país (expande a comunas/tenants)
CREATE TABLE IF NOT EXISTS acl.user_country_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  country_id uuid NOT NULL REFERENCES core.countries(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, country_id)
);

CREATE INDEX IF NOT EXISTS ix_acl_user_country_access_user
  ON acl.user_country_access(user_id);

CREATE INDEX IF NOT EXISTS ix_acl_user_country_access_country
  ON acl.user_country_access(country_id);

-- Acceso platform (backoffice global sin tenant)
CREATE TABLE IF NOT EXISTS acl.user_platform_access (
  user_id    uuid NOT NULL REFERENCES acl.users(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id)
);

-- =========================
-- 3) Migración desde acl.user_tenant_scope (legacy)
--    - tenant -> user_tenant_access
--    - platform -> user_platform_access
-- =========================

-- Si existe tabla legacy, migramos (idempotente)
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema='acl' AND table_name='user_tenant_scope'
  ) THEN
    -- tenant
    INSERT INTO acl.user_tenant_access (user_id, tenant_id)
    SELECT uts.user_id, uts.tenant_id
    FROM acl.user_tenant_scope uts
    WHERE lower(uts.scope_type) = 'tenant'
    ON CONFLICT (user_id, tenant_id) DO NOTHING;

    -- platform
    INSERT INTO acl.user_platform_access (user_id)
    SELECT DISTINCT uts.user_id
    FROM acl.user_tenant_scope uts
    WHERE lower(uts.scope_type) = 'platform'
    ON CONFLICT (user_id) DO NOTHING;
  END IF;
END$$;

-- =========================
-- 4) Vista para selector de comuna (con geo)
-- =========================
CREATE OR REPLACE VIEW acl.v_user_accessible_tenants AS
WITH direct AS (
  SELECT uta.user_id, uta.tenant_id
  FROM acl.user_tenant_access uta
),
by_region AS (
  SELECT ura.user_id, t.id AS tenant_id
  FROM acl.user_region_access ura
  JOIN core.comunas c ON c.region_id = ura.region_id
  JOIN core.tenants t ON t.comuna_id = c.id
),
by_country AS (
  SELECT uca.user_id, t.id AS tenant_id
  FROM acl.user_country_access uca
  JOIN core.regions r ON r.country_id = uca.country_id
  JOIN core.comunas c ON c.region_id = r.id
  JOIN core.tenants t ON t.comuna_id = c.id
),
scoped AS (
  SELECT * FROM direct
  UNION
  SELECT * FROM by_region
  UNION
  SELECT * FROM by_country
)
SELECT DISTINCT
  u.id          AS user_id,

  t.id          AS tenant_id,
  t.name        AS tenant_name,

  c.id          AS comuna_id,
  c.code        AS comuna_code,
  c.name        AS comuna_name,
  c.legacy_comuna_id,
  c.latitude,
  c.longitude,
  (c.latitude IS NOT NULL AND c.longitude IS NOT NULL) AS has_geo,
  CASE
    WHEN c.latitude IS NOT NULL AND c.longitude IS NOT NULL
      THEN ('POINT(' || c.longitude::text || ' ' || c.latitude::text || ')')
    ELSE NULL
  END AS geo_wkt,

  r.id          AS region_id,
  r.code        AS region_code,
  r.name        AS region_name,
  r.legacy_region_id,

  co.id         AS country_id,
  co.code       AS country_code,
  co.name       AS country_name

FROM acl.users u
JOIN core.tenants t ON t.is_active = true
LEFT JOIN core.comunas  c  ON c.id = t.comuna_id
LEFT JOIN core.regions  r  ON r.id = c.region_id
LEFT JOIN core.countries co ON co.id = r.country_id
LEFT JOIN scoped s ON s.user_id = u.id AND s.tenant_id = t.id
WHERE u.is_active = true
  AND (
    u.is_super_admin = true
    OR s.tenant_id IS NOT NULL
  );

-- Helper para validación rápida (opcional)
CREATE OR REPLACE VIEW acl.v_user_can_access_tenant AS
SELECT user_id, tenant_id
FROM acl.v_user_accessible_tenants;
