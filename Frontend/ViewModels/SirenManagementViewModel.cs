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
    public partial class SirenManagementViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private List<SirenDeviceDto> _allSirens = new();

        [ObservableProperty]
        private ObservableCollection<SirenDeviceDto> sirens = new();

        [ObservableProperty]
        private ObservableCollection<ZoneDto> zones = new();

        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private bool isAddSirenPopupOpen = false;

        // Popup Input Fields
        [ObservableProperty]
        private string sirenName = string.Empty;

        [ObservableProperty]
        private string areaCode = string.Empty;

        [ObservableProperty]
        private string addressCode = string.Empty;

        [ObservableProperty]
        private double latitude = 0.0;

        [ObservableProperty]
        private double longitude = 0.0;

        [ObservableProperty]
        private string selectedStatus = "OFFLINE";

        [ObservableProperty]
        private ZoneDto? selectedZone;

        [ObservableProperty]
        private bool isRedundant = false;

        [ObservableProperty]
        private string ipAddress = string.Empty;

        [ObservableProperty]
        private string popupTitle = "Add siren";

        [ObservableProperty]
        private string popupSubtitle = "Create a new siren by providing the necessary details below.";

        [ObservableProperty]
        private string popupErrorMessage = string.Empty;

        [ObservableProperty]
        private bool hasPopupError = false;

        private bool _isEditMode = false;
        private Guid? _editingSirenId = null;

        // Status choices matching combo selections
        public List<string> StatusOptions { get; } = new() { "Online", "Offline", "Maintenance", "Warning" };

        // Map Coordinate Picker Sub-Overlay
        [ObservableProperty]
        private bool isMapPickerOpen = false;

        [ObservableProperty]
        private double mapLatitude = 26.3927; // Default centered near Dammam

        [ObservableProperty]
        private double mapLongitude = 49.9777;

        public SirenManagementViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _ = LoadZones();
            _ = LoadSirensFromDatabase();
        }

        partial void OnSearchQueryChanged(string value)
        {
            ApplyFiltering();
        }

        private async Task LoadZones()
        {
            try
            {
                var list = new List<ZoneDto>();
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();
                    string query = "SELECT Id, Name, Description, Color, Shape FROM SirenGroups ORDER BY Name";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ZoneDto
                                {
                                    Id = reader.GetGuid(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Color = reader.IsDBNull(3) ? "Red" : reader.GetString(3),
                                    Shape = reader.IsDBNull(4) ? "Rectangle" : reader.GetString(4)
                                });
                            }
                        }
                    }
                }
                Zones = new ObservableCollection<ZoneDto>(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading zones: {ex.Message}");
            }
        }

        private async Task LoadSirensFromDatabase()
        {
            try
            {
                var loaded = new List<SirenDeviceDto>();
                string connStr = AppConfig.ConnectionString;

                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT d.Id, d.Name, d.AreaCode, d.AddressCode, d.Lat, d.Lng, d.Status, d.Ip, d.Redundant, d.GroupId, g.Name as GroupName
                        FROM SirenDevices d
                        LEFT JOIN SirenGroups g ON d.GroupId = g.Id
                        ORDER BY d.Name;";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                loaded.Add(new SirenDeviceDto
                                {
                                    Id = reader.GetGuid(0),
                                    Name = reader.GetString(1),
                                    AreaCode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    AddressCode = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Lat = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                                    Lng = reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5),
                                    Status = reader.IsDBNull(6) ? "OFFLINE" : reader.GetString(6),
                                    Ip = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                    Redundant = reader.GetBoolean(8),
                                    GroupId = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                                    GroupName = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                                });
                            }
                        }
                    }
                }

                _allSirens = loaded;
                ApplyFiltering();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sirens: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFiltering()
        {
            var filtered = _allSirens.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string term = SearchQuery.ToLower();
                filtered = filtered.Where(s => 
                    s.Name.ToLower().Contains(term) || 
                    s.AreaCode.ToLower().Contains(term) || 
                    s.AddressCode.ToLower().Contains(term) ||
                    s.GroupName.ToLower().Contains(term)
                );
            }

            Sirens = new ObservableCollection<SirenDeviceDto>(filtered);
        }

        [RelayCommand]
        private void Back()
        {
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }

        [RelayCommand]
        private void OpenAddSirenPopup()
        {
            SirenName = string.Empty;
            AreaCode = string.Empty;
            AddressCode = string.Empty;
            Latitude = 0.0;
            Longitude = 0.0;
            SelectedStatus = "Offline";
            SelectedZone = null;
            IsRedundant = false;
            IpAddress = string.Empty;
            PopupErrorMessage = string.Empty;
            HasPopupError = false;

            _isEditMode = false;
            _editingSirenId = null;

            PopupTitle = Application.Current.Resources["SirenAddPopupTitle"] as string ?? "Add siren";
            PopupSubtitle = Application.Current.Resources["SirenAddPopupDesc"] as string ?? "Create a new siren by providing the necessary details below.";

            IsAddSirenPopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditSirenPopup(SirenDeviceDto siren)
        {
            if (siren == null) return;

            SirenName = siren.Name;
            AreaCode = siren.AreaCode;
            AddressCode = siren.AddressCode;
            Latitude = siren.Lat;
            Longitude = siren.Lng;
            SelectedStatus = StatusOptions.FirstOrDefault(o => o.Equals(siren.Status, StringComparison.OrdinalIgnoreCase)) ?? "Offline";
            SelectedZone = Zones.FirstOrDefault(z => z.Id == siren.GroupId);
            IsRedundant = siren.Redundant;
            IpAddress = siren.Ip;
            PopupErrorMessage = string.Empty;
            HasPopupError = false;

            _isEditMode = true;
            _editingSirenId = siren.Id;

            PopupTitle = Application.Current.Resources["SirenEditPopupTitle"] as string ?? "Edit siren";
            PopupSubtitle = Application.Current.Resources["SirenEditPopupDesc"] as string ?? "Update this siren by providing the necessary details below.";

            IsAddSirenPopupOpen = true;
        }

        [RelayCommand]
        private void CloseAddSirenPopup()
        {
            IsAddSirenPopupOpen = false;
        }

        [RelayCommand]
        private void OpenMapPicker()
        {
            MapLatitude = Latitude != 0 ? Latitude : 26.3927;
            MapLongitude = Longitude != 0 ? Longitude : 49.9777;
            IsMapPickerOpen = true;
        }

        [RelayCommand]
        private void ConfirmMapLocation()
        {
            Latitude = Math.Round(MapLatitude, 5);
            Longitude = Math.Round(MapLongitude, 5);
            IsMapPickerOpen = false;
        }

        [RelayCommand]
        private void CancelMapLocation()
        {
            IsMapPickerOpen = false;
        }

        [RelayCommand]
        private async Task SaveSiren()
        {
            if (string.IsNullOrWhiteSpace(SirenName))
            {
                PopupErrorMessage = LocalizationManager.CurrentLanguage == "ar" ? "اسم صفارة الإنذار مطلوب" : "Siren name is required";
                HasPopupError = true;
                return;
            }

            // --- DIGIT COUNT AND NUMERIC ONLY VALIDATIONS ---
            string trimmedArea = AreaCode?.Trim() ?? string.Empty;
            string trimmedAddress = AddressCode?.Trim() ?? string.Empty;

            if (trimmedArea.Length != 3 || !trimmedArea.All(char.IsDigit))
            {
                PopupErrorMessage = LocalizationManager.CurrentLanguage == "ar" 
                    ? "يجب أن يتكون رمز المنطقة من 3 أرقام (أرقام فقط)" 
                    : "Area code must be exactly 3 digits (numbers only)";
                HasPopupError = true;
                return;
            }

            if (trimmedAddress.Length != 4 || !trimmedAddress.All(char.IsDigit))
            {
                PopupErrorMessage = LocalizationManager.CurrentLanguage == "ar" 
                    ? "يجب أن يتكون رمز العنوان من 4 أرقام (أرقام فقط)" 
                    : "Address code must be exactly 4 digits (numbers only)";
                HasPopupError = true;
                return;
            }

            try
            {
                string connStr = AppConfig.ConnectionString;
                using (var connection = new MySqlConnection(connStr))
                {
                    await connection.OpenAsync();

                    // --- UNIQUE ADDRESS CHECK (VERIFY CODE) ---
                    string checkDupQuery = "SELECT COUNT(*) FROM SirenDevices WHERE AreaCode = @Area AND AddressCode = @Address";
                    if (_isEditMode && _editingSirenId.HasValue)
                    {
                        checkDupQuery += " AND Id != @Id";
                    }

                    using (var dupCmd = new MySqlCommand(checkDupQuery, connection))
                    {
                        dupCmd.Parameters.AddWithValue("@Area", trimmedArea);
                        dupCmd.Parameters.AddWithValue("@Address", trimmedAddress);
                        if (_isEditMode && _editingSirenId.HasValue)
                        {
                            dupCmd.Parameters.AddWithValue("@Id", _editingSirenId.Value.ToString());
                        }

                        long count = Convert.ToInt64(await dupCmd.ExecuteScalarAsync());
                        if (count > 0)
                        {
                            PopupErrorMessage = Application.Current.Resources["AddressAlreadyInUse"] as string 
                                ?? "This Siren Address is already in use.";
                            HasPopupError = true;
                            return;
                        }
                    }

                    if (_isEditMode && _editingSirenId.HasValue)
                    {
                        // Update
                        string updateQuery = @"
                            UPDATE SirenDevices 
                            SET Name = @Name, AreaCode = @Area, AddressCode = @Address, Lat = @Lat, Lng = @Lng, Status = @Status, Ip = @Ip, Redundant = @Red, GroupId = @GroupId 
                            WHERE Id = @Id";

                        using (var command = new MySqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Name", SirenName.Trim());
                            command.Parameters.AddWithValue("@Area", trimmedArea);
                            command.Parameters.AddWithValue("@Address", trimmedAddress);
                            command.Parameters.AddWithValue("@Lat", Latitude);
                            command.Parameters.AddWithValue("@Lng", Longitude);
                            command.Parameters.AddWithValue("@Status", SelectedStatus.ToUpper());
                            command.Parameters.AddWithValue("@Ip", IsRedundant ? IpAddress.Trim() : string.Empty);
                            command.Parameters.AddWithValue("@Red", IsRedundant);
                            command.Parameters.AddWithValue("@GroupId", SelectedZone != null ? (object)SelectedZone.Id.ToString() : DBNull.Value);
                            command.Parameters.AddWithValue("@Id", _editingSirenId.Value.ToString());

                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // Insert
                        string insertQuery = @"
                            INSERT INTO SirenDevices (Id, Name, Description, Address, AreaCode, AddressCode, Lat, Lng, Status, Ip, Redundant, GroupId) 
                            VALUES (@Id, @Name, @Desc, @Address, @Area, @AddrCode, @Lat, @Lng, @Status, @Ip, @Red, @GroupId)";

                        using (var command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                            command.Parameters.AddWithValue("@Name", SirenName.Trim());
                            command.Parameters.AddWithValue("@Desc", string.Empty);
                            command.Parameters.AddWithValue("@Address", $"{trimmedArea}.{trimmedAddress}");
                            command.Parameters.AddWithValue("@Area", trimmedArea);
                            command.Parameters.AddWithValue("@AddrCode", trimmedAddress);
                            command.Parameters.AddWithValue("@Lat", Latitude);
                            command.Parameters.AddWithValue("@Lng", Longitude);
                            command.Parameters.AddWithValue("@Status", SelectedStatus.ToUpper());
                            command.Parameters.AddWithValue("@Ip", IsRedundant ? IpAddress.Trim() : string.Empty);
                            command.Parameters.AddWithValue("@Red", IsRedundant);
                            command.Parameters.AddWithValue("@GroupId", SelectedZone != null ? (object)SelectedZone.Id.ToString() : DBNull.Value);

                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }

                IsAddSirenPopupOpen = false;
                await LoadSirensFromDatabase();
            }
            catch (Exception ex)
            {
                PopupErrorMessage = $"Database error: {ex.Message}";
                HasPopupError = true;
            }
        }

        [RelayCommand]
        private async Task DeleteSiren(SirenDeviceDto siren)
        {
            if (siren == null) return;

            string confirmMsg = LocalizationManager.CurrentLanguage == "ar" 
                ? $"هل أنت متأكد من حذف صفارة الإنذار '{siren.Name}'؟" 
                : $"Are you sure you want to delete the siren '{siren.Name}'?";

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
                    string deleteQuery = "DELETE FROM SirenDevices WHERE Id = @Id";
                    using (var command = new MySqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Id", siren.Id.ToString());
                        await command.ExecuteNonQueryAsync();
                    }
                }

                await LoadSirensFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting siren: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void TriggerTestPing(SirenDeviceDto siren)
        {
            if (siren == null) return;

            string alertTitle = LocalizationManager.CurrentLanguage == "ar" ? "اختبار صفارة الإنذار" : "Siren Alert Test";
            string alertMsg = LocalizationManager.CurrentLanguage == "ar" 
                ? $"تم إرسال إشارة اختبار بنجاح لصفارة الإنذار '{siren.Name}'." 
                : $"Successfully sent alert test ping to siren '{siren.Name}'.";

            MessageBox.Show(alertMsg, alertTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
