using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerMarketValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MarketValue",
                table: "Players",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketValue",
                table: "Players");
        }
    }
}
