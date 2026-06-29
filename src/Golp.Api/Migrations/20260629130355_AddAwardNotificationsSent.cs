using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Golp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardNotificationsSent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AwardNotificationsSent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CircleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PeriodLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EmailSent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwardNotificationsSent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwardNotificationsSent_CircleId_PeriodType_PeriodLabel",
                table: "AwardNotificationsSent",
                columns: new[] { "CircleId", "PeriodType", "PeriodLabel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AwardNotificationsSent");
        }
    }
}
