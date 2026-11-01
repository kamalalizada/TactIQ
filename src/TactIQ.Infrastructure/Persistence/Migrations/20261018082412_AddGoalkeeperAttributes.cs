using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalkeeperAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Diving",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Handling",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kicking",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Positioning",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Reflexes",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Speed",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // Keepers in saves made before this have no keeping attributes, so
            // they would read as zero and let in everything. Their existing
            // outfield numbers are the best guide there is: a keeper's
            // "defending" was what the engine used to rate him by, and his
            // pace and physique stood in for the rest.
            migrationBuilder.Sql(
                """
                UPDATE "Players"
                SET "Reflexes"    = LEAST(99, GREATEST(30, "Defending")),
                    "Positioning" = LEAST(99, GREATEST(30, "Defending" - 2)),
                    "Handling"    = LEAST(99, GREATEST(30, "Physical" - 4)),
                    "Diving"      = LEAST(99, GREATEST(30, "Defending" - 3)),
                    "Kicking"     = LEAST(99, GREATEST(30, "Passing")),
                    "Speed"       = LEAST(99, GREATEST(20, "Pace" - 40))
                WHERE "Position" = 0 AND "Reflexes" = 0;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Diving",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Handling",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Kicking",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Positioning",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Reflexes",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Speed",
                table: "Players");
        }
    }
}
