using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Keemya.Frontend.Services
{
    public class AudioCommunicationService
    {
        private static readonly Lazy<AudioCommunicationService> _instance =
            new Lazy<AudioCommunicationService>(() => new AudioCommunicationService());

        public static AudioCommunicationService Instance => _instance.Value;

        private const int Port = 5556;
        private UdpClient? _udpListener;
        private UdpClient? _udpSender;
        private WaveInEvent? _waveSource;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _waveProvider;
        private CancellationTokenSource? _listenCts;
        private bool _isListening;
        private bool _isRecording;
        private string? _currentTargetIp;

        // Auto call tracking
        private DateTime _lastIncomingPacketTime = DateTime.MinValue;
        private System.Timers.Timer? _livenessTimer;
        private bool _isCallInitiatedLocally;

        // After a hangup the far end keeps sending until its DB poll sees it; without this its packets auto-answered the call again.
        internal static readonly TimeSpan HangupQuiet = TimeSpan.FromMilliseconds(2500);
        private string? _hungUpIp;
        private DateTime _lastHungUpPacketTime;

        internal static bool IsStillHungUp(DateTime lastPacketFromHungUpIp, DateTime now) => now - lastPacketFromHungUpIp < HangupQuiet;

        // Event to notify the UI when call state changes
        public event Action<string?, bool>? CallStateChanged;

        private AudioCommunicationService()
        {
        }

        public void Log(string msg)
        {
            SirenCommunicationService.Instance.Log($"[VoIP] {msg}");
        }

        // Most voice that may wait in the playback buffer; older audio is dropped so the call stays live.
        internal static readonly TimeSpan MaxVoiceLag = TimeSpan.FromMilliseconds(250);

        private IPAddress[] _localIps = Array.Empty<IPAddress>();

        // Looked up once per StartListening: a DNS call on every packet made the receive loop fall behind (3-4 s lag).
        private bool IsLocalIpAddress(IPAddress address) =>
            IPAddress.IsLoopback(address) || Array.IndexOf(_localIps, address) >= 0;

        internal static void AddVoice(BufferedWaveProvider provider, byte[] data, int length)
        {
            if (provider.BufferedDuration > MaxVoiceLag) provider.ClearBuffer();
            provider.AddSamples(data, 0, length);
        }

        public void StartListening()
        {
            if (_isListening) return;
            _isListening = true;
            _listenCts = new CancellationTokenSource();

            try { _localIps = Dns.GetHostEntry(Dns.GetHostName()).AddressList; }
            catch (Exception ex) { Log($"Could not read local IP addresses: {ex.Message}"); }

            // Setup Playback device
            try
            {
                _waveOut = new WaveOutEvent { DesiredLatency = 100 };
                var format = new WaveFormat(8000, 16, 1);
                _waveProvider = new BufferedWaveProvider(format)
                {
                    DiscardOnBufferOverflow = true
                };
                _waveOut.Init(_waveProvider);
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                Log($"Failed to initialize audio playback device: {ex.Message}");
            }

            // Setup UDP Listener
            try
            {
                _udpListener = new UdpClient();
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            }
            catch (Exception ex)
            {
                Log($"Failed to bind UDP audio port {Port}: {ex.Message}");
                return;
            }

            // Setup Liveness check timer
            _livenessTimer = new System.Timers.Timer(1000);
            _livenessTimer.Elapsed += (s, e) =>
            {
                if (_isRecording && !_isCallInitiatedLocally)
                {
                    // If call was auto-initiated and we haven't received packets for 2.5 seconds, disconnect
                    if ((DateTime.Now - _lastIncomingPacketTime).TotalMilliseconds > 2500)
                    {
                        Log("No incoming voice packets received for 2.5 seconds. Auto-disconnecting call.");
                        StopRecordingInternal();
                    }
                }
            };
            _livenessTimer.Start();

            var token = _listenCts.Token;
            Task.Run(async () =>
            {
                Log("Started listening for incoming intercom voice packets...");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpListener.ReceiveAsync(token);
                        if (result.Buffer.Length > 0 && _waveProvider != null)
                        {
                            var senderAddress = result.RemoteEndPoint.Address;
                            
                            // Discard packets coming from our own local machine to prevent echo feedback loops
                            if (IsLocalIpAddress(senderAddress))
                            {
                                continue;
                            }

                            // Drop the hung-up station's trailing voice until it has been quiet for HangupQuiet.
                            if (_hungUpIp != null && senderAddress.ToString() == _hungUpIp)
                            {
                                var now = DateTime.Now;
                                if (IsStillHungUp(_lastHungUpPacketTime, now))
                                {
                                    _lastHungUpPacketTime = now;
                                    continue;
                                }
                                _hungUpIp = null;
                            }

                            // Play received voice samples
                            AddVoice(_waveProvider, result.Buffer, result.Buffer.Length);
                            _lastIncomingPacketTime = DateTime.Now;

                            // Automatically open return voice channel if not already recording
                            string senderIp = senderAddress.ToString();
                            if (!_isRecording)
                            {
                                Log($"Received voice call request from {senderIp}. Auto-connecting microphone...");
                                StartRecordingInternal(senderIp, isLocalInitiation: false);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log($"UDP receive error: {ex.Message}");
                        await Task.Delay(100);
                    }
                }
                Log("Stopped listening for voice packets.");
            }, token);
        }

        public void StopListening()
        {
            _listenCts?.Cancel();
            _isListening = false;

            if (_livenessTimer != null)
            {
                _livenessTimer.Stop();
                _livenessTimer.Dispose();
                _livenessTimer = null;
            }

            try
            {
                _udpListener?.Close();
                _udpListener?.Dispose();
            }
            catch {}
            _udpListener = null;

            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
            }
            catch {}
            _waveOut = null;
            _waveProvider = null;
        }

        public void StartRecording(string targetIp)
        {
            _hungUpIp = null; // operator confirmed a call: accept this station's voice again
            StartRecordingInternal(targetIp, isLocalInitiation: true);
        }

        private void StartRecordingInternal(string targetIp, bool isLocalInitiation)
        {
            if (string.IsNullOrWhiteSpace(targetIp))
            {
                Log("Cannot start call: Target IP is empty.");
                return;
            }

            if (_isRecording)
            {
                if (_currentTargetIp == targetIp) return; // already calling this IP
                StopRecordingInternal();
            }

            _currentTargetIp = targetIp;
            _isRecording = true;
            _isCallInitiatedLocally = isLocalInitiation;
            if (!isLocalInitiation)
            {
                _lastIncomingPacketTime = DateTime.Now;
            }

            try
            {
                _udpSender = new UdpClient();
                
                _waveSource = new WaveInEvent
                {
                    DeviceNumber = -1, // Windows default mic, not just the first device
                    WaveFormat = new WaveFormat(8000, 16, 1),
                    BufferMilliseconds = 100 // low latency 100ms packet buffers
                };

                _waveSource.DataAvailable += (sender, e) =>
                {
                    if (e.BytesRecorded > 0 && _udpSender != null && !string.IsNullOrEmpty(_currentTargetIp))
                    {
                        try
                        {
                            _udpSender.Send(e.Buffer, e.BytesRecorded, _currentTargetIp, Port);
                        }
                        catch (Exception ex)
                        {
                            Log($"UDP send error: {ex.Message}");
                        }
                    }
                };

                _waveSource.StartRecording();
                Log($"Audio connection established with {targetIp}. Streaming live voice...");

                // Notify UI of new active call
                CallStateChanged?.Invoke(targetIp, true);
            }
            catch (Exception ex)
            {
                Log($"Failed to initialize audio capture device: {ex.Message}");
                _isRecording = false;
                CallStateChanged?.Invoke(null, false);
            }
        }

        public void StopRecording()
        {
            if (_isRecording && _currentTargetIp != null)
            {
                _lastHungUpPacketTime = DateTime.Now;
                _hungUpIp = _currentTargetIp;
            }
            StopRecordingInternal();
        }

        private void StopRecordingInternal()
        {
            if (!_isRecording) return;
            _isRecording = false;

            try
            {
                _waveSource?.StopRecording();
                _waveSource?.Dispose();
            }
            catch {}
            _waveSource = null;

            try
            {
                _udpSender?.Close();
                _udpSender?.Dispose();
            }
            catch {}
            _udpSender = null;

            Log("Audio connection closed. Voice stream ended.");

            // Notify UI of call disconnection
            CallStateChanged?.Invoke(null, false);
        }
    }
}
