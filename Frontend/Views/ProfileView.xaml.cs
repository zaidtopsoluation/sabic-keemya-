using System.Windows;
using System.Windows.Controls;
using Keemya.Frontend.ViewModels;

namespace Keemya.Frontend.Views
{
    public partial class ProfileView : UserControl
    {
        public ProfileView()
        {
            InitializeComponent();
        }

        private void txtCurrentPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                ((ProfileViewModel)this.DataContext).CurrentPassword = ((PasswordBox)sender).Password;
            }
        }

        private void txtNewPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                ((ProfileViewModel)this.DataContext).NewPassword = ((PasswordBox)sender).Password;
            }
        }

        private void txtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                ((ProfileViewModel)this.DataContext).ConfirmPassword = ((PasswordBox)sender).Password;
            }
        }
    }
}
