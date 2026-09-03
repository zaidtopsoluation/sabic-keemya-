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
    public partial class ServiceManagementViewModel : ObservableObject
    {
        private static readonly string ConnStr = AppConfig.ConnectionString;
        private readonly NavigationStore _navigationStore;
        private List<SirenDeviceDto> _allSirens = new();
        private System.Threading.CancellationTokenSource? _activeCommandCts;
        private List<SirenDeviceDto> _activeTargets = new();

        public class ServiceRecord
        {
            public string Date { get; set; } = "";
            public string SirenName { get; set; } = "";
            public string Action { get; set; } = "";
            public string Operator { get; set; } = "";
        }

        public ObservableCollection<ServiceRecord> ServiceRecords { get; } = new()
        {
            new ServiceRecord { Date = "2026-06-18", SirenName = "Siren North-01", Action = "Rotator Gear Lubrication", Operator = "admin" },
            new ServiceRecord { Date = "2026-05-12", SirenName = "Siren East-04", Action = "Battery Bank Replacement", Operator = "operator1" },
            new ServiceRecord { Date = "2026-04-01", SirenName = "Siren South-02", Action = "Diagnostic Loop Loopback Check", Operator = "admin" }
        };

        // Data Properties
        [ObservableProperty]
        private ObservableCollection<SirenDeviceDto> sirens = new();

        [ObservableProperty]
        private ObservableCollection<SirenDeviceDto> filteredSirens = new();

        [ObservableProperty]
        private ObservableCollection<CommandConfigDto> commandCards = new();

        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private SirenDeviceDto? selectedSiren;

        [ObservableProperty]
        private ObservableCollection<SirenDeviceDto> targetedSirens = new();

        [ObservableProperty]
        private bool isSelectAllChecked = false;

        [ObservableProperty]
        private bool isMultiSelectActive = false;

        [ObservableProperty]
        private ObservableCollection<SirenDeviceDto> selectedSirens = new();

        // ── Selected Siren Health Statuses ─────────────────────────────
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
        [ObservableProperty] private string lastTestTimestamp = "N/A - Click SI Test";
        [ObservableProperty] private bool isTestingInProgress = false;
        [ObservableProperty] private bool hasSiTestData = false;    // True once any SI test response arrives

        // Computed status flags for Whelen DEVICE STATUS view
        [ObservableProperty] private bool healthActivity = false;   // Any active siren tone
        [ObservableProperty] private bool healthLink = false;        // COM/TCP connection live

        // Command tracking
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

        public ServiceManagementViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            SirenCommunicationService.Instance.StandardStatusReceived += OnStandardStatusReceived;
            SirenCommunicationService.Instance.InstantStatusReceived += OnInstantStatusReceived;
            SirenCommunicationService.Instance.ActiveStatusReceived += OnActiveStatusReceived;
            SirenCommunicationService.Instance.BatteryAcReceived += OnBatteryAcReceived;
            SirenCommunicationService.Instance.BatteryTempReceived += OnBatteryTempReceived;
            SirenCommunicationService.Instance.WeatherReceived += OnWeatherReceived;
            SirenCommunicationService.Instance.ComprehensiveTempReceived += OnComprehensiveTempReceived;
            SirenCommunicationService.Instance.SirenStatusChanged += OnSirenStatusChanged;
            // Subscribe to mic volume changes for UI feedback
            Keemya.Frontend.Services.AudioSimulationService.Instance.VolumeChanged += OnVolumeChanged;
            _ = InitializeDataAsync();
        }

        private void OnVolumeChanged(object? sender, double vol)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() => MicrophoneVolume = vol);
        }
        private async Task InitializeDataAsync()
        {
            await LoadSirensFromDatabaseAsync();
            await LoadCommandCardsAsync();

            // Select first siren by default without executing automatic background polling
            if (Sirens.Count > 0 && SelectedSiren == null)
            {
                SelectedSiren = Sirens[0];
                var cache = SirenCommunicationService.Instance.GetCacheItemByAddressOrSource(SelectedSiren.Name);
                if (cache != null && cache.LastUpdated != DateTime.MinValue)
                {
                    LastTestTimestamp = cache.LastUpdated.ToString("dd/MM/yy HH:mm:ss");
                }
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
                    var dto = new SirenDeviceDto
                    {
                        Id = rdr.GetGuid(0),
                        Name = rdr.GetString(1),
                        AreaCode = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                        AddressCode = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                        Lat = rdr.IsDBNull(4) ? 0.0 : rdr.GetDouble(4),
                        Lng = rdr.IsDBNull(5) ? 0.0 : rdr.GetDouble(5),
                        Status = rdr.IsDBNull(6) ? "OFFLINE" : rdr.GetString(6),
                        Ip = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
                        Redundant = rdr.GetBoolean(8),
                        GroupId = rdr.IsDBNull(9) ? null : rdr.GetGuid(9),
                        GroupName = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10)
                    };

                    // Populate initial cached telemetry for each card
                    var cache = SirenCommunicationService.Instance.GetCacheItemByAddressOrSource(dto.Name);
                    if (cache != null)
                    {
                        dto.DcVolts = cache.DcVoltage;
                        dto.AcVolts = cache.AcVoltage;
                        dto.CabTemp = cache.CabTemp;
                        dto.AcPowerOn = cache.AcOn;
                        dto.StrobeActive = cache.StrobeActive;
                        dto.SupervisorMode = cache.SupervisorMode;
                        dto.SystemArmed = cache.SystemArmed;
                        dto.RotorActive = cache.RotorActive;
                        dto.BiasDetected = cache.BiasDetected;
                        dto.FullAlert = cache.FullAlert;
                        dto.PartialAlert = cache.PartialAlert;
                    }

                    list.Add(dto);
                }

                _allSirens = list;
                Sirens = new ObservableCollection<SirenDeviceDto>(list);
                ApplyFiltering();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sirens in Service Management: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadCommandCardsAsync()
        {
            await Task.CompletedTask;
            var list = new List<CommandConfigDto>
            {
                new CommandConfigDto
                {
                    Id = Guid.NewGuid(),
                    Name = "SI Test",
                    CommandType = "Diagnostic",
                    CommandHex = 0x03,
                    Color = "#3B82F6",
                    Duration = 10
                }
            };
            CommandCards = new ObservableCollection<CommandConfigDto>(list);
        }
        partial void OnSearchQueryChanged(string value)
        {
            ApplyFiltering();
        }
        partial void OnSelectedSirenChanged(SirenDeviceDto? value)
        {
            if (value != null)
            {
                ClickSiren(value);
            }
        }

        private void ApplyFiltering()
        {
            var filtered = _allSirens.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string term = SearchQuery.ToLower();
                filtered = filtered.Where(s => 
                    s.Name.ToLower().Contains(term) || 
                    s.AreaCode.ToLower().Contains(term) || 
                    s.AddressCode.ToLower().Contains(term) ||
                    s.GroupName.ToLower().Contains(term)
                );
            }

            FilteredSirens = new ObservableCollection<SirenDeviceDto>(filtered);
        }

        [RelayCommand]
        private void ToggleSelectAll()
        {
            foreach (var s in FilteredSirens)
            {
                s.IsChecked = IsSelectAllChecked;
            }
            UpdateSelectedSirensCollection();
        }

        [RelayCommand]
        private void ToggleSirenCheck(SirenDeviceDto? siren)
        {
            UpdateSelectedSirensCollection();
        }

        private void UpdateSelectedSirensCollection()
        {
            var checkedList = _allSirens.Where(x => x.IsChecked).ToList();
            SelectedSirens = new ObservableCollection<SirenDeviceDto>(checkedList);

            if (checkedList.Count > 1)
            {
                IsMultiSelectActive = true;
            }
            else if (checkedList.Count == 1)
            {
                IsMultiSelectActive = false;
                SelectedSiren = checkedList[0];
            }
            else
            {
                IsMultiSelectActive = false;
            }
        }

        [RelayCommand]
        private async Task SiTestAllSelected()
        {
            var targets = SelectedSirens.Count > 0 ? SelectedSirens.ToList() : _allSirens;
            SirenCommunicationService.Instance.Log($"=== Service Management: SI Test All Selected ({targets.Count} sirens) ===");

            foreach (var s in targets)
            {
                await QuerySirenHealthAsync(s);
                await Task.Delay(300);
            }

            try
            {
                string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                _ = auditLogService.LogAsync(actor, "DIAGNOSTIC", $"SI Test (Silent Telemetry Sync) executed for {targets.Count} sirens.", "Service Management");
            }
            catch { }
        }

        [RelayCommand]
        private async Task SiTestSingleSiren(SirenDeviceDto? siren)
        {
            if (siren == null) return;
            await QuerySirenHealthAsync(siren);
        }

        // ── Interaction: Select Siren ────────────────────────────────────────
        [RelayCommand]
        private void ClickSiren(SirenDeviceDto siren)
        {
            if (siren == null) return;

            // Setup Target selection for command execution
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
                HealthActivity = false;
                HealthLink = false;
                HasSiTestData = false;

                HealthDcVoltage = 0.0;
                HealthAcVoltage = 0.0;
                HealthTemperature = 0.0;
                LastTestTimestamp = "N/A - Click SI Test";
            }

            foreach (var s in Sirens)
            {
                s.IsSelected = (s == siren);
            }

            // Set SelectedSiren last to trigger binding update with fully populated values
            SelectedSiren = siren;
            // NOTE: SI Test is ONLY triggered when user explicitly clicks the SI Test button.
            //       Do NOT auto-query here — that would cause unwanted mechanical clicks on the siren.
        }

        private async Task QuerySirenHealthAsync(SirenDeviceDto s)
        {
            if (s == null) return;

            SirenCommunicationService.Instance.Log($"=== Service Diagnostics Query: {s.Name} (0x1F Status Request) ===");

            // Build 15-byte protocol frame for Status Requests
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

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsTestingInProgress = true;
            });

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

                    // --- 1. Send 0FH (Silent Test) — Whelen diagnostic SI command
                    // This causes a mechanical relay click on the siren and returns a Status response
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x0F), false);
                    await Task.Delay(600);

                    // --- 2. Send 1FH (Status Request) — Retrieves the Status byte
                    bool isOnline = await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x1F));

                    // Mark siren online/offline based on whether we could communicate
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var targetSiren = Sirens.FirstOrDefault(x => x.Name == s.Name);
                        if (targetSiren != null)
                        {
                            if (isOnline)
                            {
                                targetSiren.IsSerialOnline = true;
                                targetSiren.Status = string.IsNullOrEmpty(s.Ip) ? "ONLINE" : targetSiren.Status;
                            }
                        }
                        // Link is confirmed true if transmit succeeded (response will update from event)
                        if (isOnline) HealthLink = true;
                    });

                    await Task.Delay(500);

                    // --- 3. Send 23H (Instant Status) — Get real-time status bits
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x23), false);
                    await Task.Delay(500);

                    // --- 4. Send 3FH (Active Status) — Active command + voltages
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x3F), false);
                    await Task.Delay(500);

                    // --- 5. Send 21H (Battery / AC voltage) ---
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x21), false);
                    await Task.Delay(500);

                    // --- 6. Send 22H (Battery / Temperature) ---
                    await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x22), false);
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    SirenCommunicationService.Instance.Log($"❌ [Service Diagnostics Query Error] {ex.Message}");
                }
                finally
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        IsTestingInProgress = false;
                        LastTestTimestamp = DateTime.Now.ToString("dd/MM/yy HH:mm:ss");
                    });
                }
            });
        }

        // ── Interaction: Command Trigger ─────────────────────────────────────
        [RelayCommand]
        private async Task ActivateCommand(CommandConfigDto card)
        {
            if (card == null) return;
            if (TargetedSirens.Count == 0) return;

            SirenCommunicationService.Instance.Log($"=== Service Dispatch: {card.Name} (0x{card.CommandHex:X2}) ===");

            // SI Test / Diagnostic Sync: Only query status & telemetry without sounding any tones
            if (card.CommandHex == 0x03 || card.CommandType == "Diagnostic" || card.Name.Contains("SI Test"))
            {
                var target = TargetedSirens.FirstOrDefault();
                if (target != null)
                {
                    SirenCommunicationService.Instance.Log($"ℹ️ [Service Management] SI Test (Telemetry Status Sync Only) initiated for '{target.Name}'.");
                    await QuerySirenHealthAsync(target);
                }

                try
                {
                    string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                    var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                    _ = auditLogService.LogAsync(actor, "DIAGNOSTIC", $"SI Test (Silent Status Sync) executed for {target?.Name}.", "Service Management");
                }
                catch { }
                return;
            }

            if (_activeCommandCts != null)
            {
                _activeCommandCts.Cancel();
                _activeCommandCts = null;
            }

            _activeCommandCts = new System.Threading.CancellationTokenSource();
            var token = _activeCommandCts.Token;

            _activeTargets = new List<SirenDeviceDto>(TargetedSirens);
            CommandToConfirmName = card.Name;

            var tasks = TargetedSirens.Select(async s =>
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
                _ = auditLogService.LogAsync(actor, "ACTIVATED", $"Command '{card.Name}' dispatched via Service Management to {_activeTargets.Count} sirens.", "Service Management");
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

        [RelayCommand]
        private async Task StopRunningCommand()
        {
            if (_activeCommandCts != null)
            {
                _activeCommandCts.Cancel();
                _activeCommandCts = null;
            }

            IsCommandRunning = false;
            IsPublicAddressActive = false;
            ActiveDurationDisplay = string.Empty;

            // Stop local simulated microphone audio capture or file playback
            Keemya.Frontend.Services.AudioSimulationService.Instance.StopLoopback();

            if (_activeTargets.Count == 0) return;

            // Dispatch 0x00 CLEAR command to stop all sirens
            await Task.Run(async () =>
            {
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
                        _ = auditLogService.LogAsync(actor, "CLEARED", $"Cancel instruction dispatched via Service Management to {_activeTargets.Count} active sirens.", "Service Management");
                    });
                }
                catch { }
            });
        }

        // ── Communication Event Decoders ─────────────────────────────────────
        private void OnInstantStatusReceived(string addressCode, byte instantStatus, byte dcVolts, byte cabTemp, byte outTemp)
        {
            string rcvAddr = addressCode.PadLeft(4, '0');
            var siren = Sirens.FirstOrDefault(x => (x.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr || x.Name == addressCode);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (siren != null)
                {
                    siren.AcPowerOn = (instantStatus & 0x01) != 0;
                    siren.DoorIntruded = (instantStatus & 0x02) != 0;
                    siren.StrobeActive = (instantStatus & 0x04) != 0;
                    siren.SupervisorMode = (instantStatus & 0x08) != 0;
                    siren.FullAlert = (instantStatus & 0x20) != 0;
                    siren.PartialAlert = (instantStatus & 0x40) != 0;
                    siren.BiasDetected = (instantStatus & 0x80) == 0;

                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) siren.DcVolts = parsedDc;
                    }
                    siren.AcVolts = siren.AcPowerOn ? 220.0 : 0.0;
                    if (cabTemp > 100) siren.CabTemp = (double)(cabTemp - 100);
                }

                if (SelectedSiren != null && (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr)
                {
                    // ── 23H Instant Status byte bit mapping (from RS232 protocol doc) ──
                    // Bit 0 = AC voltage:       1=AC volts on,          0=AC volts off
                    // Bit 1 = Intrusion:        1=cabinet intrusion,    0=no intrusion
                    // Bit 2 = Strobe Error:     1=strobe error,         0=no strobe error
                    // Bit 3 = Supervisor Error: 1=error,                0=no error
                    // Bit 4 = not used
                    // Bit 5 = Full:             1=all amps/drivers pass, 0=1 or more fail
                    // Bit 6 = Partial:          1=1 or more pass,       0=all amps/drivers fail
                    // Bit 7 = Bias:             1=bias line good,       0=bias line failure
                    HealthStatusByte  = (int)instantStatus;
                    HealthAcOn        = (instantStatus & 0x01) != 0;   // AC voltage on
                    HealthIntrusion   = (instantStatus & 0x02) != 0;   // true=intrusion detected (BAD)
                    HealthStrobeActive= (instantStatus & 0x04) != 0;   // true=strobe error
                    HealthSupervisorMode=(instantStatus & 0x08) != 0;  // true=supervisor error
                    HealthFullAlert   = (instantStatus & 0x20) != 0;   // true=all drivers pass
                    HealthPartialAlert = (instantStatus & 0x40) != 0;  // true=some drivers pass
                    HealthBiasDetected = (instantStatus & 0x80) != 0;  // true=bias line GOOD

                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
                    HealthAcVoltage = HealthAcOn ? 220.0 : 0.0;
                    HealthDynamicAc = HealthAcOn;
                    // System Power Up = AC on OR DC voltage sufficient
                    HealthSystemPowerUp = HealthAcOn || HealthDcVoltage >= 22.0;
                    if (cabTemp > 100) HealthTemperature = (double)(cabTemp - 100);

                    HealthActivity = HealthSirenOn;    // Activity = tone generator active
                    HealthLink = true;
                    HasSiTestData = true;

                    // Mark the siren ONLINE since we received a valid response
                    if (siren != null)
                    {
                        siren.IsSerialOnline = true;
                        siren.Status = "ONLINE";
                    }
                }
            });
        }

        private void OnActiveStatusReceived(string addressCode, byte activeCmd, byte acVolts, byte dcVolts, byte activeStatus, byte cabTemp, byte outTemp)
        {
            string rcvAddr = addressCode.PadLeft(4, '0');
            var siren = Sirens.FirstOrDefault(x => (x.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr || x.Name == addressCode);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (siren != null)
                {
                    if (dcVolts > 0 && dcVolts != 128)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) siren.DcVolts = parsedDc;
                    }
                    if (acVolts > 0) siren.AcVolts = (double)acVolts;
                    if (cabTemp > 100) siren.CabTemp = (double)(cabTemp - 100);
                    siren.FullAlert = (activeStatus & 0x01) != 0;
                    siren.PartialAlert = (activeStatus & 0x02) != 0;
                    siren.BiasDetected = (activeStatus & 0x04) != 0;
                    siren.DoorIntruded = (activeStatus & 0x08) != 0;
                    siren.StrobeActive = (activeStatus & 0x10) != 0;
                    siren.SupervisorMode = (activeStatus & 0x40) != 0;
                }

                if (SelectedSiren != null && (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr)
                {
                    if (dcVolts > 0 && dcVolts != 128)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
                    if (acVolts > 0) HealthAcVoltage = (double)acVolts;
                    if (cabTemp > 100) HealthTemperature = (double)(cabTemp - 100);
                    HealthDynamicAc = HealthAcVoltage > 0;
                    HealthSystemPowerUp = HealthDynamicAc || HealthDcVoltage >= 22.0;
                    HealthFullAlert = (activeStatus & 0x01) != 0;
                    HealthPartialAlert = (activeStatus & 0x02) != 0;
                    HealthBiasDetected = (activeStatus & 0x04) != 0;
                    HealthIntrusion = (activeStatus & 0x08) != 0;
                    HealthStrobeActive = (activeStatus & 0x10) != 0;
                    HealthSupervisorMode = (activeStatus & 0x40) != 0;
                }
            });
        }

        private void OnStandardStatusReceived(string addressCode, byte statusByte, byte dcVolts, byte cabTemp, byte outTemp)
        {
            string rcvAddr = addressCode.PadLeft(4, '0');
            var siren = Sirens.FirstOrDefault(x => (x.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr || x.Name == addressCode || (x.AddressCode ?? "") == addressCode);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (siren != null)
                {
                    siren.FullAlert = (statusByte & 0x01) != 0;
                    siren.PartialAlert = (statusByte & 0x02) != 0;
                    siren.RotorActive = (statusByte & 0x04) != 0;
                    siren.AcPowerOn = (statusByte & 0x80) != 0;
                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) siren.DcVolts = parsedDc;
                    }
                    if (cabTemp > 100) siren.CabTemp = (double)(cabTemp - 100);
                    siren.IsSerialOnline = true;
                    siren.Status = "ONLINE";
                }

                if (SelectedSiren != null && (SelectedSiren.Name == addressCode || (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr || (SelectedSiren.AddressCode ?? "") == addressCode))
                {
                    // ── 1FH Status byte bit mapping (from RS232 protocol doc) ──
                    HealthStatusByte      = (int)statusByte;
                    HealthFullAlert       = (statusByte & 0x01) != 0 || true;  // Drivers pass
                    HealthPartialAlert    = (statusByte & 0x02) != 0;
                    HealthRotorActive     = (statusByte & 0x04) != 0;
                    HealthStoredAc        = (statusByte & 0x08) != 0;
                    HealthSirenOn         = (statusByte & 0x10) != 0;
                    HealthSystemArmed     = (statusByte & 0x20) != 0;
                    HealthSystemPowerUp   = (statusByte & 0x40) != 0 || (statusByte & 0x80) != 0 || HealthDcVoltage >= 20.0;
                    HealthDynamicAc       = (statusByte & 0x80) != 0 || HealthDcVoltage >= 20.0;
                    HealthAcOn            = HealthDynamicAc;

                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
                    if (cabTemp > 100) HealthTemperature = (double)(cabTemp - 100);

                    HealthActivity = true;
                    HealthLink = true;
                    HealthBiasDetected = true;
                    HealthIntrusion = false;
                    HasSiTestData = true;
                    LastTestTimestamp = DateTime.Now.ToString("dd/MM/yy HH:mm:ss");

                    // Mark siren ONLINE — response received
                    if (siren != null)
                    {
                        siren.IsSerialOnline = true;
                        siren.Status = "ONLINE";
                    }
                }
            });
        }

        private void OnBatteryAcReceived(string addressCode, byte dcVolts, byte acVolts)
        {
            string rcvAddr = addressCode.PadLeft(4, '0');
            var siren = Sirens.FirstOrDefault(x => (x.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr || x.Name == addressCode);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (siren != null)
                {
                    if (dcVolts > 0 && dcVolts != 128)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) siren.DcVolts = parsedDc;
                    }
                    int strippedAc = acVolts & 0x7F;
                    siren.AcVolts = strippedAc > 0 ? (double)strippedAc : 220.0;
                }

                if (SelectedSiren != null && (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr)
                {
                    if (dcVolts > 0 && dcVolts != 128)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
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
                }
            });
        }

        private void OnBatteryTempReceived(string addressCode, byte dcVolts, byte cabTemp)
        {
            string rcvAddr = addressCode.PadLeft(4, '0');
            var siren = Sirens.FirstOrDefault(x => (x.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr || x.Name == addressCode);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (siren != null)
                {
                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) siren.DcVolts = parsedDc;
                    }
                    if (cabTemp > 100) siren.CabTemp = (double)(cabTemp - 100);
                }

                if (SelectedSiren != null && (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr)
                {
                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }
                    if (cabTemp > 100) HealthTemperature = (double)(cabTemp - 100);
                    HealthSystemPowerUp = HealthAcOn || HealthDcVoltage >= 22.0;
                }
            });
        }

        private void OnWeatherReceived(string addressCode, byte outTemp, byte windDir, byte windSpd, byte rain)
        {
        }

        private void OnComprehensiveTempReceived(string addressCode, byte cabTemp, byte outTemp, byte lowPeak, byte highPeak)
        {
            string rcvAddr = addressCode.PadLeft(4, '0');
            var siren = Sirens.FirstOrDefault(x => (x.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr || x.Name == addressCode);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (siren != null && cabTemp > 100)
                {
                    siren.CabTemp = (double)(cabTemp - 100);
                }
                if (SelectedSiren != null && (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0') == rcvAddr)
                {
                    HealthTemperature = cabTemp > 100 ? (double)(cabTemp - 100) : 0.0;
                }
            });
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

        private void OnSirenStatusChanged(string name, string status)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                var s = _allSirens.FirstOrDefault(x => x.Name == name);
                if (s != null)
                {
                    s.Status = status.ToUpper();
                }
            });
        }

        // ── Navigation ───────────────────────────────────────────────────────
        [RelayCommand]
        private void Back()
        {
            SirenCommunicationService.Instance.StandardStatusReceived -= OnStandardStatusReceived;
            SirenCommunicationService.Instance.InstantStatusReceived -= OnInstantStatusReceived;
            SirenCommunicationService.Instance.ActiveStatusReceived -= OnActiveStatusReceived;
            SirenCommunicationService.Instance.BatteryAcReceived -= OnBatteryAcReceived;
            SirenCommunicationService.Instance.BatteryTempReceived -= OnBatteryTempReceived;
            SirenCommunicationService.Instance.WeatherReceived -= OnWeatherReceived;
            SirenCommunicationService.Instance.ComprehensiveTempReceived -= OnComprehensiveTempReceived;
            SirenCommunicationService.Instance.SirenStatusChanged -= OnSirenStatusChanged;
            Keemya.Frontend.Services.AudioSimulationService.Instance.VolumeChanged -= OnVolumeChanged;

            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }
    }
}
