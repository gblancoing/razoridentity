-- =========================================================
-- 03_seed_geo_cl.sql
-- Seed Chile (regiones) + comunas con georeferencia desde gist
-- y creación automática de tenants 1:1 por comuna
-- Ejecutar en psql conectado a acl_db
-- Fuente CSV: gist rafafdz/comunas.csv
-- =========================================================

BEGIN;

-- 1) País Chile
INSERT INTO core.countries (code, name)
VALUES ('CL', 'Chile')
ON CONFLICT (code) DO NOTHING;

-- 2) Regiones (según IDs del dataset: 1..15)
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

-- 3) Staging comunas del gist
DROP TABLE IF EXISTS core._stg_comunas_gist;
CREATE TABLE core._stg_comunas_gist (
  legacy_comuna_id int NOT NULL,
  legacy_region_id int NOT NULL,
  name             text NOT NULL,
  latitude         double precision NULL,
  longitude        double precision NULL
);

-- Carga desde RAW del gist (requiere psql + curl disponible)
\copy core._stg_comunas_gist(legacy_comuna_id, legacy_region_id, name, latitude, longitude) \
FROM PROGRAM 'curl -L -s "https://gist.githubusercontent.com/rafafdz/a67d3f6ac058c45cfad8176bf583b632/raw/c5c8369f998f430d1803d810c5500a82d49722c5/comunas.csv" | tail -n +2' \
WITH (FORMAT csv);

-- 4) Insert/upsert comunas
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

-- 5) Crear tenants 1:1 por comuna (si faltan)
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
