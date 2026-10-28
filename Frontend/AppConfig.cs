using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Keemya.Frontend
{
    public static class AppConfig
    {
        private static readonly object _syncLock = new();

        public static string PrimaryConnectionString { get; private set; } = "Server=localhost;Port=3306;Database=keemya;User=root;Password=root1234;";
        public static string SecondaryConnectionString { get; private set; } = "Server=localhost;Port=3306;Database=keemya;User=root;Password=root1234;";
        
        public static string PrimaryServerName { get; private set; } = "Server 1 (RCB)";
        public static string SecondaryServerName { get; private set; } = "Server 2 (PCB)";
        
        private static string _activeConnectionString = "Server=localhost;Port=3306;Database=keemya;User=root;Password=root1234;";
        public static string ConnectionString
        {
            get
            {
                lock (_syncLock)
                {
                    return _activeConnectionString;
                }
            }
            private set
            {
                lock (_syncLock)
                {
                    _activeConnectionString = value;
                }
            }
        }

        public static string ActiveServerName { get; private set; } = "Server 1 (RCB)";
        public static bool IsFailoverActive { get; private set; } = false;
        public static int FailoverTimeoutSeconds { get; private set; } = 2;

        public static string StationName { get; private set; } = "Admin ECC";
        public static string PttRelayPort { get; private set; } = "COM3";

        /// <summary>"Server" (owns the siren links) or "Dispatcher" (operator screen). Empty on installs made before this setting existed.</summary>
        public static string Role { get; private set; } = "";

        public static bool IsControllerStation => Role.Length > 0
            ? string.Equals(Role, "Server", StringComparison.OrdinalIgnoreCase)
            // Older installs have no Role: keep the original rule based on the station name.
            : string.Equals(StationName, "PCB-Controller", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(StationName, "RCB-Controller", StringComparison.OrdinalIgnoreCase);

        public static bool IsMasterDispatcher => IsControllerStation;

        public static event Action<string, bool>? ActiveServerChanged;

        static AppConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings))
                    {
                        if (connStrings.TryGetProperty("PrimaryConnection", out var primStr) &&
                            !string.IsNullOrWhiteSpace(primStr.GetString()))
                        {
                            PrimaryConnectionString = primStr.GetString()!;
                        }
                        else if (connStrings.TryGetProperty("DefaultConnection", out var defStr) &&
                                 !string.IsNullOrWhiteSpace(defStr.GetString()))
                        {
                            PrimaryConnectionString = defStr.GetString()!;
                        }

                        if (connStrings.TryGetProperty("SecondaryConnection", out var secStr) &&
                            !string.IsNullOrWhiteSpace(secStr.GetString()))
                        {
                            SecondaryConnectionString = secStr.GetString()!;
                        }
                        else
                        {
                            SecondaryConnectionString = PrimaryConnectionString;
                        }

                        _activeConnectionString = PrimaryConnectionString;
                    }

                    if (doc.RootElement.TryGetProperty("ServerNodes", out var nodesProp))
                    {
                        if (nodesProp.TryGetProperty("PrimaryName", out var pName) && !string.IsNullOrWhiteSpace(pName.GetString()))
                        {
                            PrimaryServerName = pName.GetString()!;
                        }
                        if (nodesProp.TryGetProperty("SecondaryName", out var sName) && !string.IsNullOrWhiteSpace(sName.GetString()))
                        {
                            SecondaryServerName = sName.GetString()!;
                        }
                        if (nodesProp.TryGetProperty("FailoverTimeoutSeconds", out var timeoutVal) && timeoutVal.TryGetInt32(out int t))
                        {
                            FailoverTimeoutSeconds = Math.Max(1, t);
                        }
                    }

                    ActiveServerName = PrimaryServerName;

                    if (doc.RootElement.TryGetProperty("StationName", out var stationNameProp))
                    {
                        var value = stationNameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            StationName = value;
                        }
                    }

                    if (doc.RootElement.TryGetProperty("Role", out var roleProp))
                    {
                        var value = roleProp.GetString()?.Trim();
                        if (string.Equals(value, "Server", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(value, "Dispatcher", StringComparison.OrdinalIgnoreCase))
                        {
                            Role = value!;
                        }
                    }

                    if (doc.RootElement.TryGetProperty("PttRelayPort", out var pttProp))
                    {
                        var value = pttProp.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            PttRelayPort = value;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to defaults
            }
        }

        public static void SwitchToSecondary(string? reason = null)
        {
            lock (_syncLock)
            {
                if (IsFailoverActive) return;
                _activeConnectionString = SecondaryConnectionString;
                IsFailoverActive = true;
                ActiveServerName = SecondaryServerName;
            }

            try
            {
                ActiveServerChanged?.Invoke(ActiveServerName, true);
            }
            catch { }
        }

        public static void SwitchToPrimary(string? reason = null)
        {
            lock (_syncLock)
            {
                if (!IsFailoverActive) return;
                _activeConnectionString = PrimaryConnectionString;
                IsFailoverActive = false;
                ActiveServerName = PrimaryServerName;
            }

            try
            {
                ActiveServerChanged?.Invoke(ActiveServerName, false);
            }
            catch { }
        }

        public static bool TestConnection(string connectionString, int timeoutSeconds = 2)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(connectionString)
                {
                    ConnectionTimeout = (uint)timeoutSeconds
                };
                using var conn = new MySqlConnection(builder.ConnectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> TestConnectionAsync(string connectionString, int timeoutSeconds = 2)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(connectionString)
                {
                    ConnectionTimeout = (uint)timeoutSeconds
                };
                using var conn = new MySqlConnection(builder.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureHealthyConnection()
        {
            if (string.Equals(PrimaryConnectionString, SecondaryConnectionString, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool currentOk = TestConnection(ConnectionString, FailoverTimeoutSeconds);
            if (!currentOk)
            {
                if (!IsFailoverActive)
                {
                    // Primary failed; test secondary
                    if (TestConnection(SecondaryConnectionString, FailoverTimeoutSeconds))
                    {
                        SwitchToSecondary("Primary connection failed health test");
                    }
                }
                else
                {
                    // Secondary failed; try returning to primary
                    if (TestConnection(PrimaryConnectionString, FailoverTimeoutSeconds))
                    {
                        SwitchToPrimary("Secondary connection failed, primary recovered");
                    }
                }
            }
            else if (IsFailoverActive)
            {
                // In failover mode: check if primary has recovered
                if (TestConnection(PrimaryConnectionString, FailoverTimeoutSeconds))
                {
                    SwitchToPrimary("Primary server recovered");
                }
            }
        }
    }
}
