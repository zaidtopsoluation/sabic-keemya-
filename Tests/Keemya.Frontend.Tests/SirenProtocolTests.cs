using Keemya.Frontend.Services;
using Xunit;

namespace Keemya.Frontend.Tests;

public class SirenFrameTests
{
    // Whelen BCN frame: STX, 3 area + 4 address digits (0x80|d), 0x80, 0x80, 0x80|cmd, ETX, checksum hi/lo, CR
    [Fact]
    public void BuildUnitFrame_EncodesLayout()
    {
        byte[] f = SirenCommunicationService.BuildUnitFrame("123", "4567", 0x1A);

        Assert.Equal(15, f.Length);
        Assert.Equal(0x02, f[0]);
        Assert.Equal(new byte[] { 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87 }, f[1..8]);
        Assert.Equal(0x80, f[8]);
        Assert.Equal(0x80, f[9]);
        Assert.Equal(0x9A, f[10]);
        Assert.Equal(0x03, f[11]);
        Assert.Equal(0x0D, f[14]);
    }

    [Theory]
    [InlineData("1", "2", 0x00)]
    [InlineData("000", "0000", 0x23)]
    [InlineData("999", "9999", 0x1E)]
    public void BuildUnitFrame_ChecksumIsXorOfFirst12Bytes(string area, string addr, byte cmd)
    {
        byte[] f = SirenCommunicationService.BuildUnitFrame(area, addr, cmd);
        byte xor = 0;
        for (int i = 0; i <= 11; i++) xor ^= f[i];
        Assert.Equal((byte)(0x80 | (xor >> 4)), f[12]);
        Assert.Equal((byte)(0x80 | (xor & 0x0F)), f[13]);
    }

    [Fact]
    public void BuildUnitFrame_PadsShortCodesWithZeros()
    {
        Assert.Equal(
            SirenCommunicationService.BuildUnitFrame("001", "0002", 0x00),
            SirenCommunicationService.BuildUnitFrame("1", "2", 0x00));
    }

    [Fact]
    public void BuildStatusFrame_EqualsUnitFrameWithStatusCommand()
    {
        Assert.Equal(
            SirenCommunicationService.BuildUnitFrame("012", "0345", 0x23),
            SirenCommunicationService.BuildStatusFrame("012", "0345"));
    }

    [Fact]
    public void BuildFrameWithCommand_KeepsAddressAndSwapsCommand()
    {
        byte[] status = SirenCommunicationService.BuildStatusFrame("012", "0345");
        Assert.Equal(
            SirenCommunicationService.BuildUnitFrame("012", "0345", 0x1A),
            SirenCommunicationService.BuildFrameWithCommand(status, 0x1A));
    }

    [Theory]
    [InlineData(0x8A, 0x85, 0xA5)]
    [InlineData(0x80, 0x80, 0x00)]
    [InlineData(0x8F, 0x8F, 0xFF)]
    public void ReconstructByte_JoinsLowNibbles(byte hi, byte lo, byte expected)
    {
        Assert.Equal(expected, SirenCommunicationService.ReconstructByte(hi, lo));
    }
}

public class SharedStatusTests
{
    // Two servers on one TCP gateway: only one hears the replies, both must show the siren as reachable.
    [Theory]
    [InlineData("OFFLINE", "WARNING", "WARNING")]   // PCB hears nothing, RCB hears TCP -> yellow on both
    [InlineData("OFFLINE", "ONLINE", "ONLINE")]
    [InlineData("WARNING", "ONLINE", "ONLINE")]
    [InlineData("ONLINE", "OFFLINE", "ONLINE")]     // own result wins when better
    [InlineData("WARNING", "OFFLINE", "WARNING")]
    [InlineData("OFFLINE", "OFFLINE", "OFFLINE")]
    [InlineData("OFFLINE", null, "OFFLINE")]        // no fresh peer data
    [InlineData("WARNING", "garbage", "WARNING")]
    public void CombineStatus_TakesBest(string own, string? peer, string expected)
    {
        Assert.Equal(expected, SirenCommunicationService.CombineStatus(own, peer));
    }
}

public class PollingStandbyTests
{
    [Theory]
    [InlineData(false, true, true, true)]    // Server 2 while Server 1 publishes -> standby
    [InlineData(false, true, false, false)]  // Server 2, Server 1 went quiet -> take over polling
    [InlineData(true, true, true, false)]    // Server 1 always polls
    [InlineData(false, false, true, false)]  // single server / no peer configured -> polls
    public void ShouldStandby(bool localPreferred, bool hasPeer, bool peerPublishing, bool expected)
    {
        Assert.Equal(expected, SirenCommunicationService.ShouldStandby(localPreferred, hasPeer, peerPublishing));
    }
}

public class HeartbeatIpTests
{
    private static (System.Net.IPAddress, System.Net.IPAddress) L(string ip, string mask) =>
        (System.Net.IPAddress.Parse(ip), System.Net.IPAddress.Parse(mask));

    [Fact]
    public void PicksSirenLanNotWifi()
    {
        // The PCB laptop on site: Wi-Fi listed first, siren switch second
        var locals = new[] { L("10.228.1.111", "255.255.255.0"), L("192.168.1.112", "255.255.255.0") };
        Assert.Equal("192.168.1.112", SirenCommunicationService.PickLocalIpForPeer(System.Net.IPAddress.Parse("192.168.1.111"), locals));
    }

    [Fact]
    public void HonoursWiderMask()
    {
        var locals = new[] { L("10.228.1.111", "255.255.255.0"), L("172.16.5.20", "255.255.0.0") };
        Assert.Equal("172.16.5.20", SirenCommunicationService.PickLocalIpForPeer(System.Net.IPAddress.Parse("172.16.9.1"), locals));
    }

    [Fact]
    public void NoMatch_ReturnsNull_AndSkipsLoopback()
    {
        var locals = new[] { L("127.0.0.1", "255.0.0.0"), L("10.228.1.111", "255.255.255.0") };
        Assert.Null(SirenCommunicationService.PickLocalIpForPeer(System.Net.IPAddress.Parse("192.168.1.111"), locals));
    }
}

public class SirenStatusRuleTests
{
    // Rule: green when every path the siren is configured for answers, yellow when only some do, red when none.
    // Redundant = TCP + serial, IP only = TCP, no IP = serial. Alarm / low battery = yellow.
    [Theory]
    // Redundant siren (IP + serial)
    [InlineData("192.168.1.100", true, true, true, false, 24.0, "ONLINE")]
    [InlineData("192.168.1.100", true, true, false, false, 24.0, "WARNING")]   // serial lost
    [InlineData("192.168.1.100", true, false, true, false, 24.0, "WARNING")]   // TCP lost
    [InlineData("192.168.1.100", true, false, false, false, 24.0, "OFFLINE")]
    // Network-only siren (CB Siren)
    [InlineData("192.168.1.100", false, true, false, false, 24.0, "ONLINE")]
    [InlineData("192.168.1.100", false, false, false, false, 24.0, "OFFLINE")]
    // Serial-only siren (no IP)
    [InlineData("", false, false, true, false, 24.0, "ONLINE")]
    [InlineData("", false, false, false, false, 24.0, "OFFLINE")]
    [InlineData("", true, false, true, false, 24.0, "ONLINE")]                // redundant flag without IP = serial only
    // Alarms and battery
    [InlineData("", false, false, false, true, 10.0, "OFFLINE")]              // offline wins over alarms
    [InlineData("", false, false, true, true, 24.0, "WARNING")]               // alarm
    [InlineData("", false, false, true, false, 21.9, "WARNING")]              // low battery
    [InlineData("", false, false, true, false, 22.0, "ONLINE")]               // threshold is exclusive
    [InlineData("", false, false, true, false, 0.0, "ONLINE")]                // 0 V = no reading
    public void ComputeStatus(string ip, bool redundant, bool tcp, bool serial, bool alarm, double dcVolts, string expected)
    {
        var item = new SirenStatusCacheItem { Ip = ip, Redundant = redundant, IsTcpOnline = tcp, IsSerialOnline = serial, DcVoltage = dcVolts, HasIntrusion = alarm };
        Assert.Equal(expected, SirenCommunicationService.ComputeStatus(item));
    }

    [Theory]
    [InlineData("192.168.1.100", true, true, true)]
    [InlineData("192.168.1.100", false, true, false)]
    [InlineData("", false, false, true)]
    [InlineData("  ", true, false, true)]
    public void Paths(string ip, bool redundant, bool tcp, bool serial)
    {
        Assert.Equal(tcp, SirenCommunicationService.UsesTcp(ip));
        Assert.Equal(serial, SirenCommunicationService.UsesSerial(ip, redundant));
    }
}
