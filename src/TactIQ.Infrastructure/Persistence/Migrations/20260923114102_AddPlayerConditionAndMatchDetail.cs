using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerConditionAndMatchDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MatchesInjured",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MatchesSuspended",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "YellowCards",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AssistPlayerId",
                table: "MatchEvents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MatchesInjured",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MatchesSuspended",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "YellowCards",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AssistPlayerId",
                table: "MatchEvents");
        }
    }
}
