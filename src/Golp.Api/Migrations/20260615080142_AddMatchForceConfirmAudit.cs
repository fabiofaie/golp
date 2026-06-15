using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Golp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchForceConfirmAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForceConfirmedAt",
                table: "Matches",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ForceConfirmedById",
                table: "Matches",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForceConfirmedAt",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ForceConfirmedById",
                table: "Matches");
        }
    }
}
