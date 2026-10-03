using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TactIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Budget",
                table: "Teams",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CurrentWeek",
                table: "Seasons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TransferOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WeekMade = table.Column<int>(type: "integer", nullable: false),
                    ResponseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FromTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTeamName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ToTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToTeamName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    Window = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transfers_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_FromTeamId",
                table: "TransferOffers",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_PlayerId",
                table: "TransferOffers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_SeasonId_Status",
                table: "TransferOffers",
                columns: new[] { "SeasonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_ToTeamId",
                table: "TransferOffers",
                column: "ToTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_SeasonId",
                table: "Transfers",
                column: "SeasonId");
            // Clubs that existed before the market did start with nothing to
            // spend, which would leave an old save unable to do any business.
            // Each is given a quarter of what its squad is worth, the same
            // share a new save starts with, worked out here in SQL because the
            // valuation curve cannot be reached from a migration.
            migrationBuilder.Sql(
                """
                WITH worth AS (
                    SELECT p."TeamId",
                           SUM(
                               GREATEST(
                                   150000,
                                   LEAST(
                                       200000000,
                                       ROUND(
                                           150000
                                           * POWER(1.19, GREATEST(0, p."Overall" - 55))
                                           * CASE
                                               WHEN p."Age" <= 21 THEN 1.25
                                               WHEN p."Age" <= 23 THEN 1.12
                                               WHEN p."Age" <= 27 THEN 1.00
                                               WHEN p."Age" <= 29 THEN 0.86
                                               WHEN p."Age" <= 31 THEN 0.60
                                               WHEN p."Age" <= 33 THEN 0.35
                                               ELSE 0.15
                                             END
                                           * CASE p."Position"
                                               WHEN 0 THEN 0.75
                                               WHEN 1 THEN 0.92
                                               WHEN 2 THEN 1.00
                                               ELSE 1.15
                                             END
                                           / 100000
                                       ) * 100000
                                   )
                               )
                           ) AS total
                    FROM "Players" p
                    GROUP BY p."TeamId"
                )
                UPDATE "Teams" t
                SET "Budget" = GREATEST(2000000, ROUND(w.total * 0.25 / 500000) * 500000)
                FROM worth w
                WHERE t."Id" = w."TeamId" AND t."Budget" = 0;
                """);

            // An existing save is mid-season, so its week is taken from the
            // league it is following rather than left at zero, where the
            // summer window would be open again.
            migrationBuilder.Sql(
                """
                UPDATE "Seasons" s
                SET "CurrentWeek" = COALESCE((
                    SELECT MAX(c."CurrentWeek")
                    FROM "Competitions" c
                    WHERE c."SeasonId" = s."Id" AND c."Kind" = 0
                ), 0);
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferOffers");

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropColumn(
                name: "Budget",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CurrentWeek",
                table: "Seasons");
        }
    }
}
