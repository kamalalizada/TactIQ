using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeMoney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrizesPaid",
                table: "Competitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            // Leagues that finished before prize money existed are marked as
            // already paid. Without this the next simulated week would hand out
            // an end-of-season purse for a table that was settled long ago.
            migrationBuilder.Sql(
                """
                UPDATE "Competitions"
                SET "PrizesPaid" = true
                WHERE "Kind" = 0
                  AND "TeamCount" > 1
                  AND "CurrentWeek" > 2 * ("TeamCount" - 1);
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrizesPaid",
                table: "Competitions");
        }
    }
}
