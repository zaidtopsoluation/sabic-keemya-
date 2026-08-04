using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Keemya.Frontend.ViewModels;
using Keemya.Frontend.Models;
using Keemya.Frontend.Stores;

namespace Keemya.Frontend.Views
{
    public partial class MapView : UserControl
    {
        private MapViewModel? _viewModel;

        public MapView()
        {
            InitializeComponent();
            DataContextChanged += MapView_DataContextChanged;
            _ = InitializeAsync();
        }

        private void MapView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.ToastRequested -= ViewModel_ToastRequested;
                _viewModel.SirensDataChanged -= ViewModel_SirensDataChanged;
            }

            _viewModel = DataContext as MapViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.ToastRequested += ViewModel_ToastRequested;
                _viewModel.SirensDataChanged += ViewModel_SirensDataChanged;
            }
        }

        private async void ViewModel_SirensDataChanged()
        {
            if (_viewModel != null && MapWebView.CoreWebView2 != null)
            {
                try
                {
                    string sirensJson = await _viewModel.GetSirensJsonAsync();
                    await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateSirens('{sirensJson}')");
                }
                catch { }
            }
        }

        private void ViewModel_ToastRequested(bool isSuccess, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (MapWebView.CoreWebView2 != null)
                {
                    string jsSafeMessage = message.Replace("'", "\\'");
                    MapWebView.CoreWebView2.ExecuteScriptAsync($"showToast({isSuccess.ToString().ToLower()}, '{jsSafeMessage}')");
                }
            });
        }

        private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapViewModel.SelectedSiren) && MapWebView.CoreWebView2 != null)
            {
                if (_viewModel?.SelectedSiren != null)
                {
                    string idStr = _viewModel.SelectedSiren.Id.ToString();
                    string source = _viewModel.LastSirenClickSource;
                    if (source == "status")
                    {
                        await MapWebView.CoreWebView2.ExecuteScriptAsync($"selectSiren('{idStr}', false, '{source}')");
                    }

                    var data = new
                    {
                        dcVoltage = _viewModel.HealthDcVoltage,
                        acVoltage = _viewModel.HealthAcVoltage,
                        temperature = _viewModel.HealthTemperature,
                        sirenOn = _viewModel.HealthSirenOn,
                        rotorActive = _viewModel.HealthRotorActive,
                        acOn = _viewModel.HealthAcOn,
                        storedAc = _viewModel.HealthStoredAc,
                        dynamicAc = _viewModel.HealthDynamicAc,
                        fullAlert = _viewModel.HealthFullAlert,
                        partialAlert = _viewModel.HealthPartialAlert,
                        intrusion = _viewModel.HealthIntrusion,
                        strobeActive = _viewModel.HealthStrobeActive,
                        biasDetected = _viewModel.HealthBiasDetected,
                        systemArmed = _viewModel.HealthSystemArmed,
                        systemPowerUp = _viewModel.HealthSystemPowerUp,
                        supervisorMode = _viewModel.HealthSupervisorMode
                    };
                    string jsonStr = System.Text.Json.JsonSerializer.Serialize(data);
                    string escapedJsonStr = jsonStr.Replace("'", "\\'");
                    await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateSirenHealthJson('{escapedJsonStr}')");
                }
                else if (_viewModel?.TargetedSirens == null || _viewModel.TargetedSirens.Count == 0)
                {
                    await MapWebView.CoreWebView2.ExecuteScriptAsync("selectSiren(null)");
                }
            }
            else if (e.PropertyName == nameof(MapViewModel.TargetedSirens) && MapWebView.CoreWebView2 != null)
            {
                if (_viewModel?.TargetedSirens != null && _viewModel.TargetedSirens.Count > 0 && _viewModel.SelectedSiren == null)
                {
                    var names = string.Join(", ", _viewModel.TargetedSirens.Select(s => s.Name));
                    var jsNames = names.Replace("'", "\\'");
                    await MapWebView.CoreWebView2.ExecuteScriptAsync($"selectZone('{jsNames}', {_viewModel.TargetedSirens.Count})");
                }
            }
            else if (e.PropertyName != null && e.PropertyName.StartsWith("Health") && MapWebView.CoreWebView2 != null)
            {
                if (_viewModel != null && _viewModel.SelectedSiren != null)
                {
                    var data = new
                    {
                        dcVoltage = _viewModel.HealthDcVoltage,
                        acVoltage = _viewModel.HealthAcVoltage,
                        temperature = _viewModel.HealthTemperature,
                        sirenOn = _viewModel.HealthSirenOn,
                        rotorActive = _viewModel.HealthRotorActive,
                        acOn = _viewModel.HealthAcOn,
                        storedAc = _viewModel.HealthStoredAc,
                        dynamicAc = _viewModel.HealthDynamicAc,
                        fullAlert = _viewModel.HealthFullAlert,
                        partialAlert = _viewModel.HealthPartialAlert,
                        intrusion = _viewModel.HealthIntrusion,
                        strobeActive = _viewModel.HealthStrobeActive,
                        biasDetected = _viewModel.HealthBiasDetected,
                        systemArmed = _viewModel.HealthSystemArmed,
                        systemPowerUp = _viewModel.HealthSystemPowerUp,
                        supervisorMode = _viewModel.HealthSupervisorMode
                    };
                    string jsonStr = System.Text.Json.JsonSerializer.Serialize(data);
                    string escapedJsonStr = jsonStr.Replace("'", "\\'");
                    await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateSirenHealthJson('{escapedJsonStr}')");
                }
            }
            else if ((e.PropertyName == nameof(MapViewModel.IsCommandRunning) ||
                      e.PropertyName == nameof(MapViewModel.ActiveDurationDisplay) ||
                      e.PropertyName == nameof(MapViewModel.CommandToConfirmName) ||
                      e.PropertyName == nameof(MapViewModel.IsPublicAddressActive)) && MapWebView.CoreWebView2 != null)
            {
                if (_viewModel != null)
                {
                    bool isRunning = _viewModel.IsCommandRunning;
                    string name = (_viewModel.CommandToConfirmName ?? "").Replace("'", "\\'");
                    string display = (_viewModel.ActiveDurationDisplay ?? "").Replace("'", "\\'");
                    bool isPA = _viewModel.IsPublicAddressActive;
                    await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateRunningCommandState({isRunning.ToString().ToLower()}, '{name}', '{display}', {isPA.ToString().ToLower()})");
                }
            }
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                // Specify custom isolated UserDataFolder in AppData to prevent lock/access issues
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string userDataFolder = Path.Combine(localAppData, "Keemya.Frontend.WebView2");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                
                await MapWebView.EnsureCoreWebView2Async(env);
                
                // Auto-grant geolocation permission
                MapWebView.CoreWebView2.PermissionRequested += (s, e) =>
                {
                    if (e.PermissionKind == Microsoft.Web.WebView2.Core.CoreWebView2PermissionKind.Geolocation)
                    {
                        e.State = Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Allow;
                    }
                };

                // Robust path finder for map.html
                string baseDir = AppContext.BaseDirectory;
                string htmlPath = Path.Combine(baseDir, "Resources", "map.html");
                string sourcePath = Path.Combine(baseDir, "Frontend", "Resources", "map.html");
                string currentPath = Path.Combine(Directory.GetCurrentDirectory(), "Frontend", "Resources", "map.html");
                string currentAltPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "map.html");

                if (File.Exists(htmlPath))
                {
                    MapWebView.Source = new Uri(htmlPath);
                }
                else if (File.Exists(sourcePath))
                {
                    MapWebView.Source = new Uri(sourcePath);
                }
                else if (File.Exists(currentPath))
                {
                    MapWebView.Source = new Uri(currentPath);
                }
                else if (File.Exists(currentAltPath))
                {
                    MapWebView.Source = new Uri(currentAltPath);
                }
                else
                {
                    Keemya.Frontend.Services.SirenCommunicationService.Instance.Log("❌ Error: map.html could not be found in any standard path!");
                }

                MapWebView.CoreWebView2.NavigationCompleted += async (s, e) =>
                {
                    // Always use light OpenStreetMap tiles as requested
                    bool isDark = false; 
                    await MapWebView.CoreWebView2.ExecuteScriptAsync($"setTheme({isDark.ToString().ToLower()})");

                    if (DataContext is MapViewModel vm)
                    {
                        string sirensJson = await vm.GetSirensJsonAsync();
                        await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateSirens('{sirensJson}')");

                        string zonesJson = vm.GetZonesJson();
                        await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateZones('{zonesJson}')");

                        string commandsJson = vm.GetCommandsJson();
                        await MapWebView.CoreWebView2.ExecuteScriptAsync($"updateCommands('{commandsJson}')");

                        await MapWebView.CoreWebView2.ExecuteScriptAsync($"setUserRole('{Session.Role}')");

                        if (vm.SelectedSiren != null)
                        {
                            string idStr = vm.SelectedSiren.Id.ToString();
                            string source = vm.LastSirenClickSource;
                            await MapWebView.CoreWebView2.ExecuteScriptAsync($"selectSiren('{idStr}', false, '{source}')");
                        }
                    }
                };

                MapWebView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    string json = e.TryGetWebMessageAsString();
                    if (string.IsNullOrEmpty(json)) return;

                    try
                    {
                        var msg = System.Text.Json.JsonDocument.Parse(json);
                        string type = msg.RootElement.GetProperty("type").GetString() ?? "";

                        if (DataContext is MapViewModel vm)
                        {
                            if (type == "sirenClicked")
                            {
                                string idStr = msg.RootElement.GetProperty("id").GetString() ?? "";
                                if (Guid.TryParse(idStr, out Guid guidId))
                                {
                                    var siren = vm.Sirens.FirstOrDefault(x => x.Id == guidId);
                                    if (siren != null)
                                    {
                                        Dispatcher.Invoke(() => 
                                        {
                                            vm.ClickSirenFromMapCommand.Execute(siren);
                                        });
                                    }
                                }
                            }
                            else if (type == "sirenSelectionChanged")
                            {
                                var idsProp = msg.RootElement.GetProperty("ids");
                                var listIds = new List<Guid>();
                                foreach (var item in idsProp.EnumerateArray())
                                {
                                    string idStr = item.GetString() ?? "";
                                    if (Guid.TryParse(idStr, out Guid guidId))
                                    {
                                        listIds.Add(guidId);
                                    }
                                }

                                Dispatcher.Invoke(() => 
                                {
                                    vm.UpdateSirenSelectionFromMap(listIds);
                                });
                            }
                            else if (type == "zoneClicked")
                            {
                                string idStr = msg.RootElement.GetProperty("id").GetString() ?? "";
                                if (Guid.TryParse(idStr, out Guid zoneId))
                                {
                                    var zone = vm.Zones.FirstOrDefault(z => z.Id == zoneId);
                                    if (zone != null)
                                    {
                                        Dispatcher.Invoke(() => 
                                        {
                                            vm.ClickZoneCommand.Execute(zone);
                                        });
                                    }
                                }
                            }
                            else if (type == "activateCommand")
                            {
                                string idStr = msg.RootElement.GetProperty("id").GetString() ?? "";
                                if (Guid.TryParse(idStr, out Guid cmdId))
                                {
                                    var card = vm.CommandCards.FirstOrDefault(x => x.Id == cmdId);
                                    if (card != null)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            vm.ActivateCommandCommand.Execute(card);
                                        });
                                    }
                                }
                            }
                            else if (type == "stopCommand")
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    vm.StopRunningCommandCommand.Execute(null);
                                });
                            }
                            else if (type == "clearSelection")
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    vm.ClearSelectionCommand.Execute(null);
                                });
                            }
                            else if (type == "back")
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    vm.BackCommand.Execute(null);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing WebMessage: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Map WebView2: {ex.Message}\nEnsure Microsoft Edge WebView2 Runtime is installed.", 
                    "WebView2 Init Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // ── XAML VALUE CONVERTERS (Kept for compilation safety/compatibility) ────

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = parameter as string == "Inverted";

            bool show = invert ? isNull : !isNull;
            return show ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ToggleStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? "CONNECTED" : "DISCONNECTED";
            }
            return "DISCONNECTED";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
