using System;

namespace Keemya.Frontend.Models
{
    public class ZoneDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = "Red";
        public string Shape { get; set; } = "Rectangle";
        
        public int TotalDevices { get; set; }
        public int OnlineDevices { get; set; }
        
        public string DevicesStatusText => $"{OnlineDevices}/{TotalDevices}";
        
        // Premium visualization properties
        public string ColorHex => Color switch
        {
            "Red" => "#EF4444",
            "Orange" => "#F97316",
            "Yellow" => "#EAB308",
            "Green" => "#10B981",
            "Blue" => "#3B82F6",
            "Purple" => "#8B5CF6",
            "Pink" => "#EC4899",
            "Cyan" => "#06B6D4",
            _ => Color.StartsWith("#") ? Color : "#6366F1" // Default Indigo
        };

        // Dark background tint for premium visual styling
        public string DarkBackgroundTint 
        {
            get
            {
                return Color switch
                {
                    "Red" => "#371A1A",
                    "Orange" => "#3B200A",
                    "Yellow" => "#352A05",
                    "Green" => "#062E1E",
                    "Blue" => "#0F2347",
                    "Purple" => "#22133D",
                    "Pink" => "#3D1228",
                    "Cyan" => "#042D35",
                    _ => Color.StartsWith("#") ? GetDarkTint(Color) : "#1E1E2F"
                };
            }
        }

        // Light background tint for light theme visual styling
        public string LightBackgroundTint 
        {
            get
            {
                return Color switch
                {
                    "Red" => "#FEF2F2",
                    "Orange" => "#FFF7ED",
                    "Yellow" => "#FEFCE8",
                    "Green" => "#F0FDF4",
                    "Blue" => "#EFF6FF",
                    "Purple" => "#F5F3FF",
                    "Pink" => "#FDF2F8",
                    "Cyan" => "#ECFEFF",
                    _ => Color.StartsWith("#") ? GetLightTint(Color) : "#F8FAFC"
                };
            }
        }

        private string GetDarkTint(string hex)
        {
            try
            {
                if (hex.Length == 7)
                {
                    int r = Convert.ToInt32(hex.Substring(1, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(3, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(5, 2), 16);
                    // 15% brightness
                    return $"#{(int)(r * 0.15):X2}{(int)(g * 0.15):X2}{(int)(b * 0.15):X2}";
                }
            }
            catch {}
            return "#1E1E2F";
        }

        private string GetLightTint(string hex)
        {
            try
            {
                if (hex.Length == 7)
                {
                    int r = Convert.ToInt32(hex.Substring(1, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(3, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(5, 2), 16);
                    // Blend with white (92% white, 8% color)
                    int nr = (int)(r * 0.08 + 255 * 0.92);
                    int ng = (int)(g * 0.08 + 255 * 0.92);
                    int nb = (int)(b * 0.08 + 255 * 0.92);
                    return $"#{nr:X2}{ng:X2}{nb:X2}";
                }
            }
            catch {}
            return "#F8FAFC";
        }
    }
}
