using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Models;
using Keemya.Frontend.Stores;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    public partial class ZoneManagementViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private List<ZoneDto> _allZones = new();

        [ObservableProperty]
        private ObservableCollection<ZoneDto> zones = new();

        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private bool isAddZonePopupOpen = false;

        // Popup Input Fields
        [ObservableProperty]
        private string groupName = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string selectedColor = "Red";

        [ObservableProperty]
        private string selectedShape = "Rectangle";

        [ObservableProperty]
        private string popupTitle = "Add Group";

        [ObservableProperty]
        private string popupSubtitle = "Create a new group by providing the necessary information.";

        [ObservableProperty]
        private string popupErrorMessage = string.Empty;

        [ObservableProperty]
        private bool hasPopupError = false;

        private bool _isEditMode = false;
        private Guid? _editingZoneId = null;

        // Color & Shape options matching screenshots
        public ObservableCollection<ColorOption> ColorOptions { get; } = new()
        {
            new ColorOption { Name = "Red", Hex = "#EF4444" },
            new ColorOption { Name = "Orange", Hex = "#F97316" },
            new ColorOption { Name = "Yellow", Hex = "#EAB308" },
            new ColorOption { Name = "Green", Hex = "#10B981" },
            new ColorOption { Name = "Blue", Hex = "#3B82F6" },
            new ColorOption { Name = "Purple", Hex = "#8B5CF6" },
            new ColorOption { Name = "Pink", Hex = "#EC4899" },
            new ColorOption { Name = "Cyan", Hex = "#06B6D4" },
            new ColorOption { Name = "Custom Color", Hex = "#6366F1" }
        };

        public List<string> ShapeOptions { get; } = new() { "Rectangle", "Circle", "Triangle" };

        public ZoneManagementViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _ = LoadZonesFromDatabase();
        }

        partial void OnSearchQueryChanged(string value)
        {
            ApplyFiltering();
        }

        partial void OnSelectedColorChanged(string value)
        {
            if (value == "Custom Color")
            {
                using var dialog = new System.Windows.Forms.ColorDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                    
                    var existing = ColorOptions.FirstOrDefault(c => c.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase) && c.Name != "Custom Color");
                    if (existing != null)
                    {
                        SelectedColor = existing.Name;
                    }
                    else
                    {
                        var customHexOpt = ColorOptions.FirstOrDefault(c => c.Name.StartsWith("#"));
                        if (customHexOpt != null) ColorOptions.Remove(customHexOpt);
                        
                        ColorOptions.Insert(ColorOptions.Count - 1, new ColorOption { Name = hex, Hex = hex });
                        SelectedColor = hex;
                    }
                }
                else
                {
                    SelectedColor = "Red"; // Fallback if cancelled
                }
            }
        }

        private async Task LoadZonesFromDatabase()
        {
            try
            {
                var loadedZones = new List<ZoneDto>();
                string connStr = AppConfig.ConnectionString;

                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    // Load groups and calculate online devices
                    string query = @"
                        SELECT g.Id, g.Name, g.Description, g.Color, g.Shape,
                               (SELECT COUNT(*) FROM SirenDevices d WHERE d.GroupId = g.Id) as TotalDevices,
                               (SELECT COUNT(*) FROM SirenDevices d WHERE d.GroupId = g.Id AND d.Status = 'ONLINE') as OnlineDevices
                        FROM SirenGroups g
                        ORDER BY g.Name;";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid id = reader.GetGuid(0);
                                string name = reader.GetString(1);
                                string desc = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                                string color = reader.IsDBNull(3) ? "Red" : reader.GetString(3);
                                string shape = reader.IsDBNull(4) ? "Rectangle" : reader.GetString(4);
                                int totalDev = reader.GetInt32(5);
                                int onlineDev = reader.GetInt32(6);

                                loadedZones.Add(new ZoneDto
                                {
                                    Id = id,
                                    Name = name,
                                    Description = desc,
                                    Color = color,
                                    Shape = shape,
                                    TotalDevices = totalDev,
                                    OnlineDevices = onlineDev
                                });
                            }
                        }
                    }
                }

                _allZones = loadedZones;
                ApplyFiltering();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading zones: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFiltering()
        {
            var filtered = _allZones.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string term = SearchQuery.ToLower();
                filtered = filtered.Where(z => z.Name.ToLower().Contains(term) || z.Description.ToLower().Contains(term));
            }

            Zones = new ObservableCollection<ZoneDto>(filtered);
        }

        [RelayCommand]
        private void Back()
        {
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }

        [RelayCommand]
        private void OpenAddZonePopup()
        {
            GroupName = string.Empty;
            Description = string.Empty;
            SelectedColor = "Red";
            SelectedShape = "Rectangle";
            PopupErrorMessage = string.Empty;
            HasPopupError = false;

            _isEditMode = false;
            _editingZoneId = null;

            // Load localized strings for Popup
            PopupTitle = Application.Current.Resources["AddGroup"] as string ?? "Add Group";
            PopupSubtitle = Application.Current.Resources["AddGroupPopupDesc"] as string ?? "Create a new group by providing the necessary information.";

            IsAddZonePopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditZonePopup(ZoneDto zone)
        {
            if (zone == null) return;

            GroupName = zone.Name;
            Description = zone.Description;
            
            // Handle custom hex color restoring
            if (zone.Color.StartsWith("#") && !ColorOptions.Any(c => c.Name == zone.Color))
            {
                var customHexOpt = ColorOptions.FirstOrDefault(c => c.Name.StartsWith("#"));
                if (customHexOpt != null) ColorOptions.Remove(customHexOpt);
                ColorOptions.Insert(ColorOptions.Count - 1, new ColorOption { Name = zone.Color, Hex = zone.Color });
            }
            
            SelectedColor = zone.Color;
            SelectedShape = zone.Shape;
            PopupErrorMessage = string.Empty;
            HasPopupError = false;

            _isEditMode = true;
            _editingZoneId = zone.Id;

            // Load localized strings for Popup
            PopupTitle = Application.Current.Resources["EditGroup"] as string ?? "Edit Group";
            PopupSubtitle = Application.Current.Resources["EditGroupPopupDesc"] as string ?? "Update this group by providing the necessary information.";

            IsAddZonePopupOpen = true;
        }

        [RelayCommand]
        private void CloseAddZonePopup()
        {
            IsAddZonePopupOpen = false;
        }

        [RelayCommand]
        private async Task SaveZone()
        {
            if (string.IsNullOrWhiteSpace(GroupName))
            {
                PopupErrorMessage = LocalizationManager.CurrentLanguage == "ar" ? "اسم المجموعة مطلوب" : "Group name is required";
                HasPopupError = true;
                return;
            }

            try
            {
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    if (_isEditMode && _editingZoneId.HasValue)
                    {
                        // Update
                        string updateQuery = "UPDATE SirenGroups SET Name = @Name, Description = @Desc, Color = @Color, Shape = @Shape WHERE Id = @Id";
                        using (var command = new MySqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Name", GroupName);
                            command.Parameters.AddWithValue("@Desc", Description);
                            command.Parameters.AddWithValue("@Color", SelectedColor);
                            command.Parameters.AddWithValue("@Shape", SelectedShape);
                            command.Parameters.AddWithValue("@Id", _editingZoneId.Value.ToString());

                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // Insert
                        string insertQuery = "INSERT INTO SirenGroups (Id, Name, Description, Color, Shape) VALUES (@Id, @Name, @Desc, @Color, @Shape)";
                        using (var command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                            command.Parameters.AddWithValue("@Name", GroupName);
                            command.Parameters.AddWithValue("@Desc", Description);
                            command.Parameters.AddWithValue("@Color", SelectedColor);
                            command.Parameters.AddWithValue("@Shape", SelectedShape);

                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }

                IsAddZonePopupOpen = false;
                await LoadZonesFromDatabase();
            }
            catch (Exception ex)
            {
                PopupErrorMessage = $"Database error: {ex.Message}";
                HasPopupError = true;
            }
        }

        [RelayCommand]
        private async Task DeleteZone(ZoneDto zone)
        {
            if (zone == null) return;

            string confirmMsg = LocalizationManager.CurrentLanguage == "ar" 
                ? $"هل أنت متأكد من حذف المنطقة '{zone.Name}'؟" 
                : $"Are you sure you want to delete the zone '{zone.Name}'?";

            var result = MessageBox.Show(confirmMsg, 
                LocalizationManager.CurrentLanguage == "ar" ? "تأكيد الحذف" : "Confirm Delete", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    // Check if there are sirens referencing this zone
                    string checkQuery = "SELECT COUNT(*) FROM SirenDevices WHERE GroupId = @Id";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", zone.Id.ToString());
                        long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

                        if (count > 0)
                        {
                            string warningMsg = LocalizationManager.CurrentLanguage == "ar"
                                ? "لا يمكن حذف هذه المنطقة لأنها تحتوي على أجهزة صفارات إنذار نشطة. يرجى نقل الأجهزة إلى منطقة أخرى أولاً."
                                : "Cannot delete this zone because it contains active siren devices. Please re-assign devices first.";
                            MessageBox.Show(warningMsg, 
                                LocalizationManager.CurrentLanguage == "ar" ? "تنبيه" : "Warning", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Delete SirenGroup
                    string deleteQuery = "DELETE FROM SirenGroups WHERE Id = @Id";
                    using (var command = new MySqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Id", zone.Id.ToString());
                        await command.ExecuteNonQueryAsync();
                    }
                }

                await LoadZonesFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting zone: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ColorOption
    {
        public string Name { get; set; } = string.Empty;
        public string Hex { get; set; } = string.Empty;

        public string LocalizedName => LocalizationManager.CurrentLanguage == "ar" 
            ? Name switch
            {
                "Red" => "أحمر",
                "Orange" => "برتقالي",
                "Yellow" => "أصفر",
                "Green" => "أخضر",
                "Blue" => "أزرق",
                "Purple" => "بنفسجي",
                "Pink" => "وردي",
                "Cyan" => "سماوي",
                "Custom Color" => "لون مخصص",
                _ => Name
            }
            : Name;
    }
}
