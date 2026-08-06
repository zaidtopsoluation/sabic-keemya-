using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Models;
using Keemya.Frontend.Stores;
using MySqlConnector;
using System.Windows.Media;
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
        [ObservableProperty] private ObservableCollection<SirenRowItem> allSirenItems = new();
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

        // ── Station Routing Selections ──────────────────────────────────────
        [ObservableProperty] private bool isAdminEccSelected;
        [ObservableProperty] private bool isPcbEcsSelected;
        [ObservableProperty] private bool isRcbEcsSelected;
        [ObservableProperty] private bool isPcbControllerSelected;
        [ObservableProperty] private bool isRcbControllerSelected;

        [ObservableProperty]
        private ObservableCollection<StationStatusDto> stationStatuses = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AdminEccStatusColor))]
        private string adminEccStatus = "OFFLINE";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PcbEcsStatusColor))]
        private string pcbEcsStatus = "OFFLINE";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RcbEcsStatusColor))]
        private string rcbEcsStatus = "OFFLINE";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PcbControllerStatusColor))]
        private string pcbControllerStatus = "OFFLINE";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RcbControllerStatusColor))]
        private string rcbControllerStatus = "OFFLINE";

        public Brush AdminEccStatusColor => GetBrushForStatus(AdminEccStatus);
        public Brush PcbEcsStatusColor => GetBrushForStatus(PcbEcsStatus);
        public Brush RcbEcsStatusColor => GetBrushForStatus(RcbEcsStatus);

        public string ConfirmButtonText => CommandToConfirmName?.StartsWith("INTERCOM:") == true ? "Confirm Call" : "Confirm Activation";
        public string AbortButtonText => CommandToConfirmName?.StartsWith("INTERCOM:") == true ? "Abort Call" : "Abort Activation";
        public string TitleBarText => CommandToConfirmName?.StartsWith("INTERCOM:") == true ? "Voice Intercom Request" : "Activation Sequence";
        public Visibility SirenDetailsVisibility => CommandToConfirmName?.StartsWith("INTERCOM:") == true ? Visibility.Collapsed : Visibility.Visible;
        public string AdminEccButtonText => ActiveIntercomStation == "Admin ECC" ? "Admin ECC\n(ON CALL)" : "Admin ECC";
        public string PcbEcsButtonText => ActiveIntercomStation == "PCB/ECS" ? "PCB/ECS\n(ON CALL)" : "PCB/ECS";
        public string RcbEcsButtonText => ActiveIntercomStation == "RCB/ECS" ? "RCB/ECS\n(ON CALL)" : "RCB/ECS";

        [ObservableProperty]
        private string? activeIntercomStation;
        public Brush PcbControllerStatusColor => GetBrushForStatus(PcbControllerStatus);
        public Brush RcbControllerStatusColor => GetBrushForStatus(RcbControllerStatus);

        private Brush GetBrushForStatus(string status)
        {
            return status == "ONLINE"
                ? new SolidColorBrush(Color.FromRgb(45, 106, 79))  // Deep Green #2D6A4F
                : new SolidColorBrush(Color.FromRgb(128, 15, 47)); // Deep Red #800F2F
        }

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

        private List<CommandCardDto> _allCommands = new();
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

            // Initialize station status refresh timer (poll every 1 second for instant intercom synchronization)
            var stationTimer = new System.Windows.Threading.DispatcherTimer();
            stationTimer.Interval = TimeSpan.FromSeconds(1);
            stationTimer.Tick += async (s, e) => await LoadStationStatusesAsync();
            stationTimer.Start();
            _ = Task.Run(() => LoadStationStatusesAsync());

            // Start listening for incoming VoIP intercom voice calls
            try
            {
                Keemya.Frontend.Services.AudioCommunicationService.Instance.StartListening();
            }
            catch {}
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
                AllSirenItems = new ObservableCollection<SirenRowItem>(sirens);
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
                                     WHERE c.IsEnabled = 1";

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

                _allCommands = cards;
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
        private void SelectFireAlarm()
        {
            var cmd = _allCommands.FirstOrDefault(c => c.CommandHex == 2);
            if (cmd != null)
            {
                _commandToConfirm = cmd;
                CommandToConfirmName = "FIRE ALARM";
                IsConfirmingCommand = true;
            }
            else
            {
                MessageBox.Show("Pre-configured Attack tone command (Fire Alarm) not found in database.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void SelectGasAlarm()
        {
            var cmd = _allCommands.FirstOrDefault(c => c.CommandHex == 1);
            if (cmd != null)
            {
                _commandToConfirm = cmd;
                CommandToConfirmName = "GAS ALARM";
                IsConfirmingCommand = true;
            }
            else
            {
                MessageBox.Show("Pre-configured Wail tone command (Gas Alarm) not found in database.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void SelectAllClearAlarm()
        {
            var cmd = _allCommands.FirstOrDefault(c => c.CommandHex == 5);
            if (cmd != null)
            {
                _commandToConfirm = cmd;
                CommandToConfirmName = "ALL CLEAR ALARM";
                IsConfirmingCommand = true;
            }
            else
            {
                MessageBox.Show("Pre-configured Air Horn command (All Clear Alarm) not found in database.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void SelectResetAlarm()
        {
            _commandToConfirm = new CommandCardDto
            {
                Id = Guid.NewGuid(),
                Name = "RESET ALARM",
                CommandHex = 0,
                Color = "Yellow",
                CommandType = "Reset/Clear"
            };
            CommandToConfirmName = "RESET ALARM";
            IsConfirmingCommand = true;
        }

        [RelayCommand]
        private void SelectAttention()
        {
            var cmd = _allCommands.FirstOrDefault(c => c.CommandHex == 3);
            if (cmd != null)
            {
                _commandToConfirm = cmd;
                CommandToConfirmName = "ATTENTION";
                IsConfirmingCommand = true;
            }
            else
            {
                MessageBox.Show("Pre-configured Alert tone command (Attention) not found in database.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void SelectEmCallPaging()
        {
            var cmd = _allCommands.FirstOrDefault(c => c.CommandHex == 4);
            if (cmd != null)
            {
                _commandToConfirm = cmd;
                CommandToConfirmName = "EM. CALL PAGING";
                IsConfirmingCommand = true;
            }
            else
            {
                MessageBox.Show("Pre-configured PA Paging command not found in database.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void SelectEmCallRadioPaging()
        {
            var cmd = _allCommands.FirstOrDefault(c => c.CommandHex == 4);
            if (cmd != null)
            {
                _commandToConfirm = cmd;
                CommandToConfirmName = "EM. CALL RADIO & PAGING";
                IsConfirmingCommand = true;
            }
            else
            {
                MessageBox.Show("Pre-configured PA Radio Paging command not found in database.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void SelectPublicAddress()
        {
            var cmd = _allCommands.FirstOrDefault(c => c.CommandHex == 4);
            if (cmd != null)
            {
                _commandToConfirm = cmd;
                CommandToConfirmName = "PUBLIC ADDRESS";
                IsConfirmingCommand = true;
            }
            else
            {
                var mockCmd = new CommandCardDto
                {
                    Id = Guid.NewGuid(),
                    Name = "PUBLIC ADDRESS",
                    CommandType = "Live",
                    CommandHex = 4,
                    Color = "#3B82F6",
                    Duration = 0
                };
                _commandToConfirm = mockCmd;
                CommandToConfirmName = "PUBLIC ADDRESS";
                IsConfirmingCommand = true;
            }
        }

        [RelayCommand]
        private void SelectAdminEcc()
        {
            if (ActiveIntercomStation == "Admin ECC")
            {
                StopIntercomCall();
            }
            else
            {
                CommandToConfirmName = "INTERCOM: Admin ECC";
                IsConfirmingCommand = true;
                OnPropertyChanged(nameof(TitleBarText));
                OnPropertyChanged(nameof(ConfirmButtonText));
                OnPropertyChanged(nameof(AbortButtonText));
                OnPropertyChanged(nameof(SirenDetailsVisibility));
            }
        }

        [RelayCommand]
        private void SelectPcbEcs()
        {
            if (ActiveIntercomStation == "PCB/ECS")
            {
                StopIntercomCall();
            }
            else
            {
                CommandToConfirmName = "INTERCOM: PCB/ECS";
                IsConfirmingCommand = true;
                OnPropertyChanged(nameof(TitleBarText));
                OnPropertyChanged(nameof(ConfirmButtonText));
                OnPropertyChanged(nameof(AbortButtonText));
                OnPropertyChanged(nameof(SirenDetailsVisibility));
            }
        }

        [RelayCommand]
        private void SelectRcbEcs()
        {
            if (ActiveIntercomStation == "RCB/ECS")
            {
                StopIntercomCall();
            }
            else
            {
                CommandToConfirmName = "INTERCOM: RCB/ECS";
                IsConfirmingCommand = true;
                OnPropertyChanged(nameof(TitleBarText));
                OnPropertyChanged(nameof(ConfirmButtonText));
                OnPropertyChanged(nameof(AbortButtonText));
                OnPropertyChanged(nameof(SirenDetailsVisibility));
            }
        }

        [RelayCommand]
        private void SelectPcbController()
        {
            // Passive status only, no selection action
        }

        [RelayCommand]
        private void SelectRcbController()
        {
            // Passive status only, no selection action
        }

        [RelayCommand]
        private void CancelActivate()
        {
            _commandToConfirm = null;
            IsConfirmingCommand = false;
            OnPropertyChanged(nameof(SirenDetailsVisibility));
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

            // Stop any active VoIP intercom call
            if (!string.IsNullOrEmpty(ActiveIntercomStation))
            {
                StopIntercomCall();
            }

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
            if (CommandToConfirmName != null && CommandToConfirmName.StartsWith("INTERCOM: "))
            {
                string targetStation = CommandToConfirmName.Replace("INTERCOM: ", "");
                IsConfirmingCommand = false;
                StartIntercomCall(targetStation);
                return;
            }

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

            if (card.CommandHex == 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        IsCommandRunning = true;
                        ActiveDurationDisplay = "Resetting All Sirens...";
                        await ClearAll();
                    }
                    catch (Exception ex)
                    {
                        Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"Error during Reset Alarm: {ex.Message}");
                    }
                    finally
                    {
                        IsCommandRunning = false;
                    }
                });
                return;
            }

            // Always target all sirens (Broadcast)
            List<SirenRowItem> targets = _allSirens.ToList();
            bool isBroadcast = true;

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

            // Start Audio Loopback simulation or VoIP audio stream if it's the Public Address command (0x04) or Custom (0x64)
            if (card.CommandHex == 0x04 || card.CommandHex == 0x64)
            {
                IsPublicAddressActive = true;
                
                if (!string.IsNullOrEmpty(card.AudioFilePath) && System.IO.File.Exists(card.AudioFilePath))
                {
                    Keemya.Frontend.Services.AudioSimulationService.Instance.StartFilePlayback(card.AudioFilePath);
                }
                else
                {
                    if (AppConfig.StationName == "Admin ECC")
                    {
                        Keemya.Frontend.Services.AudioSimulationService.Instance.StartLoopback();
                    }
                    else
                    {
                        // We are the office PC: Start live PA voice streaming to Admin ECC
                        ActiveIntercomStation = "PA:Admin ECC";
                        _ = UpdateActiveCallTargetInDbAsync("PA:Admin ECC");
                        _ = Task.Run(async () =>
                        {
                            string? ip = await GetStationIpAsync("Admin ECC");
                            if (!string.IsNullOrEmpty(ip))
                            {
                                Keemya.Frontend.Services.AudioCommunicationService.Instance.StartRecording(ip);
                            }
                        });
                    }
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

        private async Task LoadStationStatusesAsync()
        {
            try
            {
                var list = new List<StationStatusDto>();
                using (var connection = new MySqlConnection(ConnStr))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT Id, Name, Type, IpAddress, Status, ActiveCallTarget FROM StationStatuses";
                    using (var cmd = new MySqlCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new StationStatusDto
                            {
                                Id = reader.GetGuid(0),
                                Name = reader.GetString(1),
                                Type = reader.GetString(2),
                                IpAddress = reader.GetString(3),
                                Status = reader.GetString(4),
                                ActiveCallTarget = reader.IsDBNull(5) ? null : reader.GetString(5)
                            });
                        }
                    }
                }

                // Update the collection on UI thread
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    foreach (var item in list)
                    {
                        var existing = StationStatuses.FirstOrDefault(x => x.Name == item.Name);
                        if (existing != null)
                        {
                            existing.Status = item.Status;
                            existing.IpAddress = item.IpAddress;
                            existing.ActiveCallTarget = item.ActiveCallTarget;
                        }
                        else
                        {
                            StationStatuses.Add(item);
                        }
                    }

                    // Update local helper properties for command center toggle backgrounds
                    var admin = list.FirstOrDefault(x => x.Name == "Admin ECC");
                    AdminEccStatus = admin?.Status ?? "OFFLINE";

                    var pcb = list.FirstOrDefault(x => x.Name == "PCB/ECS");
                    PcbEcsStatus = pcb?.Status ?? "OFFLINE";

                    var rcb = list.FirstOrDefault(x => x.Name == "RCB/ECS");
                    RcbEcsStatus = rcb?.Status ?? "OFFLINE";

                    // Controllers show the health status of their respective office PC workstations instead of hardware pings
                    PcbControllerStatus = PcbEcsStatus;
                    RcbControllerStatus = RcbEcsStatus;

                    // DB-driven Intercom voice Call Synchronization
                    // Find if another workstation has initiated a call to our workstation
                    var caller = list.FirstOrDefault(x => x.Type == "Workstation" && x.Name != AppConfig.StationName && x.ActiveCallTarget != null && x.ActiveCallTarget.EndsWith(AppConfig.StationName));
                    if (caller != null)
                    {
                        string? targetSignal = caller.ActiveCallTarget;
                        bool isLivePA = targetSignal?.StartsWith("PA:") == true;
                        string expectedStationState = isLivePA ? "PA:" + caller.Name : caller.Name;
                        
                        // If they are calling us and we haven't connected locally yet, answer and start streaming back!
                        if (ActiveIntercomStation != expectedStationState)
                        {
                            if (isLivePA)
                            {
                                Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"[VoIP] DB Sync: Incoming live Public Address broadcast from {caller.Name}. Keying PTT relay...");
                                ActiveIntercomStation = expectedStationState;
                                Keemya.Frontend.Services.AudioSimulationService.Instance.SetPttRelayState(true);
                            }
                            else
                            {
                                Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"[VoIP] DB Sync: Incoming call request detected from {caller.Name}. Connecting voice link...");
                                StartIntercomCallInternal(caller.Name);
                            }
                        }
                    }
                    else
                    {
                        // If no remote machine is calling us, but we are currently marked as in an incoming call (meaning they hung up), disconnect!
                        if (!string.IsNullOrEmpty(ActiveIntercomStation))
                        {
                            bool isLivePA = ActiveIntercomStation.StartsWith("PA:");
                            string cleanCallerName = ActiveIntercomStation.Replace("PA:", "");
                            
                            var myRow = list.FirstOrDefault(x => x.Name == AppConfig.StationName);
                            if (myRow == null || myRow.ActiveCallTarget != ActiveIntercomStation)
                            {
                                var callerRow = list.FirstOrDefault(x => x.Name == cleanCallerName);
                                if (callerRow == null || callerRow.ActiveCallTarget != (isLivePA ? "PA:" + AppConfig.StationName : AppConfig.StationName))
                                {
                                    if (isLivePA)
                                    {
                                        Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"[VoIP] DB Sync: Live Public Address ended by {cleanCallerName}. Releasing PTT relay...");
                                        ActiveIntercomStation = null;
                                        Keemya.Frontend.Services.AudioSimulationService.Instance.SetPttRelayState(false);
                                    }
                                    else
                                    {
                                        Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"[VoIP] DB Sync: Call ended by remote workstation.");
                                        StopIntercomCallInternal();
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch
            {
                // Silence DB errors during polling
            }
        }

        private async Task UpdateActiveCallTargetInDbAsync(string? target)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    await conn.OpenAsync();
                    if (target == null)
                    {
                        using (var cmd = new MySqlCommand("UPDATE StationStatuses SET ActiveCallTarget = NULL WHERE Name = @MyName OR ActiveCallTarget = @MyName", conn))
                        {
                            cmd.Parameters.AddWithValue("@MyName", AppConfig.StationName);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        using (var cmd = new MySqlCommand("UPDATE StationStatuses SET ActiveCallTarget = @Target WHERE Name = @MyName", conn))
                        {
                            cmd.Parameters.AddWithValue("@Target", target);
                            cmd.Parameters.AddWithValue("@MyName", AppConfig.StationName);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"[VoIP] Failed to update call target in DB: {ex.Message}");
            }
        }

        private void StartIntercomCall(string stationName)
        {
            _ = UpdateActiveCallTargetInDbAsync(stationName);
            StartIntercomCallInternal(stationName);
        }

        private void StopIntercomCall()
        {
            _ = UpdateActiveCallTargetInDbAsync(null);
            StopIntercomCallInternal();
        }

        private void StartIntercomCallInternal(string stationName)
        {
            if (!string.IsNullOrEmpty(ActiveIntercomStation) && ActiveIntercomStation != stationName)
            {
                StopIntercomCallInternal();
            }

            ActiveIntercomStation = stationName;
            
            if (stationName == "PCB/ECS") IsPcbEcsSelected = true;
            if (stationName == "RCB/ECS") IsRcbEcsSelected = true;
            if (stationName == "Admin ECC") IsAdminEccSelected = true;

            // Notify button texts to display "(ON CALL)"
            OnPropertyChanged(nameof(AdminEccButtonText));
            OnPropertyChanged(nameof(PcbEcsButtonText));
            OnPropertyChanged(nameof(RcbEcsButtonText));

            _ = Task.Run(async () =>
            {
                string? ip = await GetStationIpAsync(stationName);
                if (string.IsNullOrEmpty(ip))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StopIntercomCallInternal();
                    });
                    return;
                }
                Keemya.Frontend.Services.AudioCommunicationService.Instance.StartRecording(ip);
            });
        }

        private void StopIntercomCallInternal()
        {
            IsAdminEccSelected = false;
            IsPcbEcsSelected = false;
            IsRcbEcsSelected = false;
            Keemya.Frontend.Services.AudioCommunicationService.Instance.StopRecording();
            ActiveIntercomStation = null;

            // Notify button texts to remove "(ON CALL)"
            OnPropertyChanged(nameof(AdminEccButtonText));
            OnPropertyChanged(nameof(PcbEcsButtonText));
            OnPropertyChanged(nameof(RcbEcsButtonText));

            // Force refresh of the color properties
            OnPropertyChanged(nameof(AdminEccStatusColor));
            OnPropertyChanged(nameof(PcbEcsStatusColor));
            OnPropertyChanged(nameof(RcbEcsStatusColor));
        }

        private async Task<string?> GetStationNameByIpAsync(string ip)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand("SELECT Name FROM StationStatuses WHERE IpAddress = @Ip LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@Ip", ip);
                        var name = await cmd.ExecuteScalarAsync();
                        return name?.ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> GetStationIpAsync(string stationName)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand("SELECT IpAddress FROM StationStatuses WHERE Name = @Name LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", stationName);
                        var ip = await cmd.ExecuteScalarAsync();
                        return ip?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Keemya.Frontend.Services.SirenCommunicationService.Instance.Log($"[VoIP] Failed to get IP for {stationName}: {ex.Message}");
                return null;
            }
        }
    }
}
