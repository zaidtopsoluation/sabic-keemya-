using Keemya.Frontend.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Keemya.Frontend.Services
{
    public record ToneOption(int Hex, string Name)
    {
        public string Display => $"{Name}  (0x{Hex:X2})";
    }

    /// <summary>
    /// Activation buttons shown in Command Center and Map. Rows live in CommandConfigs with ShowOnPanel = 1,
    /// so admins add/edit buttons from the Button Configuration module without code changes.
    /// </summary>
    public static class PanelButtonService
    {
        // Whelen RS-232 doc 04-01111092-05C, "Siren Command Definitions" p.7-8: commands that make sense on an activation button.
        public static readonly IReadOnlyList<ToneOption> Tones = new List<ToneOption>
        {
            new(0x00, "Clear / Reset"),
            new(0x01, "Wail"),
            new(0x02, "Attack"),
            new(0x03, "Alert"),
            new(0x04, "Public Address"),
            new(0x05, "Air Horn"),
            new(0x06, "Hi-Lo"),
            new(0x07, "Whoop"),
            new(0x08, "Wail-2 (Noon Test)"),
        }
        .Concat(Enumerable.Range(1, 8).Select(n => new ToneOption(0x30 + n, $"Voice Message {n}")))
        .Concat(Enumerable.Range(9, 4).Select(n => new ToneOption(0x32 + n, $"Voice Message {n}")))
        .Concat(Enumerable.Range(13, 4).Select(n => new ToneOption(0x04 + n, $"Voice Message {n}")))
        .ToList();

        public static readonly IReadOnlyList<string> PresetColors = new[]
        {
            "#A72A2A", "#EF4444", "#E68A00", "#F97316", "#E6D900", "#10B981",
            "#0F766E", "#06B6D4", "#2A75B3", "#3B82F6", "#5C2AB3", "#8B5CF6", "#EC4899", "#475569"
        };

        public static bool IsValidColor(string? color) =>
            color != null && Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$");

        public static string ToHex(System.Windows.Media.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>Black text on light backgrounds (e.g. the yellow Reset button), white otherwise.</summary>
        public static string TextColorFor(string colorHex)
        {
            if (!IsValidColor(colorHex)) return "#FFFFFF";
            int r = Convert.ToInt32(colorHex.Substring(1, 2), 16);
            int g = Convert.ToInt32(colorHex.Substring(3, 2), 16);
            int b = Convert.ToInt32(colorHex.Substring(5, 2), 16);
            return (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.6 ? "#000000" : "#FFFFFF";
        }

        /// <summary>Returns an error message, or null when the button is valid.</summary>
        public static string? Validate(string? label, int? toneHex, string? color, string? durationText, out int duration)
        {
            duration = 0;
            if (string.IsNullOrWhiteSpace(label)) return "Button label is required.";
            if (label.Trim().Length > 40) return "Button label must be 40 characters or less.";
            if (toneHex == null || Tones.All(t => t.Hex != toneHex)) return "Select a siren tone / command.";
            if (!IsValidColor(color)) return "Color must be a hex value like #A72A2A.";
            if (!string.IsNullOrWhiteSpace(durationText) &&
                (!int.TryParse(durationText.Trim(), out duration) || duration < 0 || duration > 3600))
                return "Run time must be a number of seconds between 0 and 3600 (0 = manual stop).";
            return null;
        }

        public static async Task<List<CommandConfigDto>> LoadAsync(bool includeDisabled = false)
        {
            var list = new List<CommandConfigDto>();
            using var conn = new MySqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            string sql = "SELECT Id, Name, CommandType, CommandHex, Color, Duration, SortOrder, IsEnabled " +
                         "FROM CommandConfigs WHERE ShowOnPanel = 1" + (includeDisabled ? "" : " AND IsEnabled = 1") +
                         " ORDER BY SortOrder, Name";
            using var cmd = new MySqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new CommandConfigDto
                {
                    Id = rdr.GetGuid(0),
                    Name = rdr.GetString(1),
                    CommandType = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    CommandHex = rdr.GetInt32(3),
                    Color = rdr.IsDBNull(4) ? "#3B82F6" : rdr.GetString(4),
                    Duration = rdr.GetInt32(5),
                    SortOrder = rdr.GetInt32(6),
                    IsEnabled = rdr.GetBoolean(7)
                });
            }
            return list;
        }

        public static async Task SaveAsync(CommandConfigDto b)
        {
            using var conn = new MySqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "INSERT INTO CommandConfigs (Id, Name, Description, CommandType, CommandHex, Color, Duration, IsEnabled, SortOrder, ShowOnPanel) " +
                "VALUES (@Id, @Name, '', @Type, @Hex, @Color, @Duration, @Enabled, @Sort, 1) " +
                "ON DUPLICATE KEY UPDATE Name = @Name, CommandType = @Type, CommandHex = @Hex, Color = @Color, " +
                "Duration = @Duration, IsEnabled = @Enabled, SortOrder = @Sort", conn);
            cmd.Parameters.AddWithValue("@Id", b.Id.ToString());
            cmd.Parameters.AddWithValue("@Name", b.Name);
            cmd.Parameters.AddWithValue("@Type", b.CommandType);
            cmd.Parameters.AddWithValue("@Hex", b.CommandHex);
            cmd.Parameters.AddWithValue("@Color", b.Color);
            cmd.Parameters.AddWithValue("@Duration", b.Duration);
            cmd.Parameters.AddWithValue("@Enabled", b.IsEnabled);
            cmd.Parameters.AddWithValue("@Sort", b.SortOrder);
            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task DeleteAsync(Guid id)
        {
            using var conn = new MySqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM CommandConfigs WHERE Id = @Id AND ShowOnPanel = 1", conn);
            cmd.Parameters.AddWithValue("@Id", id.ToString());
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
