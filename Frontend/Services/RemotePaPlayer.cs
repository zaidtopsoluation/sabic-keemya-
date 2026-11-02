using System;
using System.Linq;
using System.Threading.Tasks;
using Keemya.Frontend.ViewModels;
using MySqlConnector;

namespace Keemya.Frontend.Services
{
    // Servers play a dispatcher's pre-recorded PA into their aux output (→ MuxLab → siren).
    // A dispatcher requests it by setting its StationStatuses.ActiveCallTarget to "PA:SEQ:clip|clip|...", "PA:FILE:name"
    // or "PA:BTN:<CommandConfigs.Id>" (a button with uploaded audio).
    // This used to live in the Command Center page, so a server showing any other page played nothing.
    internal static class RemotePaPlayer
    {
        private static string? _playing;

        public static void Start() => _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                try { Apply(await ReadRequestAsync()); } catch { }
            }
        });

        // Pre-recorded message buttons: a server plays pre-tone + the uploaded audio itself; a dispatcher asks its server.
        public static void PlayButton(Guid buttonId)
        {
            if (AppConfig.IsControllerStation) _ = PlayButtonLocalAsync(buttonId);
            else _ = SetOwnRequestAsync("PA:BTN:" + buttonId);
        }

        public static void StopButton()
        {
            if (!AppConfig.IsControllerStation) _ = SetOwnRequestAsync(null);
        }

        private static async Task PlayButtonLocalAsync(Guid buttonId)
        {
            try
            {
                string? file = await PanelButtonService.GetAudioFileAsync(buttonId);
                if (file == null)
                {
                    SirenCommunicationService.Instance.Log($"[PA] Button {buttonId} has no uploaded audio.");
                    return;
                }
                var playlist = new[] { CommandCenterViewModel.FindAudioFile(CommandCenterViewModel.PreTone), file }
                    .Where(p => p != null).Select(p => p!).ToList();
                SirenCommunicationService.Instance.Log($"[PA] Playing pre-recorded button audio {System.IO.Path.GetFileName(file)}.");
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => AudioSimulationService.Instance.StartSequentialPlayback(playlist));
            }
            catch (Exception ex)
            {
                SirenCommunicationService.Instance.Log($"[PA] Could not play button audio: {ex.Message}");
            }
        }

        // Only clears a button request, never an intercom call this station has open.
        private static async Task SetOwnRequestAsync(string? request)
        {
            try
            {
                using var conn = new MySqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(request != null
                    ? "UPDATE StationStatuses SET ActiveCallTarget = @req WHERE Name = @me"
                    : "UPDATE StationStatuses SET ActiveCallTarget = NULL WHERE Name = @me AND ActiveCallTarget LIKE 'PA:BTN:%'", conn);
                cmd.Parameters.AddWithValue("@req", request);
                cmd.Parameters.AddWithValue("@me", AppConfig.StationName);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                SirenCommunicationService.Instance.Log($"[PA] Could not send pre-recorded request to the server: {ex.Message}");
            }
        }

        // Play or stop only when the request changes, so one message is not restarted every second.
        internal static bool NeedsChange(string? playing, string? requested) => playing != requested;

        private static async Task<string?> ReadRequestAsync()
        {
            using var conn = new MySqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT ActiveCallTarget FROM StationStatuses WHERE Name <> @me AND (ActiveCallTarget LIKE 'PA:SEQ:%' OR ActiveCallTarget LIKE 'PA:FILE:%' OR ActiveCallTarget LIKE 'PA:BTN:%') LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@me", AppConfig.StationName);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private static void Apply(string? requested)
        {
            if (!NeedsChange(_playing, requested)) return;
            _playing = requested;

            if (requested != null && requested.StartsWith("PA:BTN:") && Guid.TryParse(requested.Substring("PA:BTN:".Length), out var buttonId))
            {
                _ = PlayButtonLocalAsync(buttonId);
                return;
            }

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                var audio = AudioSimulationService.Instance;
                if (requested == null)
                {
                    SirenCommunicationService.Instance.Log("[PA] Dispatcher pre-recorded message ended. Stopping audio.");
                    audio.StopLoopback();
                }
                else if (requested.StartsWith("PA:SEQ:"))
                {
                    var playlist = requested.Substring("PA:SEQ:".Length).Split('|')
                        .Select(CommandCenterViewModel.FindAudioFile).Where(p => p != null).Select(p => p!).ToList();
                    SirenCommunicationService.Instance.Log($"[PA] Playing dispatcher pre-recorded message ({playlist.Count} clips).");
                    audio.StartSequentialPlayback(playlist);
                }
                else
                {
                    string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", requested.Substring("PA:FILE:".Length));
                    SirenCommunicationService.Instance.Log($"[PA] Playing dispatcher pre-recorded file {path}.");
                    audio.StartFilePlayback(path);
                }
            });
        }
    }
}
