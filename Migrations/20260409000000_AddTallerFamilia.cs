using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    /// <inheritdoc />
    [Migration("20260409000000_AddTallerFamilia")]
    public partial class AddTallerFamilia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tabla TallerFamilias
            migrationBuilder.CreateTable(
                name: "TallerFamilias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProyectoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerFamilias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerFamilias_TallerProyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "TallerProyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerFamilias_ProyectoId",
                table: "TallerFamilias",
                column: "ProyectoId");

            // FamiliaId nullable en TallerContratos
            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "TallerContratos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TallerContratos_FamiliaId",
                table: "TallerContratos",
                column: "FamiliaId");

            migrationBuilder.AddForeignKey(
                name: "FK_TallerContratos_TallerFamilias_FamiliaId",
                table: "TallerContratos",
                column: "FamiliaId",
                principalTable: "TallerFamilias",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TallerContratos_TallerFamilias_FamiliaId",
                table: "TallerContratos");

            migrationBuilder.DropIndex(
                name: "IX_TallerContratos_FamiliaId",
                table: "TallerContratos");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "TallerContratos");

            migrationBuilder.DropTable(
                name: "TallerFamilias");
        }
    }
}
