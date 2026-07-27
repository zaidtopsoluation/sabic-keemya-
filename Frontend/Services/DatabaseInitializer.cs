using MySqlConnector;
using System;
using System.IO;
using System.Windows;

namespace Keemya.Frontend.Services
{
    public static class DatabaseInitializer
    {
        public static void InitializeDatabase()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(AppConfig.ConnectionString))
                {
                    return;
                }

                var builder = new MySqlConnectionStringBuilder(AppConfig.ConnectionString);
                string targetDb = builder.Database;

                if (string.IsNullOrWhiteSpace(targetDb))
                {
                    return;
                }

                // 1. Connect to MySQL without specifying a target database, so we can create it if it doesn't exist.
                // This avoids the "Unknown database" exception on open.
                var serverOnlyBuilder = new MySqlConnectionStringBuilder(AppConfig.ConnectionString)
                {
                    Database = "" // Clear the database name to connect to the server itself
                };

                using (var connection = new MySqlConnection(serverOnlyBuilder.ConnectionString))
                {
                    connection.Open();
                    using (var command = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{targetDb}`;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                // 2. Now connect to the target database and check if the 'CommandConfigs' table exists.
                bool tablesExist = false;
                using (var connection = new MySqlConnection(builder.ConnectionString))
                {
                    connection.Open();
                    using (var command = new MySqlCommand("SHOW TABLES LIKE 'CommandConfigs';", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                tablesExist = true;
                            }
                        }
                    }
                }

                // 3. If 'CommandConfigs' does not exist, it means the database is empty. Load and run schema.sql.
                if (!tablesExist)
                {
                    string sqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB", "schema.sql");
                    if (!File.Exists(sqlPath))
                    {
                        // Fallback check directly in the BaseDirectory
                        sqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schema.sql");
                    }

                    if (File.Exists(sqlPath))
                    {
                        string sqlContent = File.ReadAllText(sqlPath);
                        // Split SQL script into individual statements by semicolon followed by a newline
                        string[] statements = sqlContent.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries);

                        using (var connection = new MySqlConnection(builder.ConnectionString))
                        {
                            connection.Open();
                            using (var transaction = connection.BeginTransaction())
                            {
                                try
                                {
                                    foreach (var rawStatement in statements)
                                    {
                                        string statement = rawStatement.Trim();
                                        if (string.IsNullOrWhiteSpace(statement))
                                        {
                                            continue;
                                        }

                                        // Ignore manual transaction boundaries since we control it programmatically
                                        if (statement.Equals("START TRANSACTION", StringComparison.OrdinalIgnoreCase) ||
                                            statement.Equals("COMMIT", StringComparison.OrdinalIgnoreCase))
                                        {
                                            continue;
                                        }

                                        using (var command = new MySqlCommand(statement, connection, transaction))
                                        {
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                    transaction.Commit();
                                }
                                catch (Exception)
                                {
                                    transaction.Rollback();
                                    throw;
                                }
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "Database was created successfully, but the database initialization file 'DB/schema.sql' could not be found.\n\nPlease ensure 'DB/schema.sql' is in the application folder.",
                            "Database Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // 4. Ensure TempPassword column exists dynamically
                try
                {
                    using (var connection = new MySqlConnection(builder.ConnectionString))
                    {
                        connection.Open();
                        using (var command = new MySqlCommand("ALTER TABLE Users ADD COLUMN TempPassword VARCHAR(255) NULL;", connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch { /* Column already exists or table doesn't exist yet */ }

                // 5. Safeguard: Check if Users table is empty. If empty, seed default admin.
                try
                {
                    using (var connection = new MySqlConnection(builder.ConnectionString))
                    {
                        connection.Open();
                        bool isEmpty = false;
                        using (var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM Users", connection))
                        {
                            isEmpty = Convert.ToInt32(checkCmd.ExecuteScalar()) == 0;
                        }

                        if (isEmpty)
                        {
                            string adminId = Guid.NewGuid().ToString();
                            string hashedDefault = PasswordHasher.HashPassword("admin123");
                            string seedSql = "INSERT INTO Users (Id, Username, Password, Enabled, IsFirstTimeLogin, Role, Created) VALUES (@Id, 'admin', @Password, 1, 0, 'Admin', @Created)";
                            using (var seedCmd = new MySqlCommand(seedSql, connection))
                            {
                                seedCmd.Parameters.AddWithValue("@Id", adminId);
                                seedCmd.Parameters.AddWithValue("@Password", hashedDefault);
                                seedCmd.Parameters.AddWithValue("@Created", DateTime.UtcNow);
                                seedCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                catch { /* Ignore database connectivity errors during seed check */ }

                // 6. Migrate any existing plaintext passwords to securely hashed passwords
                try
                {
                    using (var connection = new MySqlConnection(builder.ConnectionString))
                    {
                        connection.Open();
                        var usersToMigrate = new List<(string Username, string PlainTextPassword)>();
                        
                        using (var selectCmd = new MySqlCommand("SELECT Username, Password FROM Users", connection))
                        using (var reader = selectCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader.GetString(0);
                                string passwordVal = reader.GetString(1);
                                
                                bool isHashed = false;
                                try
                                {
                                    byte[] bytes = Convert.FromBase64String(passwordVal);
                                    if (bytes.Length == 36)
                                    {
                                        isHashed = true;
                                    }
                                }
                                catch {}
                                
                                if (!isHashed)
                                {
                                    usersToMigrate.Add((username, passwordVal));
                                }
                            }
                        }
                        
                        foreach (var user in usersToMigrate)
                        {
                            string hashed = PasswordHasher.HashPassword(user.PlainTextPassword);
                            using (var updateCmd = new MySqlCommand("UPDATE Users SET Password = @Pass WHERE Username = @User", connection))
                            {
                                updateCmd.Parameters.AddWithValue("@Pass", hashed);
                                updateCmd.Parameters.AddWithValue("@User", user.Username);
                                updateCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                catch { /* Ignore migration errors */ }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to automatically set up the database:\n\n{ex.Message}\n\nPlease ensure your MySQL credentials in appsettings.json are correct and that MySQL Server is running.",
                    "Database Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
