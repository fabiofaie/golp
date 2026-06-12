using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Golp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCircleAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CircleAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CircleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WinnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: true),
                    TotalDelta = table.Column<int>(type: "int", nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircleAwards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CircleAwards_CircleId_PeriodType_PeriodYear_PeriodMonth",
                table: "CircleAwards",
                columns: new[] { "CircleId", "PeriodType", "PeriodYear", "PeriodMonth" },
                unique: true,
                filter: "[PeriodMonth] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CircleAwards");
        }
    }
}
