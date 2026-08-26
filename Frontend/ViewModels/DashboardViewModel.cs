using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Stores;
using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace Keemya.Frontend.ViewModels
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly NavigationStore _navigationStore;

        [ObservableProperty]
        private int totalZones = 0;

        [ObservableProperty]
        private int activeAlarms = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OnlineDevicesText))]
        private int onlineDevicesCount = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OnlineDevicesText))]
        private int totalDevicesCount = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SystemUptimeText))]
        [NotifyPropertyChangedFor(nameof(SystemUptimeStatusText))]
        private double systemUptimePercentage = 0.0;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SystemUptimeText))]
        [NotifyPropertyChangedFor(nameof(OnlineDevicesText))]
        [NotifyPropertyChangedFor(nameof(SystemUptimeStatusText))]
        private bool isPinging = true;
        
        public string SystemUptimeText => IsPinging ? "Scanning..." : $"{SystemUptimePercentage:0.#}%";
        public string OnlineDevicesText => IsPinging ? $"Pinging..." : $"{OnlineDevicesCount}/{TotalDevicesCount}";
        
        public string SystemUptimeStatusText 
        {
            get
            {
                if (IsPinging) return "Checking...";
                if (SystemUptimePercentage >= 90) return "Excellent";
                if (SystemUptimePercentage >= 60) return "Good";
                return "Needs Attention";
            }
        }

        [ObservableProperty] private bool isMapVisible = false;
        [ObservableProperty] private bool isCommandCenterVisible = false;
        [ObservableProperty] private bool isSirenMgmtVisible = false;
        [ObservableProperty] private bool isUserMgmtVisible = false;
        [ObservableProperty] private bool isNotificationsVisible = false;
        [ObservableProperty] private bool isAuditLogsVisible = false;
        [ObservableProperty] private bool isProfileVisible = false;
        [ObservableProperty] private bool isServiceMgmtVisible = false;

        public DashboardViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            InitializeRolePermissions();
            _ = LoadStatsAsync();
        }

        private void InitializeRolePermissions()
        {
            string role = (Session.Role ?? "Admin").ToUpper();
            
            // Map: Admin, Operator, Service
            IsMapVisible = true; 
            
            // CommandCenter: Admin, Operator
            IsCommandCenterVisible = role == "ADMIN" || role == "ADMINISTRATOR" || role == "OPERATOR";
            
            // SirenManagement: Admin
            IsSirenMgmtVisible = role == "ADMIN" || role == "ADMINISTRATOR";
            
            // UserManagement: Admin
            IsUserMgmtVisible = role == "ADMIN" || role == "ADMINISTRATOR";
            
            // Notifications: Admin, Operator, Service
            IsNotificationsVisible = true;
            
            // AuditLogs: Admin, Service
            IsAuditLogsVisible = role == "ADMIN" || role == "ADMINISTRATOR" || role == "SERVICE";
            
            // Profile: Admin, Operator, Service
            IsProfileVisible = true;
            
            // ServiceManagement: Admin, Service
            IsServiceMgmtVisible = role == "ADMIN" || role == "ADMINISTRATOR" || role == "SERVICE";
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                IsPinging = true;

                // Subscribe to real-time status changed events
                Keemya.Frontend.Services.SirenCommunicationService.Instance.SirenStatusChanged += OnSirenStatusChanged;

                string connStr = AppConfig.ConnectionString;
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                // 1. Total Zones
                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM SirenGroups", conn))
                {
                    TotalZones = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // 2. Load and calculate from in-memory cache
                UpdateCountersFromCache();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading dashboard stats: " + ex.Message);
            }
            finally
            {
                IsPinging = false;
            }
        }

        private void UpdateCountersFromCache()
        {
            var cachedSirens = Keemya.Frontend.Services.SirenCommunicationService.Instance.GetCachedStatuses();
            
            if (cachedSirens.Count == 0)
            {
                // Fallback / initial load directly from DB if cache not populated yet
                try
                {
                    string connStr = AppConfig.ConnectionString;
                    using var conn = new MySqlConnection(connStr);
                    conn.Open();
                    using var cmd = new MySqlCommand("SELECT Status FROM SirenDevices", conn);
                    using var reader = cmd.ExecuteReader();
                    int total = 0;
                    int online = 0;
                    int alarms = 0;
                    while (reader.Read())
                    {
                        total++;
                        string status = reader.IsDBNull(0) ? "OFFLINE" : reader.GetString(0).ToUpper();
                        if (status == "ONLINE" || status == "WARNING") online++;
                        if (status == "WARNING" || status == "DANGER") alarms++;
                    }

                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        TotalDevicesCount = total;
                        OnlineDevicesCount = online;
                        ActiveAlarms = alarms;
                        if (total > 0)
                        {
                            SystemUptimePercentage = Math.Round(((double)online / total) * 100, 1);
                        }
                    });
                    return;
                }
                catch
                {
                    // Ignore and proceed
                }
            }

            int totalCount = cachedSirens.Count;
            int onlineCount = 0;
            int alarmCount = 0;

            foreach (var s in cachedSirens)
            {
                if (s.IsOnline || s.IsSerialOnline || s.IsTcpOnline || s.LastKnownStatus == "ONLINE" || s.LastKnownStatus == "WARNING") 
                    onlineCount++;
                if (s.HasAlarm) 
                    alarmCount++;
            }

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                TotalDevicesCount = totalCount;
                OnlineDevicesCount = onlineCount;
                ActiveAlarms = alarmCount;
                if (totalCount > 0)
                {
                    SystemUptimePercentage = Math.Round(((double)onlineCount / totalCount) * 100, 1);
                }
                else
                {
                    SystemUptimePercentage = 0.0;
                }
            });
        }

        private void OnSirenStatusChanged(string sirenName, string status)
        {
            UpdateCountersFromCache();
        }

        public void Dispose()
        {
            Keemya.Frontend.Services.SirenCommunicationService.Instance.SirenStatusChanged -= OnSirenStatusChanged;
        }

        [RelayCommand]
        private void NavigateToUserManagement()
        {
            if (IsUserMgmtVisible)
                _navigationStore.CurrentViewModel = new UserManagementViewModel(_navigationStore);
        }



        [RelayCommand]
        private void NavigateToProfile()
        {
            if (IsProfileVisible)
                _navigationStore.CurrentViewModel = new ProfileViewModel(_navigationStore);
        }

        [RelayCommand]
        private void NavigateToSirenManagement()
        {
            if (IsSirenMgmtVisible)
                _navigationStore.CurrentViewModel = new SirenManagementViewModel(_navigationStore);
        }


        [RelayCommand]
        private void NavigateToCommandCenter()
        {
            if (IsCommandCenterVisible)
                _navigationStore.CurrentViewModel = new CommandCenterViewModel(_navigationStore);
        }

        [RelayCommand]
        private void NavigateToMap()
        {
            if (IsMapVisible)
                _navigationStore.CurrentViewModel = new MapViewModel(_navigationStore);
        }

        [RelayCommand]
        private void NavigateToAuditLogs()
        {
            if (IsAuditLogsVisible)
                _navigationStore.CurrentViewModel = new AuditLogsViewModel(_navigationStore);
        }

        [RelayCommand]
        private void NavigateToNotifications()
        {
            if (IsNotificationsVisible)
                _navigationStore.CurrentViewModel = new NotificationsViewModel(_navigationStore);
        }

        [RelayCommand]
        private void NavigateToServiceManagement()
        {
            if (IsServiceMgmtVisible)
                _navigationStore.CurrentViewModel = new ServiceManagementViewModel(_navigationStore);
        }
    }
}

