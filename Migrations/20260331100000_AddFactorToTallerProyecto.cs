using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RazorIdentity.Migrations
{
    /// <inheritdoc />
    public partial class AddFactorToTallerProyecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Factor",
                table: "TallerProyectos");

            migrationBuilder.DropColumn(
                name: "FechaTasaCambio",
                table: "TallerProyectos");
        }
    }
}
