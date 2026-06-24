using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Golp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSportsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PointUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sets = table.Column<bool>(type: "bit", nullable: false),
                    TeamSize = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SetWeight = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sports", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Sports",
                columns: new[] { "Id", "DisplayName", "IsActive", "Key", "PointUnit", "SetWeight", "Sets", "TeamSize" },
                values: new object[,]
                {
                    { 1, "Padel", true, "padel", "games", 0.40000000000000002, true, 2 },
                    { 2, "Beach Tennis", true, "beachtennis", "games", 0.40000000000000002, true, 2 },
                    { 3, "Basket 2v2", true, "basket2v2", "points", 0.0, false, 2 },
                    { 4, "Burraco", true, "burraco", "score", 0.0, false, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Key",
                table: "Sports",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sports");
        }
    }
}
