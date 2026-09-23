using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCupCompetition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayPenalties",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Competition",
                table: "Matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomePenalties",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Leg",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TieId",
                table: "Matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CupWinnerTeamId",
                table: "Leagues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentCupRound",
                table: "Leagues",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_LeagueId_Competition",
                table: "Matches",
                columns: new[] { "LeagueId", "Competition" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TieId",
                table: "Matches",
                column: "TieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_LeagueId_Competition",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_TieId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "AwayPenalties",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Competition",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "HomePenalties",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Leg",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "TieId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CupWinnerTeamId",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "CurrentCupRound",
                table: "Leagues");
        }
    }
}
