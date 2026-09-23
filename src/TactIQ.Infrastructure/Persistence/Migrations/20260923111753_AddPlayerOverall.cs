using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerOverall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Overall",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Overall",
                table: "Players");
        }
    }
}
