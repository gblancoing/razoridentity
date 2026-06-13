-- Roles granulares para Payments.App
-- Asignables desde la pagina Usuarios del ComunaClick.Admin
-- Idempotente: ON CONFLICT (name) DO NOTHING

INSERT INTO acl.roles (name, created_at)
VALUES
  ('payments.viewer',   now()),
  ('payments.operator', now()),
  ('payments.admin',    now())
ON CONFLICT (name) DO NOTHING;
