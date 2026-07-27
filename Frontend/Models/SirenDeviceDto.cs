using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Keemya.Frontend.Models
{
    public partial class SirenDeviceDto : ObservableObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AreaCode { get; set; } = string.Empty;
        public string AddressCode { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColorBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryVoltage))]
        [NotifyPropertyChangedFor(nameof(HasACPower))]
        private string status = "OFFLINE";

        public string Ip { get; set; } = string.Empty;
        public bool Redundant { get; set; }
        public Guid? GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;

        // Rich Presentation Properties
        public string StatusColorBrush => Status.ToUpper() switch
        {
            "ONLINE" => "#10B981",       // Green
            "MAINTENANCE" => "#3B82F6",  // Blue
            "WARNING" => "#F59E0B",      // Amber
            _ => "#EF4444"               // Red for OFFLINE
        };

        public string Subtext => $"{AreaCode} • {AddressCode} • {(string.IsNullOrEmpty(GroupName) ? "No Group" : GroupName)}";

        // Compatibility Properties for WebView2 Map
        public string IpAddress => Ip;
        public double Latitude => Lat;
        public double Longitude => Lng;
        public double BatteryVoltage => Status.ToUpper() == "ONLINE" ? 24.2 : 0.0;
        public bool HasACPower => Status.ToUpper() == "ONLINE";
    }
}
