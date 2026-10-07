using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPitchSpot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LineupRole",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PitchX",
                table: "Players",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PitchY",
                table: "Players",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineupRole",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PitchX",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PitchY",
                table: "Players");
        }
    }
}
