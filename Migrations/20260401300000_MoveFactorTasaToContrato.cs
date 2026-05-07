using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    /// <inheritdoc />
    public partial class MoveFactorTasaToContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Agregar columnas a TallerContratos
            migrationBuilder.AddColumn<decimal>(
                name: "TasaCambio",
                table: "TallerContratos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 900m);

            migrationBuilder.AddColumn<decimal>(
                name: "Factor",
                table: "TallerContratos",
                type: "numeric(18,10)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaTasaCambio",
                table: "TallerContratos",
                type: "date",
                nullable: true);

            // Copiar valores del proyecto padre a cada contrato
            migrationBuilder.Sql(@"
                UPDATE ""TallerContratos"" c
                SET ""TasaCambio""     = p.""TasaCambio"",
                    ""Factor""         = p.""Factor"",
                    ""FechaTasaCambio"" = p.""FechaTasaCambio""
                FROM ""TallerProyectos"" p
                WHERE c.""ProyectoId"" = p.""Id""
            ");

            // Eliminar columnas de TallerProyectos
            migrationBuilder.DropColumn(name: "TasaCambio",      table: "TallerProyectos");
            migrationBuilder.DropColumn(name: "Factor",           table: "TallerProyectos");
            migrationBuilder.DropColumn(name: "FechaTasaCambio", table: "TallerProyectos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TasaCambio",
                table: "TallerProyectos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 900m);

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

            migrationBuilder.DropColumn(name: "TasaCambio",      table: "TallerContratos");
            migrationBuilder.DropColumn(name: "Factor",           table: "TallerContratos");
            migrationBuilder.DropColumn(name: "FechaTasaCambio", table: "TallerContratos");
        }
    }
}
