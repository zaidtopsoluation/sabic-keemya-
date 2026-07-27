using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Keemya.Frontend.Services
{
    public class NotificationItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "Info"; // Danger, Warning, Info
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
        
        // Helper properties for UI
        public string TypeColor => Type switch
        {
            "Danger" => "#EF4444",
            "Warning" => "#F59E0B",
            _ => "#3B82F6"
        };

        public string IconKind => Type switch
        {
            "Danger" => "AlertOctagon",
            "Warning" => "AlertCircle",
            _ => "Information"
        };
    }

    public class NotificationService
    {
        private readonly string _connectionString = AppConfig.ConnectionString;

        public async Task EnsureTableExistsAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if table already exists in the database schema before creating
                string checkTableExistsSql = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'keemya' AND table_name = 'notifications'";
                using var checkExistCmd = new MySqlCommand(checkTableExistsSql, connection);
                bool tableExisted = Convert.ToInt32(await checkExistCmd.ExecuteScalarAsync()) > 0;

                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS notifications (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        title VARCHAR(255) NOT NULL,
                        message TEXT NOT NULL,
                        type VARCHAR(50) NOT NULL DEFAULT 'Info',
                        timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                        is_read TINYINT(1) DEFAULT 0
                    );";

                using var command = new MySqlCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();

                // Seed some mock data ONLY if the table did not exist at all before this call
                if (!tableExisted)
                {
                    // Fetch real sirens to use their names
                    var sirenNames = new List<string>();
                    try
                    {
                        string selectSirensSql = "SELECT Name FROM SirenDevices LIMIT 5";
                        using var sirensCommand = new MySqlCommand(selectSirensSql, connection);
                        using var reader = await sirensCommand.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            sirenNames.Add(reader.GetString(0));
                        }
                    }
                    catch {}

                    // Fallback names if database is empty of sirens
                    while (sirenNames.Count < 5)
                    {
                        sirenNames.Add($"Siren Device 0{sirenNames.Count + 1}");
                    }

                    string seedSql = @"
                        INSERT INTO notifications (title, message, type, timestamp, is_read) VALUES
                        ('Cabinet Intrusion Alert', @MsgIntrusion, 'Danger', NOW() - INTERVAL 5 MINUTE, 0),
                        ('AC Power Loss Warning', @MsgAcLoss, 'Warning', NOW() - INTERVAL 15 MINUTE, 0),
                        ('Siren Offline Alarm', @MsgOffline, 'Danger', NOW() - INTERVAL 1 HOUR, 1),
                        ('User Login Success', 'Operator user successfully logged in from station console.', 'Info', NOW() - INTERVAL 2 HOUR, 1),
                        ('Low Battery Warning', @MsgLowBattery, 'Warning', NOW() - INTERVAL 5 HOUR, 1),
                        ('System Command Sent', @MsgCmd, 'Info', NOW() - INTERVAL 1 DAY, 1);";

                    using var seedCommand = new MySqlCommand(seedSql, connection);
                    seedCommand.Parameters.AddWithValue("@MsgIntrusion", $"Cabinet door opened/tampered at {sirenNames[2]}.");
                    seedCommand.Parameters.AddWithValue("@MsgAcLoss", $"{sirenNames[1]} has lost main AC Power supply and is running on backup battery.");
                    seedCommand.Parameters.AddWithValue("@MsgOffline", $"{sirenNames[4]} is unresponsive and has gone offline.");
                    seedCommand.Parameters.AddWithValue("@MsgLowBattery", $"{sirenNames[0]} backup battery voltage is below 11.8V.");
                    seedCommand.Parameters.AddWithValue("@MsgCmd", $"Emergency Wail tone command dispatched to {sirenNames[0]}.");
                    
                    await seedCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating/seeding notifications table: {ex.Message}");
            }
        }

        public async Task<string> GetSirenNameByAddressAsync(string addressCode)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                
                string sql = "SELECT Name FROM SirenDevices WHERE AddressCode = @AddressCode OR AddressCode = @AddressCodeTrimmed LIMIT 1";
                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@AddressCode", addressCode);
                command.Parameters.AddWithValue("@AddressCodeTrimmed", addressCode.TrimStart('0'));
                
                var name = await command.ExecuteScalarAsync();
                if (name != null)
                {
                    return name.ToString() ?? $"Siren {addressCode}";
                }
            }
            catch {}
            return $"Siren {addressCode}";
        }

        public async Task<int> GetUnreadCountAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM notifications WHERE is_read = 0";
                using var command = new MySqlCommand(sql, connection);
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            catch
            {
                return 0;
            }
        }

        public async Task<List<NotificationItem>> GetNotificationsAsync()
        {
            await EnsureTableExistsAsync();
            var list = new List<NotificationItem>();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string selectSql = "SELECT id, title, message, type, timestamp, is_read FROM notifications ORDER BY timestamp DESC LIMIT 1000";
                using var command = new MySqlCommand(selectSql, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new NotificationItem
                    {
                        Id = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        Message = reader.GetString(2),
                        Type = reader.GetString(3),
                        Timestamp = reader.GetDateTime(4),
                        IsRead = reader.GetBoolean(5)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading notifications: {ex.Message}");
            }

            return list;
        }

        public async Task MarkAllAsReadAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string updateSql = "UPDATE notifications SET is_read = 1";
                using var command = new MySqlCommand(updateSql, connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking notifications as read: {ex.Message}");
            }
        }

        public async Task ClearAllAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string deleteSql = "DELETE FROM notifications";
                using var command = new MySqlCommand(deleteSql, connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing notifications: {ex.Message}");
            }
        }

        public async Task DeleteNotificationAsync(int id)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string deleteSql = "DELETE FROM notifications WHERE id = @Id";
                using var command = new MySqlCommand(deleteSql, connection);
                command.Parameters.AddWithValue("@Id", id);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting notification: {ex.Message}");
            }
        }

        public async Task AddNotificationAsync(string title, string message, string type)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string insertSql = "INSERT INTO notifications (title, message, type, timestamp, is_read) VALUES (@Title, @Message, @Type, @Timestamp, 0)";
                using var command = new MySqlCommand(insertSql, connection);
                command.Parameters.AddWithValue("@Title", title);
                command.Parameters.AddWithValue("@Message", message);
                command.Parameters.AddWithValue("@Type", type);
                command.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding notification: {ex.Message}");
            }
        }
    }
}
