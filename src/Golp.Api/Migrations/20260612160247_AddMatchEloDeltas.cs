using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Golp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchEloDeltas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeltaTeam1Player1",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeltaTeam1Player2",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeltaTeam2Player1",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeltaTeam2Player2",
                table: "Matches",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeltaTeam1Player1",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "DeltaTeam1Player2",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "DeltaTeam2Player1",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "DeltaTeam2Player2",
                table: "Matches");
        }
    }
}
