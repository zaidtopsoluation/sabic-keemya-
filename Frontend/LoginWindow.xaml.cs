using System.Windows;
using System.Windows.Controls;
using Keemya.Frontend.ViewModels;

namespace Keemya.Frontend
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private bool _isPasswordVisible = false;

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null && !_isPasswordVisible)
            { 
                ((LoginViewModel)this.DataContext).Password = ((PasswordBox)sender).Password;
                txtVisiblePassword.Text = ((PasswordBox)sender).Password;
            }
        }

        private void txtVisiblePassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DataContext != null && _isPasswordVisible)
            {
                ((LoginViewModel)this.DataContext).Password = txtVisiblePassword.Text;
                txtPassword.Password = txtVisiblePassword.Text;
            }
        }

        private void btnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                txtVisiblePassword.Visibility = Visibility.Visible;
                txtPassword.Visibility = Visibility.Collapsed;
                iconTogglePassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
            }
            else
            {
                txtPassword.Visibility = Visibility.Visible;
                txtVisiblePassword.Visibility = Visibility.Collapsed;
                iconTogglePassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
            }
        }

        private bool _isNewPasswordVisible = false;

        private void txtNewPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null && !_isNewPasswordVisible)
            { 
                ((LoginViewModel)this.DataContext).NewPassword = ((PasswordBox)sender).Password;
                txtVisibleNewPassword.Text = ((PasswordBox)sender).Password;
            }
        }

        private void txtVisibleNewPassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DataContext != null && _isNewPasswordVisible)
            {
                ((LoginViewModel)this.DataContext).NewPassword = txtVisibleNewPassword.Text;
                txtNewPassword.Password = txtVisibleNewPassword.Text;
            }
        }

        private void btnToggleNewPassword_Click(object sender, RoutedEventArgs e)
        {
            _isNewPasswordVisible = !_isNewPasswordVisible;

            if (_isNewPasswordVisible)
            {
                txtVisibleNewPassword.Visibility = Visibility.Visible;
                txtNewPassword.Visibility = Visibility.Collapsed;
                iconToggleNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
            }
            else
            {
                txtNewPassword.Visibility = Visibility.Visible;
                txtVisibleNewPassword.Visibility = Visibility.Collapsed;
                iconToggleNewPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
            }
        }

        private bool _isConfirmPasswordVisible = false;

        private void txtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null && !_isConfirmPasswordVisible)
            { 
                ((LoginViewModel)this.DataContext).ConfirmPassword = ((PasswordBox)sender).Password;
                txtVisibleConfirmPassword.Text = ((PasswordBox)sender).Password;
            }
        }

        private void txtVisibleConfirmPassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DataContext != null && _isConfirmPasswordVisible)
            {
                ((LoginViewModel)this.DataContext).ConfirmPassword = txtVisibleConfirmPassword.Text;
                txtConfirmPassword.Password = txtVisibleConfirmPassword.Text;
            }
        }

        private void btnToggleConfirmPassword_Click(object sender, RoutedEventArgs e)
        {
            _isConfirmPasswordVisible = !_isConfirmPasswordVisible;

            if (_isConfirmPasswordVisible)
            {
                txtVisibleConfirmPassword.Visibility = Visibility.Visible;
                txtConfirmPassword.Visibility = Visibility.Collapsed;
                iconToggleConfirmPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
            }
            else
            {
                txtConfirmPassword.Visibility = Visibility.Visible;
                txtVisibleConfirmPassword.Visibility = Visibility.Collapsed;
                iconToggleConfirmPassword.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
            }
        }
    }
}
