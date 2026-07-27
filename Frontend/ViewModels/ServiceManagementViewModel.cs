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

            // Select first siren by default
            if (Sirens.Count > 0)
            {
                SelectedSiren = Sirens[0];
                ClickSiren(SelectedSiren);
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
                    list.Add(new SirenDeviceDto
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
                    });
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
            try
            {
                var list = new List<CommandConfigDto>();
                using var conn = new MySqlConnection(ConnStr);
                await conn.OpenAsync();

                const string sql = @"SELECT Id, Name, CommandType, CommandHex, Color, Duration
                                     FROM CommandConfigs
                                     WHERE IsSystemDefault = 0 AND IsEnabled = 1
                                     ORDER BY SortOrder, Name";

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
                        Color = rdr.IsDBNull(4) ? "Blue" : rdr.GetString(4),
                        Duration = rdr.GetInt32(5)
                    });
                }

                CommandCards = new ObservableCollection<CommandConfigDto>(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading commands in Service Management: {ex.Message}");
            }
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

                HealthDcVoltage = 0.0;
                HealthAcVoltage = 0.0;
                HealthTemperature = 0.0;
            }

            // Set SelectedSiren last to trigger binding update with fully populated values
            SelectedSiren = siren;

            // Trigger background C2030 status request query (15-byte protocol frame)
            _ = Task.Run(() => QuerySirenHealthAsync(siren));
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
                    bool isOnline = await SirenCommunicationService.Instance.ExecuteTransmitAsync(s.Name, s.Ip, s.Redundant, BuildFrame(0x23));
                    if (!isOnline)
                    {
                        SirenCommunicationService.Instance.Log($"Service Diagnostics: {s.Name} is unreachable.");
                        return;
                    }

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
                    SirenCommunicationService.Instance.Log($"❌ [Service Diagnostics Query Error] {ex.Message}");
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
            if (SelectedSiren == null) return;
            string targetAddr = (SelectedSiren.AddressCode ?? "0000").PadLeft(4, '0');
            string rcvAddr = addressCode.PadLeft(4, '0');

            if (targetAddr == rcvAddr)
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    HealthStatusByte = (int)instantStatus;

                    HealthAcOn = (instantStatus & 0x01) != 0;
                    HealthIntrusion = (instantStatus & 0x02) != 0;
                    HealthStrobeActive = (instantStatus & 0x04) != 0;
                    HealthSupervisorMode = (instantStatus & 0x08) != 0;
                    HealthFullAlert = (instantStatus & 0x20) != 0;
                    HealthPartialAlert = (instantStatus & 0x40) != 0;
                    HealthBiasDetected = (instantStatus & 0x80) == 0;

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
                    HealthSirenOn = (statusByte & 0x10) != 0;
                    HealthSystemArmed = (statusByte & 0x20) != 0;
                    
                    HealthFullAlert = (statusByte & 0x01) != 0;
                    HealthPartialAlert = (statusByte & 0x02) != 0;
                    HealthRotorActive = (statusByte & 0x04) != 0;
                    HealthStoredAc = (statusByte & 0x08) != 0;
                    HealthAcOn = (statusByte & 0x80) != 0;
                    HealthDynamicAc = (statusByte & 0x80) != 0;

                    HealthSystemPowerUp = (statusByte & 0x40) != 0 || HealthAcOn || HealthDcVoltage >= 22.0;

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
                    if (dcVolts > 0)
                    {
                        double parsedDc = Math.Round(dcVolts * (35.0 / 255.0), 1);
                        if (parsedDc > 5.0) HealthDcVoltage = parsedDc;
                    }

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
            // Ready for future weather bindings
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
                    HealthTemperature = cabTemp > 100 ? (double)(cabTemp - 100) : 0.0;
                });
            }
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
