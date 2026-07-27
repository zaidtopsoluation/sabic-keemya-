using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Keemya.Frontend.ViewModels;

namespace Keemya.Frontend.Views
{
    public partial class SirenManagementView : UserControl
    {
        private SirenManagementViewModel? _vm;
        private bool _pickerWebViewReady = false;

        public SirenManagementView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        // ── Track the ViewModel so we can react to IsMapPickerOpen ──────────
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
                _vm.PropertyChanged -= Vm_PropertyChanged;

            _vm = DataContext as SirenManagementViewModel;

            if (_vm != null)
                _vm.PropertyChanged += Vm_PropertyChanged;
        }

        private async void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // When the picker overlay becomes visible → initialise/refresh the WebView2
            if (e.PropertyName == nameof(SirenManagementViewModel.IsMapPickerOpen) && _vm != null)
            {
                if (_vm.IsMapPickerOpen)
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (!_pickerWebViewReady)
                            await InitPickerWebViewAsync();
                        else
                            await RefreshPickerMapAsync();
                    });
                }
            }
        }

        // ── One-time WebView2 initialisation ─────────────────────────────────
        private async System.Threading.Tasks.Task InitPickerWebViewAsync()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                // Use a separate user-data folder so it does not clash with the main MapView
                string userDataFolder = Path.Combine(localAppData, "Keemya.Frontend.PickerWebView2");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);

                await PickerMapWebView.EnsureCoreWebView2Async(env);

                // Resolve picker_map.html path
                string baseDir = AppContext.BaseDirectory;
                string htmlPath       = Path.Combine(baseDir, "Resources", "picker_map.html");
                string sourcePath     = Path.Combine(baseDir, "Frontend", "Resources", "picker_map.html");
                string currentPath    = Path.Combine(Directory.GetCurrentDirectory(), "Frontend", "Resources", "picker_map.html");
                string currentAltPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "picker_map.html");

                string resolvedPath =
                    File.Exists(htmlPath)       ? htmlPath       :
                    File.Exists(sourcePath)     ? sourcePath     :
                    File.Exists(currentPath)    ? currentPath    :
                    File.Exists(currentAltPath) ? currentAltPath : string.Empty;

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    MessageBox.Show("picker_map.html could not be found.", "Map Picker Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Once navigation completes → call JS to centre on current coords
                PickerMapWebView.CoreWebView2.NavigationCompleted += async (s, navArgs) =>
                {
                    if (_vm != null)
                    {
                        double lat = _vm.MapLatitude;
                        double lng = _vm.MapLongitude;
                        await PickerMapWebView.CoreWebView2
                            .ExecuteScriptAsync($"initPickerMap({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lng.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
                    }
                };

                // Receive messages back from JavaScript
                PickerMapWebView.CoreWebView2.WebMessageReceived += OnPickerWebMessageReceived;

                PickerMapWebView.Source = new Uri(resolvedPath);
                _pickerWebViewReady = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialise map picker: {ex.Message}\nEnsure Microsoft Edge WebView2 Runtime is installed.",
                    "WebView2 Init Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Re-centre the map when picker is reopened with new default coords ─
        private async System.Threading.Tasks.Task RefreshPickerMapAsync()
        {
            if (PickerMapWebView.CoreWebView2 == null || _vm == null) return;

            double lat = _vm.MapLatitude;
            double lng = _vm.MapLongitude;
            await PickerMapWebView.CoreWebView2
                .ExecuteScriptAsync($"initPickerMap({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lng.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        }

        // ── Handle JS → C# messages ──────────────────────────────────────────
        private void OnPickerWebMessageReceived(object? sender,
            Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var doc  = System.Text.Json.JsonDocument.Parse(json);
                string type = doc.RootElement.GetProperty("type").GetString() ?? "";

                Dispatcher.Invoke(() =>
                {
                    if (_vm == null) return;

                    if (type == "locationPicked")
                    {
                        // Live update ViewModel as user clicks / drags the pin
                        double lat = doc.RootElement.GetProperty("lat").GetDouble();
                        double lng = doc.RootElement.GetProperty("lng").GetDouble();
                        _vm.MapLatitude  = Math.Round(lat, 5);
                        _vm.MapLongitude = Math.Round(lng, 5);
                    }
                    else if (type == "confirmedLocation")
                    {
                        // User pressed Confirm in JS (not used currently — WPF button does it)
                        if (doc.RootElement.GetProperty("lat").ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            double lat = doc.RootElement.GetProperty("lat").GetDouble();
                            double lng = doc.RootElement.GetProperty("lng").GetDouble();
                            _vm.MapLatitude  = Math.Round(lat, 5);
                            _vm.MapLongitude = Math.Round(lng, 5);
                        }
                        _vm.ConfirmMapLocationCommand.Execute(null);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PickerMap message error: {ex.Message}");
            }
        }
    }
}
