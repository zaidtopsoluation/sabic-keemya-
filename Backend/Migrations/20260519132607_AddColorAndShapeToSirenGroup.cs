using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keemya.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddColorAndShapeToSirenGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "SirenGroups",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Shape",
                table: "SirenGroups",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Created",
                value: new DateTime(2026, 5, 19, 13, 26, 6, 638, DateTimeKind.Utc).AddTicks(5106));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "SirenGroups");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "SirenGroups");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Created",
                value: new DateTime(2026, 5, 19, 8, 46, 4, 639, DateTimeKind.Utc).AddTicks(7117));
        }
    }
}
