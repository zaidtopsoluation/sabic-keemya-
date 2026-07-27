using System;
using System.Windows;
using Microsoft.Win32;
using Keemya.Frontend.Services;
using Keemya.Frontend.Stores;

namespace Keemya.Frontend
{
    public partial class LicenseWindow : Window
    {
        public LicenseWindow()
        {
            InitializeComponent();
            LoadMachineId();
        }

        private void LoadMachineId()
        {
            try
            {
                txtMachineId.Text = LicenseService.GetMachineId();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to retrieve Machine ID: {ex.Message}");
            }
        }

        private void CopyMachineId_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtMachineId.Text))
            {
                Clipboard.SetText(txtMachineId.Text);
                MessageBox.Show("Machine ID copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BrowseLicense_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select license.lic File",
                Filter = "License Files (*.lic)|*.lic",
                FileName = "license.lic"
            };

            if (dialog.ShowDialog() == true)
            {
                txtLicensePath.Text = dialog.FileName;
                tbStatus.Text = ""; // Clear errors
            }
        }

        private void Activate_Click(object sender, RoutedEventArgs e)
        {
            string licensePath = txtLicensePath.Text;
            if (string.IsNullOrEmpty(licensePath))
            {
                ShowError("Please select a license file first.");
                return;
            }

            // Install and verify license
            if (LicenseService.InstallLicense(licensePath, out string errorMessage))
            {
                // Successful activation!
                tbStatus.Foreground = System.Windows.Media.Brushes.MediumSeaGreen;
                tbStatus.Text = "Activation Successful! Opening application...";

                MessageBox.Show("Application activated successfully!", "Activation Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                // Open Login Window and close activation window
                var loginWindow = new LoginWindow();
                loginWindow.FlowDirection = LocalizationManager.CurrentLanguage == "ar"
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
                
                loginWindow.Show();
                this.Close();
            }
            else
            {
                ShowError(errorMessage);
            }
        }

        private void ShowError(string message)
        {
            tbStatus.Foreground = System.Windows.Media.Brushes.Tomato;
            tbStatus.Text = message;
        }
    }
}
