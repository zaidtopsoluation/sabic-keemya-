using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Models;
using Keemya.Frontend.Services;
using Keemya.Frontend.Stores;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    /// <summary>Admin/Service module: add, edit, color, reorder and delete the activation buttons shown in Command Center and Map.</summary>
    public partial class ButtonConfigViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private Guid? _editingId;

        [ObservableProperty] private ObservableCollection<CommandConfigDto> buttons = new();

        [ObservableProperty] private bool isPopupOpen;
        [ObservableProperty] private string popupTitle = "Add button";
        [ObservableProperty] private string popupErrorMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewText))]
        private string buttonLabel = string.Empty;

        [ObservableProperty] private ToneOption? selectedTone;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewBackground))]
        [NotifyPropertyChangedFor(nameof(PreviewForeground))]
        [NotifyPropertyChangedFor(nameof(PickerColor))]
        private string buttonColor = "#A72A2A";

        // Bridges the visual ColorPicker to the stored hex string
        public System.Windows.Media.Color PickerColor
        {
            get => PanelButtonService.IsValidColor(ButtonColor)
                ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ButtonColor)
                : System.Windows.Media.Colors.Gray;
            set
            {
                string hex = PanelButtonService.ToHex(value);
                if (!string.Equals(hex, ButtonColor, StringComparison.OrdinalIgnoreCase)) ButtonColor = hex;
            }
        }

        [ObservableProperty] private string durationText = "90";
        [ObservableProperty] private bool isButtonEnabled = true;

        public IReadOnlyList<ToneOption> Tones => PanelButtonService.Tones;
        public IReadOnlyList<string> PresetColors => PanelButtonService.PresetColors;

        public string PreviewText => string.IsNullOrWhiteSpace(ButtonLabel) ? "BUTTON" : ButtonLabel.Trim().ToUpper();
        public string PreviewBackground => PanelButtonService.IsValidColor(ButtonColor) ? ButtonColor : "#475569";
        public string PreviewForeground => PanelButtonService.TextColorFor(PreviewBackground);

        public ButtonConfigViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _ = LoadAsync();
        }

        private static bool CanEdit()
        {
            string role = (Session.Role ?? "").ToUpper();
            return role == "ADMIN" || role == "ADMINISTRATOR" || role == "SERVICE";
        }

        private async Task LoadAsync()
        {
            try
            {
                Buttons = new ObservableCollection<CommandConfigDto>(await PanelButtonService.LoadAsync(includeDisabled: true));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading buttons:\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Back() => _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);

        [RelayCommand]
        private void OpenAdd()
        {
            _editingId = null;
            PopupTitle = "Add button";
            ButtonLabel = string.Empty;
            SelectedTone = Tones.FirstOrDefault(t => t.Hex == 0x01);
            ButtonColor = "#A72A2A";
            DurationText = "90";
            IsButtonEnabled = true;
            PopupErrorMessage = string.Empty;
            IsPopupOpen = true;
        }

        [RelayCommand]
        private void OpenEdit(CommandConfigDto button)
        {
            if (button == null) return;
            _editingId = button.Id;
            PopupTitle = "Edit button";
            ButtonLabel = button.Name;
            SelectedTone = Tones.FirstOrDefault(t => t.Hex == button.CommandHex);
            ButtonColor = button.ColorHex;
            DurationText = button.Duration.ToString();
            IsButtonEnabled = button.IsEnabled;
            PopupErrorMessage = string.Empty;
            IsPopupOpen = true;
        }

        [RelayCommand]
        private void ClosePopup() => IsPopupOpen = false;

        [RelayCommand]
        private void PickColor(string color) => ButtonColor = color;

        [RelayCommand]
        private async Task Save()
        {
            if (!CanEdit()) return;

            string? error = PanelButtonService.Validate(ButtonLabel, SelectedTone?.Hex, ButtonColor, DurationText, out int duration);
            if (error != null)
            {
                PopupErrorMessage = error;
                return;
            }

            var existing = Buttons.FirstOrDefault(b => b.Id == _editingId);
            var button = new CommandConfigDto
            {
                Id = _editingId ?? Guid.NewGuid(),
                Name = ButtonLabel.Trim().ToUpper(),
                CommandType = SelectedTone!.Name,
                CommandHex = SelectedTone.Hex,
                Color = ButtonColor.ToUpper(),
                Duration = duration,
                IsEnabled = IsButtonEnabled,
                SortOrder = existing?.SortOrder ?? (Buttons.Count == 0 ? 1 : Buttons.Max(b => b.SortOrder) + 1)
            };

            try
            {
                await PanelButtonService.SaveAsync(button);
                await new AuditLogService().LogAsync(Session.Username, existing == null ? "Create" : "Update",
                    $"Activation button '{button.Name}' -> {button.ToneDisplay}, color {button.Color}, run time {button.Duration}s, enabled {button.IsEnabled}",
                    "Button Configuration", button.Id.ToString());
                IsPopupOpen = false;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                PopupErrorMessage = $"Could not save: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task Delete(CommandConfigDto button)
        {
            if (button == null || !CanEdit()) return;
            if (MessageBox.Show($"Delete button '{button.Name}'?\nIt will disappear from Command Center and Map.", "Delete Button",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                await PanelButtonService.DeleteAsync(button.Id);
                await new AuditLogService().LogAsync(Session.Username, "Delete", $"Activation button '{button.Name}' deleted",
                    "Button Configuration", button.Id.ToString());
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete button:\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private Task MoveUp(CommandConfigDto button) => Move(button, -1);

        [RelayCommand]
        private Task MoveDown(CommandConfigDto button) => Move(button, +1);

        private async Task Move(CommandConfigDto button, int delta)
        {
            if (button == null || !CanEdit()) return;
            var list = Buttons.ToList();
            int i = list.IndexOf(button), j = i + delta;
            if (i < 0 || j < 0 || j >= list.Count) return;
            (list[i], list[j]) = (list[j], list[i]);
            try
            {
                // Renumber everything so legacy rows sharing a SortOrder still move.
                for (int k = 0; k < list.Count; k++)
                {
                    if (list[k].SortOrder == k + 1) continue;
                    list[k].SortOrder = k + 1;
                    await PanelButtonService.SaveAsync(list[k]);
                }
                Buttons = new ObservableCollection<CommandConfigDto>(list);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not reorder buttons:\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
