using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Media;

namespace Keemya.Frontend.Models
{
    public partial class StationStatusDto : ObservableObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Workstation" or "Controller"
        public string IpAddress { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColorBrush))]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        private string status = "OFFLINE"; // "ONLINE" or "OFFLINE"

        public Brush StatusColorBrush => Status == "ONLINE"
            ? new SolidColorBrush(Color.FromRgb(45, 106, 79))  // Deep Green
            : new SolidColorBrush(Color.FromRgb(128, 15, 47)); // Deep Red

        public string StatusText => Status == "ONLINE" ? "ONLINE" : "OFFLINE";
    }
}
