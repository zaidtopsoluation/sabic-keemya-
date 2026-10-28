using Keemya.Frontend.Services;
using Xunit;

namespace Keemya.Frontend.Tests;

public class SerialAutoDetectTests
{
    [Fact]
    public void ScanOrder_LastKnownFirst_ThenNumeric()
    {
        var order = SirenCommunicationService.OrderPortsForScan(new[] { "COM10", "COM3", "COM6", "COM4", "com6" }, "COM6").ToArray();
        Assert.Equal(new[] { "COM6", "COM3", "COM4", "COM10" }, order);
    }

    [Fact]
    public void ScanOrder_MissingPreferred_StillScansAll()
    {
        var order = SirenCommunicationService.OrderPortsForScan(new[] { "COM5", "COM2" }, "COM4").ToArray();
        Assert.Equal(new[] { "COM2", "COM5" }, order);
    }

    private static readonly byte[] Request = SirenCommunicationService.BuildStatusFrame("444", "4444");
    private static readonly byte[] Reply = { 0x02, 0x84, 0x84, 0x84, 0x84, 0x84, 0x84, 0x84, 0x80, 0xB4, 0xA3, 0x80, 0x87, 0x8A, 0x8F, 0x03, 0x81, 0x80, 0x0D };

    [Fact]
    public void Echo_Only_LeavesNothing()
    {
        Assert.Empty(SirenCommunicationService.StripLeadingEcho(Request, Request));
    }

    [Fact]
    public void Echo_FollowedByRealReply_KeepsReply()
    {
        var data = Request.Concat(Reply).ToArray();
        Assert.Equal(Reply, SirenCommunicationService.StripLeadingEcho(data, Request));
    }

    [Fact]
    public void RealReply_IsUntouched()
    {
        Assert.Equal(Reply, SirenCommunicationService.StripLeadingEcho(Reply, Request));
    }

    [Fact]
    public void NoSentFrame_IsUntouched()
    {
        Assert.Equal(Request, SirenCommunicationService.StripLeadingEcho(Request, null));
    }
}
