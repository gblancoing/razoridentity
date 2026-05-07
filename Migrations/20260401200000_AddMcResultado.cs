using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    /// <inheritdoc />
    public partial class AddMcResultado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TallerMcResultados",
                columns: table => new
                {
                    Id           = table.Column<Guid>(type: "uuid", nullable: false),
                    ContratoId   = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCalculo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    P10          = table.Column<double>(type: "double precision", nullable: false),
                    P50          = table.Column<double>(type: "double precision", nullable: false),
                    P80          = table.Column<double>(type: "double precision", nullable: false),
                    P90          = table.Column<double>(type: "double precision", nullable: false),
                    Media        = table.Column<double>(type: "double precision", nullable: false),
                    DesvStd      = table.Column<double>(type: "double precision", nullable: false),
                    Iteraciones  = table.Column<int>(type: "integer", nullable: false),
                    ItemMediasJson  = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    HistogramaJson  = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    CdfJson         = table.Column<string>(type: "text", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerMcResultados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerMcResultados_TallerContratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "TallerContratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerMcResultados_ContratoId",
                table: "TallerMcResultados",
                column: "ContratoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TallerMcResultados");
        }
    }
}
