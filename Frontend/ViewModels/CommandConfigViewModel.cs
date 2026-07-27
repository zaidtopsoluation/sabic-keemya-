using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Models;
using Keemya.Frontend.Stores;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace Keemya.Frontend.ViewModels
{
    public partial class CommandConfigViewModel : ObservableObject
    {
        private static readonly string ConnStr = AppConfig.ConnectionString;

        private readonly NavigationStore _navigationStore;
        private List<CommandConfigDto> _allCommands = new();

        // ── List State ──────────────────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<CommandConfigDto> commands = new();

        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private int totalCount = 0;

        [ObservableProperty]
        private int enabledCount = 0;

        // ── Popup State ─────────────────────────────────────────────────────

        [ObservableProperty]
        private bool isPopupOpen = false;

        [ObservableProperty]
        private string popupTitle = "Add New Command";

        [ObservableProperty]
        private string popupSubtitle = "Fill out the form below to create a new command.";

        [ObservableProperty]
        private string popupErrorMessage = string.Empty;

        [ObservableProperty]
        private bool hasPopupError = false;

        // Popup form fields
        [ObservableProperty]
        private string commandName = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string selectedCommandType = string.Empty;

        [ObservableProperty]
        private string selectedColor = "Blue";

        [ObservableProperty]
        private int duration = 0;

        [ObservableProperty]
        private bool isCommandEnabled = true;

        private bool _isEditMode = false;
        private Guid? _editingId = null;
        private Guid? _editingAudioFileId = null;

        [ObservableProperty]
        private string? selectedAudioFilePath;

        [RelayCommand]
        private void BrowseAudioFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav|All Files|*.*",
                Title = "Select Audio File"
            };
            if (dialog.ShowDialog() == true)
            {
                SelectedAudioFilePath = dialog.FileName;
            }
        }

        // ── Protocol Command Type Dropdown ──────────────────────────────────
        // This list drives the "Command Type" dropdown.
        // Each entry maps a display name → the CommandType key stored in the DB.
        public List<ProtocolCommandOption> ProtocolCommands { get; } = new()
        {
            // Group 0 — Core Tones
            new("Clear",          "Clear",         0x00, 0),
            new("Wail",           "Wail",          0x01, 4),
            new("Attack",         "Attack",        0x02, 4),
            new("Alert",          "Alert",         0x03, 4),
            new("Public Address", "PublicAddress", 0x04, 0),
            new("Air Horn",       "AirHorn",       0x05, 4),
            new("Hi-Lo",          "HiLo",          0x06, 4),
            new("Whoop",          "Whoop",         0x07, 4),
            new("Noon Test",      "NoonTest",      0x08, 4),
            new("Silent Test",    "SilentTest",    0x0F, 4),
            // Group 1 — System Control
            new("Status Request", "StatusRequest", 0x1F, 4),
            new("Arm System",     "ArmSystem",     0x18, 4),
            new("Dis-arm System", "DisarmSystem",  0x19, 4),
            new("Siren On",       "SirenOn",       0x1A, 4),
            new("Siren Off",      "SirenOff",      0x1B, 4),
            new("Instant Status", "InstantStatus", 0x23, 4),
            new("Counter",        "Counter",       0x16, 2),
            new("Clear Counter",  "ClearCounter",  0x17, 2),
            new("Test Clear",     "TestClear",     0x1E, 0),
            new("Battery / AC",   "BatteryAC",     0x21, 4),
            new("Battery / Temp", "BatteryTemp",   0x22, 4),
            new("Transmit Off",   "TransmitOff",   0x24, 0),
            // Group 1 — Digital Voice (13-16)
            new("Message 13",     "Message13",     0x11, 0),
            new("Message 14",     "Message14",     0x12, 0),
            new("Message 15",     "Message15",     0x13, 0),
            new("Message 16",     "Message16",     0x14, 0),
            // Group 3 — Digital Voice (1-12)
            new("Message 1",      "Message1",      0x31, 0),
            new("Message 2",      "Message2",      0x32, 0),
            new("Message 3",      "Message3",      0x33, 0),
            new("Message 4",      "Message4",      0x34, 0),
            new("Message 5",      "Message5",      0x35, 0),
            new("Message 6",      "Message6",      0x36, 0),
            new("Message 7",      "Message7",      0x37, 0),
            new("Message 8",      "Message8",      0x38, 0),
            new("Message 9",      "Message9",      0x3B, 0),
            new("Message 10",     "Message10",     0x3C, 0),
            new("Message 11",     "Message11",     0x3D, 0),
            new("Message 12",     "Message12",     0x3E, 0),
            // Group 3 — Strobe
            new("Strobe On",      "StrobeOn",      0x39, 0),
            new("Strobe Off",     "StrobeOff",     0x3A, 0),
            // Custom
            new("Custom",         "Custom",        0x64, 0),
        };

        // Color options (same palette as the Zone/Siren modules)
        public ObservableCollection<ColorOption> ColorOptions { get; } = new()
        {
            new ColorOption { Name = "Red",    Hex = "#EF4444" },
            new ColorOption { Name = "Orange", Hex = "#F97316" },
            new ColorOption { Name = "Yellow", Hex = "#EAB308" },
            new ColorOption { Name = "Green",  Hex = "#10B981" },
            new ColorOption { Name = "Blue",   Hex = "#3B82F6" },
            new ColorOption { Name = "Purple", Hex = "#8B5CF6" },
            new ColorOption { Name = "Pink",   Hex = "#EC4899" },
            new ColorOption { Name = "Cyan",   Hex = "#06B6D4" },
            new ColorOption { Name = "Custom Color", Hex = "#6366F1" }
        };

        // ── Constructor ─────────────────────────────────────────────────────

        public CommandConfigViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _ = LoadCommandsAsync();
        }

        // ── Search ──────────────────────────────────────────────────────────

        partial void OnSearchQueryChanged(string value) => ApplyFilter();

        partial void OnSelectedColorChanged(string value)
        {
            if (value == "Custom Color")
            {
                using var dialog = new System.Windows.Forms.ColorDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                    
                    var existing = ColorOptions.FirstOrDefault(c => c.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase) && c.Name != "Custom Color");
                    if (existing != null)
                    {
                        SelectedColor = existing.Name;
                    }
                    else
                    {
                        var customHexOpt = ColorOptions.FirstOrDefault(c => c.Name.StartsWith("#"));
                        if (customHexOpt != null) ColorOptions.Remove(customHexOpt);
                        
                        ColorOptions.Insert(ColorOptions.Count - 1, new ColorOption { Name = hex, Hex = hex });
                        SelectedColor = hex;
                    }
                }
                else
                {
                    SelectedColor = "Blue"; // Fallback if cancelled
                }
            }
        }

        private void ApplyFilter()
        {
            var filtered = _allCommands.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string term = SearchQuery.ToLower();
                filtered = filtered.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    c.Description.ToLower().Contains(term) ||
                    c.CommandType.ToLower().Contains(term));
            }

            Commands = new ObservableCollection<CommandConfigDto>(filtered);
            TotalCount   = _allCommands.Count;
            EnabledCount = _allCommands.Count(c => c.IsEnabled);
        }

        // ── Data Loading ─────────────────────────────────────────────────────

        private async Task LoadCommandsAsync()
        {
            try
            {
                var list = new List<CommandConfigDto>();
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                // Ensure SortOrder column exists (compatible with all MySQL versions)
                const string checkCol = @"
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'CommandConfigs'
                      AND COLUMN_NAME  = 'SortOrder';";
                using (var chk = new MySqlCommand(checkCol, conn))
                {
                    long exists = (long)(await chk.ExecuteScalarAsync() ?? 0L);
                    if (exists == 0)
                    {
                        const string addCol = "ALTER TABLE CommandConfigs ADD COLUMN SortOrder INT NOT NULL DEFAULT 0;";
                        using var alter = new MySqlCommand(addCol, conn);
                        await alter.ExecuteNonQueryAsync();
                    }
                }

                // Ensure AudioFileId column exists
                const string checkAudioCol = @"
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'CommandConfigs'
                      AND COLUMN_NAME  = 'AudioFileId';";
                using (var chkAudio = new MySqlCommand(checkAudioCol, conn))
                {
                    long existsAudio = (long)(await chkAudio.ExecuteScalarAsync() ?? 0L);
                    if (existsAudio == 0)
                    {
                        const string addAudioCol = "ALTER TABLE CommandConfigs ADD COLUMN AudioFileId CHAR(36) NULL;";
                        using var alterAudio = new MySqlCommand(addAudioCol, conn);
                        await alterAudio.ExecuteNonQueryAsync();
                    }
                }

                // Load user-created commands ordered by SortOrder
                const string sql = @"
                    SELECT c.Id, c.Name, c.Description, c.CommandType, c.CommandHex,
                           c.ExpectedResponseBytes, c.Color, c.IsEnabled, c.Duration, c.SortOrder,
                           c.AudioFileId, a.FilePath
                    FROM   CommandConfigs c
                    LEFT JOIN AudioFiles a ON c.AudioFileId = a.Id
                    WHERE  c.IsSystemDefault = 0
                    ORDER  BY c.SortOrder ASC, c.Name ASC;";

                using var cmd = new MySqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    list.Add(new CommandConfigDto
                    {
                        Id                    = rdr.GetGuid(0),
                        Name                  = rdr.GetString(1),
                        Description           = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                        CommandType           = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                        CommandHex            = rdr.GetInt32(4),
                        ExpectedResponseBytes = rdr.GetInt32(5),
                        Color                 = rdr.IsDBNull(6) ? "Blue" : rdr.GetString(6),
                        IsEnabled             = rdr.GetBoolean(7),
                        Duration              = rdr.GetInt32(8),
                        SortOrder             = rdr.GetInt32(9),
                        AudioFileId           = rdr.IsDBNull(10) ? (Guid?)null : rdr.GetGuid(10),
                        AudioFilePath         = rdr.IsDBNull(11) ? null : rdr.GetString(11)
                    });
                }

                _allCommands = list;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading commands: {ex.Message}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Drag-and-Drop Reorder ────────────────────────────────────────────

        [RelayCommand]
        private async Task Reorder((CommandConfigDto Dragged, CommandConfigDto Target) args)
        {
            if (args.Dragged == null || args.Target == null || args.Dragged.Id == args.Target.Id)
                return;

            // Move dragged item to target position in the live list
            var list = Commands.ToList();
            int fromIdx = list.IndexOf(args.Dragged);
            int toIdx   = list.IndexOf(args.Target);
            if (fromIdx < 0 || toIdx < 0) return;

            list.RemoveAt(fromIdx);
            list.Insert(toIdx, args.Dragged);

            // Reassign SortOrder values
            for (int i = 0; i < list.Count; i++)
                list[i].SortOrder = i;

            Commands = new ObservableCollection<CommandConfigDto>(list);
            _allCommands = list;

            // Persist new order to DB
            try
            {
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();
                foreach (var item in list)
                {
                    const string sql = "UPDATE CommandConfigs SET SortOrder = @Order WHERE Id = @Id;";
                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Order", item.SortOrder);
                    cmd.Parameters.AddWithValue("@Id",    item.Id.ToString());
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving order: {ex.Message}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Navigation ───────────────────────────────────────────────────────

        [RelayCommand]
        private void Back() =>
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);

        // ── Popup: Open / Close ──────────────────────────────────────────────

        [RelayCommand]
        private void OpenAddCommandPopup()
        {
            // Reset fields
            CommandName          = string.Empty;
            Description          = string.Empty;
            SelectedCommandType  = ProtocolCommands.First().Key;
            SelectedColor        = "Blue";
            Duration             = 0;
            IsCommandEnabled     = true;
            SelectedAudioFilePath = null;
            PopupErrorMessage    = string.Empty;
            HasPopupError        = false;

            _isEditMode   = false;
            _editingId    = null;
            _editingAudioFileId = null;
            PopupTitle    = "Add New Command";
            PopupSubtitle = "Fill out the form below to create a new command.";

            IsPopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditCommandPopup(CommandConfigDto item)
        {
            if (item == null) return;
            CommandName         = item.Name;
            Description         = item.Description;
            SelectedCommandType = item.CommandType;
            
            // Handle custom hex color restoring
            if (item.Color.StartsWith("#") && !ColorOptions.Any(c => c.Name == item.Color))
            {
                var customHexOpt = ColorOptions.FirstOrDefault(c => c.Name.StartsWith("#"));
                if (customHexOpt != null) ColorOptions.Remove(customHexOpt);
                ColorOptions.Insert(ColorOptions.Count - 1, new ColorOption { Name = item.Color, Hex = item.Color });
            }
            
            SelectedColor       = item.Color;
            Duration            = item.Duration;
            IsCommandEnabled    = item.IsEnabled;
            SelectedAudioFilePath = item.AudioFilePath;
            PopupErrorMessage   = string.Empty;
            HasPopupError       = false;

            _isEditMode   = true;
            _editingId    = item.Id;
            _editingAudioFileId = item.AudioFileId;
            PopupTitle    = "Edit Command";
            PopupSubtitle = "Update this command configuration.";

            IsPopupOpen = true;
        }

        [RelayCommand]
        private void ClosePopup() => IsPopupOpen = false;

        // ── Popup: Save ──────────────────────────────────────────────────────

        [RelayCommand]
        private async Task SaveCommand()
        {
            if (string.IsNullOrWhiteSpace(CommandName))
            {
                PopupErrorMessage = "Command name is required.";
                HasPopupError = true;
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectedCommandType))
            {
                PopupErrorMessage = "Please select a command type.";
                HasPopupError = true;
                return;
            }

            // Resolve the protocol option to pull CommandHex and ExpectedResponseBytes
            var proto = ProtocolCommands.FirstOrDefault(p => p.Key == SelectedCommandType);
            int hexVal = proto?.CommandHex ?? 0;
            int responseBytes = proto?.ExpectedResponseBytes ?? 0;

            try
            {
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                Guid? finalAudioId = _editingAudioFileId;

                // Handle Audio File saving
                if (!string.IsNullOrWhiteSpace(SelectedAudioFilePath) && File.Exists(SelectedAudioFilePath) && SelectedCommandType == "Custom")
                {
                    // If it's an absolute path, it means we selected a new file from our disk.
                    if (Path.IsPathRooted(SelectedAudioFilePath))
                    {
                        string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");
                        if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

                        string ext = Path.GetExtension(SelectedAudioFilePath);
                        string storedName = $"{Guid.NewGuid()}{ext}";
                        string destPath = Path.Combine(uploadsDir, storedName);

                        File.Copy(SelectedAudioFilePath, destPath, true);

                        finalAudioId = Guid.NewGuid();

                        // Insert into AudioFiles
                        const string audioSql = @"
                            INSERT INTO AudioFiles (Id, FileName, FilePath, FileSize)
                            VALUES (@AId, @FileName, @FilePath, 0);";
                        using var audioCmd = new MySqlCommand(audioSql, conn);
                        audioCmd.Parameters.AddWithValue("@AId", finalAudioId.Value.ToString());
                        audioCmd.Parameters.AddWithValue("@FileName", Path.GetFileName(SelectedAudioFilePath));
                        audioCmd.Parameters.AddWithValue("@FilePath", storedName);
                        await audioCmd.ExecuteNonQueryAsync();
                    }
                }
                else if (SelectedCommandType != "Custom")
                {
                    finalAudioId = null;
                }

                if (_isEditMode && _editingId.HasValue)
                {
                    const string sql = @"
                        UPDATE CommandConfigs
                        SET    Name = @Name, Description = @Desc,
                               CommandType = @Type, CommandHex = @Hex,
                               ExpectedResponseBytes = @Resp, Color = @Color,
                               IsEnabled = @Enabled, Duration = @Dur, AudioFileId = @AudioId
                        WHERE  Id = @Id;";

                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Name",    CommandName);
                    cmd.Parameters.AddWithValue("@Desc",    Description);
                    cmd.Parameters.AddWithValue("@Type",    SelectedCommandType);
                    cmd.Parameters.AddWithValue("@Hex",     hexVal);
                    cmd.Parameters.AddWithValue("@Resp",    responseBytes);
                    cmd.Parameters.AddWithValue("@Color",   SelectedColor);
                    cmd.Parameters.AddWithValue("@Enabled", IsCommandEnabled);
                    cmd.Parameters.AddWithValue("@Dur",     Duration);
                    cmd.Parameters.AddWithValue("@AudioId", finalAudioId?.ToString());
                    cmd.Parameters.AddWithValue("@Id",      _editingId.Value.ToString());
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    const string sql = @"
                        INSERT INTO CommandConfigs
                            (Id, Name, Description, CommandType, CommandHex,
                             ExpectedResponseBytes, Color, IsEnabled, Duration, IsSystemDefault, AudioFileId)
                        VALUES
                            (@Id, @Name, @Desc, @Type, @Hex,
                             @Resp, @Color, @Enabled, @Dur, 0, @AudioId);";

                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id",      Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue("@Name",    CommandName);
                    cmd.Parameters.AddWithValue("@Desc",    Description);
                    cmd.Parameters.AddWithValue("@Type",    SelectedCommandType);
                    cmd.Parameters.AddWithValue("@Hex",     hexVal);
                    cmd.Parameters.AddWithValue("@Resp",    responseBytes);
                    cmd.Parameters.AddWithValue("@Color",   SelectedColor);
                    cmd.Parameters.AddWithValue("@Enabled", IsCommandEnabled);
                    cmd.Parameters.AddWithValue("@Dur",     Duration);
                    cmd.Parameters.AddWithValue("@AudioId", finalAudioId?.ToString());
                    await cmd.ExecuteNonQueryAsync();
                }

                IsPopupOpen = false;
                await LoadCommandsAsync();
            }
            catch (Exception ex)
            {
                PopupErrorMessage = $"Database error: {ex.Message}";
                HasPopupError = true;
            }
        }

        // ── Toggle Enabled ───────────────────────────────────────────────────

        [RelayCommand]
        private async Task ToggleEnabled(CommandConfigDto item)
        {
            if (item == null) return;
            try
            {
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                bool newState = !item.IsEnabled;
                const string sql = "UPDATE CommandConfigs SET IsEnabled = @Enabled WHERE Id = @Id;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Enabled", newState);
                cmd.Parameters.AddWithValue("@Id",      item.Id.ToString());
                await cmd.ExecuteNonQueryAsync();

                item.IsEnabled = newState;
                await LoadCommandsAsync(); // refresh list
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating command: {ex.Message}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Delete ───────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task DeleteCommand(CommandConfigDto item)
        {
            if (item == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{item.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                const string sql = "DELETE FROM CommandConfigs WHERE Id = @Id;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", item.Id.ToString());
                await cmd.ExecuteNonQueryAsync();

                await LoadCommandsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting command: {ex.Message}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ── Helper types ──────────────────────────────────────────────────────────

    /// <summary>One entry in the Protocol Command dropdown.</summary>
    public class ProtocolCommandOption
    {
        public string DisplayName        { get; }
        public string Key                { get; }  // stored as CommandType in DB
        public int    CommandHex         { get; }
        public int    ExpectedResponseBytes { get; }

        public ProtocolCommandOption(string displayName, string key, int commandHex, int expectedResponseBytes)
        {
            DisplayName            = displayName;
            Key                    = key;
            CommandHex             = commandHex;
            ExpectedResponseBytes  = expectedResponseBytes;
        }

        /// <summary>Label shown in the dropdown, e.g. "Wail".</summary>
        public string Label => DisplayName;
    }
}
