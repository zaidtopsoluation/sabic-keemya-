using System;
using System.Threading.Tasks;
using System.Windows;
using Keemya.Frontend.Stores;
using Keemya.Frontend.ViewModels;
namespace Keemya.Frontend
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── Global exception handlers — show errors instead of silent crash ──
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show(
                    $"UI Error:\n\n{ex.Exception.GetType().Name}\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                    "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                ex.SetObserved();
                var baseEx = ex.Exception.GetBaseException();
                if (baseEx is TaskCanceledException || baseEx is OperationCanceledException)
                {
                    return; // Ignore canceled background task timeouts silently
                }

                Current?.Dispatcher?.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Background Task Error:\n\n{baseEx.GetType().Name}\n{baseEx.Message}",
                        "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            };

            // ── Startup ─────────────────────────────────────────────────────────
            Keemya.Frontend.Services.DatabaseInitializer.InitializeDatabase();
            LocalizationManager.Initialize();

            // ── License Verification ────────────────────────────────────────────
            if (!Keemya.Frontend.Services.LicenseService.VerifyLicense(out _))
            {
                var licenseWindow = new LicenseWindow();
                licenseWindow.FlowDirection = LocalizationManager.CurrentLanguage == "ar"
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
                licenseWindow.Show();
                return;
            }

            // Initialize audit logs table
            try
            {
                var auditLogService = new Keemya.Frontend.Services.AuditLogService();
                _ = auditLogService.EnsureTableExistsAsync(); // Fire and forget or block
            }
            catch { }

            var (username, role) = SessionManager.GetSession();
            if (!string.IsNullOrEmpty(username))
            {
                Session.Username = username;
                Session.Role = role ?? "Admin";

                var navigationStore = new NavigationStore();
                navigationStore.CurrentViewModel = new DashboardViewModel(navigationStore);

                var mainViewModel = new MainViewModel(navigationStore);
                mainViewModel.CurrentUsername = username;

                var mainWindow = new MainWindow();
                mainWindow.DataContext = mainViewModel;
                mainWindow.FlowDirection = LocalizationManager.CurrentLanguage == "ar"
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
                mainWindow.Show();
            }
            else
            {
                var loginWindow = new LoginWindow();
                loginWindow.FlowDirection = LocalizationManager.CurrentLanguage == "ar"
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
                loginWindow.Show();
            }
        }
    }
}
