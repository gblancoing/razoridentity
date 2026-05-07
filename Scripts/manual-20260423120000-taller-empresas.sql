-- Ejecutar en la MISMA base PostgreSQL que usa la app (la de tu ConnectionStrings:DefaultConnection).
-- Si la migración ya está en __EFMigrationsHistory, NO ejecutes esto (usar solo: dotnet ef database update).
-- Tras ejecutar, recargar la web.

BEGIN;

CREATE TABLE IF NOT EXISTS "TallerEmpresas" (
    "Id" uuid NOT NULL,
    "ProyectoId" uuid NOT NULL,
    "Rut" character varying(20) NOT NULL,
    "Nombre" character varying(300) NOT NULL,
    "Contacto" character varying(200) NULL,
    "Email" character varying(200) NULL,
    "Telefono" character varying(50) NULL,
    "Notas" character varying(500) NULL,
    "Orden" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TallerEmpresas" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TallerEmpresas_TallerProyectos_ProyectoId" FOREIGN KEY ("ProyectoId") REFERENCES "TallerProyectos" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_TallerEmpresas_ProyectoId" ON "TallerEmpresas" ("ProyectoId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TallerEmpresas_ProyectoId_Rut" ON "TallerEmpresas" ("ProyectoId", "Rut");

ALTER TABLE "TallerContratos" ADD COLUMN IF NOT EXISTS "EmpresaId" uuid NULL;
CREATE INDEX IF NOT EXISTS "IX_TallerContratos_EmpresaId" ON "TallerContratos" ("EmpresaId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_TallerContratos_TallerEmpresas_EmpresaId'
    ) THEN
        ALTER TABLE "TallerContratos"
        ADD CONSTRAINT "FK_TallerContratos_TallerEmpresas_EmpresaId"
        FOREIGN KEY ("EmpresaId") REFERENCES "TallerEmpresas" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260423120000_AddTallerEmpresaContratista', '8.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423120000_AddTallerEmpresaContratista'
);

COMMIT;
