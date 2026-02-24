-- =========================================================
-- comunaclick_db__03_seed_geo_cl_pgadmin.sql
-- PGADMIN-FRIENDLY
--
-- Ejecutar en Query Tool (conectado a comunaclick_db).
--
-- Este script NO usa \copy (psql). En su lugar:
--   1) Ejecuta este script completo (crea staging y deja lista la importación).
--   2) Importa el CSV a core._stg_comunas_gist desde pgAdmin:
--        core -> Tables -> _stg_comunas_gist -> Import/Export Data -> Import
--      CSV columnas: comuna_id,region_id,nombre,latitud,longitud (con header)
--   3) Ejecuta la sección "UPSERT + CREAR TENANTS" al final (puedes re-ejecutar todo el script).
--
-- Fuente CSV sugerida: gist rafafdz (comunas.csv)
-- =========================================================

BEGIN;

-- =========================
-- 1) País Chile
-- =========================
INSERT INTO core.countries (code, name)
VALUES ('CL', 'Chile')
ON CONFLICT (code) DO NOTHING;

-- =========================
-- 2) Regiones (según IDs del dataset: 1..15)
-- =========================
WITH c AS (
  SELECT id AS country_id FROM core.countries WHERE code='CL'
),
m AS (
  SELECT *
  FROM (VALUES
    (1,  'Arica y Parinacota',                                   'CL-R01'),
    (2,  'Tarapacá',                                             'CL-R02'),
    (3,  'Antofagasta',                                          'CL-R03'),
    (4,  'Atacama',                                              'CL-R04'),
    (5,  'Coquimbo',                                             'CL-R05'),
    (6,  'Valparaíso',                                           'CL-R06'),
    (7,  'Metropolitana de Santiago',                            'CL-R07'),
    (8,  'Libertador General Bernardo O''Higgins',               'CL-R08'),
    (9,  'Maule',                                                'CL-R09'),
    (10, 'Biobío',                                               'CL-R10'),
    (11, 'La Araucanía',                                         'CL-R11'),
    (12, 'Los Ríos',                                             'CL-R12'),
    (13, 'Los Lagos',                                            'CL-R13'),
    (14, 'Aysén del General Carlos Ibáñez del Campo',            'CL-R14'),
    (15, 'Magallanes y de la Antártica Chilena',                 'CL-R15')
  ) AS v(legacy_region_id, name, code)
)
INSERT INTO core.regions (country_id, code, name, legacy_region_id, is_active)
SELECT c.country_id, m.code, m.name, m.legacy_region_id, true
FROM c JOIN m ON true
ON CONFLICT (country_id, legacy_region_id)
DO UPDATE SET
  name = EXCLUDED.name,
  code = EXCLUDED.code,
  is_active = true;

-- =========================
-- 3) Staging para importación CSV en pgAdmin
-- =========================
DROP TABLE IF EXISTS core._stg_comunas_gist;
CREATE TABLE core._stg_comunas_gist (
  legacy_comuna_id int NOT NULL,       -- comuna_id del CSV
  legacy_region_id int NOT NULL,       -- region_id del CSV
  name             text NOT NULL,      -- nombre del CSV
  latitude         double precision NULL,
  longitude        double precision NULL
);

-- =========================================================
-- IMPORTANTE: Importar CSV aquí (pgAdmin)
-- =========================================================
-- En pgAdmin:
--   Schemas -> core -> Tables -> _stg_comunas_gist
--   Click derecho -> Import/Export Data -> Import
--     Filename: comunas.csv
--     Format: csv
--     Header: Yes
--     Columns (en este orden):
--       legacy_comuna_id, legacy_region_id, name, latitude, longitude
--
-- El CSV del gist viene como:
--   comuna_id,region_id,nombre,latitud,longitud
-- Por eso al importar, mapea:
--   comuna_id  -> legacy_comuna_id
--   region_id  -> legacy_region_id
--   nombre     -> name
--   latitud    -> latitude
--   longitud   -> longitude

-- =========================
-- 4) UPSERT comunas desde staging (ejecuta DESPUÉS de importar)
-- =========================
WITH r AS (
  SELECT id AS region_id, legacy_region_id
  FROM core.regions
  WHERE country_id = (SELECT id FROM core.countries WHERE code='CL')
)
INSERT INTO core.comunas (region_id, code, name, legacy_comuna_id, latitude, longitude, is_active)
SELECT
  r.region_id,
  ('CL-C' || lpad(s.legacy_comuna_id::text, 4, '0')) AS code,
  s.name,
  s.legacy_comuna_id,
  s.latitude,
  s.longitude,
  true
FROM core._stg_comunas_gist s
JOIN r ON r.legacy_region_id = s.legacy_region_id
ON CONFLICT (region_id, legacy_comuna_id)
DO UPDATE SET
  name = EXCLUDED.name,
  latitude = EXCLUDED.latitude,
  longitude = EXCLUDED.longitude,
  is_active = true;

-- =========================
-- 5) Crear tenants 1:1 por comuna (si faltan)
-- =========================
INSERT INTO core.tenants (comuna_id, name, is_active)
SELECT c.id, c.name, true
FROM core.comunas c
LEFT JOIN core.tenants t ON t.comuna_id = c.id
WHERE t.id IS NULL;

COMMIT;

-- Verificación rápida:
-- SELECT count(*) AS regiones FROM core.regions WHERE country_id=(SELECT id FROM core.countries WHERE code='CL');
-- SELECT count(*) AS comunas FROM core.comunas;
-- SELECT count(*) AS tenants_con_comuna FROM core.tenants WHERE comuna_id IS NOT NULL;
