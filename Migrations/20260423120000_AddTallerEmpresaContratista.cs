using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    [Migration("20260423120000_AddTallerEmpresaContratista")]
    public partial class AddTallerEmpresaContratista : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TallerEmpresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProyectoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Contacto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallerEmpresas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TallerEmpresas_TallerProyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "TallerProyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TallerEmpresas_ProyectoId",
                table: "TallerEmpresas",
                column: "ProyectoId");

            migrationBuilder.CreateIndex(
                name: "IX_TallerEmpresas_ProyectoId_Rut",
                table: "TallerEmpresas",
                columns: new[] { "ProyectoId", "Rut" },
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "TallerContratos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TallerContratos_EmpresaId",
                table: "TallerContratos",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_TallerContratos_TallerEmpresas_EmpresaId",
                table: "TallerContratos",
                column: "EmpresaId",
                principalTable: "TallerEmpresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TallerContratos_TallerEmpresas_EmpresaId",
                table: "TallerContratos");

            migrationBuilder.DropIndex(
                name: "IX_TallerContratos_EmpresaId",
                table: "TallerContratos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "TallerContratos");

            migrationBuilder.DropTable(
                name: "TallerEmpresas");
        }
    }
}
