using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    /// <inheritdoc />
    public partial class PmoFinancePostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Base ya puede estar alineada (sin Factor/Tasa en proyecto); evitar fallo si la columna no existe.
            migrationBuilder.Sql("""
                ALTER TABLE "TallerProyectos" DROP COLUMN IF EXISTS "Factor";
                ALTER TABLE "TallerProyectos" DROP COLUMN IF EXISTS "FechaTasaCambio";
                ALTER TABLE "TallerProyectos" DROP COLUMN IF EXISTS "TasaCambio";
                """);

            // IF NOT EXISTS: la secuencia puede existir ya (migración parcial, restore u otra rama).
            migrationBuilder.Sql("""
                CREATE SEQUENCE IF NOT EXISTS "PmoVectorFinancieroRowSequence";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ItemMediasJson",
                table: "TallerMcResultados",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "HistogramaJson",
                table: "TallerMcResultados",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "{}");

            migrationBuilder.AlterColumn<string>(
                name: "CdfJson",
                table: "TallerMcResultados",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "[]");

            migrationBuilder.AlterColumn<decimal>(
                name: "Peso",
                table: "TallerItems",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Clase",
                table: "TallerItems",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.Sql(
                """ALTER TABLE "TallerItems" ADD COLUMN IF NOT EXISTS "CostoClpOverride" numeric(18,2) NULL;""");

            migrationBuilder.AlterColumn<int>(
                name: "Orden",
                table: "TallerFamilias",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "Orden",
                table: "TallerEmpresas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.Sql("""
                ALTER TABLE "TallerContratos" ADD COLUMN IF NOT EXISTS "Factor" numeric(20,15) NOT NULL DEFAULT 0;
                ALTER TABLE "TallerContratos" ADD COLUMN IF NOT EXISTS "FechaTasaCambio" date NULL;
                ALTER TABLE "TallerContratos" ADD COLUMN IF NOT EXISTS "TasaCambio" numeric(18,4) NOT NULL DEFAULT 0;
                """);

            // Tablas PMO ya pueden existir (misma BD que antes / migración a medias). IF NOT EXISTS evita 42P07.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "api_acumulada" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_api_acumulada" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "api_parcial" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_api_parcial" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "npc_acumulado" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_npc_acumulado" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "npc_parcial" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_npc_parcial" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "real_acumulado" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_real_acumulado" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "real_parcial" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_real_parcial" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "v0_acumulada" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_v0_acumulada" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "v0_parcial" (
                    id integer NOT NULL DEFAULT nextval('"PmoVectorFinancieroRowSequence"'),
                    proyecto_id integer NOT NULL,
                    centro_costo character varying(500) NOT NULL,
                    periodo date NOT NULL,
                    tipo character varying(200) NOT NULL,
                    cat_vp character varying(200) NOT NULL,
                    detalle_factorial character varying(500) NOT NULL,
                    monto numeric(18,4) NOT NULL,
                    CONSTRAINT "PK_v0_parcial" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "financiero_sap" (
                    id integer GENERATED BY DEFAULT AS IDENTITY NOT NULL,
                    id_sap character varying(30) NOT NULL,
                    proyecto_id integer NOT NULL,
                    centro_costo_nombre character varying(100) NULL,
                    version_sap character varying(50) NULL,
                    descripcion character varying(500) NULL,
                    grupo_version character varying(50) NULL,
                    periodo date NULL,
                    "MO" numeric(15,2) NOT NULL,
                    "IC" numeric(15,2) NOT NULL,
                    "EM" numeric(15,2) NOT NULL,
                    "IE" numeric(15,2) NOT NULL,
                    "SC" numeric(15,2) NOT NULL,
                    "AD" numeric(15,2) NOT NULL,
                    "CL" numeric(15,2) NOT NULL,
                    "CT" numeric(15,2) NOT NULL,
                    fecha_creacion timestamp with time zone NOT NULL,
                    fecha_actualizacion timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_financiero_sap" PRIMARY KEY (id));
                CREATE TABLE IF NOT EXISTS "vc_project_9c" (
                    id_c9 character varying(20) NOT NULL,
                    proyecto_id integer NOT NULL,
                    periodo date NOT NULL,
                    cat_vp character varying(30) NOT NULL,
                    moneda_base integer NOT NULL,
                    base numeric(15,2) NOT NULL,
                    cambio numeric(15,2) NOT NULL,
                    control numeric(15,2) NOT NULL,
                    tendencia numeric(15,2) NOT NULL,
                    eat numeric(15,2) NOT NULL,
                    compromiso numeric(15,2) NOT NULL,
                    incurrido numeric(15,2) NOT NULL,
                    financiero numeric(15,2) NOT NULL,
                    por_comprometer numeric(15,2) NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_vc_project_9c" PRIMARY KEY (proyecto_id, id_c9));
                CREATE INDEX IF NOT EXISTS "IX_api_acumulada_proyecto_id" ON "api_acumulada" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_api_parcial_proyecto_id" ON "api_parcial" (proyecto_id);
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_financiero_sap_id_sap_proyecto_id" ON "financiero_sap" (id_sap, proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_financiero_sap_periodo" ON "financiero_sap" (periodo);
                CREATE INDEX IF NOT EXISTS "IX_financiero_sap_proyecto_id" ON "financiero_sap" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_npc_acumulado_proyecto_id" ON "npc_acumulado" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_npc_parcial_proyecto_id" ON "npc_parcial" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_real_acumulado_proyecto_id" ON "real_acumulado" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_real_parcial_proyecto_id" ON "real_parcial" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_v0_acumulada_proyecto_id" ON "v0_acumulada" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_v0_parcial_proyecto_id" ON "v0_parcial" (proyecto_id);
                CREATE INDEX IF NOT EXISTS "IX_vc_project_9c_proyecto_id_periodo" ON "vc_project_9c" (proyecto_id, periodo);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "vc_project_9c";
                DROP TABLE IF EXISTS "financiero_sap";
                DROP TABLE IF EXISTS "api_acumulada";
                DROP TABLE IF EXISTS "api_parcial";
                DROP TABLE IF EXISTS "npc_acumulado";
                DROP TABLE IF EXISTS "npc_parcial";
                DROP TABLE IF EXISTS "real_acumulado";
                DROP TABLE IF EXISTS "real_parcial";
                DROP TABLE IF EXISTS "v0_acumulada";
                DROP TABLE IF EXISTS "v0_parcial";
                """);

            migrationBuilder.DropColumn(
                name: "CostoClpOverride",
                table: "TallerItems");

            migrationBuilder.DropColumn(
                name: "Factor",
                table: "TallerContratos");

            migrationBuilder.DropColumn(
                name: "FechaTasaCambio",
                table: "TallerContratos");

            migrationBuilder.DropColumn(
                name: "TasaCambio",
                table: "TallerContratos");

            migrationBuilder.Sql("""
                DROP SEQUENCE IF EXISTS "PmoVectorFinancieroRowSequence";
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "Factor",
                table: "TallerProyectos",
                type: "numeric(18,10)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaTasaCambio",
                table: "TallerProyectos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TasaCambio",
                table: "TallerProyectos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "ItemMediasJson",
                table: "TallerMcResultados",
                type: "text",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "HistogramaJson",
                table: "TallerMcResultados",
                type: "text",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CdfJson",
                table: "TallerMcResultados",
                type: "text",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Peso",
                table: "TallerItems",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Clase",
                table: "TallerItems",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Orden",
                table: "TallerFamilias",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Orden",
                table: "TallerEmpresas",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
