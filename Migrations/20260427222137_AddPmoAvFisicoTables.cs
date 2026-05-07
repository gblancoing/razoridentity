using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    /// <inheritdoc />
    public partial class AddPmoAvFisicoTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "av_fisico_real", "av_fisico_npc", "av_fisico_poa", "av_fisico_v0", "av_fisico_api" })
            {
                migrationBuilder.Sql($"""
                    CREATE TABLE IF NOT EXISTS "{table}" (
                        "id" character varying(20) NOT NULL,
                        "proyecto_id" integer NOT NULL,
                        "periodo" date NOT NULL,
                        "vector" character varying(10) NOT NULL,
                        "ie_parcial" numeric(12,8) NOT NULL,
                        "ie_acumulado" numeric(12,8) NOT NULL,
                        "em_parcial" numeric(12,8) NOT NULL,
                        "em_acumulado" numeric(12,8) NOT NULL,
                        "mo_parcial" numeric(12,8) NOT NULL,
                        "mo_acumulado" numeric(12,8) NOT NULL,
                        "api_parcial" numeric(12,8) NOT NULL,
                        "api_acum" numeric(12,8) NOT NULL,
                        "created_at" timestamp with time zone NULL,
                        "updated_at" timestamp with time zone NULL,
                        CONSTRAINT "PK_{table}" PRIMARY KEY ("id")
                    );
                    CREATE INDEX IF NOT EXISTS "IX_{table}_proyecto_id_periodo" ON "{table}" ("proyecto_id", "periodo");
                    CREATE INDEX IF NOT EXISTS "IX_{table}_vector" ON "{table}" ("vector");
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "av_fisico_api";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "av_fisico_v0";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "av_fisico_poa";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "av_fisico_npc";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "av_fisico_real";""");
        }
    }
}
