using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Keemya.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "CommandConfigs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "CommandHex",
                table: "CommandConfigs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedResponseBytes",
                table: "CommandConfigs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "CommandConfigs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "CommandConfigs",
                columns: new[] { "Id", "AudioId", "Color", "CommandHex", "CommandType", "Description", "Duration", "ExpectedResponseBytes", "IsEnabled", "Name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), null, "Blue", 0, "Clear", "Clears any event in progress.", 0, 0, true, "Clear" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), null, "Red", 1, "Wail", "Wail tone warning.", 0, 4, true, "Wail" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), null, "Red", 2, "Attack", "Attack tone warning.", 0, 4, true, "Attack" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), null, "Orange", 3, "Alert", "Alert tone warning.", 0, 4, true, "Alert" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), null, "Purple", 4, "PublicAddress", "Live public address — tone generator bypassed.", 0, 0, true, "Public Address" },
                    { new Guid("00000000-0000-0000-0001-000000000006"), null, "Orange", 5, "AirHorn", "Air horn tone warning.", 0, 4, true, "Air Horn" },
                    { new Guid("00000000-0000-0000-0001-000000000007"), null, "Yellow", 6, "HiLo", "Hi-Lo tone warning.", 0, 4, true, "Hi-Lo" },
                    { new Guid("00000000-0000-0000-0001-000000000008"), null, "Yellow", 7, "Whoop", "Whoop tone warning.", 0, 4, true, "Whoop" },
                    { new Guid("00000000-0000-0000-0001-000000000009"), null, "Green", 8, "NoonTest", "Short wail-2 tone (noon test).", 0, 4, true, "Noon Test" },
                    { new Guid("00000000-0000-0000-0001-000000000010"), null, "Cyan", 15, "SilentTest", "Initiates diagnostic silent test, produces a status response.", 0, 4, true, "Silent Test" },
                    { new Guid("00000000-0000-0000-0002-000000000001"), null, "Blue", 31, "StatusRequest", "Retrieves the full status byte from the siren.", 0, 4, true, "Status Request" },
                    { new Guid("00000000-0000-0000-0002-000000000002"), null, "Green", 24, "ArmSystem", "Arms the Instant Status response.", 0, 4, true, "Arm System" },
                    { new Guid("00000000-0000-0000-0002-000000000003"), null, "Red", 25, "DisarmSystem", "Disables the Instant Status response.", 0, 4, true, "Dis-arm System" },
                    { new Guid("00000000-0000-0000-0002-000000000004"), null, "Green", 26, "SirenOn", "Enables the tone generator and digital voice.", 0, 4, true, "Siren On" },
                    { new Guid("00000000-0000-0000-0002-000000000005"), null, "Red", 27, "SirenOff", "Disables the tone generator; digital voice stays active.", 0, 4, true, "Siren Off" },
                    { new Guid("00000000-0000-0000-0002-000000000006"), null, "Cyan", 35, "InstantStatus", "Get real-time instant status of the remote siren station.", 0, 4, true, "Instant Status" },
                    { new Guid("00000000-0000-0000-0002-000000000007"), null, "Blue", 22, "Counter", "Tone activation software counter request.", 0, 2, true, "Counter" },
                    { new Guid("00000000-0000-0000-0002-000000000008"), null, "Blue", 23, "ClearCounter", "Clears the software tone activation counter to zero.", 0, 2, true, "Clear Counter" },
                    { new Guid("00000000-0000-0000-0002-000000000009"), null, "Blue", 30, "TestClear", "Clears LEDs.", 0, 0, true, "Test Clear" },
                    { new Guid("00000000-0000-0000-0002-000000000010"), null, "Green", 33, "BatteryAC", "Requests battery DC voltage and AC voltage measurements.", 0, 4, true, "Battery / AC" },
                    { new Guid("00000000-0000-0000-0002-000000000011"), null, "Green", 34, "BatteryTemp", "Requests battery DC voltage and cabinet temperature.", 0, 4, true, "Battery / Temp" },
                    { new Guid("00000000-0000-0000-0002-000000000012"), null, "Orange", 36, "TransmitOff", "Disables the transmit repeat feature during Instant Status.", 0, 0, true, "Transmit Off" },
                    { new Guid("00000000-0000-0000-0003-000000000001"), null, "Purple", 17, "Message13", "Initiates digital voice message 13 (RSDVM module).", 0, 0, true, "Message 13" },
                    { new Guid("00000000-0000-0000-0003-000000000002"), null, "Purple", 18, "Message14", "Initiates digital voice message 14 (RSDVM module).", 0, 0, true, "Message 14" },
                    { new Guid("00000000-0000-0000-0003-000000000003"), null, "Purple", 19, "Message15", "Initiates digital voice message 15 (RSDVM module).", 0, 0, true, "Message 15" },
                    { new Guid("00000000-0000-0000-0003-000000000004"), null, "Purple", 20, "Message16", "Initiates digital voice message 16 (RSDVM module).", 0, 0, true, "Message 16" },
                    { new Guid("00000000-0000-0000-0004-000000000001"), null, "Purple", 49, "Message1", "Initiates digital voice message 1 (RSDVM module).", 0, 0, true, "Message 1" },
                    { new Guid("00000000-0000-0000-0004-000000000002"), null, "Purple", 50, "Message2", "Initiates digital voice message 2 (RSDVM module).", 0, 0, true, "Message 2" },
                    { new Guid("00000000-0000-0000-0004-000000000003"), null, "Purple", 51, "Message3", "Initiates digital voice message 3 (RSDVM module).", 0, 0, true, "Message 3" },
                    { new Guid("00000000-0000-0000-0004-000000000004"), null, "Purple", 52, "Message4", "Initiates digital voice message 4 (RSDVM module).", 0, 0, true, "Message 4" },
                    { new Guid("00000000-0000-0000-0004-000000000005"), null, "Purple", 53, "Message5", "Initiates digital voice message 5 (RSDVM module).", 0, 0, true, "Message 5" },
                    { new Guid("00000000-0000-0000-0004-000000000006"), null, "Purple", 54, "Message6", "Initiates digital voice message 6 (RSDVM module).", 0, 0, true, "Message 6" },
                    { new Guid("00000000-0000-0000-0004-000000000007"), null, "Purple", 55, "Message7", "Initiates digital voice message 7 (RSDVM module).", 0, 0, true, "Message 7" },
                    { new Guid("00000000-0000-0000-0004-000000000008"), null, "Purple", 56, "Message8", "Initiates digital voice message 8 (RSDVM module).", 0, 0, true, "Message 8" },
                    { new Guid("00000000-0000-0000-0004-000000000009"), null, "Purple", 59, "Message9", "Initiates digital voice message 9 (RSDVM module).", 0, 0, true, "Message 9" },
                    { new Guid("00000000-0000-0000-0004-000000000010"), null, "Purple", 60, "Message10", "Initiates digital voice message 10 (RSDVM module).", 0, 0, true, "Message 10" },
                    { new Guid("00000000-0000-0000-0004-000000000011"), null, "Purple", 61, "Message11", "Initiates digital voice message 11 (RSDVM module).", 0, 0, true, "Message 11" },
                    { new Guid("00000000-0000-0000-0004-000000000012"), null, "Purple", 62, "Message12", "Initiates digital voice message 12 (RSDVM module).", 0, 0, true, "Message 12" },
                    { new Guid("00000000-0000-0000-0005-000000000001"), null, "Yellow", 57, "StrobeOn", "Activates the strobe light.", 0, 0, true, "Strobe On" },
                    { new Guid("00000000-0000-0000-0005-000000000002"), null, "Yellow", 58, "StrobeOff", "De-activates the strobe light.", 0, 0, true, "Strobe Off" }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Created",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000001"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000002"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000003"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000005"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000006"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000007"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000008"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000009"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000010"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000006"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000007"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000008"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000009"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000010"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000011"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000012"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000002"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000003"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000004"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000001"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000002"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000003"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000004"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000005"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000006"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000007"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000008"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000009"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000010"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000011"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000012"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000001"));

            migrationBuilder.DeleteData(
                table: "CommandConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"));

            migrationBuilder.DropColumn(
                name: "Color",
                table: "CommandConfigs");

            migrationBuilder.DropColumn(
                name: "CommandHex",
                table: "CommandConfigs");

            migrationBuilder.DropColumn(
                name: "ExpectedResponseBytes",
                table: "CommandConfigs");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "CommandConfigs");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Created",
                value: new DateTime(2026, 5, 20, 5, 37, 59, 987, DateTimeKind.Utc).AddTicks(2370));
        }
    }
}
