using System.IO;
using Xunit;

namespace Keemya.Frontend.Tests;

// The plant PCs run without internet: the map pages must only use the local tiles/ folder.
public class OfflineMapTests
{
    private static string ReadResource(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", name));

    [Theory]
    [InlineData("map.html")]
    [InlineData("picker_map.html")]
    public void MapPage_UsesOnlyLocalTiles(string page)
    {
        string html = ReadResource(page);

        Assert.Contains("L.tileLayer('tiles/{z}/{x}/{y}.jpg'", html);
        Assert.DoesNotContain("google.com", html);
        Assert.DoesNotContain("L.tileLayer('http", html);
    }

    [Fact]
    public void MapPage_SetThemeDoesNotUseUndefinedTileLayers()
    {
        string html = ReadResource("map.html");

        Assert.Contains("function setTheme(", html);
        Assert.DoesNotContain("lightTiles", html);
        Assert.DoesNotContain("darkTiles", html);
    }
}
