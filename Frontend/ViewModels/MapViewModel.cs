using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Models;
using Keemya.Frontend.Stores;
using Keemya.Frontend.Services;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    public partial class MapViewModel : ObservableObject
    {
        private static readonly string ConnStr = AppConfig.ConnectionString;
        private readonly NavigationStore _navigationStore;

        public event Action? SirensDataChanged;

        // GIS Bounding box for mapping coordinates to map pixels
        public double MinLat => 22.0;
        public double MaxLat => 31.0;
        public double MinLng => 44.0;
        public double MaxLng => 56.0;

        [ObservableProperty]
        private ObservableCollection<SirenDeviceDto> sirens = new();

        public ObservableCollection<string> CommLogs => SirenCommunicationService.Instance.Logs;

        [ObservableProperty]
        private ObservableCollection<ZoneDto> zones = new();

        [ObservableProperty]
        private ObservableCollection<CommandConfigDto> commandCards = new();

        // ── Map Interactive States ──────────────────────────────────────────
        [ObservableProperty]
        private double zoomScale = 1.0;

        [ObservableProperty]
        private double mapOffsetX = 0.0;

        [ObservableProperty]
        private double mapOffsetY = 0.0;

        [ObservableProperty]
        private int zoomLevelDisplay = 6; // Center zoom Level

        // ── Selection and Overlay States ─────────────────────────────────────
        [ObservableProperty]
        private SirenDeviceDto? selectedSiren;

        [ObservableProperty]
        private string lastSirenClickSource = "status";

        [ObservableProperty]
        private ObservableCollection<SirenDeviceDto> targetedSirens = new();

        [ObservableProperty]
        private ObservableCollection<StationStatusDto> stationStatuses = new();

        [ObservableProperty]
        private bool isDiagnosticsOpen = false;

        [ObservableProperty]
        private bool isSidebarOpen = false;

        [ObservableProperty]
        private string sidebarTitle = "Siren Activation";

        [ObservableProperty]
        private string sidebarSubtitle = "Select a siren or group to activate.";

        [ObservableProperty]
        private string targetLabel = "Select Siren(s)";

        // ── Selected Siren Mock Health Statuses ─────────────────────────────
        [ObservableProperty] private bool healthSirenOn = false;
        [ObservableProperty] private bool healthAcOn = false;
        [ObservableProperty] private bool healthDynamicAc = false;
        [ObservableProperty] private bool healthPartialAlert = false;
        [ObservableProperty] private bool healthStrobeActive = false;
        [ObservableProperty] private bool healthSystemArmed = false;
        [ObservableProperty] private bool healthSupervisorMode = false;
        [ObservableProperty] private bool healthRotorActive = false;
        [ObservableProperty] private bool healthStoredAc = false;
        [ObservableProperty] private bool healthFullAlert = false;
        [ObservableProperty] private bool healthIntrusion = false;
        [ObservableProperty] private bool healthBiasDetected = false;
        [ObservableProperty] private bool healthSystemPowerUp = false;

        [ObservableProperty] private double healthDcVoltage = 0.0;
        [ObservableProperty] private double healthAcVoltage = 0.0;
        [ObservableProperty] private double healthTemperature = 0.0;
        
        [ObservableProperty] private int? healthStatusByte;

        [ObservableProperty]
        private bool isCommandRunning;

        [ObservableProperty]
        private string commandToConfirmName = string.Empty;

        [ObservableProperty]
        private string activeDurationDisplay = string.Empty;

        [ObservableProperty]
        private bool isPublicAddressActive;

        [ObservableProperty]
        private double microphoneVolume;

        private System.Threading.CancellationTokenSource? _activeCommandCts;
        private List<SirenDeviceDto> _activeTargets = new();

        public event Action<bool, string>? ToastRequested;

        public MapViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _ = InitializeDataAsync();
            SirenCommunicationService.Instance.StandardStatusReceived += OnStandardStatusReceived;
            SirenCommunicationService.Instance.InstantStatusReceived += OnInstantStatusReceived;
            SirenCommunicationService.Instance.ActiveStatusReceived += OnActiveStatusReceived;
            SirenCommunicationService.Instance.WeatherReceived += OnWeatherReceived;
            SirenCommunicationService.Instance.ComprehensiveTempReceived += OnComprehensiveTempReceived;
            SirenCommunicationService.Instance.BatteryAcReceived += OnBatteryAcReceived;
            SirenCommunicationService.Instance.BatteryTempReceived += OnBatteryTempReceived;
            SirenCommunicationService.Instance.SirenStatusChanged += OnSirenStatusChanged;

            // Subscribe to mic volume changes for UI feedback
            Keemya.Frontend.Services.AudioSimulationService.Instance.VolumeChanged += (s, vol) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => MicrophoneVolume = vol);
            };

            // Initialize station status refresh timer
            var stationTimer = new System.Windows.Threading.DispatcherTimer();
            stationTimer.Interval = TimeSpan.FromSeconds(5);
            stationTimer.Tick += async (s, e) => await LoadStationStatusesAsync();
            stationTimer.Start();
            _ = Task.Run(() => LoadStationStatusesAsync());
        }

        private async Task InitializeDataAsync()
        {
            await LoadZonesFromDatabaseAsync();
            await LoadSirensFromDatabaseAsync();
            await LoadCommandCardsAsync();
        }

        private async Task LoadZonesFromDatabaseAsync()
        {
            try
            {
                var list = new List<ZoneDto>();
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();
                
                using var cmd = new MySqlCommand("SELECT Id, Name, Description, Color, Shape FROM SirenGroups ORDER BY Name", conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    list.Add(new ZoneDto
                    {
                        Id = rdr.GetGuid(0),
                        Name = rdr.GetString(1),
                        Description = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                        Color = rdr.IsDBNull(3) ? "Red" : rdr.GetString(3),
                        Shape = rdr.IsDBNull(4) ? "Rectangle" : rdr.GetString(4)
                    });
                }
                Zones = new ObservableCollection<ZoneDto>(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading zones in map: {ex.Message}");
            }
        }

        private async Task LoadSirensFromDatabaseAsync()
        {
            try
            {
                var list = new List<SirenDeviceDto>();
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                const string query = @"
                    SELECT d.Id, d.Name, d.AreaCode, d.AddressCode, d.Lat, d.Lng, d.Status, d.Ip, d.Redundant, d.GroupId, g.Name as GroupName
                    FROM SirenDevices d
                    LEFT JOIN SirenGroups g ON d.GroupId = g.Id
                    ORDER BY d.Name;";

                using var cmd = new MySqlCommand(query, conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var sName = rdr.GetString(1);
                    var cache = SirenCommunicationService.Instance.GetCacheItemByAddressOrSource(sName);

                    list.Add(new SirenDeviceDto
                    {
                        Id = rdr.GetGuid(0),
                        Name = sName,
                        AreaCode = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                        AddressCode = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                        Lat = rdr.IsDBNull(4) ? 0.0 : rdr.GetDouble(4),
                        Lng = rdr.IsDBNull(5) ? 0.0 : rdr.GetDouble(5),
                        Status = rdr.IsDBNull(6) ? "OFFLINE" : rdr.GetString(6),
                        Ip = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
                        Redundant = rdr.GetBoolean(8),
                        GroupId = rdr.IsDBNull(9) ? null : rdr.GetGuid(9),
                        GroupName = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10),
                        IsTcpOnline = cache?.IsTcpOnline ?? false,
                        IsSerialOnline = cache?.IsSerialOnline ?? false
                    });
                }

                Sirens = new ObservableCollection<SirenDeviceDto>(list);

                // Update Zone Count Stats
                foreach (var z in Zones)
                {
                    z.TotalDevices = list.Count(s => s.GroupId == z.Id);
                    z.OnlineDevices = list.Count(s => s.GroupId == z.Id && (s.Status.ToUpper() == "ONLINE" || s.Status.ToUpper() == "WARNING"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sirens: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCommandCardsAsync()
        {
            try
            {
                var list = new List<CommandConfigDto>
                {
                    new CommandConfigDto
                    {
                        Id = Guid.Parse("00000000-0000-0000-0001-000000000003"),
                        Name = "FIRE ALARM",
                        CommandType = "Attack",
                        CommandHex = 2,
                        Color = "#A72A2A",
                        Duration = 90
                    },
                    new CommandConfigDto
                    {
                        Id = Guid.Parse("00000000-0000-0000-0001-000000000002"),
                        Name = "GAS ALARM",
                        CommandType = "Wail",
                        CommandHex = 1,
                        Color = "#E68A00",
                        Duration = 90
                    },
                    new CommandConfigDto
                    {
                        Id = Guid.Parse("00000000-0000-0000-0001-000000000006"),
                        Name = "ALL CLEAR ALARM",
                        CommandType = "AirHorn",
                        CommandHex = 5,
                        Color = "#2A75B3",
                        Duration = 90
                    },
                    new CommandConfigDto
                    {
                        Id = Guid.Parse("00000000-0000-0000-0001-000000000001"),
                        Name = "RESET ALARM",
                        CommandType = "Clear",
                        CommandHex = 0,
                        Color = "#E6D900",
                        Duration = 0
                    },
                    new CommandConfigDto
                    {
                        Id = Guid.Parse("00000000-0000-0000-0001-000000000005"),
                        Name = "PUBLIC ADDRESS",
                        CommandType = "PublicAddress",
                        CommandHex = 4,
                        Color = "#3B82F6",
                        Duration = 0
                    },
                    new CommandConfigDto
                    {
                        Id = Guid.Parse("00000000-0000-0000-0001-000000000004"),
                        Name = "ATTENTION",
                        CommandType = "Alert",
                        CommandHex = 3,
                        Color = "#5C2AB3",
                        Duration = 90
                    }
                };

                CommandCards = new ObservableCollection<CommandConfigDto>(list);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading commands in map: {ex.Message}");
            }
        }

        // ── Map Actions ──────────────────────────────────────────────────────
        [RelayCommand]
        private void ZoomIn()
        {
            if (ZoomScale < 3.0)
            {
                ZoomScale = Math.Round(ZoomScale + 0.2, 1);
                ZoomLevelDisplay = (int)(6 * ZoomScale);
            }
        }

        [RelayCommand]
        private void ZoomOut()
        {
            if (ZoomScale > 0.6)
            {
                ZoomScale = Math.Round(ZoomScale - 0.2, 1);
                ZoomLevelDisplay = (int)(6 * ZoomScale);
            }
        }

        [RelayCommand]
        private void ResetZoom()
        {
            ZoomScale = 1.0;
            MapOffsetX = 0.0;
            MapOffsetY = 0.0;
            ZoomLevelDisplay = 6;
        }

        // ── Interaction: Select Siren ────────────────────────────────────────
        [RelayCommand]
        private void ClickSirenFromMap(SirenDeviceDto siren)
        {
            LastSirenClickSource = "map";
            ClickSiren(siren);
        }

        [RelayCommand]
        private void ClickSirenFromList(SirenDeviceDto siren)
        {
            LastSirenClickSource = "status";
            ClickSiren(siren);
        }

        [RelayCommand]
        private void ClickSiren(SirenDeviceDto siren)
        {
            if (siren == null) return;

            // Setup Target selection for right sidebar
            TargetedSirens.Clear();
            TargetedSirens.Add(siren);

            // Load cached telemetry if available, otherwise clear
            var cache = SirenCommunicationService.Instance.GetCacheItemByAddressOrSource(siren.Name);
            if (cache != null)
            {
                HealthStatusByte = cache.StatusByte;
                HealthSirenOn = cache.SirenOn;
                HealthAcOn = cache.AcOn;
                HealthDynamicAc = cache.DynamicAc;
                HealthPartialAlert = cache.PartialAlert;
                HealthStrobeActive = cache.StrobeActive;
                HealthSystemArmed = cache.SystemArmed;
                HealthSupervisorMode = cache.SupervisorMode;
                HealthRotorActive = cache.RotorActive;
                HealthStoredAc = cache.StoredAc;
                HealthFullAlert = cache.FullAlert;
                HealthIntrusion = cache.Intrusion;
                HealthBiasDetected = cache.BiasDetected;
                HealthSystemPowerUp = cache.SystemPowerUp;

                HealthDcVoltage = cache.DcVoltage;
                HealthAcVoltage = cache.AcVoltage;
                HealthTemperature = cache.CabTemp;
            }
            else
            {
                HealthStatusByte = null;
                HealthSirenOn = false;
                HealthAcOn = false;
                HealthDynamicAc = false;
                HealthPartialAlert = false;
                HealthStrobeActive = false;
                HealthSystemArmed = false;
                HealthSupervisorMode = false;
                HealthRotorActive = false;
                HealthStoredAc = false;
                HealthFullAlert = false;
                HealthIntrusion = false;
                HealthBiasDetected = false;
                HealthSystemPowerUp = false;

                HealthDcVoltage = 0.0;
                HealthAcVoltage = 0.0;
                HealthTemperature = 0.0;
            }

            // Set SelectedSiren last to trigger WebView2 update with fully populated values
            SelectedSiren = siren;

            IsDiagnosticsOpen = true;

            // Show sidebar
            SidebarTitle = "Siren Activation";
            SidebarSubtitle = "Selected: " + siren.Name;
            TargetLabel = "Selected: " + siren.Name;
            IsSidebarOpen = true;

            // Trigger background C2030 status request query (15-byte protocol frame)
            _ = Task.Run(() => QuerySirenHealthAsync(siren));
        }

        public void UpdateSirenSelectionFromMap(List<Guid> ids)
        {
            TargetedSirens.Clear();
            foreach (var id in ids)
            {
                var siren = Sirens.FirstOrDefault(x => x.Id == id);
                if (siren != null)
                {
                    TargetedSirens.Add(siren);
                }
            }

            if (ids.Count > 0)
            {
                var lastId = ids[ids.Count - 1];
                var lastSiren = Sirens.FirstOrDefault(x => x.Id == lastId);
                
                LastSirenClickSource = "map";
                SelectedSiren = lastSiren;

                if (lastSiren != null)
                {
                    var cache = SirenCommunicationService.Instance.GetCacheItemByAddressOrSource(lastSiren.Name);
                    if (cache != null)
                    {
                        HealthStatusByte = cache.StatusByte;
                        HealthSirenOn = cache.SirenOn;
                        HealthAcOn = cache.AcOn;
                        HealthDynamicAc = cache.DynamicAc;
                        HealthPartialAlert = cache.PartialAlert;
                        HealthStrobeActive = cache.StrobeActive;
                        HealthSystemArmed = cache.SystemArmed;
                        HealthSupervisorMode = cache.SupervisorMode;
                        HealthRotorActive = cache.RotorActive;
                        HealthStoredAc = cache.StoredAc;
                        HealthFullAlert = cache.FullAlert;
                        HealthIntrusion = cache.Intrusion;
                        HealthBiasDetected = cache.BiasDetected;
                        HealthSystemPowerUp = cache.SystemPowerUp;
                        HealthDcVoltage = cache.DcVoltage;
                        HealthAcVoltage = cache.AcVoltage;
                        HealthTemperature = cache.CabTemp;
                    }
                    else
                    {
                        HealthStatusByte = null;
                        HealthSirenOn = false;
                        HealthAcOn = false;
                        HealthDynamicAc = false;
                        HealthPartialAlert = false;
                        HealthStrobeActive = false;
                        HealthSystemArmed = false;
                        HealthSupervisorMode = false;
                        HealthRotorActive = false;
                        HealthStoredAc = false;
                        HealthFullAlert = false;
                        HealthIntrusion = false;
                        HealthBiasDetected = false;
                        HealthSystemPowerUp = false;

                        HealthDcVoltage = 0.0;
                        HealthAcVoltage = 0.0;
                        HealthTemperature = 0.0;
                    }
                }

                IsDiagnosticsOpen = true;
                string namesStr = string.Join(", ", TargetedSirens.Select(s => s.Name));
                SidebarTitle = "Siren Activation";
                SidebarSubtitle = "Selected: " + namesStr;
                TargetLabel = "Selected: " + namesStr;
                IsSidebarOpen = true;
            }
            else
            {
                SelectedSiren = null;
                IsSidebarOpen = false;
                SidebarSubtitle = "None Selected";
                TargetLabel = "-";
            }
        }

        // ── Interaction: Select Zone ─────────────────────────────────────────
        [RelayCommand]
        private void ClickZone(ZoneDto zone)
        {
            if (zone == null) return;

            // Select all sirens in this zone
            var zoneSirens = Sirens.Where(s => s.GroupId == zone.Id).ToList();
            if (zoneSirens.Count == 0)
            {
                MessageBox.Show("No sirens configured in this zone.", "Empty Zone", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TargetedSirens = new ObservableCollection<SirenDeviceDto>(zoneSirens);
            SelectedSiren = null;
            IsDiagnosticsOpen = false;

            SidebarTitle = "Siren Activation";
            SidebarSubtitle = $"{zoneSirens.Count} sirens selected";
            TargetLabel = $"{zone.Name} ({zoneSirens.Count} available)";
            IsSidebarOpen = true;
        }

        [ObservableProperty]
        private string statusMessage = "System Ready. All gateways operational.";

        [RelayCommand]
        private void CloseDiagnostics()
        {
            IsDiagnosticsOpen = false;
        }

        [RelayCommand]
        private void CloseSidebar()
        {
            IsSidebarOpen = false;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            SelectedSiren = null;
            TargetedSirens.Clear();
            IsDiagnosticsOpen = false;
            IsSidebarOpen = false;
            StatusMessage = "Selection cleared.";
        }

        [RelayCommand]
        private async Task Wail()
        {
            StatusMessage = "Dispatching WAIL emergency alert...";
            var card = CommandCards.FirstOrDefault(c => c.Name.ToLower().Contains("wail"));
            if (card != null) 
            {
                await ActivateCommand(card);
                StatusMessage = "WAIL alert dispatched successfully.";
            }
            else
            {
                StatusMessage = "Wail command config not found.";
            }
        }

        [RelayCommand]
        private async Task Attack()
        {
            StatusMessage = "Dispatching ATTACK fast pulse alert...";
            var card = CommandCards.FirstOrDefault(c => c.Name.ToLower().Contains("attack"));
            if (card != null) 
            {
                await ActivateCommand(card);
                StatusMessage = "ATTACK alert dispatched successfully.";
            }
            else
            {
                StatusMessage = "Attack command config not found.";
            }
        }

        [RelayCommand]
        private async Task SITest()
        {
            StatusMessage = "Dispatching SI TEST short impulse...";
            var card = CommandCards.FirstOrDefault(c => c.Name.ToLower().Contains("test") || c.Name.ToLower().Contains("si"));
            if (card != null) 
            {
                await ActivateCommand(card);
                StatusMessage = "SI TEST dispatched successfully.";
            }
            else
            {
                StatusMessage = "SI Test command config not found.";
            }
        }

        [RelayCommand]
        private async Task PA()
        {
            StatusMessage = "Dispatching PUBLIC ADDRESS voice command...";
            var card = CommandCards.FirstOrDefault(c => c.Name.ToLower().Contains("pa") || c.Name.ToLower().Contains("address") || c.Name.ToLower().Contains("voice"));
            if (card != null) 
            {
                await ActivateCommand(card);
                StatusMessage = "PUBLIC ADDRESS voice dispatched successfully.";
            }
            else
            {
                StatusMessage = "PA command config not found.";
            }
        }

        [RelayCommand]
        private async Task Stop()
        {
            StatusMessage = "Dispatching STOP command...";
            var card = CommandCards.FirstOrDefault(c => c.Name.ToLower().Contains("stop") || c.Name.ToLower().Contains("cancel"));
            if (card != null) 
            {
                await ActivateCommand(card);
                StatusMessage = "All siren actions stopped.";
            }
            else
            {
                StatusMessage = "Stop command config not found.";
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            ClearSelection();
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
            IsPublicAddressActive = false;

            // Stop any active Audio Loopback simulation
            Keemya.Frontend.Services.AudioSimulationService.Instance.StopLoopback();

            // Immediately send the CLEAR command in the background ONLY to active targets
            _ = Task.Run(async () => 
            {
                SirenCommunicationService.Instance.Log("=== AUTOMATIC/MANUAL CANCEL INITIATED (MAP) ===");
                
                var serialTargets = _activeTargets.Where(s => string.IsNullOrWhiteSpace(s.Ip)).ToList();
                if (serialTargets.Count > 0)
                {
                    await SirenCommunicationService.Instance.SendWildcardClearAsync();
                }

                var tasks = _activeTargets.Select(async s =>
                {
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x00));
                    await Task.Delay(950);
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x10));
                    await Task.Delay(950);
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x30));
                    await Task.Delay(950);
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildUnitFrame(s, 0x1E));
                });

                await Task.WhenAll(tasks);

                // Log the action
                try
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
                    {
                        string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                        var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                        _ = auditLogService.LogAsync(actor, "CLEARED", $"Cancel instruction dispatched to {_activeTargets.Count} active sirens.", "Map");
                    });
                }
                catch { }
            });
        }

        // ── Interaction: Command Trigger ─────────────────────────────────────
        [RelayCommand]
        private async Task ActivateCommand(CommandConfigDto card)
        {
            var role = (Session.Role ?? "Admin").ToUpper();
            if (role == "SERVICE")
            {
                MessageBox.Show("Technical/Service users are not authorized to dispatch emergency activations.", 
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (card == null) return;
            if (TargetedSirens.Count == 0) return;

            SirenCommunicationService.Instance.Log($"=== Map Dispatch: {card.Name} (0x{card.CommandHex:X2}) ===");

            if (_activeCommandCts != null)
            {
                _activeCommandCts.Cancel();
                _activeCommandCts = null;
            }

            _activeCommandCts = new System.Threading.CancellationTokenSource();
            var token = _activeCommandCts.Token;

            _activeTargets = new List<SirenDeviceDto>(TargetedSirens);
            CommandToConfirmName = card.Name;

            var sortedTargets = TargetedSirens
                .OrderByDescending(s => s.Status.ToUpper() == "ONLINE" || s.Status.ToUpper() == "WARNING")
                .ToList();

            var tasks = sortedTargets.Select(async s =>
            {
                if (token.IsCancellationRequested) return;

                // Build 15-byte protocol frame
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

                int sendHex = card.CommandHex == 0x64 ? 0x04 : card.CommandHex;
                frame[8] = 0x80;
                frame[9] = 0x80;
                frame[10] = (byte)(0x80 | sendHex);

                frame[11] = 0x03;

                byte xorSum = 0;
                for (int i = 0; i <= 11; i++)
                {
                    xorSum ^= frame[i];
                }
                frame[12] = (byte)(0x80 | (xorSum >> 4));
                frame[13] = (byte)(0x80 | (xorSum & 0x0F));
                frame[14] = 0x0D;

                await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, frame);
            });

            await Task.WhenAll(tasks);

            // Log the action
            try
            {
                string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                _ = auditLogService.LogAsync(actor, "ACTIVATED", $"Command '{card.Name}' dispatched to {_activeTargets.Count} sirens.", "Map");
            }
            catch { }

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
                            
                            System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
                            {
                                ActiveDurationDisplay = $"Auto-canceling in {i} seconds...";
                            });
                            
                            await Task.Delay(1000, token);
                        }

                        if (!token.IsCancellationRequested)
                        {
                            System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
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

        // ── Navigation ───────────────────────────────────────────────────────
        [RelayCommand]
        private void Back()
        {
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }

        private async Task QuerySirenHealthAsync(SirenDeviceDto s)
        {
            if (s == null) return;

            SirenCommunicationService.Instance.Log($"=== Map Diagnostics Query: {s.Name} (0x1F Status Request) ===");

            // Build 15-byte protocol frame for 0x1F (Status Request)
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

            // We will dispatch two sequential commands: 23H (Instant Status) and 3FH (Active Status)
            _ = Task.Run(async () =>
            {
                try
                {

                    // Helper function to build a frame for a specific command
                    byte[] BuildFrame(byte cmd)
                    {
                        byte[] newFrame = new byte[15];
                        Array.Copy(frame, newFrame, frame.Length);
                        newFrame[8] = 0x80;
                        newFrame[9] = 0x80;
                        newFrame[10] = (byte)(0x80 | cmd);
                        newFrame[11] = 0x03;
                        byte xor = 0;
                        for (int i = 0; i <= 11; i++) xor ^= newFrame[i];
                        newFrame[12] = (byte)(0x80 | (xor >> 4));
                        newFrame[13] = (byte)(0x80 | (xor & 0x0F));
                        newFrame[14] = 0x0D;
                        return newFrame;
                    }

                    // --- 1. Send 23H (Instant Status) ---
                    // We use 23H as the primary online check because it's universally supported by all C2030 hardware
                    bool isOnline = await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x23));
                    
                    if (!isOnline)
                    {
                        // Abort the rest of the queries if the siren is offline to prevent UI hang
                        ToastRequested?.Invoke(false, "Siren unreachable or offline.");
                        return;
                    }
                    
                    // Siren is online, show success toast and continue
                    ToastRequested?.Invoke(true, "Telemetry synced successfully.");
                    await Task.Delay(200);

                    // --- 2. Send 3FH (Active Status) ---
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x3F), false);
                    await Task.Delay(200);

                    // --- 3. Send 1FH (Standard Status) ---
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x1F), false);
                    await Task.Delay(200);

                    // --- 4. Send 21H (Battery / AC) ---
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x21), false);
                    await Task.Delay(200);

                    // --- 5. Send 22H (Battery / Temperature) ---
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x22), false);
                    await Task.Delay(200);

                    // --- 6. Send 2AH (Weather Status) ---
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x2A), false);
                }
                catch (Exception ex)
                {
                    SirenCommunicationService.Instance.Log($"❌ [Map Diagnostics Query Error] {ex.Message}");
                }
            });
        }

        private void OnInstantStatusReceived(string addressCode, byte instantStatus, byte dcVolts, byte cabTemp, byte outTemp)
        {
            if (SelectedSiren == null) return;
            string targetAddr = (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0');
            string rcvAddr = addressCode.PadLeft(4, '0');

            if (targetAddr == rcvAddr)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    HealthStatusByte = (int)instantStatus;

                    // Decode 23H (Instant Status) according to C2030 Manual Page 11/12
                    HealthAcOn = (instantStatus & 0x01) != 0;         // Bit 0: AC Voltage (1 = On)
                    HealthIntrusion = (instantStatus & 0x02) != 0;    // Bit 1: Cabinet Intrusion (1 = Open/Intrusion)
                    HealthStrobeActive = (instantStatus & 0x04) != 0; // Bit 2: Strobe Error (1 = Error)
                    HealthSupervisorMode = (instantStatus & 0x08) != 0; // Bit 3: Supervisor Error (1 = Error)
                    HealthFullAlert = (instantStatus & 0x20) != 0;    // Bit 5: Full Alert (1 = Pass)
                    HealthPartialAlert = (instantStatus & 0x40) != 0; // Bit 6: Partial Alert (1 = Pass)
                    HealthBiasDetected = (instantStatus & 0x80) == 0; // Bit 7: Bias (1 = Bias Line Good, 0 = Failure)

                    // Update voltages and temperatures
                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
                    
                    HealthAcVoltage = HealthAcOn ? 220.0 : 0.0;
                    HealthDynamicAc = HealthAcOn;
                    HealthSystemPowerUp = HealthAcOn || HealthDcVoltage >= 22.0;
                    
                    if (cabTemp > 100)
                    {
                        HealthTemperature = (double)(cabTemp - 100);
                    }
                });
            }
        }

        private void OnActiveStatusReceived(string addressCode, byte activeCmd, byte acVolts, byte dcVolts, byte activeStatus, byte cabTemp, byte outTemp)
        {
            if (SelectedSiren == null) return;
            string targetAddr = (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0');
            string rcvAddr = addressCode.PadLeft(4, '0');

            if (targetAddr == rcvAddr)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (dcVolts > 0 && dcVolts != 128)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
                    
                    // Update AC voltage based on active AC status and value
                    if (acVolts > 0)
                    {
                        HealthAcVoltage = (double)acVolts;
                    }

                    if (cabTemp > 100)
                    {
                        HealthTemperature = (double)(cabTemp - 100);
                    }

                    HealthDynamicAc = HealthAcVoltage > 0;
                    HealthSystemPowerUp = HealthDynamicAc || HealthDcVoltage >= 22.0;

                    // Decode 3FH (Active Status) according to C2030 Manual Page 12
                    HealthFullAlert = (activeStatus & 0x01) != 0;      // Bit 0: stored FULL data (0 = fail, 1 = pass)
                    HealthPartialAlert = (activeStatus & 0x02) != 0;   // Bit 1: stored PARTIAL data (0 = fail, 1 = pass)
                    HealthBiasDetected = (activeStatus & 0x04) != 0;   // Bit 2: Tone Gen BIAS state (0 = inactive, 1 = active)
                    HealthIntrusion = (activeStatus & 0x08) != 0;      // Bit 3: Intrusion state (0 = door closed, 1 = door open)
                    HealthStrobeActive = (activeStatus & 0x10) != 0;   // Bit 4: Strobevisor error (0 = no error, 1 = error)
                    HealthSupervisorMode = (activeStatus & 0x40) != 0; // Bit 6: Supervisor error (0 = no error, 1 = error)
                    // Note: Bit 5 (stored Rotor data) indicates whether rotor is oscillating (1) or stationary (0).
                    // We do not set HealthRotorActive here, as 1FH Standard Status contains the actual Rotor health/incremented flag.
                });
            }
        }

        private void OnStandardStatusReceived(string addressCode, byte statusByte, byte dcVolts, byte cabTemp, byte outTemp)
        {
            if (SelectedSiren == null) return;
            string targetAddr = (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0');
            string rcvAddr = addressCode.PadLeft(4, '0');

            if (targetAddr == rcvAddr)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // 1FH Standard Status Request gives us the generic status bits
                    HealthSirenOn = (statusByte & 0x10) != 0;      // Bit 4: Siren On
                    HealthSystemArmed = (statusByte & 0x20) != 0;  // Bit 5: System Armed
                    
                    // Decoded from page 11
                    HealthFullAlert = (statusByte & 0x01) != 0;     // Bit 0: Full (1 = all pass, 0 = fail)
                    HealthPartialAlert = (statusByte & 0x02) != 0;  // Bit 1: Partial (1 = 1+ pass, 0 = fail)
                    HealthRotorActive = (statusByte & 0x04) != 0;   // Bit 2: Rotor (1 = incremented/OK, 0 = fail)
                    HealthStoredAc = (statusByte & 0x08) != 0;      // Bit 3: Stored AC (1 = AC on during tone, 0 = off)
                    HealthAcOn = (statusByte & 0x80) != 0;          // Bit 7: Dynamic AC (1 = AC volts on, 0 = off)
                    HealthDynamicAc = (statusByte & 0x80) != 0;     // Bit 7: Dynamic AC

                    HealthSystemPowerUp = (statusByte & 0x40) != 0 || HealthAcOn || HealthDcVoltage >= 22.0; // Bit 6: System Power Up or general power check

                    // Update voltages and temperatures
                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
                    if (cabTemp > 100)
                    {
                        HealthTemperature = (double)(cabTemp - 100);
                    }
                });
            }
        }

        private void OnBatteryAcReceived(string addressCode, byte dcVolts, byte acVolts)
        {
            if (SelectedSiren == null) return;
            string targetAddr = (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0');
            string rcvAddr = addressCode.PadLeft(4, '0');

            if (targetAddr == rcvAddr)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // Update DC voltage if valid (greater than 0 and not the 128 placeholder)
                    if (dcVolts > 0 && dcVolts != 128)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }

                    // Update AC voltage based on AC-on status bit and measured AC
                    if (HealthAcOn)
                    {
                        int strippedAc = acVolts & 0x7F;
                        HealthAcVoltage = strippedAc > 0 ? (double)strippedAc : 220.0;
                    }
                    else
                    {
                        HealthAcVoltage = 0.0;
                    }
                    HealthDynamicAc = HealthAcVoltage > 0;
                    HealthSystemPowerUp = HealthAcOn || HealthDcVoltage >= 22.0;
                });
            }
        }

        private void OnBatteryTempReceived(string addressCode, byte dcVolts, byte cabTemp)
        {
            if (SelectedSiren == null) return;
            string targetAddr = (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0');
            string rcvAddr = addressCode.PadLeft(4, '0');

            if (targetAddr == rcvAddr)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // Update DC voltage if valid
                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }

                    // Update temperature if valid
                    if (cabTemp > 100)
                    {
                        HealthTemperature = (double)(cabTemp - 100);
                    }
                    HealthSystemPowerUp = HealthAcOn || HealthDcVoltage >= 22.0;
                });
            }
        }

        private void OnWeatherReceived(string addressCode, byte outTemp, byte windDir, byte windSpd, byte rain)
        {
            // Note: Ready for future UI bindings if weather data is added to the Map UI
        }

        private void OnComprehensiveTempReceived(string addressCode, byte cabTemp, byte outTemp, byte lowPeak, byte highPeak)
        {
            if (SelectedSiren == null) return;
            string targetAddr = (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0');
            string rcvAddr = addressCode.PadLeft(4, '0');

            if (targetAddr == rcvAddr)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // 2BH Temperature Command provides Cabinet Temperature as the first byte
                    HealthTemperature = cabTemp > 100 ? (double)(cabTemp - 100) : 0.0;
                });
            }
        }

        public async Task<string> GetSirensJsonAsync()
        {
            var list = Sirens.Select(s => new
            {
                id = s.Id.ToString(),
                name = s.Name,
                lat = s.Lat,
                lng = s.Lng,
                status = s.Status,
                groupName = s.GroupName,
                subtext = s.Subtext,
                ipAddress = s.IpAddress,
                batteryVoltage = s.BatteryVoltage,
                hasACPower = s.HasACPower
            }).ToList();
            return System.Text.Json.JsonSerializer.Serialize(list);
        }

        public string GetZonesJson()
        {
            return "[]";
        }

        public string GetCommandsJson()
        {
            var list = CommandCards.Select(c => new
            {
                id = c.Id.ToString(),
                name = c.Name,
                duration = c.Duration
            }).ToList();
            return System.Text.Json.JsonSerializer.Serialize(list);
        }

        private byte[] BuildUnitFrame(SirenDeviceDto s, byte commandHex)
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

        private void OnSirenStatusChanged(string sirenName, string status)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                var siren = Sirens.FirstOrDefault(s => s.Name == sirenName);
                if (siren != null)
                {
                    siren.Status = status.ToUpper();
                    
                    var cache = SirenCommunicationService.Instance.GetCacheItemByAddressOrSource(sirenName);
                    if (cache != null)
                    {
                        siren.IsTcpOnline = cache.IsTcpOnline;
                        siren.IsSerialOnline = cache.IsSerialOnline;
                    }
                    
                    // Update Zone online/offline counters dynamically
                    foreach (var z in Zones)
                    {
                        z.OnlineDevices = Sirens.Count(s => s.GroupId == z.Id && (s.Status.ToUpper() == "ONLINE" || s.Status.ToUpper() == "WARNING"));
                    }

                    // Notify view to refresh map pins
                    SirensDataChanged?.Invoke();
                }
            });
        }

        private async Task LoadStationStatusesAsync()
        {
            try
            {
                var list = new List<StationStatusDto>();
                using (var connection = new MySqlConnection(ConnStr))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT Id, Name, Type, IpAddress, Status FROM StationStatuses";
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
                                Status = reader.GetString(4)
                            });
                        }
                    }
                }

                // Update the collection on UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    foreach (var item in list)
                    {
                        var existing = StationStatuses.FirstOrDefault(x => x.Name == item.Name);
                        if (existing != null)
                        {
                            existing.Status = item.Status;
                            existing.IpAddress = item.IpAddress;
                        }
                        else
                        {
                            StationStatuses.Add(item);
                        }
                    }
                });
            }
            catch
            {
                // Silence DB errors during polling
            }
        }
    }
}
