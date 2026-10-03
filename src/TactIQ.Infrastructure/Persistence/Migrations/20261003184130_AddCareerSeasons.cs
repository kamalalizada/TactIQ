using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Seasons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SeasonRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    CompetitionName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ChampionTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChampionTeamName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UserRank = table.Column<int>(type: "integer", nullable: true),
                    UserPoints = table.Column<int>(type: "integer", nullable: true),
                    UserBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonRecords_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRecords_SeasonId_Year",
                table: "SeasonRecords",
                columns: new[] { "SeasonId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeasonRecords");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Seasons");
        }
    }
}
