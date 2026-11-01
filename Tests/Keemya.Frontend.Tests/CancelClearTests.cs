using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Keemya.Frontend.Tests;

// Field test 2026-11-01 (dispatcher queue): a broadcast cancel sent 4 frames per siren (~40 s for 10 sirens),
// and the late clear frames stopped tones activated during that time.
public class CancelClearTests
{
    private static string Source(string relative, [CallerFilePath] string here = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "Frontend", relative));

    private static string BroadcastCancelBranch()
    {
        string src = Source(Path.Combine("ViewModels", "CommandCenterViewModel.cs"));
        int start = src.IndexOf("if (_activeTargets.Count == _allSirens.Count)");
        int end = src.IndexOf("else if (_mode == SelectionMode.Zone)", start);
        Assert.True(start >= 0 && end > start, "broadcast cancel branch not found");
        return src.Substring(start, end - start);
    }

    [Fact]
    public void BroadcastCancel_UsesAreaWildcards_NotPerSirenFrames()
    {
        string branch = BroadcastCancelBranch();
        Assert.DoesNotContain("BuildUnitFrame", branch);
        Assert.Contains("BuildAreaWildcardFrame(areaCode, 0x10)", branch);
        Assert.Contains("BuildAreaWildcardFrame(areaCode, 0x30)", branch);
    }

    [Fact]
    public void CancelDelays_StopWhenANewActivationStarts()
    {
        string src = Source(Path.Combine("ViewModels", "CommandCenterViewModel.cs"));
        Assert.DoesNotMatch(new Regex(@"Task\.Delay\(950\);"), src);
        Assert.Contains("_clearCts?.Cancel(); // stop a still-running cancel", src);
    }

    [Fact]
    public void Dispatcher_RechecksServerDuringSession()
    {
        string app = Source("App.xaml.cs");
        Assert.Contains("AppConfig.EnsureHealthyConnection()", app);
    }
}
