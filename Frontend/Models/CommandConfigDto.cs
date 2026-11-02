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
        public string? AudioName            { get; set; } = null;          // uploaded file name of a pre-recorded message button
        public byte[]? AudioData            { get; set; } = null;          // set only when a new file is uploaded

        // Command Center layout (like GV Core): free position on a snap grid, null = not placed yet
        public int?   PosX                  { get; set; } = null;
        public int?   PosY                  { get; set; } = null;
        public string ButtonSize            { get; set; } = "Medium";      // Small / Medium / Large

        public int    X            => PosX ?? 0;
        public int    Y            => PosY ?? 0;
        public int    ButtonWidth  => Keemya.Frontend.Services.PanelButtonService.SizeOf(ButtonSize).Width;
        public int    ButtonHeight => Keemya.Frontend.Services.PanelButtonService.SizeOf(ButtonSize).Height;
        public double ButtonFontSize => ButtonSize switch { "Small" => 11, "Large" => 16, _ => 13 };

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

        public string TextColorHex => Keemya.Frontend.Services.PanelButtonService.TextColorFor(ColorHex);

        public string ToneDisplay => Keemya.Frontend.Services.PanelButtonService.Tones
            .FirstOrDefault(t => t.Hex == CommandHex)?.Display ?? CommandHexLabel;
    }
}
