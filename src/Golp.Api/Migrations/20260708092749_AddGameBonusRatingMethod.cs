using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Golp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGameBonusRatingMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameBonusWinnerPoints",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameBonusWindowMatches",
                table: "Circles",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "GameBonusWindowWeeks",
                table: "Circles",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<string>(
                name: "RatingMethod",
                table: "Circles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Elo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameBonusWinnerPoints",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "GameBonusWindowMatches",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "GameBonusWindowWeeks",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "RatingMethod",
                table: "Circles");
        }
    }
}
