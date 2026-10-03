using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Players seeded before roles existed would all read as goalkeepers,
            // which would wreck a season already under way. Give each of them the
            // middle role of the line they play in: a centre-back, a central
            // midfielder or a centre-forward. New seasons carry real roles from
            // the seed, and a manager can move anyone from the squad screen.
            migrationBuilder.Sql(
                """
                UPDATE "Players"
                SET "Role" = CASE "Position"
                    WHEN 0 THEN 0   -- GK  -> GK
                    WHEN 1 THEN 2   -- DEF -> CB
                    WHEN 2 THEN 5   -- MID -> CM
                    ELSE 10         -- FWD -> ST
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Players");
        }
    }
}
