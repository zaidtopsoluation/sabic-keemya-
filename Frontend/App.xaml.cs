using System;
using System.Threading.Tasks;
using System.Windows;
using Keemya.Frontend.Stores;
using Keemya.Frontend.ViewModels;
namespace Keemya.Frontend
{
    public partial class App : Application
    {
        // XamlParseException hides the real cause (file, line, control) in its inner exception, so put that first.
        internal static string FormatError(Exception error)
        {
            Exception root = error.GetBaseException();
            return $"{root.GetType().Name}: {root.Message}\n\n{error}";
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── Global exception handlers — show errors instead of silent crash ──
            DispatcherUnhandledException += (s, ex) =>
            {
                string report = FormatError(ex.Exception);
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{report}\n\n"); }
                catch { /* install folder not writable: still show the dialog */ }
                MessageBox.Show($"UI Error (details saved to error.log):\n\n{report}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Server PCs keep their own MySQL in sync with the other server (no-op on dispatchers)
            Keemya.Frontend.Services.DatabaseSyncService.StartForThisServer();

            // Servers play dispatcher live PA into their aux output; listen from startup, not only while the Command Center is open.
            if (AppConfig.IsControllerStation)
            {
                try { Keemya.Frontend.Services.AudioCommunicationService.Instance.StartListening(); }
                catch { }
            }
            else
            {
                // Dispatchers re-check their server every 5 s: if it goes down mid-session, commands move to the other server's queue.
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(5000);
                        try { AppConfig.EnsureHealthyConnection(); } catch { }
                    }
                });
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
