using Keemya.Frontend.Models;
using Keemya.Frontend.Services;
using Xunit;

namespace Keemya.Frontend.Tests;

public class PanelButtonTests
{
    // Codes from the Whelen RS-232 doc "Siren Command Definitions" (p.7-8)
    [Theory]
    [InlineData(0x00, "Clear / Reset")]
    [InlineData(0x01, "Wail")]
    [InlineData(0x04, "Public Address")]
    [InlineData(0x07, "Whoop")]
    [InlineData(0x31, "Voice Message 1")]
    [InlineData(0x38, "Voice Message 8")]
    [InlineData(0x3B, "Voice Message 9")]
    [InlineData(0x3E, "Voice Message 12")]
    [InlineData(0x11, "Voice Message 13")]
    [InlineData(0x14, "Voice Message 16")]
    public void Tones_MatchWhelenCodes(int hex, string name)
    {
        Assert.Contains(PanelButtonService.Tones, t => t.Hex == hex && t.Name == name);
    }

    [Fact]
    public void Tones_HaveUniqueCodes_AndNoNonActivationCommands()
    {
        Assert.Equal(PanelButtonService.Tones.Count, PanelButtonService.Tones.Select(t => t.Hex).Distinct().Count());
        // Siren Off / Status / Silent Test must never be put on an operator button
        Assert.DoesNotContain(PanelButtonService.Tones, t => t.Hex is 0x1B or 0x1F or 0x0F or 0x23);
    }

    [Fact]
    public void PresetColors_AreAllValid()
    {
        Assert.All(PanelButtonService.PresetColors, c => Assert.True(PanelButtonService.IsValidColor(c)));
    }

    [Theory]
    [InlineData("#A72A2A", true)]
    [InlineData("#e6d900", true)]
    [InlineData("A72A2A", false)]
    [InlineData("#A72A2", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("Red", false)]
    [InlineData(null, false)]
    public void IsValidColor(string? color, bool expected)
    {
        Assert.Equal(expected, PanelButtonService.IsValidColor(color));
    }

    [Theory]
    [InlineData("#E6D900", "#000000")] // Reset yellow keeps black text like today
    [InlineData("#FFFFFF", "#000000")]
    [InlineData("#A72A2A", "#FFFFFF")]
    [InlineData("#E68A00", "#FFFFFF")] // Gas alarm orange keeps white text
    [InlineData("#3B82F6", "#FFFFFF")]
    [InlineData("bad", "#FFFFFF")]
    public void TextColorFor(string background, string expected)
    {
        Assert.Equal(expected, PanelButtonService.TextColorFor(background));
    }

    [Theory]
    [InlineData("FIRE ALARM", 0x02, "#A72A2A", "90", null, 90)]
    [InlineData("RESET", 0x00, "#E6D900", "", null, 0)]        // blank run time = manual stop
    [InlineData("", 0x01, "#A72A2A", "90", "label", 0)]
    [InlineData("   ", 0x01, "#A72A2A", "90", "label", 0)]
    [InlineData("A", null, "#A72A2A", "90", "tone", 0)]
    [InlineData("A", 0x1B, "#A72A2A", "90", "tone", 0)]       // Siren Off not allowed
    [InlineData("A", 0x01, "red", "90", "Color", 0)]
    [InlineData("A", 0x01, "#A72A2A", "-5", "Run time", 0)]
    [InlineData("A", 0x01, "#A72A2A", "abc", "Run time", 0)]
    [InlineData("A", 0x01, "#A72A2A", "3601", "Run time", 0)]
    public void Validate(string label, int? hex, string color, string duration, string? errorContains, int expectedDuration)
    {
        string? error = PanelButtonService.Validate(label, hex, color, duration, out int parsed);
        if (errorContains == null)
        {
            Assert.Null(error);
            Assert.Equal(expectedDuration, parsed);
        }
        else
        {
            Assert.NotNull(error);
            Assert.Contains(errorContains, error, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Validate_RejectsLabelOver40Chars()
    {
        Assert.NotNull(PanelButtonService.Validate(new string('X', 41), 0x01, "#A72A2A", "90", out _));
        Assert.Null(PanelButtonService.Validate(new string('X', 40), 0x01, "#A72A2A", "90", out _));
    }

    [Fact]
    public void ToHex_RoundTripsPickerColor()
    {
        var c = System.Windows.Media.Color.FromRgb(0xA7, 0x2A, 0x05);
        string hex = PanelButtonService.ToHex(c);
        Assert.Equal("#A72A05", hex);
        Assert.True(PanelButtonService.IsValidColor(hex));
        Assert.Equal(c, (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }

    [Fact]
    public void ToHex_DropsAlpha()
    {
        Assert.Equal("#102030", PanelButtonService.ToHex(System.Windows.Media.Color.FromArgb(0x80, 0x10, 0x20, 0x30)));
    }

    [Fact]
    public void PreRecordedButton_NeedsUploadedAudio()
    {
        Assert.Contains(PanelButtonService.Tones, t => t.Hex == PanelButtonService.PreRecordedHex);
        Assert.Contains("audio", PanelButtonService.Validate("EVACUATE", PanelButtonService.PreRecordedHex, "#A72A2A", "90", out _), StringComparison.OrdinalIgnoreCase);
        Assert.Null(PanelButtonService.Validate("EVACUATE", PanelButtonService.PreRecordedHex, "#A72A2A", "90", out _, hasAudio: true));
        Assert.Null(PanelButtonService.Validate("FIRE", 0x01, "#A72A2A", "90", out _)); // other tones need no audio
    }

    [Fact]
    public void CachedAudio_KeepsExtension_AndNewUploadGetsNewFile()
    {
        var id = System.Guid.NewGuid();
        string first = PanelButtonService.CacheAudio(id, "Evacuate.MP3", new byte[] { 1, 2, 3 });
        string second = PanelButtonService.CacheAudio(id, "Evacuate.mp3", new byte[] { 4, 5, 6 });
        try
        {
            Assert.EndsWith(".mp3", first);
            Assert.NotEqual(first, second); // a file still playing is never overwritten
            Assert.Equal(new byte[] { 4, 5, 6 }, System.IO.File.ReadAllBytes(second));
            Assert.Equal(first, PanelButtonService.CacheAudio(id, "Evacuate.mp3", new byte[] { 1, 2, 3 }));
        }
        finally { System.IO.File.Delete(first); System.IO.File.Delete(second); }
    }

    [Theory]
    [InlineData(0, 300, 0)]
    [InlineData(17, 300, 12)]     // rounds to the 12 px grid
    [InlineData(19, 300, 24)]
    [InlineData(-40, 300, 0)]     // cannot leave the canvas on the left/top
    [InlineData(999, 324, 324)]   // nor on the right/bottom
    public void Snap_KeepsButtonsOnTheGridAndCanvas(double value, int max, int expected)
    {
        Assert.Equal(expected, PanelButtonService.Snap(value, max));
    }

    [Fact]
    public void UnplacedButtons_GoToFirstFreeSpot_WithoutOverlapping()
    {
        // Upgrade from the old fixed grid: no positions stored yet
        var buttons = Enumerable.Range(0, 5).Select(_ => new CommandConfigDto()).ToList();
        PanelButtonService.PlaceUnplaced(buttons);

        Assert.Equal((0, 0), (buttons[0].X, buttons[0].Y));
        Assert.All(buttons, b => Assert.True(b.X + b.ButtonWidth <= PanelButtonService.LayoutWidth));
        for (int i = 0; i < buttons.Count; i++)
            for (int j = i + 1; j < buttons.Count; j++)
                Assert.False(buttons[i].X < buttons[j].X + buttons[j].ButtonWidth && buttons[j].X < buttons[i].X + buttons[i].ButtonWidth &&
                             buttons[i].Y < buttons[j].Y + buttons[j].ButtonHeight && buttons[j].Y < buttons[i].Y + buttons[i].ButtonHeight,
                             $"buttons {i} and {j} overlap");
    }

    [Fact]
    public void PlacedButtons_KeepTheirPosition()
    {
        var placed = new CommandConfigDto { PosX = 0, PosY = 0 };
        var added = new CommandConfigDto();
        PanelButtonService.PlaceUnplaced(new List<CommandConfigDto> { placed, added });
        Assert.Equal((0, 0), (placed.X, placed.Y));
        Assert.NotEqual((0, 0), (added.X, added.Y));
    }

    [Fact]
    public void AutoArrange_MakesRowsOfTheChosenColumns_AndLimitsToCanvasWidth()
    {
        var buttons = Enumerable.Range(0, 4).Select(_ => new CommandConfigDto()).ToList();
        PanelButtonService.AutoArrange(buttons, 3);
        Assert.Equal(new[] { 0, 0, 0, 87 }, buttons.Select(b => b.Y));   // 75 high + 12 gap
        Assert.Equal(new[] { 0, 152, 304, 0 }, buttons.Select(b => b.X)); // 140 wide + 12 gap

        PanelButtonService.AutoArrange(buttons, 9); // only 3 medium buttons fit in 464 px
        Assert.All(buttons, b => Assert.True(b.X + b.ButtonWidth <= PanelButtonService.LayoutWidth));
    }

    [Theory]
    [InlineData("Small", 100, 55)]
    [InlineData("Medium", 140, 75)]
    [InlineData("Large", 190, 100)]
    [InlineData("bad", 140, 75)]
    public void ButtonSizes_LikeGvSmallMediumLarge(string size, int w, int h)
    {
        Assert.Equal((w, h), PanelButtonService.SizeOf(size));
    }

    [Fact]
    public void Dto_ShowsToneName_AndContrastText()
    {
        var b = new CommandConfigDto { CommandHex = 0x07, Color = "#E6D900" };
        Assert.Equal("Whoop  (0x07)", b.ToneDisplay);
        Assert.Equal("#000000", b.TextColorHex);
    }
}
