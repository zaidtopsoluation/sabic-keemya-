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
    public void Dto_ShowsToneName_AndContrastText()
    {
        var b = new CommandConfigDto { CommandHex = 0x07, Color = "#E6D900" };
        Assert.Equal("Whoop  (0x07)", b.ToneDisplay);
        Assert.Equal("#000000", b.TextColorHex);
    }
}
