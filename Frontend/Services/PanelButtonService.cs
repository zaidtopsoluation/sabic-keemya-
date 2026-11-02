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
        // Not a Whelen code: the app sends Public Address (0x04) to the sirens and the server plays the button's uploaded audio.
        public const int PreRecordedHex = 0x64;
        public const int MaxAudioBytes = 10 * 1024 * 1024;

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
            new(PreRecordedHex, "Pre-recorded Message (uploaded audio)"),
        }
        .Concat(Enumerable.Range(1, 8).Select(n => new ToneOption(0x30 + n, $"Voice Message {n}")))
        .Concat(Enumerable.Range(9, 4).Select(n => new ToneOption(0x32 + n, $"Voice Message {n}")))
        .Concat(Enumerable.Range(13, 4).Select(n => new ToneOption(0x04 + n, $"Voice Message {n}")))
        .ToList();

        // ── Layout (GV Core style: buttons placed freely, snapped to a grid) ──
        // The Command Center button area is 500 px wide and lines up with the station buttons 28 px in, so the canvas is 464 px.
        public const int LayoutWidth = 464, LayoutHeight = 420, GridStep = 12, Gap = 12;
        public static readonly IReadOnlyList<string> ButtonSizes = new[] { "Small", "Medium", "Large" };

        public static (int Width, int Height) SizeOf(string? size) => size switch
        {
            "Small" => (100, 55),
            "Large" => (190, 100),
            _ => (140, 75)
        };

        /// <summary>Rounds a dragged position to the grid and keeps the button inside the canvas.</summary>
        public static int Snap(double value, int max) =>
            Math.Clamp((int)Math.Round(value / GridStep) * GridStep, 0, Math.Max(0, max));

        private static bool Overlaps(int x, int y, string size, CommandConfigDto other)
        {
            var (w, h) = SizeOf(size);
            var (ow, oh) = SizeOf(other.ButtonSize);
            return x < other.X + ow + Gap && other.X < x + w + Gap && y < other.Y + oh + Gap && other.Y < y + h + Gap;
        }

        /// <summary>First grid spot, row by row, where a button of this size fits without touching the placed ones.</summary>
        public static (int X, int Y) FirstFreeSpot(IEnumerable<CommandConfigDto> placed, string size)
        {
            var (w, h) = SizeOf(size);
            var list = placed.ToList();
            for (int y = 0; y < 2000; y += GridStep)
                for (int x = 0; x + w <= LayoutWidth; x += GridStep)
                    if (!list.Any(p => Overlaps(x, y, size, p))) return (x, y);
            return (0, 0);
        }

        /// <summary>Gives every button without a position the first free spot, in list order (new buttons, upgrades).</summary>
        public static void PlaceUnplaced(IList<CommandConfigDto> buttons)
        {
            var placed = buttons.Where(b => b.PosX != null && b.PosY != null).ToList();
            foreach (var b in buttons.Where(b => b.PosX == null || b.PosY == null))
            {
                (b.PosX, b.PosY) = FirstFreeSpot(placed, b.ButtonSize);
                placed.Add(b);
            }
        }

        /// <summary>Evenly spaced rows of the given number of columns, sized for the largest button.</summary>
        public static void AutoArrange(IList<CommandConfigDto> buttons, int columns)
        {
            if (buttons.Count == 0) return;
            int cellW = buttons.Max(b => SizeOf(b.ButtonSize).Width) + Gap;
            int cellH = buttons.Max(b => SizeOf(b.ButtonSize).Height) + Gap;
            columns = Math.Clamp(columns, 1, Math.Max(1, (LayoutWidth + Gap) / cellW));
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].PosX = i % columns * cellW;
                buttons[i].PosY = i / columns * cellH;
            }
        }

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
        public static string? Validate(string? label, int? toneHex, string? color, string? durationText, out int duration, bool hasAudio = false)
        {
            duration = 0;
            if (string.IsNullOrWhiteSpace(label)) return "Button label is required.";
            if (label.Trim().Length > 40) return "Button label must be 40 characters or less.";
            if (toneHex == null || Tones.All(t => t.Hex != toneHex)) return "Select a siren tone / command.";
            if (toneHex == PreRecordedHex && !hasAudio) return "Upload the audio file for the pre-recorded message.";
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
            string sql = "SELECT Id, Name, CommandType, CommandHex, Color, Duration, SortOrder, IsEnabled, AudioName, PosX, PosY, ButtonSize " +
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
                    IsEnabled = rdr.GetBoolean(7),
                    AudioName = rdr.IsDBNull(8) ? null : rdr.GetString(8),
                    PosX = rdr.IsDBNull(9) ? null : rdr.GetInt32(9),
                    PosY = rdr.IsDBNull(10) ? null : rdr.GetInt32(10),
                    ButtonSize = rdr.IsDBNull(11) ? "Medium" : rdr.GetString(11)
                });
            }
            PlaceUnplaced(list);
            // Reading order of the layout, so the Map's vertical list matches the Command Center
            return list.OrderBy(b => b.Y).ThenBy(b => b.X).ToList();
        }

        public static async Task SaveAsync(CommandConfigDto b)
        {
            using var conn = new MySqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "INSERT INTO CommandConfigs (Id, Name, Description, CommandType, CommandHex, Color, Duration, IsEnabled, SortOrder, ShowOnPanel, AudioName, AudioData, ButtonSize) " +
                "VALUES (@Id, @Name, '', @Type, @Hex, @Color, @Duration, @Enabled, @Sort, 1, @AudioName, @AudioData, @Size) " +
                "ON DUPLICATE KEY UPDATE Name = @Name, CommandType = @Type, CommandHex = @Hex, Color = @Color, " +
                "Duration = @Duration, IsEnabled = @Enabled, SortOrder = @Sort, ButtonSize = @Size, " +
                // keep the stored audio unless a new file was uploaded
                "AudioName = COALESCE(@AudioName, AudioName), AudioData = COALESCE(@AudioData, AudioData)", conn);
            cmd.Parameters.AddWithValue("@Size", ButtonSizes.Contains(b.ButtonSize) ? b.ButtonSize : "Medium");
            cmd.Parameters.AddWithValue("@AudioName", b.AudioData == null ? null : b.AudioName);
            cmd.Parameters.AddWithValue("@AudioData", b.AudioData == null ? null : Convert.ToBase64String(b.AudioData));
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

        /// <summary>Writes the button's uploaded audio to a local cache file and returns its path, or null when it has none.</summary>
        public static async Task<string?> GetAudioFileAsync(Guid id)
        {
            using var conn = new MySqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("SELECT AudioName, AudioData FROM CommandConfigs WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id.ToString());
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync() || rdr.IsDBNull(0) || rdr.IsDBNull(1)) return null;

            byte[] data = Convert.FromBase64String(rdr.GetString(1));
            return CacheAudio(id, rdr.GetString(0), data);
        }

        // NAudio picks the decoder from the extension, so the cache file keeps it. The content hash in the name means a
        // re-uploaded file never overwrites one that is still playing.
        internal static string CacheAudio(Guid id, string audioName, byte[] data)
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TS_EMS", "ButtonAudio");
            System.IO.Directory.CreateDirectory(dir);
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(data))[..8];
            string path = System.IO.Path.Combine(dir, $"{id}_{hash}{System.IO.Path.GetExtension(audioName).ToLowerInvariant()}");
            if (!System.IO.File.Exists(path)) System.IO.File.WriteAllBytes(path, data);
            return path;
        }

        public static async Task SaveLayoutAsync(IEnumerable<CommandConfigDto> buttons)
        {
            using var conn = new MySqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            foreach (var b in buttons)
            {
                using var cmd = new MySqlCommand("UPDATE CommandConfigs SET PosX = @X, PosY = @Y WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@X", b.PosX);
                cmd.Parameters.AddWithValue("@Y", b.PosY);
                cmd.Parameters.AddWithValue("@Id", b.Id.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
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
