using System.IO;
using System.Text.Json;
using Keemya.Frontend.Models;
using Keemya.Frontend.Services;
using Keemya.Frontend.ViewModels;
using Xunit;

namespace Keemya.Frontend.Tests;

// Paging/Radio C2030s: configured like sirens, but no map marker, no horn faults, no siren tones.
public class InterfaceDeviceTests
{
    [Theory]
    [InlineData(null, "Siren")]
    [InlineData("", "Siren")]
    [InlineData("Siren", "Siren")]
    [InlineData("paging", "Paging")]
    [InlineData(" RADIO ", "Radio")]
    [InlineData("unknown", "Siren")]
    public void Normalize_FallsBackToSiren(string? value, string expected)
    {
        Assert.Equal(expected, SirenDeviceTypes.Normalize(value));
    }

    [Fact]
    public void NewDevice_DefaultsToSiren()
    {
        Assert.Equal(SirenDeviceTypes.Siren, new SirenDeviceDto().DeviceType);
        Assert.Equal(SirenDeviceTypes.Siren, new SirenStatusCacheItem().DeviceType);
    }

    [Theory]
    [InlineData("Siren", true)]
    [InlineData("Paging", false)]
    [InlineData("Radio", false)]
    public void AmplifierFailure_IsAlarmOnlyForSirens(string type, bool expectedAlarm)
    {
        var item = new SirenStatusCacheItem { DeviceType = type, HasFullAlertFailure = true, HasPartialAlertFailure = true, HasRotorFailure = true, HasStrobeError = true };
        Assert.Equal(expectedAlarm, item.HasAlarm);
    }

    [Theory]
    [InlineData("Paging")]
    [InlineData("Radio")]
    public void CabinetAlarms_StillCountForInterfaces(string type)
    {
        Assert.True(new SirenStatusCacheItem { DeviceType = type, HasIntrusion = true }.HasAlarm);
        Assert.True(new SirenStatusCacheItem { DeviceType = type, HasAcLoss = true }.HasAlarm);
        Assert.True(new SirenStatusCacheItem { DeviceType = type, HasLowBattery = true }.HasAlarm);
    }

    [Theory]
    [InlineData("Siren", "WARNING")]
    [InlineData("Paging", "ONLINE")]
    [InlineData("Radio", "ONLINE")]
    public void ConnectedInterface_WithAmpBits_ShowsOnline(string type, string expected)
    {
        var item = new SirenStatusCacheItem { DeviceType = type, Ip = "", IsSerialOnline = true, HasFullAlertFailure = true };
        Assert.Equal(expected, SirenCommunicationService.ComputeStatus(item));
    }

    [Fact]
    public void MapJson_CarriesDeviceType()
    {
        var json = MapViewModel.BuildSirensJson(new[]
        {
            new SirenDeviceDto { Name = "HB Poly Siren", DeviceType = "Siren", Lat = 27.03, Lng = 49.59 },
            new SirenDeviceDto { Name = "PAGA Interface", DeviceType = "paging" }
        });

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Siren", doc.RootElement[0].GetProperty("deviceType").GetString());
        Assert.Equal("Paging", doc.RootElement[1].GetProperty("deviceType").GetString());
    }

    [Fact]
    public void MapPage_ShowsInterfacesCard_AndHasNoZones()
    {
        string html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "map.html"));

        Assert.Contains("id=\"interfaces-list\"", html);
        Assert.Contains("function renderInterfaces(", html);
        Assert.Contains("function isMapSiren(", html);
        Assert.DoesNotContain("zone", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommLogs_ShownOnlyInDebugBuilds()
    {
#if DEBUG
        Assert.True(AppConfig.ShowCommLogs);
#else
        Assert.False(AppConfig.ShowCommLogs);
#endif
    }
}
