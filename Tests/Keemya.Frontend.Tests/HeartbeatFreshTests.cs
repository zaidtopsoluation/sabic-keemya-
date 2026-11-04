using Keemya.Frontend.Services;
using Xunit;

namespace Keemya.Frontend.Tests;

// Field 2026-11-04: with RCB's app closed, Admin showed PCB red; heartbeats were judged against Admin's own clock.
public class HeartbeatFreshTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 10, 0, 0);

    [Fact]
    public void ClockSkew_IsIgnored_OnceTheHeartbeatChanges()
    {
        // The station's clock is 2 minutes behind this PC's.
        Assert.False(SirenCommunicationService.IsHeartbeatFresh("skewed", Now.AddMinutes(-2), Now));
        Assert.True(SirenCommunicationService.IsHeartbeatFresh("skewed", Now.AddMinutes(-2).AddSeconds(5), Now.AddSeconds(5)));
    }

    [Fact]
    public void StoppedStation_GoesStale_EvenIfItsClockRunsAhead()
    {
        var stamp = Now.AddSeconds(10);
        Assert.True(SirenCommunicationService.IsHeartbeatFresh("stopped", stamp, Now));
        Assert.True(SirenCommunicationService.IsHeartbeatFresh("stopped", stamp, Now.AddSeconds(10)));
        Assert.False(SirenCommunicationService.IsHeartbeatFresh("stopped", stamp, Now.AddSeconds(16)));
    }

    [Fact]
    public void NoHeartbeat_IsOffline()
    {
        Assert.False(SirenCommunicationService.IsHeartbeatFresh("never", null, Now));
    }
}
