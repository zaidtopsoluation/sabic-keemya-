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
        int start = src.IndexOf("if (cancelAll)");
        int end = src.IndexOf("else if (_mode == SelectionMode.Zone)", start);
        Assert.True(start >= 0 && end > start, "broadcast cancel branch not found");
        return src.Substring(start, end - start);
    }

    [Fact]
    // 2026-11-04: like GV Core's abort, a dispatcher's Stop is one queued cancel-all that the server sends itself
    // (per-siren frames too, so it works with RCB closed), instead of ~40 rows queued 1 s apart.
    public void BroadcastCancel_IsOneCancelAll()
    {
        string branch = BroadcastCancelBranch();
        Assert.Contains("CancelAllSirensAsync(clearToken)", branch);
        Assert.DoesNotContain("BuildUnitFrame", branch);
    }

    [Fact]
    public void CancelAll_ClearsEverySirenFirst_ThenGroupClears()
    {
        var order = Keemya.Frontend.Services.SirenCommunicationService.PerSirenClears(new[] { "A", "B" }).ToList();
        Assert.Equal(new[] { ("A", (byte)0x00), ("B", (byte)0x00), ("A", (byte)0x10), ("B", (byte)0x10),
                             ("A", (byte)0x30), ("B", (byte)0x30), ("A", (byte)0x1E), ("B", (byte)0x1E) }, order);
    }

    [Fact]
    public void CancelAll_IsHandledByServer_AndStopsWhenANewToneStarts()
    {
        string svc = Source(Path.Combine("Services", "SirenCommunicationService.cs"));
        Assert.Contains("item.Target == CancelAllTarget", svc);
        Assert.Contains("_toneStarts == toneStarts", svc);
    }

    [Fact]
    public void QueueListener_DoesNotWaitForSlowRows()
    {
        // A cancel queued behind a slow row (COM wildcard clear took ~8 s) used to wait for it.
        string svc = Source(Path.Combine("Services", "SirenCommunicationService.cs"));
        Assert.DoesNotContain("await Task.WhenAll(tasks);", svc);
        Assert.Contains("_queueInFlight.TryAdd(p.Id, 0)", svc);
    }

    [Fact]
    public void CancelDelays_StopWhenANewActivationStarts()
    {
        string src = Source(Path.Combine("ViewModels", "CommandCenterViewModel.cs"));
        Assert.DoesNotMatch(new Regex(@"Task\.Delay\(950\);"), src);
        Assert.Contains("_clearCts?.Cancel(); // stop a still-running cancel", src);
    }

    // Field 2026-11-04: RCB app closed, RCB MySQL up -> the dispatcher kept queueing into RCB's DB and nothing sent it.
    [Theory]
    [InlineData(true, false, 0)]   // own queue, healthy link: at once
    [InlineData(false, false, 3)]  // own queue, no siren link: let a healthy server take it first
    [InlineData(true, true, 1)]    // other server's queue: only rows its app left waiting
    [InlineData(false, true, 3)]
    public void QueueGrace_TakesOtherServersRowsAfterOneSecond(bool linkHealthy, bool peerQueue, int expected)
    {
        Assert.Equal(expected, Keemya.Frontend.Services.SirenCommunicationService.QueueGraceSeconds(linkHealthy, peerQueue));
    }

    [Fact]
    public void Server_ListensOnBothQueues_AndDispatcherHeartbeatsBoth()
    {
        string svc = Source(Path.Combine("Services", "SirenCommunicationService.cs"));
        Assert.Contains("RunCommandQueueLoop(AppConfig.PrimaryConnectionString, peerQueue: false)", svc);
        Assert.Contains("peerQueue: true", svc);
        Assert.Contains("_ = TouchOtherServerHeartbeatAsync(myIp)", svc);
    }

    [Fact]
    public void Dispatcher_RechecksServerDuringSession()
    {
        string app = Source("App.xaml.cs");
        Assert.Contains("AppConfig.EnsureHealthyConnection()", app);
    }
}
