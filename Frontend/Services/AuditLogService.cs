using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace Keemya.Frontend.Services
{
    public class AuditLogService
    {
        private readonly string _connectionString = AppConfig.ConnectionString;

        public async Task EnsureTableExistsAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                
                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS audit_logs (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        actor VARCHAR(255) NOT NULL,
                        action VARCHAR(100) NOT NULL,
                        description TEXT,
                        module VARCHAR(100) NOT NULL,
                        entity_id VARCHAR(255),
                        timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                    );";
                    
                using var command = new MySqlCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating audit_logs table: {ex.Message}");
            }
        }

        public async Task LogAsync(string actor, string action, string description, string module, string entityId = null)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                string insertSql = @"
                    INSERT INTO audit_logs (actor, action, description, module, entity_id, timestamp)
                    VALUES (@actor, @action, @description, @module, @entityId, @timestamp)";

                using var command = new MySqlCommand(insertSql, connection);
                command.Parameters.AddWithValue("@actor", actor);
                command.Parameters.AddWithValue("@action", action);
                command.Parameters.AddWithValue("@description", description);
                command.Parameters.AddWithValue("@module", module);
                command.Parameters.AddWithValue("@entityId", string.IsNullOrEmpty(entityId) ? (object)DBNull.Value : entityId);
                command.Parameters.AddWithValue("@timestamp", DateTime.UtcNow);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing audit log: {ex.Message}");
                // In a production app, we might want to log this to a file or handle it otherwise
            }
        }
    }
}
