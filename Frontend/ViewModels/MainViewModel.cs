using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Stores;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;

        public ObservableObject? CurrentViewModel => _navigationStore.CurrentViewModel;

        public bool IsHeaderVisible => CurrentViewModel is not MapViewModel;

        public Thickness MainGridMargin => CurrentViewModel is MapViewModel ? new Thickness(0) : new Thickness(30);

        [ObservableProperty]
        private int unreadNotificationsCount;

        [ObservableProperty]
        private string currentUsername = "admin";

        private readonly System.Timers.Timer _unreadTimer;

        public MainViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;

            // Start a timer to poll for unread notifications count every 2 seconds
            _unreadTimer = new System.Timers.Timer(2000);
            _unreadTimer.Elapsed += async (s, e) => await UpdateUnreadCountAsync();
            _unreadTimer.Start();

            _ = UpdateUnreadCountAsync();
        }

        private async System.Threading.Tasks.Task UpdateUnreadCountAsync()
        {
            try
            {
                var ns = new Keemya.Frontend.Services.NotificationService();
                int count = await ns.GetUnreadCountAsync();
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    UnreadNotificationsCount = count;
                });
            }
            catch {}
        }

        private void OnCurrentViewModelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
            OnPropertyChanged(nameof(IsHeaderVisible));
            OnPropertyChanged(nameof(MainGridMargin));
            _ = UpdateUnreadCountAsync();
        }

        public string CurrentLanguageText => LocalizationManager.CurrentLanguage == "ar" ? "English" : "العربية";

        [RelayCommand]
        private void ToggleLanguage()
        {
            string nextLang = LocalizationManager.CurrentLanguage == "ar" ? "en" : "ar";
            LocalizationManager.SetLanguage(nextLang);
            OnPropertyChanged(nameof(CurrentLanguageText));
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
        private void NavigateToNotifications()
        {
            _navigationStore.CurrentViewModel = new NotificationsViewModel(_navigationStore);
        }
    }
}
