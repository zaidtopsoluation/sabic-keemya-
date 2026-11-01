using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;

namespace Keemya.Frontend.Models
{
    // SirenDevices.DeviceType values. Paging/Radio are C2030s in the control room that feed the
    // client's PAGA or radio: no horns, no map position.
    public static class SirenDeviceTypes
    {
        public const string Siren = "Siren";
        public const string Paging = "Paging";
        public const string Radio = "Radio";

        public static readonly string[] All = { Siren, Paging, Radio };

        public static string Normalize(string? value) =>
            All.FirstOrDefault(t => t.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Siren;

        public static bool IsSiren(string? value) => Normalize(value) == Siren;
    }

    public partial class SirenDeviceDto : ObservableObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DeviceType { get; set; } = SirenDeviceTypes.Siren;
        public string AreaCode { get; set; } = string.Empty;
        public string AddressCode { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColorBrush))]
        [NotifyPropertyChangedFor(nameof(BatteryVoltage))]
        [NotifyPropertyChangedFor(nameof(HasACPower))]
        private string status = "OFFLINE";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColorBrush))]
        private bool isTcpOnline = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColorBrush))]
        private bool isSerialOnline = false;

        public string Ip { get; set; } = string.Empty;
        public bool Redundant { get; set; }
        public Guid? GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;

        [ObservableProperty]
        private bool isChecked = false;

        [ObservableProperty]
        private bool isSelected = false;

        [ObservableProperty]
        private double dcVolts = 0.0;

        [ObservableProperty]
        private double acVolts = 0.0;

        [ObservableProperty]
        private double cabTemp = 0.0;

        [ObservableProperty]
        private bool acPowerOn = false;

        [ObservableProperty]
        private bool doorIntruded = false;

        [ObservableProperty]
        private bool strobeActive = false;

        [ObservableProperty]
        private bool supervisorMode = false;

        [ObservableProperty]
        private bool systemArmed = true;

        [ObservableProperty]
        private bool rotorActive = true;

        [ObservableProperty]
        private bool biasDetected = true;

        [ObservableProperty]
        private bool fullAlert = false;

        [ObservableProperty]
        private bool partialAlert = false;

        // Rich Presentation Properties
        public string StatusColorBrush
        {
            get
            {
                if (Status.Equals("OFFLINE", StringComparison.OrdinalIgnoreCase))
                    return "#EF4444";     // Red (Offline)
                if (Status.Equals("MAINTENANCE", StringComparison.OrdinalIgnoreCase))
                    return "#3B82F6";     // Blue (Maintenance)
                if (Status.Equals("WARNING", StringComparison.OrdinalIgnoreCase))
                    return "#F59E0B";     // Yellow (Warning)
                if (Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
                    return "#10B981";     // Green (Online)
                return "#EF4444";         // Red default
            }
        }

        public string Subtext => $"Area: {AreaCode} • ID: {AddressCode}";
        public bool IsOffline => Status.Equals("OFFLINE", StringComparison.OrdinalIgnoreCase);
        public bool IsActive => !IsOffline;

        // Compatibility Properties for WebView2 Map
        public string IpAddress => Ip;
        public double Latitude => Lat;
        public double Longitude => Lng;
        public double BatteryVoltage => Status.ToUpper() == "ONLINE" ? 24.2 : (DcVolts > 0 ? DcVolts : 0.0);
        public bool HasACPower => Status.ToUpper() == "ONLINE" || AcPowerOn;
    }
}
