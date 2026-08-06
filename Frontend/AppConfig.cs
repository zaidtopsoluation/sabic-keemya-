using System;
using System.IO;
using System.Text.Json;

namespace Keemya.Frontend
{
    public static class AppConfig
    {
        public static string ConnectionString { get; private set; } = "Server=localhost;Port=3306;Database=keemya;User=root;Password=root1234;";
        public static string StationName { get; private set; } = "Admin ECC";
        public static string PttRelayPort { get; private set; } = "COM3";

        static AppConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings) &&
                        connStrings.TryGetProperty("DefaultConnection", out var connStr))
                    {
                        var value = connStr.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            ConnectionString = value;
                        }
                    }

                    if (doc.RootElement.TryGetProperty("StationName", out var stationNameProp))
                    {
                        var value = stationNameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            StationName = value;
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
                // Fallback to default
            }
        }
    }
}
