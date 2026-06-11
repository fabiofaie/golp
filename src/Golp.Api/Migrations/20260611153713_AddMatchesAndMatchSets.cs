using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Golp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchesAndMatchSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CircleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WinnerTeam = table.Column<int>(type: "int", nullable: false),
                    Team1Player1Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Team1Player2Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Team2Player1Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Team2Player2Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Users_Team1Player1Id",
                        column: x => x.Team1Player1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Users_Team1Player2Id",
                        column: x => x.Team1Player2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Users_Team2Player1Id",
                        column: x => x.Team2Player1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Users_Team2Player2Id",
                        column: x => x.Team2Player2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetNumber = table.Column<int>(type: "int", nullable: false),
                    Team1Score = table.Column<int>(type: "int", nullable: false),
                    Team2Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchSets_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CircleId_Status",
                table: "Matches",
                columns: new[] { "CircleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CreatedById",
                table: "Matches",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Team1Player1Id",
                table: "Matches",
                column: "Team1Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Team1Player2Id",
                table: "Matches",
                column: "Team1Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Team2Player1Id",
                table: "Matches",
                column: "Team2Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Team2Player2Id",
                table: "Matches",
                column: "Team2Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSets_MatchId",
                table: "MatchSets",
                column: "MatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchSets");

            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
