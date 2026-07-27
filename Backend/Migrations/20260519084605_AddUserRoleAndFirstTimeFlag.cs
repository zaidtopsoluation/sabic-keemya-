using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keemya.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleAndFirstTimeFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFirstTimeLogin",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "Created", "IsFirstTimeLogin", "Role" },
                values: new object[] { new DateTime(2026, 5, 19, 8, 46, 4, 639, DateTimeKind.Utc).AddTicks(7117), false, "Admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFirstTimeLogin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Created",
                value: new DateTime(2026, 5, 19, 7, 57, 22, 788, DateTimeKind.Utc).AddTicks(3813));
        }
    }
}
