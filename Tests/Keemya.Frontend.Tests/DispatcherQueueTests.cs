using System;
using System.IO;
using System.Runtime.CompilerServices;
using Keemya.Frontend.Services;
using Xunit;

namespace Keemya.Frontend.Tests;

// Field test 2026-11-01: a dispatcher Command Center activation queued one row per siren, and each row started
// its own tone sequence, cancelling the others; only single-siren Map activations worked.
public class DispatcherQueueTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 15, 16, 7);

    [Fact]
    public void RowsOfOneActivation_ShareTheSequence()
    {
        Assert.True(SirenCommunicationService.ReuseQueueToneSequence(true, T0, T0.AddMilliseconds(80)));
        Assert.True(SirenCommunicationService.ReuseQueueToneSequence(true, T0, T0.AddSeconds(2)));
    }

    [Fact]
    public void LaterActivation_StartsANewSequence()
    {
        Assert.False(SirenCommunicationService.ReuseQueueToneSequence(true, T0, T0.AddSeconds(3)));
    }

    [Fact]
    public void CancelledSequence_IsNeverReused()
    {
        // activate, cancel, activate again within 2 s: the new tone must not ride on the cancelled sequence
        Assert.False(SirenCommunicationService.ReuseQueueToneSequence(false, T0, T0.AddMilliseconds(500)));
    }

    [Theory]
    [InlineData(0x03, true)]
    [InlineData(0x04, true)]
    [InlineData(0x31, true)]
    [InlineData(0x1A, true)]
    [InlineData(0x00, false)]
    [InlineData(0x10, false)]
    [InlineData(0x1E, false)]
    [InlineData(0x30, false)]
    public void ToneCommands_AreRecognised(int cmd, bool tone)
    {
        Assert.Equal(tone, SirenCommunicationService.IsToneCommand((byte)cmd));
    }

    [Fact]
    public void StationStatus_UsesHeartbeatOnly_NoPing()
    {
        string src = File.ReadAllText(ServicePath());
        Assert.DoesNotContain("PingIpAddressAsync", src);
        Assert.Contains("string newStatus = hasRecentHeartbeat ? \"ONLINE\" : \"OFFLINE\";", src);
    }

    private static string ServicePath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "Frontend", "Services", "SirenCommunicationService.cs");
}
