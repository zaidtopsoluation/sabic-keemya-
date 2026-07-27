using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Stores;
using MySqlConnector;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    public partial class ProfileViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;

        [ObservableProperty]
        private string username = "admin";

        [ObservableProperty]
        private string status = "Active";

        // Change Password Dialog Bindings
        [ObservableProperty]
        private bool isChangePasswordPopupOpen = false;

        [ObservableProperty]
        private string currentPassword = string.Empty;

        [ObservableProperty]
        private string newPassword = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPopupError))]
        private string popupErrorMessage = string.Empty;

        public bool HasPopupError => !string.IsNullOrEmpty(PopupErrorMessage);

        public ProfileViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;

            // Load the current active logged-in user from the session
            Username = Session.Username;
        }

        [RelayCommand]
        private void OpenChangePasswordPopup()
        {
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            PopupErrorMessage = string.Empty;
            IsChangePasswordPopupOpen = true;
        }

        [RelayCommand]
        private void CloseChangePasswordPopup()
        {
            IsChangePasswordPopupOpen = false;
        }

        [RelayCommand]
        private async Task SavePassword()
        {
            if (string.IsNullOrWhiteSpace(CurrentPassword) || 
                string.IsNullOrWhiteSpace(NewPassword) || 
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                PopupErrorMessage = "All password fields are required.";
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                PopupErrorMessage = "New password and confirmation do not match.";
                return;
            }

            if (!Keemya.Frontend.Services.PasswordValidator.IsValid(NewPassword, out string validationError))
            {
                PopupErrorMessage = validationError;
                return;
            }

            try
            {
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    // Verify current password
                    using (var checkCmd = new MySqlCommand("SELECT Password FROM Users WHERE Username = @user", connection))
                    {
                        checkCmd.Parameters.AddWithValue("@user", Username);
                        var dbPass = await checkCmd.ExecuteScalarAsync() as string;
                        if (dbPass == null || !Keemya.Frontend.Services.PasswordHasher.VerifyPassword(CurrentPassword, dbPass))
                        {
                            PopupErrorMessage = "Current password is incorrect.";
                            return;
                        }
                    }

                    // Update to the new password in DB
                    using (var updateCmd = new MySqlCommand("UPDATE Users SET Password = @newPass WHERE Username = @user", connection))
                    {
                        updateCmd.Parameters.AddWithValue("@newPass", Keemya.Frontend.Services.PasswordHasher.HashPassword(NewPassword));
                        updateCmd.Parameters.AddWithValue("@user", Username);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }

                // Success
                IsChangePasswordPopupOpen = false;
                MessageBox.Show("Password changed successfully!", "Profile Update", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                PopupErrorMessage = "Database error: " + ex.Message;
            }
        }

        [RelayCommand]
        private void Logout(Window currentWindow)
        {
            SessionManager.ClearSession(); // Clear persisted session!
            var loginWindow = new LoginWindow();
            loginWindow.FlowDirection = LocalizationManager.CurrentLanguage == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            loginWindow.Show();
            currentWindow?.Close();
        }

        [RelayCommand]
        private void Back()
        {
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }
    }
}
