using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Keemya.Frontend.ViewModels;
using Xunit;

namespace Keemya.Frontend.Tests;

// Every Command Center dropdown value must resolve to a clip in Assets\Audio, or that part of the announcement is silently skipped.
public class AnnouncementAudioTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static string AudioDir([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "Frontend", "Assets", "Audio");

    private static string Normalize(string name) => CommandCenterViewModel.NormalizeAudioKey(name);

    private static string Source(string relative, [CallerFilePath] string here = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "Frontend", relative));

    [Fact]
    public void Servers_PlayDispatcherPreRecordedPa_OnAnyPage()
    {
        // Field 2026-11-02: only a server showing the Command Center played a dispatcher's pre-recorded message.
        string app = Source("App.xaml.cs");
        int controller = app.IndexOf("if (AppConfig.IsControllerStation)");
        Assert.True(app.IndexOf("RemotePaPlayer.Start()") > controller && controller >= 0);
        // the page must not play it too, or the server would start it twice
        string vm = Source(Path.Combine("ViewModels", "CommandCenterViewModel.cs"));
        Assert.DoesNotContain("x.ActiveCallTarget.StartsWith(\"PA:SEQ:\")", vm);
    }

    [Fact]
    public void FrontOfAndInFrontOf_FindTheSameClip()
    {
        Assert.Equal(Normalize("In front of Polymer Lab"), Normalize("Front of polymer lab"));
        Assert.Equal(Normalize("In front of Polymer Lab"), Normalize("Front polymer lab"));
    }

    // Same lookup as CommandCenterViewModel.FindAudioFile: exact name, then case-insensitive, then normalized.
    private static bool HasClip(string name, string[] clips) =>
        clips.Any(c => string.Equals(c, name, System.StringComparison.OrdinalIgnoreCase)) ||
        clips.Any(c => Normalize(c) == Normalize(name));

    private static IEnumerable<string> DropdownValues()
    {
        var plants = (Dictionary<string, List<string>>)typeof(CommandCenterViewModel).GetField("PlantAssemblyPoints", Private)!.GetValue(null)!;
        var allPoints = (List<string>)typeof(CommandCenterViewModel).GetField("AllUniqueAssemblyPoints", Private)!.GetValue(null)!;
        // Fixed lists and lead-in phrases from the constructor and SelectedAudioItems
        var fixedValues = new[]
        {
            "Fire", "Gas release", "Chemical spoilage", "Toxic Gas Release", "Shelter in place",
            "Real", "Drill", "1", "2", "3",
            "North to South", "South to North", "East to west", "West to East",
            "Category", "In", "Wind direction is", "Assembly point is"
        };
        return fixedValues.Concat(plants.Keys).Concat(plants.Values.SelectMany(v => v)).Concat(allPoints).Distinct();
    }

    [Fact]
    public void EveryAnnouncement_StartsWithThePreTone()
    {
        var vm = (CommandCenterViewModel)RuntimeHelpers.GetUninitializedObject(typeof(CommandCenterViewModel));
        var items = (string[])typeof(CommandCenterViewModel).GetMethod("SelectedAudioItems", Private)!.Invoke(vm, null)!;
        Assert.Equal(CommandCenterViewModel.PreTone, items[0]);
        Assert.True(File.Exists(Path.Combine(AudioDir(), CommandCenterViewModel.PreTone + ".mp3")));
    }

    [Fact]
    public void ClipsWithDifferentSampleRates_PlayAsOneMessage()
    {
        // Field 2026-11-02: 8 kHz pre-tone + 44.1 kHz speech made the concatenation throw, so nothing played.
        var clips = Directory.GetFiles(AudioDir(), "*.mp3").Select(f => new NAudio.Wave.AudioFileReader(f)).ToList();
        try
        {
            int rate = clips.Max(c => c.WaveFormat.SampleRate);
            int channels = clips.Max(c => c.WaveFormat.Channels);
            var joined = new NAudio.Wave.SampleProviders.ConcatenatingSampleProvider(
                clips.Select(c => Keemya.Frontend.Services.AudioSimulationService.ToFormat(c, rate, channels)));
            Assert.True(joined.Read(new float[4096], 0, 4096) > 0);
        }
        finally { clips.ForEach(c => c.Dispose()); }
    }

    [Fact]
    public void EveryDropdownValue_HasAnAudioClip()
    {
        string[] clips = Directory.GetFiles(AudioDir()).Select(Path.GetFileNameWithoutExtension).ToArray()!;
        var missing = DropdownValues().Where(v => !HasClip(v, clips)).ToList();
        Assert.True(missing.Count == 0, "No audio clip for: " + string.Join(", ", missing));
    }
}
