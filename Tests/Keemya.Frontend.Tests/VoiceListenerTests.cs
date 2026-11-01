using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Keemya.Frontend.Tests;

// Dispatcher live PA streams over UDP to the server, which plays it into the aux cable (MuxLab -> C2030).
// The server must listen from app start; before, it only listened while its Command Center screen was open.
public class VoiceListenerTests
{
    private static string AppSource([CallerFilePath] string here = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "Frontend", "App.xaml.cs"));

    [Fact]
    public void ServerStartsVoiceListenerAtStartup()
    {
        string src = AppSource();
        int guard = src.IndexOf("if (AppConfig.IsControllerStation)");
        int listen = src.IndexOf("AudioCommunicationService.Instance.StartListening()");

        Assert.True(guard >= 0 && listen > guard, "App startup must start the voice listener on server stations");
    }
}
