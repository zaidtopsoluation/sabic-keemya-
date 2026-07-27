using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keemya.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaAndAddressCodeToSirenDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressCode",
                table: "SirenDevices",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AreaCode",
                table: "SirenDevices",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Created",
                value: new DateTime(2026, 5, 20, 5, 37, 59, 987, DateTimeKind.Utc).AddTicks(2370));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressCode",
                table: "SirenDevices");

            migrationBuilder.DropColumn(
                name: "AreaCode",
                table: "SirenDevices");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Created",
                value: new DateTime(2026, 5, 19, 13, 26, 6, 638, DateTimeKind.Utc).AddTicks(5106));
        }
    }
}
