using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace Keemya.Frontend.Services
{
    public class NotificationSettings
    {
        public bool EmailEnabled { get; set; } = true;
        public bool SmsEnabled { get; set; } = false;
        public bool SystemEnabled { get; set; } = true;
        
        public bool QuietHoursEnabled { get; set; } = false;
        public string QuietHoursStart { get; set; } = "22:00";
        public string QuietHoursEnd { get; set; } = "08:00";
        
        public string PrimaryEmail { get; set; } = "";
        public string SecondaryEmail { get; set; } = "";
        public string PrimaryPhone { get; set; } = "";
        public string RoutingMode { get; set; } = "Primary Only";

        // Triggers
        public bool NotifyIntrusion { get; set; } = true;
        public bool NotifyAcLoss { get; set; } = true;
        public bool NotifyLowBattery { get; set; } = true;
        public bool NotifyOffline { get; set; } = true;
        public bool NotifyEmergencyCmd { get; set; } = true;
        public bool NotifyUserLogin { get; set; } = true;
    }

    public class NotificationSettingsService
    {
        private readonly string _connectionString = AppConfig.ConnectionString;

        public async Task EnsureTableExistsAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS notification_settings (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        email_enabled TINYINT(1) DEFAULT 1,
                        sms_enabled TINYINT(1) DEFAULT 0,
                        system_enabled TINYINT(1) DEFAULT 1,
                        quiet_hours_enabled TINYINT(1) DEFAULT 0,
                        quiet_hours_start VARCHAR(5) DEFAULT '22:00',
                        quiet_hours_end VARCHAR(5) DEFAULT '08:00',
                        primary_email VARCHAR(255) DEFAULT '',
                        secondary_email VARCHAR(255) DEFAULT '',
                        primary_phone VARCHAR(50) DEFAULT '',
                        routing_mode VARCHAR(50) DEFAULT 'Primary Only',
                        notify_intrusion TINYINT(1) DEFAULT 1,
                        notify_ac_loss TINYINT(1) DEFAULT 1,
                        notify_low_battery TINYINT(1) DEFAULT 1,
                        notify_offline TINYINT(1) DEFAULT 1,
                        notify_emergency_cmd TINYINT(1) DEFAULT 1,
                        notify_user_login TINYINT(1) DEFAULT 1
                    );";

                using var command = new MySqlCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating notification_settings table: {ex.Message}");
            }
        }

        public async Task<NotificationSettings> LoadSettingsAsync()
        {
            await EnsureTableExistsAsync();
            var settings = new NotificationSettings();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string selectSql = @"
                    SELECT 
                        email_enabled, sms_enabled, system_enabled, 
                        quiet_hours_enabled, quiet_hours_start, quiet_hours_end, 
                        primary_email, secondary_email, primary_phone, routing_mode,
                        notify_intrusion, notify_ac_loss, notify_low_battery, 
                        notify_offline, notify_emergency_cmd, notify_user_login 
                    FROM notification_settings 
                    LIMIT 1";

                using var command = new MySqlCommand(selectSql, connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    settings.EmailEnabled = reader.GetBoolean(0);
                    settings.SmsEnabled = reader.GetBoolean(1);
                    settings.SystemEnabled = reader.GetBoolean(2);
                    settings.QuietHoursEnabled = reader.GetBoolean(3);
                    settings.QuietHoursStart = reader.GetString(4);
                    settings.QuietHoursEnd = reader.GetString(5);
                    settings.PrimaryEmail = reader.IsDBNull(6) ? "" : reader.GetString(6);
                    settings.SecondaryEmail = reader.IsDBNull(7) ? "" : reader.GetString(7);
                    settings.PrimaryPhone = reader.IsDBNull(8) ? "" : reader.GetString(8);
                    settings.RoutingMode = reader.IsDBNull(9) ? "Primary Only" : reader.GetString(9);
                    settings.NotifyIntrusion = reader.GetBoolean(10);
                    settings.NotifyAcLoss = reader.GetBoolean(11);
                    settings.NotifyLowBattery = reader.GetBoolean(12);
                    settings.NotifyOffline = reader.GetBoolean(13);
                    settings.NotifyEmergencyCmd = reader.GetBoolean(14);
                    settings.NotifyUserLogin = reader.GetBoolean(15);
                }
                else
                {
                    // If no settings exist, insert defaults
                    reader.Close();
                    string insertSql = @"
                        INSERT INTO notification_settings (
                            email_enabled, sms_enabled, system_enabled, 
                            quiet_hours_enabled, quiet_hours_start, quiet_hours_end, 
                            primary_email, secondary_email, primary_phone, routing_mode,
                            notify_intrusion, notify_ac_loss, notify_low_battery, 
                            notify_offline, notify_emergency_cmd, notify_user_login
                        ) VALUES (1, 0, 1, 0, '22:00', '08:00', '', '', '', 'Primary Only', 1, 1, 1, 1, 1, 1)";
                    
                    using var insertCommand = new MySqlCommand(insertSql, connection);
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading notification settings: {ex.Message}");
            }

            return settings;
        }

        public async Task SaveSettingsAsync(NotificationSettings settings)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if row exists
                string checkSql = "SELECT COUNT(*) FROM notification_settings";
                using var checkCommand = new MySqlCommand(checkSql, connection);
                long count = Convert.ToInt64(await checkCommand.ExecuteScalarAsync());

                if (count == 0)
                {
                    string insertSql = @"
                        INSERT INTO notification_settings (
                            email_enabled, sms_enabled, system_enabled, 
                            quiet_hours_enabled, quiet_hours_start, quiet_hours_end, 
                            primary_email, secondary_email, primary_phone, routing_mode,
                            notify_intrusion, notify_ac_loss, notify_low_battery, 
                            notify_offline, notify_emergency_cmd, notify_user_login
                        ) VALUES (
                            @EmailEnabled, @SmsEnabled, @SystemEnabled, 
                            @QuietHoursEnabled, @QuietHoursStart, @QuietHoursEnd, 
                            @PrimaryEmail, @SecondaryEmail, @PrimaryPhone, @RoutingMode,
                            @NotifyIntrusion, @NotifyAcLoss, @NotifyLowBattery, 
                            @NotifyOffline, @NotifyEmergencyCmd, @NotifyUserLogin
                        )";
                    using var insertCommand = new MySqlCommand(insertSql, connection);
                    AddParameters(insertCommand, settings);
                    await insertCommand.ExecuteNonQueryAsync();
                }
                else
                {
                    string updateSql = @"
                        UPDATE notification_settings SET 
                            email_enabled = @EmailEnabled, 
                            sms_enabled = @SmsEnabled, 
                            system_enabled = @SystemEnabled, 
                            quiet_hours_enabled = @QuietHoursEnabled, 
                            quiet_hours_start = @QuietHoursStart, 
                            quiet_hours_end = @QuietHoursEnd, 
                            primary_email = @PrimaryEmail, 
                            secondary_email = @SecondaryEmail, 
                            primary_phone = @PrimaryPhone, 
                            routing_mode = @RoutingMode,
                            notify_intrusion = @NotifyIntrusion,
                            notify_ac_loss = @NotifyAcLoss,
                            notify_low_battery = @NotifyLowBattery,
                            notify_offline = @NotifyOffline,
                            notify_emergency_cmd = @NotifyEmergencyCmd,
                            notify_user_login = @NotifyUserLogin";
                    using var updateCommand = new MySqlCommand(updateSql, connection);
                    AddParameters(updateCommand, settings);
                    await updateCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving notification settings: {ex.Message}");
            }
        }

        private void AddParameters(MySqlCommand command, NotificationSettings settings)
        {
            command.Parameters.AddWithValue("@EmailEnabled", settings.EmailEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@SmsEnabled", settings.SmsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@SystemEnabled", settings.SystemEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@QuietHoursEnabled", settings.QuietHoursEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@QuietHoursStart", settings.QuietHoursStart);
            command.Parameters.AddWithValue("@QuietHoursEnd", settings.QuietHoursEnd);
            command.Parameters.AddWithValue("@PrimaryEmail", settings.PrimaryEmail ?? "");
            command.Parameters.AddWithValue("@SecondaryEmail", settings.SecondaryEmail ?? "");
            command.Parameters.AddWithValue("@PrimaryPhone", settings.PrimaryPhone ?? "");
            command.Parameters.AddWithValue("@RoutingMode", settings.RoutingMode ?? "Primary Only");
            command.Parameters.AddWithValue("@NotifyIntrusion", settings.NotifyIntrusion ? 1 : 0);
            command.Parameters.AddWithValue("@NotifyAcLoss", settings.NotifyAcLoss ? 1 : 0);
            command.Parameters.AddWithValue("@NotifyLowBattery", settings.NotifyLowBattery ? 1 : 0);
            command.Parameters.AddWithValue("@NotifyOffline", settings.NotifyOffline ? 1 : 0);
            command.Parameters.AddWithValue("@NotifyEmergencyCmd", settings.NotifyEmergencyCmd ? 1 : 0);
            command.Parameters.AddWithValue("@NotifyUserLogin", settings.NotifyUserLogin ? 1 : 0);
        }
    }
}
