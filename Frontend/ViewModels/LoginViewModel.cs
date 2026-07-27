using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using MySqlConnector;
using System;
using System.Threading.Tasks;
using Keemya.Frontend.Stores;

namespace Keemya.Frontend.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string username = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        // First Time Password Change Bindings
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LoginFormVisibility))]
        [NotifyPropertyChangedFor(nameof(ChangePasswordFormVisibility))]
        private bool isFirstTimeLoginActive = false;

        [ObservableProperty]
        private string newPassword = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

        public Visibility LoginFormVisibility => IsFirstTimeLoginActive ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ChangePasswordFormVisibility => IsFirstTimeLoginActive ? Visibility.Visible : Visibility.Collapsed;

        public string CurrentLanguageText => LocalizationManager.CurrentLanguage == "ar" ? "English" : "العربية";

        [RelayCommand]
        private void ToggleLanguage()
        {
            string nextLang = LocalizationManager.CurrentLanguage == "ar" ? "en" : "ar";
            LocalizationManager.SetLanguage(nextLang);
            OnPropertyChanged(nameof(CurrentLanguageText));
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        [RelayCommand]
        private async Task Login(Window currentWindow)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                OnPropertyChanged(nameof(HasError));
                return;
            }

            ErrorMessage = "";
            OnPropertyChanged(nameof(HasError));

            try
            {
                string connStr = AppConfig.ConnectionString;
                bool isAuthenticated = false;
                bool isFirstTime = false;

                string userRole = "Admin";
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    using (var command = new MySqlCommand("SELECT Password, IsFirstTimeLogin, Role FROM Users WHERE Username = @user", connection))
                    {
                        command.Parameters.AddWithValue("@user", Username);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string dbPassword = reader.GetString(0);
                                isFirstTime = reader.GetBoolean(1);
                                userRole = reader.IsDBNull(2) ? "Admin" : reader.GetString(2);

                                if (Keemya.Frontend.Services.PasswordHasher.VerifyPassword(Password, dbPassword))
                                {
                                    isAuthenticated = true;
                                }
                            }
                        }
                    }
                }

                if (isAuthenticated)
                {
                    if (isFirstTime)
                    {
                        // Enable first-time password change UI
                        IsFirstTimeLoginActive = true;
                        ErrorMessage = "First-time login detected. You must change your temporary password to proceed.";
                        OnPropertyChanged(nameof(HasError));
                    }
                    else
                    {
                        // Log the login action
                        try
                        {
                            var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                            await auditLogService.LogAsync(Username, "LOGIN", "User logged in successfully", "Authentication");

                            var notificationService = new Keemya.Frontend.Services.NotificationService();
                            await notificationService.AddNotificationAsync("User Login Success", $"User '{Username}' successfully logged in from station console.", "Info");
                        }
                        catch { }

                        // Success! Proceed to Dashboard
                        NavigateToDashboard(currentWindow, userRole);
                    }
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                    OnPropertyChanged(nameof(HasError));
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Database connection error: " + ex.Message;
                OnPropertyChanged(nameof(HasError));
            }
        }

        [RelayCommand]
        private async Task ChangePassword(Window currentWindow)
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Please fill in all password fields.";
                OnPropertyChanged(nameof(HasError));
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                OnPropertyChanged(nameof(HasError));
                return;
            }

            if (!Keemya.Frontend.Services.PasswordValidator.IsValid(NewPassword, out string validationError))
            {
                ErrorMessage = validationError;
                OnPropertyChanged(nameof(HasError));
                return;
            }

            try
            {
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    // Update password, flag, last login, and clear TempPassword in database
                    using (var command = new MySqlCommand("UPDATE Users SET Password = @newPass, IsFirstTimeLogin = FALSE, LastLogin = @now, TempPassword = NULL WHERE Username = @user", connection))
                    {
                        command.Parameters.AddWithValue("@newPass", Keemya.Frontend.Services.PasswordHasher.HashPassword(NewPassword));
                        command.Parameters.AddWithValue("@now", DateTime.UtcNow);
                        command.Parameters.AddWithValue("@user", Username);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Password changed successfully! Proceed to Dashboard
                // Fetch user role for newly set password
                string userRole = "Admin";
                try
                {
                    using (var connection = new MySqlConnection(connStr))
                    {
                        await connection.OpenAsync();
                        using (var cmd = new MySqlCommand("SELECT Role FROM Users WHERE Username = @user", connection))
                        {
                            cmd.Parameters.AddWithValue("@user", Username);
                            var obj = await cmd.ExecuteScalarAsync();
                            if (obj != null) userRole = obj.ToString() ?? "Admin";
                        }
                    }
                }
                catch { }
                NavigateToDashboard(currentWindow, userRole);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error updating password: " + ex.Message;
                OnPropertyChanged(nameof(HasError));
            }
        }

        private void NavigateToDashboard(Window currentWindow, string role)
        {
            Session.Username = Username; // Seed shared static session!
            Session.Role = role;
            SessionManager.SaveSession(Username, role); // Persist session locally!

            var navigationStore = new NavigationStore();
            navigationStore.CurrentViewModel = new DashboardViewModel(navigationStore);

            var mainViewModel = new MainViewModel(navigationStore);
            mainViewModel.CurrentUsername = Username;

            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainViewModel;
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.FlowDirection = LocalizationManager.CurrentLanguage == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            mainWindow.Show();

            currentWindow?.Close();
        }

        [RelayCommand]
        private void Close(Window currentWindow)
        {
            currentWindow?.Close();
        }
    }
}
