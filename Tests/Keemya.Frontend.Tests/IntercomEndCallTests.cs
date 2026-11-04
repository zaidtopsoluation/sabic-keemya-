using Keemya.Frontend.ViewModels;
using Xunit;

namespace Keemya.Frontend.Tests;

// Field 2026-11-03: the intercom popup offered "Confirm Call" but no way to end a running call.
public class IntercomEndCallTests
{
    [Theory]
    [InlineData("Admin ECC", true)]
    [InlineData("PCB/ECS", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("PA:PCB-Controller", false)]   // live PA is ended with Stop, not End Call
    [InlineData("PA:SEQ:pre-tone|fire", false)]
    public void EndCall_OnlyOffered_DuringStationCall(string? active, bool expected)
    {
        Assert.Equal(expected, CommandCenterViewModel.IsVoiceCall(active));
    }

    // Field 2026-11-04: after End Call the remote's trailing voice auto-answered and reopened the call.
    [Theory]
    [InlineData(0, true)]
    [InlineData(2400, true)]
    [InlineData(2500, false)]
    [InlineData(5000, false)]
    public void HungUpStation_IsIgnored_UntilQuiet(int msSinceLastPacket, bool stillHungUp)
    {
        var last = new DateTime(2026, 11, 4, 12, 0, 0);
        Assert.Equal(stillHungUp, Keemya.Frontend.Services.AudioCommunicationService.IsStillHungUp(last, last.AddMilliseconds(msSinceLastPacket)));
    }
}
