using System;

namespace Keemya.Frontend.Models
{
    public class CommandConfigDto
    {
        public Guid   Id                    { get; set; }
        public string Name                  { get; set; } = string.Empty;
        public string Description           { get; set; } = string.Empty;
        public string CommandType           { get; set; } = string.Empty;  // e.g. "Wail", "Attack"
        public int    CommandHex            { get; set; } = 0;             // actual protocol byte
        public int    ExpectedResponseBytes { get; set; } = 0;
        public string Color                 { get; set; } = "Blue";
        public bool   IsEnabled             { get; set; } = true;
        public int    Duration              { get; set; } = 0;             // seconds, 0 = manual stop
        public int    SortOrder            { get; set; } = 0;             // display order for drag-reorder
        public Guid?  AudioFileId           { get; set; } = null;          // Link to AudioFiles table
        public string? AudioFilePath        { get; set; } = null;          // Local path or stored name for playing

        // ── Computed helpers for the View ──────────────────────────────────

        /// <summary>Human-readable hex label, e.g. "0x01".</summary>
        public string CommandHexLabel => $"0x{CommandHex:X2}";

        /// <summary>Duration display: "Manual Stop" when 0, otherwise "Xm Ys".</summary>
        public string DurationDisplay => Duration == 0
            ? "Manual Stop"
            : $"{Duration / 60}m{Duration % 60}s";

        /// <summary>Maps the stored color name to a WPF-compatible hex string for the dot/accent.</summary>
        public string ColorHex => Color switch
        {
            "Red"    => "#EF4444",
            "Orange" => "#F97316",
            "Yellow" => "#EAB308",
            "Green"  => "#10B981",
            "Blue"   => "#3B82F6",
            "Purple" => "#8B5CF6",
            "Pink"   => "#EC4899",
            "Cyan"   => "#06B6D4",
            _        => Color.StartsWith("#") ? Color : "#6366F1"
        };
    }
}
