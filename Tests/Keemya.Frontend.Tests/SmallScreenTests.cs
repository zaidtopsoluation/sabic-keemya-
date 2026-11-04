using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Keemya.Frontend.Tests;

// Field 2026-11-02: on a small screen the last dashboard module (Button Configuration) was cut off with no way to reach it.
public class SmallScreenTests
{
    private static string View(string name, [CallerFilePath] string here = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "Frontend", "Views", name));

    [Theory]
    [InlineData("DashboardView.xaml", "<ScrollViewer VerticalScrollBarVisibility=\"Auto\"", "<Grid Margin=\"25,0,25,25\">")]
    [InlineData("ButtonConfigView.xaml", "<ScrollViewer VerticalScrollBarVisibility=\"Auto\"", "<TextBlock Text=\"{Binding PopupTitle}\"")]
    public void TallContent_ScrollsInsteadOfBeingCutOff(string view, string scroller, string content)
    {
        string xaml = View(view);
        int s = xaml.IndexOf(scroller);
        Assert.True(s >= 0 && s < xaml.IndexOf(content), $"{content} must be inside a ScrollViewer in {view}");
    }
}
