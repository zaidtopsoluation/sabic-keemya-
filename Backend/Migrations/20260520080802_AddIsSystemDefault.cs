using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keemya.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSystemDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefault",
                table: "CommandConfigs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000001"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000002"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000003"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000005"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000006"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000007"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000008"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000009"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000010"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000006"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000007"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000008"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000009"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000010"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000011"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000012"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000002"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000003"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000004"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000001"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000002"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000003"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000004"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000005"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000006"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000007"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000008"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000009"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000010"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000011"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000012"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000001"),
                column: "IsSystemDefault",
                value: true);

            migrationBuilder.UpdateData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"),
                column: "IsSystemDefault",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystemDefault",
                table: "CommandConfigs");
        }
    }
}
