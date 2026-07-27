using System;
using System.IO;
using System.Text.Json;

namespace Keemya.Frontend
{
    public static class AppConfig
    {
        public static string ConnectionString { get; private set; } = "Server=localhost;Port=3306;Database=keemya;User=root;Password=root1234;";

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
                }
            }
            catch
            {
                // Fallback to default
            }
        }
    }
}
