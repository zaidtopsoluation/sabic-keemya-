using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Stores;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    // ── Left-panel tree items ────────────────────────────────────────────────

    public partial class ZoneSelectionItem : ObservableObject
    {
        public Guid   Id       { get; set; }
        public string Name     { get; set; } = string.Empty;
        public List<SirenRowItem> Sirens { get; set; } = new();

        [ObservableProperty] private bool isExpanded = false;
        [ObservableProperty] private bool isSelected  = false;

        public int    TotalSirens  => Sirens.Count;
        public int    OnlineSirens => Sirens.Count(s => s.IsOnline);
        public string Subtitle     => $"{TotalSirens} siren{(TotalSirens == 1 ? "" : "s")} • {OnlineSirens} ONLINE";
        public string OnlineDot    => OnlineSirens > 0 ? "#10B981" : "#6B7280";
        public string ChevronKind  => IsExpanded ? "ChevronDown" : "ChevronRight";
    }

    public partial class SirenRowItem : ObservableObject
    {
        public Guid    Id            { get; set; }
        public string  Name          { get; set; } = string.Empty;
        public string  AreaCode      { get; set; } = string.Empty;
        public string  AddressCode   { get; set; } = string.Empty;
        public bool    IsOnline      { get; set; }
        public Guid?   GroupId       { get; set; }
        public string  Ip            { get; set; } = string.Empty;
        public bool    Redundant     { get; set; }

        [ObservableProperty] private bool isSelected = false;

        public string StatusColor   => IsOnline ? "#10B981" : "#6B7280";
        public string AddressDisplay => $"{AreaCode}-{AddressCode}";
    }

    // ── Right-panel command card ─────────────────────────────────────────────

    public partial class CommandCardDto : ObservableObject
    {
        public Guid   Id          { get; set; }
        public string Name        { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;
        public int    CommandHex  { get; set; }
        public string Color       { get; set; } = "Blue";
        public int    Duration    { get; set; }
        public string? AudioFilePath { get; set; }

        [ObservableProperty] private string targetLabel    = "Broadcast to All Sirens";
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvailableText))]
        private int    availableCount = 0;

        public string ColorHex => Color switch
        {
            "Red"    => "#EF4444",
            "Orange" => "#F97316",
            "Yellow" => "#EAB308",
            "Green"  => "#10B981",
            "Blue"   => "#3B82F6",
            "Purple" => "#8B5CF6",
            "Pink"   => "#EC4899",
            "Cyan"   => "#06B6D4",
            _        => Color.StartsWith("#") ? Color : "#6366F1"
        };

        public string ColorLightBg => Color switch
        {
            "Red"    => "#FEE2E2",
            "Orange" => "#FFEDD5",
            "Yellow" => "#FEF9C3",
            "Green"  => "#DCFCE7",
            "Blue"   => "#DBEAFE",
            "Purple" => "#F3E8FF",
            "Pink"   => "#FCE7F3",
            "Cyan"   => "#CFFAFE",
            _        => "#EEF2FF"
        };

        public string ColorForeground => Color switch
        {
            "Red"    => "#DC2626",
            "Orange" => "#EA580C",
            "Yellow" => "#CA8A04",
            "Green"  => "#15803D",
            "Blue"   => "#1D4ED8",
            "Purple" => "#6D28D9",
            "Pink"   => "#BE185D",
            "Cyan"   => "#0E7490",
            _        => "#4F46E5"
        };

        public string DurationDisplay => Duration == 0 ? "Manual Stop" : $"{Duration}s";
        public string AvailableText   => $"{AvailableCount} Siren{(AvailableCount == 1 ? "" : "s")} Available";
    }

    // ── Main ViewModel ───────────────────────────────────────────────────────

    public partial class CommandCenterViewModel : ObservableObject
    {
        private static readonly string ConnStr = AppConfig.ConnectionString;
        private readonly NavigationStore _nav;

        // All raw data (for computing selection counts)
        private List<SirenRowItem>   _allSirens = new();
        private List<ZoneSelectionItem> _allZones = new();

        // ── Left panel ──────────────────────────────────────────────────────
        [ObservableProperty] private bool isBroadcastSelected = true;
        [ObservableProperty] private ObservableCollection<ZoneSelectionItem> zoneItems = new();
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUngroupedSirens))]
        [NotifyPropertyChangedFor(nameof(UngroupedSubtitle))]
        private ObservableCollection<SirenRowItem> ungroupedSirens = new();
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UngroupedChevronKind))]
        private bool isUngroupedExpanded = false;

        [ObservableProperty] private int totalSirensCount   = 0;
        [ObservableProperty] private int availableSirensCount = 0;
        [ObservableProperty] private int groupsCount        = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UngroupedOnlineColor))]
        [NotifyPropertyChangedFor(nameof(UngroupedSubtitle))]
        private int ungroupedOnline    = 0;

        public string UngroupedChevronKind => IsUngroupedExpanded ? "ChevronDown" : "ChevronRight";
        public string UngroupedOnlineColor => UngroupedOnline > 0 ? "#10B981" : "#6B7280";
        public string UngroupedSubtitle    => $"{UngroupedSirens.Count} siren{(UngroupedSirens.Count == 1 ? "" : "s")} • {UngroupedOnline} ONLINE";
        public bool   HasUngroupedSirens   => UngroupedSirens != null && UngroupedSirens.Count > 0;

        // ── Selection state ─────────────────────────────────────────────────
        private enum SelectionMode { Broadcast, Zone, Siren }
        private SelectionMode _mode = SelectionMode.Broadcast;
        private Guid?  _selZoneId  = null;
        private Guid?  _selSirenId = null;

        [ObservableProperty] private string currentTargetLabel = "Broadcast to All Sirens";
        [ObservableProperty] private int    currentTargetCount = 0;

        // ── Right panel ─────────────────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<CommandCardDto> commandCards = new();

        // ── Constructor ─────────────────────────────────────────────────────
        [ObservableProperty]
        private bool isConfirmingCommand;

        [ObservableProperty]
        private bool isCommandRunning;

        [ObservableProperty]
        private string activeDurationDisplay = string.Empty;

        [ObservableProperty]
        private string commandToConfirmName = string.Empty;

        [ObservableProperty]
        private bool isPublicAddressActive;

        [ObservableProperty]
        private double microphoneVolume;

        private CommandCardDto? _commandToConfirm;
        private System.Threading.CancellationTokenSource? _activeCommandCts;
        private List<SirenRowItem> _activeTargets = new();

        public CommandCenterViewModel(NavigationStore nav)
        {
            _nav = nav;
            
            // Subscribe to mic volume changes for UI feedback
            Keemya.Frontend.Services.AudioSimulationService.Instance.VolumeChanged += (s, vol) => 
            {
                Application.Current.Dispatcher.Invoke(() => MicrophoneVolume = vol);
            };

            _ = LoadAllDataAsync();
        }

        // ────────────────────────────────────────────────────────────────────
        // Data Loading
        // ────────────────────────────────────────────────────────────────────

        private async Task LoadAllDataAsync()
        {
            await LoadSirensAndZonesAsync();
            await LoadCommandCardsAsync();
            ApplyBroadcastSelection(); // default selection
        }

        private async Task LoadSirensAndZonesAsync()
        {
            try
            {
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                // Load zones
                var zones = new List<ZoneSelectionItem>();
                using (var cmd = new MySqlCommand("SELECT Id, Name FROM SirenGroups ORDER BY Name", conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                    while (await rdr.ReadAsync())
                        zones.Add(new ZoneSelectionItem { Id = rdr.GetGuid(0), Name = rdr.GetString(1) });

                // Load all sirens
                var sirens = new List<SirenRowItem>();
                using (var cmd = new MySqlCommand(
                    "SELECT Id, Name, AreaCode, AddressCode, Status, GroupId, Ip, Redundant FROM SirenDevices ORDER BY Name", conn))
                using (var rdr = await cmd.ExecuteReaderAsync())
                    while (await rdr.ReadAsync())
                        sirens.Add(new SirenRowItem
                        {
                            Id          = rdr.GetGuid(0),
                            Name        = rdr.GetString(1),
                            AreaCode    = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                            AddressCode = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                            IsOnline    = !rdr.IsDBNull(4) && (rdr.GetString(4) == "ONLINE" || rdr.GetString(4) == "WARNING"),
                            GroupId     = rdr.IsDBNull(5) ? (Guid?)null : rdr.GetGuid(5),
                            Ip          = rdr.IsDBNull(6) ? "" : rdr.GetString(6),
                            Redundant   = !rdr.IsDBNull(7) && rdr.GetBoolean(7)
                        });

                _allSirens = sirens;

                // Assign sirens to zones
                foreach (var z in zones)
                    z.Sirens = sirens.Where(s => s.GroupId == z.Id).ToList();

                _allZones = zones;
                ZoneItems = new ObservableCollection<ZoneSelectionItem>(zones);
                UngroupedSirens = new ObservableCollection<SirenRowItem>(
                    sirens.Where(s => s.GroupId == null));

                // Stats
                TotalSirensCount    = sirens.Count;
                AvailableSirensCount = sirens.Count(s => s.IsOnline);
                GroupsCount         = zones.Count;
                UngroupedOnline     = sirens.Count(s => s.GroupId == null && s.IsOnline);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading siren data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCommandCardsAsync()
        {
            try
            {
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                const string sql = @"SELECT c.Id, c.Name, c.CommandType, c.CommandHex, c.Color, c.Duration, a.FilePath
                                     FROM CommandConfigs c
                                     LEFT JOIN AudioFiles a ON c.AudioFileId = a.Id
                                     WHERE c.IsSystemDefault = 0 AND c.IsEnabled = 1
                                     ORDER BY c.SortOrder ASC, c.Name ASC";

                var cards = new List<CommandCardDto>();
                using var cmd = new MySqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    cards.Add(new CommandCardDto
                    {
                        Id          = rdr.GetGuid(0),
                        Name        = rdr.GetString(1),
                        CommandType = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                        CommandHex  = rdr.GetInt32(3),
                        Color       = rdr.IsDBNull(4) ? "Blue" : rdr.GetString(4),
                        Duration    = rdr.GetInt32(5),
                        AudioFilePath = rdr.IsDBNull(6) ? null : System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "uploads", rdr.GetString(6))
                    });

                CommandCards = new ObservableCollection<CommandCardDto>(cards);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading commands:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Selection Commands
        // ────────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void SelectBroadcast()
        {
            ClearAllHighlights();
            IsBroadcastSelected = true;
            _mode = SelectionMode.Broadcast;
            ApplyBroadcastSelection();
        }

        [RelayCommand]
        private void SelectZone(ZoneSelectionItem zone)
        {
            if (zone == null) return;
            ClearAllHighlights();
            zone.IsSelected = true;
            _mode = SelectionMode.Zone;
            _selZoneId = zone.Id;

            CurrentTargetLabel = zone.Name;
            CurrentTargetCount = zone.OnlineSirens;
            UpdateCards(zone.Name, zone.OnlineSirens);
        }

        [RelayCommand]
        private void ToggleZoneExpand(ZoneSelectionItem zone)
        {
            if (zone == null) return;
            zone.IsExpanded = !zone.IsExpanded;
            OnPropertyChanged(nameof(ZoneItems));
        }

        [RelayCommand]
        private void SelectSiren(SirenRowItem siren)
        {
            if (siren == null) return;
            ClearAllHighlights();
            siren.IsSelected = true;
            _mode = SelectionMode.Siren;
            _selSirenId = siren.Id;

            CurrentTargetLabel = siren.Name;
            CurrentTargetCount = siren.IsOnline ? 1 : 0;
            UpdateCards(siren.Name, siren.IsOnline ? 1 : 0);
        }

        [RelayCommand]
        private void ToggleUngrouped()
        {
            IsUngroupedExpanded = !IsUngroupedExpanded;
        }

        [RelayCommand]
        private void ClearSelection() => SelectBroadcast();

        // ────────────────────────────────────────────────────────────────────
        // Activation (UI only for now — wire hardware later)
        // ────────────────────────────────────────────────────────────────────

        public ObservableCollection<string> CommLogs => Keemya.Frontend.Services.SirenCommunicationService.Instance.Logs;

        [RelayCommand]
        private void CopyLogs()
        {
            if (CommLogs == null || CommLogs.Count == 0) return;
            string allLogs = string.Join(Environment.NewLine, CommLogs);
            Clipboard.SetText(allLogs);
        }

        [RelayCommand]
        private void ConfirmActivate(CommandCardDto card)
        {
            if (card == null) return;
            
            _commandToConfirm = card;
            CommandToConfirmName = card.Name;
            IsConfirmingCommand = true;
        }

        [RelayCommand]
        private void CancelActivate()
        {
            _commandToConfirm = null;
            IsConfirmingCommand = false;
        }

        [RelayCommand]
        private void StopRunningCommand()
        {
            if (_activeCommandCts != null)
            {
                _activeCommandCts.Cancel();
                _activeCommandCts = null;
            }

            IsCommandRunning = false;
            IsConfirmingCommand = false;
            IsPublicAddressActive = false;

            // Stop any active Audio Loopback simulation
            Keemya.Frontend.Services.AudioSimulationService.Instance.StopLoopback();

            // Immediately send the CLEAR command in the background ONLY to active targets
            _ = Task.Run(async () => 
            {
                Keemya.Frontend.Services.SirenCommunicationService.Instance.Log("=== AUTOMATIC/MANUAL CANCEL INITIATED ===");
                var serialTargets = _activeTargets.Where(s => string.IsNullOrWhiteSpace(s.Ip))
                    .OrderByDescending(s => s.IsOnline)
                    .ToList();
                var tcpTargets = _activeTargets.Where(s => !string.IsNullOrWhiteSpace(s.Ip)).ToList();

                // Send TCP commands in parallel
                var tcpTasks = tcpTargets.Select(async s =>
                {
                    await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x00));
                    await Task.Delay(950);
                    await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x10));
                    await Task.Delay(950);
                    await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x30));
                    await Task.Delay(950);
                    await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x1E));
                });
                var tcpPromise = Task.WhenAll(tcpTasks);

                if (serialTargets.Count > 0)
                {
                    await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendWildcardClearAsync();
                }

                // If the active targets include all sirens (broadcast), use wildcard cancel only
                if (_activeTargets.Count == _allSirens.Count)
                {
                    // Send specific frames to all serial targets
                    foreach (var s in serialTargets)
                    {
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x00), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x10), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x30), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x1E), trackStatus: false);
                    }
                }
                else if (_mode == SelectionMode.Zone)
                {
                    // For zone, send area wildcard cancel frames per unique AreaCode in serialTargets
                    var uniqueAreas = serialTargets.Select(s => s.AreaCode ?? "000").Distinct().ToList();
                    bool isFirst = true;
                    foreach (var areaCode in uniqueAreas)
                    {
                        if (!isFirst)
                        {
                            await Task.Delay(950);
                        }
                        isFirst = false;

                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendSerialCommandAsync(BuildAreaWildcardFrame(areaCode, 0x00), expectsAck: false, isUserInitiated: true);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendSerialCommandAsync(BuildAreaWildcardFrame(areaCode, 0x10), expectsAck: false, isUserInitiated: true);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendSerialCommandAsync(BuildAreaWildcardFrame(areaCode, 0x30), expectsAck: false, isUserInitiated: true);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendSerialCommandAsync(BuildAreaWildcardFrame(areaCode, 0x1E), expectsAck: false, isUserInitiated: true);
                    }

                    // Send specific frames to all serial targets
                    foreach (var s in serialTargets)
                    {
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x00), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x10), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x30), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x1E), trackStatus: false);
                    }
                }
                else
                {
                    // Otherwise, send to specific selected targets sequentially with a delay
                    bool isFirst = true;
                    foreach (var s in serialTargets)
                    {
                        if (!isFirst)
                        {
                            await Task.Delay(950);
                        }
                        isFirst = false;

                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x00), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x10), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x30), trackStatus: false);
                        await Task.Delay(950);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x1E), trackStatus: false);
                    }
                }

                await tcpPromise;

                // Log the action
                try
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                        var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                        _ = auditLogService.LogAsync(actor, "CLEARED", $"Cancel instruction dispatched to {_activeTargets.Count} active sirens.", "Command Center");
                    });
                }
                catch { }
            });
        }

        [RelayCommand]
        private void ExecuteActivate()
        {
            var role = (Session.Role ?? "Admin").ToUpper();
            if (role == "SERVICE")
            {
                MessageBox.Show("Technical/Service users are not authorized to dispatch emergency activations.", 
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var card = _commandToConfirm;
            
            if (card == null) return;

            // Dismiss the confirmation overlay so running overlay is shown cleanly
            IsConfirmingCommand = false;

            // 1. Identify target sirens based on selection mode
            List<SirenRowItem> targets = new();
            bool isBroadcast = false;
            if (IsBroadcastSelected)
            {
                targets = _allSirens.ToList();
                isBroadcast = true;
            }
            else if (_mode == SelectionMode.Zone && _selZoneId.HasValue)
            {
                targets = _allSirens.Where(s => s.GroupId == _selZoneId.Value).ToList();
            }
            else if (_mode == SelectionMode.Siren && _selSirenId.HasValue)
            { 
                targets = _allSirens.Where(s => s.Id == _selSirenId.Value).ToList();
            }

            if (targets.Count == 0)
            {
                MessageBox.Show("No sirens selected or available to target.", "No Targets", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _activeTargets = targets.ToList(); // Save for cancellation

            // Create cancellation token for this activation run (and any auto-cancel timers)
            _activeCommandCts = new System.Threading.CancellationTokenSource();
            var token = _activeCommandCts.Token;

            Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"=== Manual Trigger Initiated: {card.Name} (0x{card.CommandHex:X2}) ===");
            
            // Execute transmission in the background to avoid freezing the UI
            _ = Task.Run(async () => 
            {
                int sendHex = card.CommandHex == 0x64 ? 0x04 : card.CommandHex;
                var serialTargets = targets.Where(s => string.IsNullOrWhiteSpace(s.Ip))
                    .OrderByDescending(s => s.IsOnline)
                    .ToList();
                var tcpTargets = targets.Where(s => !string.IsNullOrWhiteSpace(s.Ip)).ToList();

                // 1. Send TCP/IP commands to real sirens with IP addresses in parallel
                var tcpTasks = tcpTargets.Select(async s =>
                {
                    if (token.IsCancellationRequested) return;

                    byte[] frame = new byte[15];
                    frame[0] = 0x02;

                    string area = (s.AreaCode ?? "000").PadLeft(3, '0');
                    frame[1] = (byte)(0x80 | (area[0] - '0'));
                    frame[2] = (byte)(0x80 | (area[1] - '0'));
                    frame[3] = (byte)(0x80 | (area[2] - '0'));

                    string addr = (s.AddressCode ?? "0000").PadLeft(4, '0');
                    frame[4] = (byte)(0x80 | (addr[0] - '0'));
                    frame[5] = (byte)(0x80 | (addr[1] - '0'));
                    frame[6] = (byte)(0x80 | (addr[2] - '0'));
                    frame[7] = (byte)(0x80 | (addr[3] - '0'));

                    frame[8] = 0x80;
                    frame[9] = 0x80;
                    frame[10] = (byte)(0x80 | sendHex);

                    frame[11] = 0x03;

                    byte xorSum = 0;
                    for (int i = 0; i <= 11; i++) xorSum ^= frame[i];
                    frame[12] = (byte)(0x80 | (xorSum >> 4));
                    frame[13] = (byte)(0x80 | (xorSum & 0x0F));
                    frame[14] = 0x0D;

                    await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, frame);
                });
                var tcpPromise = Task.WhenAll(tcpTasks);

                // 2. Send Serial commands
                if (isBroadcast)
                {
                    // For broadcast, send ONLY a single global wildcard serial frame
                    byte[] wildcardFrame = BuildWildcardFrame((byte)sendHex);
                    await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendSerialCommandAsync(wildcardFrame, expectsAck: false, isUserInitiated: true);

                    // Send specific frames to all serial targets to guarantee activation
                    foreach (var s in serialTargets)
                    {
                        if (token.IsCancellationRequested) break;
                        await Task.Delay(500, token);

                        byte[] frame = new byte[15];
                        frame[0] = 0x02;

                        string area = (s.AreaCode ?? "000").PadLeft(3, '0');
                        frame[1] = (byte)(0x80 | (area[0] - '0'));
                        frame[2] = (byte)(0x80 | (area[1] - '0'));
                        frame[3] = (byte)(0x80 | (area[2] - '0'));

                        string addr = (s.AddressCode ?? "0000").PadLeft(4, '0');
                        frame[4] = (byte)(0x80 | (addr[0] - '0'));
                        frame[5] = (byte)(0x80 | (addr[1] - '0'));
                        frame[6] = (byte)(0x80 | (addr[2] - '0'));
                        frame[7] = (byte)(0x80 | (addr[3] - '0'));

                        frame[8] = 0x80;
                        frame[9] = 0x80;
                        frame[10] = (byte)(0x80 | sendHex);

                        frame[11] = 0x03;

                        byte xorSum = 0;
                        for (int i = 0; i <= 11; i++) xorSum ^= frame[i];
                        frame[12] = (byte)(0x80 | (xorSum >> 4));
                        frame[13] = (byte)(0x80 | (xorSum & 0x0F));
                        frame[14] = 0x0D;

                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, frame);
                    }
                }
                else if (_mode == SelectionMode.Zone)
                {
                    // For zone, send one area wildcard frame per unique AreaCode in serialTargets
                    var uniqueAreas = serialTargets.Select(s => s.AreaCode ?? "000").Distinct().ToList();
                    bool isFirst = true;
                    foreach (var areaCode in uniqueAreas)
                    {
                        if (token.IsCancellationRequested) break;

                        if (!isFirst)
                        {
                            await Task.Delay(500, token);
                        }
                        isFirst = false;

                        byte[] areaWildcardFrame = BuildAreaWildcardFrame(areaCode, (byte)sendHex);
                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendSerialCommandAsync(areaWildcardFrame, expectsAck: false, isUserInitiated: true);
                    }

                    // Send specific frames to all serial targets to guarantee activation
                    foreach (var s in serialTargets)
                    {
                        if (token.IsCancellationRequested) break;
                        await Task.Delay(500, token);

                        byte[] frame = new byte[15];
                        frame[0] = 0x02;

                        string area = (s.AreaCode ?? "000").PadLeft(3, '0');
                        frame[1] = (byte)(0x80 | (area[0] - '0'));
                        frame[2] = (byte)(0x80 | (area[1] - '0'));
                        frame[3] = (byte)(0x80 | (area[2] - '0'));

                        string addr = (s.AddressCode ?? "0000").PadLeft(4, '0');
                        frame[4] = (byte)(0x80 | (addr[0] - '0'));
                        frame[5] = (byte)(0x80 | (addr[1] - '0'));
                        frame[6] = (byte)(0x80 | (addr[2] - '0'));
                        frame[7] = (byte)(0x80 | (addr[3] - '0'));

                        frame[8] = 0x80;
                        frame[9] = 0x80;
                        frame[10] = (byte)(0x80 | sendHex);

                        frame[11] = 0x03;

                        byte xorSum = 0;
                        for (int i = 0; i <= 11; i++) xorSum ^= frame[i];
                        frame[12] = (byte)(0x80 | (xorSum >> 4));
                        frame[13] = (byte)(0x80 | (xorSum & 0x0F));
                        frame[14] = 0x0D;

                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, frame);
                    }
                }
                else
                {
                    // Otherwise, send specific serial frames to selected sirens sequentially with delay
                    bool isFirst = true;
                    foreach (var s in serialTargets)
                    {
                        if (token.IsCancellationRequested) break;

                        if (!isFirst)
                        {
                            await Task.Delay(500, token);
                        }
                        isFirst = false;

                        byte[] frame = new byte[15];
                        frame[0] = 0x02;

                        string area = (s.AreaCode ?? "000").PadLeft(3, '0');
                        frame[1] = (byte)(0x80 | (area[0] - '0'));
                        frame[2] = (byte)(0x80 | (area[1] - '0'));
                        frame[3] = (byte)(0x80 | (area[2] - '0'));

                        string addr = (s.AddressCode ?? "0000").PadLeft(4, '0');
                        frame[4] = (byte)(0x80 | (addr[0] - '0'));
                        frame[5] = (byte)(0x80 | (addr[1] - '0'));
                        frame[6] = (byte)(0x80 | (addr[2] - '0'));
                        frame[7] = (byte)(0x80 | (addr[3] - '0'));

                        frame[8] = 0x80;
                        frame[9] = 0x80;
                        frame[10] = (byte)(0x80 | sendHex);

                        frame[11] = 0x03;

                        byte xorSum = 0;
                        for (int i = 0; i <= 11; i++) xorSum ^= frame[i];
                        frame[12] = (byte)(0x80 | (xorSum >> 4));
                        frame[13] = (byte)(0x80 | (xorSum & 0x0F));
                        frame[14] = 0x0D;

                        await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, frame);
                    }
                }

                await tcpPromise;
            });

            // Transition UI to Active State
            IsCommandRunning = true;

            // Start Audio Loopback simulation if it's the Public Address command (0x04) or Custom (0x64)
            if (card.CommandHex == 0x04 || card.CommandHex == 0x64)
            {
                IsPublicAddressActive = true;
                
                if (!string.IsNullOrEmpty(card.AudioFilePath) && System.IO.File.Exists(card.AudioFilePath))
                {
                    Keemya.Frontend.Services.AudioSimulationService.Instance.StartFilePlayback(card.AudioFilePath);
                }
                else
                {
                    Keemya.Frontend.Services.AudioSimulationService.Instance.StartLoopback();
                }
            }

            if (card.Duration > 0)
            {
                // Start background countdown
                _ = Task.Run(async () =>
                {
                    try
                    {
                        for (int i = card.Duration; i > 0; i--)
                        {
                            if (token.IsCancellationRequested) break;
                            
                            Application.Current.Dispatcher.Invoke(() => 
                            {
                                ActiveDurationDisplay = $"Auto-canceling in {i} seconds...";
                            });
                            
                            await Task.Delay(1000, token);
                        }

                        if (!token.IsCancellationRequested)
                        {
                            Application.Current.Dispatcher.Invoke(() => 
                            {
                                ActiveDurationDisplay = "Auto-canceling now...";
                                StopRunningCommandCommand.Execute(null);
                            });
                        }
                    }
                    catch (TaskCanceledException) { }
                });
            }
            else
            {
                ActiveDurationDisplay = "Manual Stop Required";
            }
        }

        [RelayCommand]
        private async Task ClearAll()
        {
            Keemya.Frontend.Services.SirenCommunicationService.Instance.Log("=== MANUAL CANCEL (CLEAR ALL) INITIATED ===");

            // 1. Build and send wildcard serial frames for all groups (including binary cancel packets)
            await Keemya.Frontend.Services.SirenCommunicationService.Instance.SendWildcardClearAsync();

            // 2. Send TCP/IP commands to real sirens with IP addresses in parallel
            var realTcpSirens = _allSirens.Where(s => !string.IsNullOrWhiteSpace(s.Ip)).ToList();
            var tcpTasks = realTcpSirens.Select(async s =>
            {
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x00));
                await Task.Delay(950);
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x10));
                await Task.Delay(950);
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x30));
                await Task.Delay(950);
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x1E));
            });
            var tcpPromise = Task.WhenAll(tcpTasks);

            var serialTargets = _allSirens.Where(s => string.IsNullOrWhiteSpace(s.Ip))
                .OrderByDescending(s => s.IsOnline)
                .ToList();
            // Send specific frames to all serial targets
            foreach (var s in serialTargets)
            {
                await Task.Delay(950);
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x00), trackStatus: false);
                await Task.Delay(950);
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x10), trackStatus: false);
                await Task.Delay(950);
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x30), trackStatus: false);
                await Task.Delay(950);
                await Keemya.Frontend.Services.SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x1E), trackStatus: false);
            }

            await tcpPromise;

            // Log the action
            try
            {
                string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                await auditLogService.LogAsync(actor, "CLEARED", "Cancel (Clear All) instruction dispatched to all sirens.", "Command Center");
            }
            catch { }

            MessageBox.Show("Cancel (Clear All) instruction dispatched to all sirens.", "System Cancel Sent", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private byte[] BuildWildcardFrame(byte commandHex)
        {
            byte[] frame = new byte[15];
            frame[0] = 0x02; // STX

            // Area Code (wildcards)
            frame[1] = 0x8F;
            frame[2] = 0x8F;
            frame[3] = 0x8F;

            // Address Code (wildcards)
            frame[4] = 0x8F;
            frame[5] = 0x8F;
            frame[6] = 0x8F;
            frame[7] = 0x8F;

            // Command bytes
            frame[8] = 0x80;
            frame[9] = 0x80;
            frame[10] = (byte)(0x80 | commandHex);

            frame[11] = 0x03; // ETX

            // BCN Checksum
            byte xorSum = 0;
            for (int i = 0; i <= 11; i++)
            {
                xorSum ^= frame[i];
            }
            frame[12] = (byte)(0x80 | (xorSum >> 4));
            frame[13] = (byte)(0x80 | (xorSum & 0x0F));
            frame[14] = 0x0D; // CR

            return frame;
        }

        private byte[] BuildAreaWildcardFrame(string areaCode, byte commandHex)
        {
            byte[] frame = new byte[15];
            frame[0] = 0x02; // STX

            // Area Code
            string area = (areaCode ?? "000").PadLeft(3, '0');
            frame[1] = (byte)(0x80 | (area[0] - '0'));
            frame[2] = (byte)(0x80 | (area[1] - '0'));
            frame[3] = (byte)(0x80 | (area[2] - '0'));

            // Address Code (wildcards)
            frame[4] = 0x8F;
            frame[5] = 0x8F;
            frame[6] = 0x8F;
            frame[7] = 0x8F;

            // Command bytes
            frame[8] = 0x80;
            frame[9] = 0x80;
            frame[10] = (byte)(0x80 | commandHex);

            frame[11] = 0x03; // ETX

            // BCN Checksum
            byte xorSum = 0;
            for (int i = 0; i <= 11; i++)
            {
                xorSum ^= frame[i];
            }
            frame[12] = (byte)(0x80 | (xorSum >> 4));
            frame[13] = (byte)(0x80 | (xorSum & 0x0F));
            frame[14] = 0x0D; // CR

            return frame;
        }

        private byte[] BuildUnitFrame(SirenRowItem s, byte commandHex)
        {
            byte[] frame = new byte[15];
            frame[0] = 0x02; // STX

            string area = (s.AreaCode ?? "000").PadLeft(3, '0');
            frame[1] = (byte)(0x80 | (area[0] - '0'));
            frame[2] = (byte)(0x80 | (area[1] - '0'));
            frame[3] = (byte)(0x80 | (area[2] - '0'));

            string addr = (s.AddressCode ?? "0000").PadLeft(4, '0');
            frame[4] = (byte)(0x80 | (addr[0] - '0'));
            frame[5] = (byte)(0x80 | (addr[1] - '0'));
            frame[6] = (byte)(0x80 | (addr[2] - '0'));
            frame[7] = (byte)(0x80 | (addr[3] - '0'));

            frame[8] = 0x80;
            frame[9] = 0x80;
            frame[10] = (byte)(0x80 | commandHex);

            frame[11] = 0x03; // ETX

            byte xorSum = 0;
            for (int i = 0; i <= 11; i++) xorSum ^= frame[i];
            frame[12] = (byte)(0x80 | (xorSum >> 4));
            frame[13] = (byte)(0x80 | (xorSum & 0x0F));
            frame[14] = 0x0D; // CR

            return frame;
        }

        // ── Navigation ───────────────────────────────────────────────────────

        [RelayCommand]
        private void Back() =>
            _nav.CurrentViewModel = new DashboardViewModel(_nav);

        // ────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────

        private void ApplyBroadcastSelection()
        {
            IsBroadcastSelected = true;
            CurrentTargetLabel  = "Broadcast to All Sirens";
            CurrentTargetCount  = AvailableSirensCount;
            UpdateCards("Broadcast to All Sirens", AvailableSirensCount);
        }

        private void ClearAllHighlights()
        {
            IsBroadcastSelected = false;
            foreach (var z in ZoneItems)
            {
                z.IsSelected = false;
                foreach (var s in z.Sirens) s.IsSelected = false;
            }
            foreach (var s in UngroupedSirens) s.IsSelected = false;
        }

        private void UpdateCards(string label, int count)
        {
            foreach (var c in CommandCards)
            {
                c.TargetLabel    = label;
                c.AvailableCount = count;
            }
        }
    }
}
