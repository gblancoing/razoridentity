using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    /// <inheritdoc />
    public partial class AddTallerCostos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TallerProyectos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaEjercicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Organizacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TasaCambio = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerProyectos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerProyectos_UserId",
                table: "TallerProyectos",
                column: "UserId");

            migrationBuilder.CreateTable(
                name: "TallerContratos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProyectoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    NombrePaquete = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CapexUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CompometidoUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PorComprometidoUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimadoTerminoUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerContratos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerContratos_TallerProyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "TallerProyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerContratos_ProyectoId",
                table: "TallerContratos",
                column: "ProyectoId");

            migrationBuilder.CreateTable(
                name: "TallerItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContratoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    CodigoItem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Unidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubPartida = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EsCerteza = table.Column<bool>(type: "boolean", nullable: false),
                    CostoUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClaseEstimacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Consideraciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MinPct = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaxPct = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MinKusd = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxKusd = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ViaRiesgo = table.Column<bool>(type: "boolean", nullable: false),
                    Oportunidades = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amenazas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Clase = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Peso = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerItems_TallerContratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "TallerContratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerItems_ContratoId",
                table: "TallerItems",
                column: "ContratoId");

            migrationBuilder.CreateTable(
                name: "TallerRevisionesRiesgos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProyectoId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroRevision = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerRevisionesRiesgos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerRevisionesRiesgos_TallerProyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "TallerProyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerRevisionesRiesgos_ProyectoId",
                table: "TallerRevisionesRiesgos",
                column: "ProyectoId");

            migrationBuilder.CreateTable(
                name: "TallerRiesgos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CodigoRiesgo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ImpactoProableUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ImpactoMinUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ImpactoMaxUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EsOportunidad = table.Column<bool>(type: "boolean", nullable: false),
                    NotasCambio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ItemViaRiesgoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerRiesgos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerRiesgos_TallerRevisionesRiesgos_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "TallerRevisionesRiesgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerRiesgos_RevisionId",
                table: "TallerRiesgos",
                column: "RevisionId");

            migrationBuilder.CreateTable(
                name: "TallerAnalisis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProyectoId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionRiesgosId = table.Column<Guid>(type: "uuid", nullable: true),
                    NombreProyecto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CodigoProyecto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaEjercicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Iteraciones = table.Column<int>(type: "integer", nullable: false),
                    SemillaAleatoria = table.Column<int>(type: "integer", nullable: true),
                    ResultadosJson = table.Column<string>(type: "text", nullable: false),
                    FechaEjecucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerAnalisis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerAnalisis_TallerProyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "TallerProyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerAnalisis_ProyectoId",
                table: "TallerAnalisis",
                column: "ProyectoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TallerAnalisis");
            migrationBuilder.DropTable(name: "TallerRiesgos");
            migrationBuilder.DropTable(name: "TallerRevisionesRiesgos");
            migrationBuilder.DropTable(name: "TallerItems");
            migrationBuilder.DropTable(name: "TallerContratos");
            migrationBuilder.DropTable(name: "TallerProyectos");
        }
    }
}
