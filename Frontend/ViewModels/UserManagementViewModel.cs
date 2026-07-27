using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Models;
using Keemya.Frontend.Stores;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;

        [ObservableProperty]
        private int totalUsers = 0;

        [ObservableProperty]
        private int administrators = 0;

        [ObservableProperty]
        private int operators = 0;

        [ObservableProperty]
        private int activeUsers = 0;

        [ObservableProperty]
        private ObservableCollection<UserDto> users = new();

        private List<UserDto> _allUsers = new();

        [ObservableProperty]
        private string searchQuery = string.Empty;

        partial void OnSearchQueryChanged(string value)
        {
            FilterUsers();
        }

        private void FilterUsers()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                Users = new ObservableCollection<UserDto>(_allUsers);
            }
            else
            {
                var query = SearchQuery.Trim().ToLower();
                var filtered = _allUsers.FindAll(u =>
                    u.Username.ToLower().Contains(query) ||
                    u.Role.ToLower().Contains(query) ||
                    u.Status.ToLower().Contains(query)
                );
                Users = new ObservableCollection<UserDto>(filtered);
            }
        }

        // Popup Bindings
        [ObservableProperty]
        private bool isAddUserPopupOpen = false;

        [ObservableProperty]
        private bool isEditMode = false;

        [ObservableProperty]
        private string popupTitle = "Add User";

        [ObservableProperty]
        private string popupSubtitle = "Fill in the details to create a new user account.";

        private string _editingUsername = string.Empty;

        [ObservableProperty]
        private string newUsername = string.Empty;

        [ObservableProperty]
        private string selectedRole = "Service";

        [ObservableProperty]
        private string selectedStatus = "Active";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPopupError))]
        private string popupErrorMessage = string.Empty;

        public bool HasPopupError => !string.IsNullOrEmpty(PopupErrorMessage);

        public List<string> RoleList { get; } = new() { "Admin", "Operator", "Service" };
        public List<string> StatusList { get; } = new() { "Active", "Inactive" };

        public UserManagementViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;

            // Load live users from MySQL database
            _ = LoadUsersFromDatabase();
        }

        private async Task LoadUsersFromDatabase()
        {
            try
            {
                var loadedUsers = new List<UserDto>();
                int total = 0;
                int adminCount = 0;
                int opCount = 0;
                int activeCount = 0;

                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    using (var command = new MySqlCommand("SELECT Username, Role, Enabled, LastLogin, Created, IsFirstTimeLogin, TempPassword FROM Users ORDER BY Created DESC", connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string username = reader.GetString(0);
                                string role = reader.GetString(1);
                                bool enabled = reader.GetBoolean(2);
                                DateTime? lastLogin = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                                DateTime created = reader.GetDateTime(4);
                                bool isFirstTime = reader.GetBoolean(5);
                                string tempPassword = reader.IsDBNull(6) ? "-" : reader.GetString(6);

                                string status = enabled ? "Active" : "Inactive";
                                string lastLoginStr = lastLogin.HasValue ? lastLogin.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
                                string createdStr = created.ToString("yyyy-MM-dd HH:mm:ss");

                                // The user's request:
                                // "temporary password field when created so it will show here after user login and change so it will not show here"
                                // We use `IsFirstTimeLogin` as a flag. If it's true, show the temporary password. If false, show "-"
                                string tempPass = isFirstTime ? tempPassword : "-";

                                loadedUsers.Add(new UserDto
                                {
                                    Username = username,
                                    Role = role,
                                    Status = status,
                                    LastLogin = lastLoginStr,
                                    Created = createdStr,
                                    TemporaryPassword = tempPass
                                });

                                total++;
                                if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
                                    adminCount++;
                                else if (role.Equals("Operator", StringComparison.OrdinalIgnoreCase))
                                    opCount++;

                                if (enabled)
                                    activeCount++;
                            }
                        }
                    }
                }

                // Update properties on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _allUsers = loadedUsers;
                    FilterUsers();
                    
                    TotalUsers = total;
                    Administrators = adminCount;
                    Operators = opCount;
                    ActiveUsers = activeCount;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading users: " + ex.Message);
            }
        }

        [RelayCommand]
        private void OpenAddUserPopup()
        {
            IsEditMode = false;
            PopupTitle = "Add User";
            PopupSubtitle = "Fill in the details to create a new user account.";
            NewUsername = string.Empty;
            SelectedRole = "Service";
            SelectedStatus = "Active";
            PopupErrorMessage = string.Empty;
            IsAddUserPopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditUserPopup(UserDto user)
        {
            if (user == null) return;
            
            IsEditMode = true;
            _editingUsername = user.Username;
            PopupTitle = "Edit User";
            PopupSubtitle = "Modify the user's role and status.";
            NewUsername = user.Username;
            SelectedRole = user.Role;
            SelectedStatus = user.Status;
            PopupErrorMessage = string.Empty;
            IsAddUserPopupOpen = true;
        }

        [RelayCommand]
        private async Task DeleteUser(UserDto user)
        {
            if (user == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete user '{user.Username}'?", "Delete User", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();
                    using (var cmd = new MySqlCommand("DELETE FROM Users WHERE Username = @user", connection))
                    {
                        cmd.Parameters.AddWithValue("@user", user.Username);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                
                try
                {
                    string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                    var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                    await auditLogService.LogAsync(actor, "DELETED", $"Deleted user {user.Username}", "User Management", user.Username);
                }
                catch { }

                await LoadUsersFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error deleting user: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CloseAddUserPopup()
        {
            IsAddUserPopupOpen = false;
        }

        [RelayCommand]
        private async Task SaveUser()
        {
            if (string.IsNullOrWhiteSpace(NewUsername))
            {
                PopupErrorMessage = "Username is required.";
                return;
            }

            try
            {
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    if (!IsEditMode || (IsEditMode && NewUsername != _editingUsername))
                    {
                        // Check if username already exists
                        using (var checkCmd = new MySqlCommand("SELECT COUNT(1) FROM Users WHERE Username = @user", connection))
                        {
                            checkCmd.Parameters.AddWithValue("@user", NewUsername);
                            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                            if (count > 0)
                            {
                                PopupErrorMessage = "Username already exists.";
                                return;
                            }
                        }
                    }

                    if (IsEditMode)
                    {
                        string updateQuery = "UPDATE Users SET Username = @newUser, Enabled = @enabled, Role = @role WHERE Username = @oldUser";
                        using (var updateCmd = new MySqlCommand(updateQuery, connection))
                        {
                            updateCmd.Parameters.AddWithValue("@newUser", NewUsername);
                            updateCmd.Parameters.AddWithValue("@enabled", SelectedStatus == "Active" ? 1 : 0);
                            updateCmd.Parameters.AddWithValue("@role", SelectedRole);
                            updateCmd.Parameters.AddWithValue("@oldUser", _editingUsername);
                            await updateCmd.ExecuteNonQueryAsync();
                        }

                        // Log the update action
                        try
                        {
                            string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                            var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                            await auditLogService.LogAsync(actor, "UPDATED", $"Updated user {NewUsername}", "User Management", NewUsername);
                        }
                        catch { }
                    }
                    else
                    {
                        // Generate a random temporary password matching complexity rules
                        string tempPassword = Keemya.Frontend.Services.PasswordValidator.GenerateTempPassword();
                        string hashedPassword = Keemya.Frontend.Services.PasswordHasher.HashPassword(tempPassword);

                        // Insert user
                        string query = "INSERT INTO Users (Id, Username, Password, Enabled, IsFirstTimeLogin, Role, Created, TempPassword) VALUES (@id, @user, @pass, @enabled, @isFirstTime, @role, @created, @tempPass)";
                        using (var insertCmd = new MySqlCommand(query, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                            insertCmd.Parameters.AddWithValue("@user", NewUsername);
                            insertCmd.Parameters.AddWithValue("@pass", hashedPassword);
                            insertCmd.Parameters.AddWithValue("@enabled", SelectedStatus == "Active" ? 1 : 0);
                            insertCmd.Parameters.AddWithValue("@isFirstTime", 1);
                            insertCmd.Parameters.AddWithValue("@role", SelectedRole);
                            insertCmd.Parameters.AddWithValue("@created", DateTime.UtcNow);
                            insertCmd.Parameters.AddWithValue("@tempPass", tempPassword);

                            await insertCmd.ExecuteNonQueryAsync();
                        }

                        // Log the user creation action
                        try
                        {
                            string actor = Keemya.Frontend.Stores.Session.Username ?? "System";
                            var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                            await auditLogService.LogAsync(actor, "CREATED", $"Created user {NewUsername} with role {SelectedRole}", "User Management", NewUsername);
                        }
                        catch { }
                    }
                }



                // Success! Close popup and reload users
                IsAddUserPopupOpen = false;
                await LoadUsersFromDatabase();
            }
            catch (Exception ex)
            {
                PopupErrorMessage = "Database error: " + ex.Message;
            }
        }

        [RelayCommand]
        private void Back()
        {
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }
    }
}
