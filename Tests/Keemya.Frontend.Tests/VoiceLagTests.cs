using Keemya.Frontend.Services;
using NAudio.Wave;
using Xunit;

namespace Keemya.Frontend.Tests;

// Field 2026-11-03: dispatcher-to-dispatcher intercom lagged 3-4 s because late voice piled up in the 5 s playback buffer.
public class VoiceLagTests
{
    [Fact]
    public void BackedUpVoice_IsDropped_SoTheCallStaysLive()
    {
        var provider = new BufferedWaveProvider(new WaveFormat(8000, 16, 1)) { DiscardOnBufferOverflow = true };
        var packet = new byte[1600]; // one 100 ms mic packet at 8 kHz 16-bit mono

        for (int i = 0; i < 40; i++) AudioCommunicationService.AddVoice(provider, packet, packet.Length); // 4 s burst

        Assert.True(provider.BufferedDuration <= AudioCommunicationService.MaxVoiceLag + TimeSpan.FromMilliseconds(100),
            $"lag {provider.BufferedDuration.TotalMilliseconds} ms");
    }

    [Fact]
    public void NormalStream_IsNotDropped()
    {
        var provider = new BufferedWaveProvider(new WaveFormat(8000, 16, 1));
        AudioCommunicationService.AddVoice(provider, new byte[1600], 1600);
        AudioCommunicationService.AddVoice(provider, new byte[1600], 1600);
        Assert.Equal(200, provider.BufferedDuration.TotalMilliseconds);
    }
}
