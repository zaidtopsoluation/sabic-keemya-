using System;
using NAudio.Wave;

namespace Keemya.Frontend.Services
{
    public class AudioSimulationService : IDisposable
    {
        private static AudioSimulationService? _instance;
        public static AudioSimulationService Instance => _instance ??= new AudioSimulationService();

        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _bufferedWaveProvider;
        private AudioFileReader? _audioFileReader;
        private System.Windows.Threading.DispatcherTimer? _volumeTimer;

        public event EventHandler<double>? VolumeChanged;

        public void SetPttRelayState(bool state)
        {
            // Only key the PTT relay if the local station is the main Admin ECC dispatcher
            if (AppConfig.StationName != "Admin ECC") return;

            string portName = AppConfig.PttRelayPort;
            if (string.IsNullOrWhiteSpace(portName) || portName.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                using (var port = new System.IO.Ports.SerialPort(portName, 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One))
                {
                    port.Open();
                    string cmd = state ? "relay on 0\r" : "relay off 0\r";
                    port.Write(cmd);
                    System.Diagnostics.Debug.WriteLine($"[PTT Relay] Sent command to {portName}: {cmd.Trim()}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PTT Relay] Failed to control PTT relay on {portName}: {ex.Message}");
            }
        }

        public void StartLoopback()
        {
            StopLoopback(); // Ensure any existing loopback is stopped
            SetPttRelayState(true);

            try
            {
                // Capture from default microphone
                _waveIn = new WaveInEvent();
                _waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
                _waveIn.BufferMilliseconds = 50;

                // Set up buffer
                _bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat);
                _bufferedWaveProvider.DiscardOnBufferOverflow = true;
                _bufferedWaveProvider.BufferDuration = TimeSpan.FromSeconds(5);

                // Playback to default speaker
                _waveOut = new WaveOutEvent();
                _waveOut.DesiredLatency = 200; // Increase latency to prevent dropouts
                _waveOut.Init(_bufferedWaveProvider);

                // Pipe data from mic to buffer
                _waveIn.DataAvailable += (sender, e) =>
                {
                    _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

                    // Calculate peak volume for UI
                    float max = 0;
                    for (int index = 0; index < e.BytesRecorded; index += 2)
                    {
                        short sample = (short)((e.Buffer[index + 1] << 8) | e.Buffer[index]);
                        float sample32 = sample / 32768f;
                        if (sample32 < 0) sample32 = -sample32;
                        if (sample32 > max) max = sample32;
                    }
                    
                    VolumeChanged?.Invoke(this, max);
                };

                _waveIn.StartRecording();
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start audio loopback: {ex.Message}");
                StopLoopback();
            }
        }

        private List<AudioFileReader> _openedReaders = new();

        public bool IsPlaybackActive() => (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing);

        public void StartSequentialPlayback(List<string> filePaths)
        {
            StopLoopback();
            SetPttRelayState(true);

            try
            {
                _openedReaders.Clear();
                var providers = new List<ISampleProvider>();

                foreach (var path in filePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        var reader = new AudioFileReader(path);
                        _openedReaders.Add(reader);
                        providers.Add(reader);
                    }
                }

                if (providers.Count == 0)
                {
                    StopLoopback();
                    return;
                }

                var concatProvider = new NAudio.Wave.SampleProviders.ConcatenatingSampleProvider(providers);

                _waveOut = new WaveOutEvent();
                _waveOut.DesiredLatency = 200;
                _waveOut.Init(concatProvider);

                _waveOut.PlaybackStopped += (s, e) =>
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        StopLoopback();
                    });
                };

                _waveOut.Play();

                _volumeTimer = new System.Windows.Threading.DispatcherTimer();
                _volumeTimer.Interval = TimeSpan.FromMilliseconds(50);
                var random = new Random();
                _volumeTimer.Tick += (s, e) =>
                {
                    if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        VolumeChanged?.Invoke(this, random.NextDouble() * 0.5 + 0.3);
                    }
                };
                _volumeTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start sequential concatenation: {ex.Message}");
                StopLoopback();
            }
        }

        public void StartFilePlayback(string filePath)
        {
            StopLoopback();
            SetPttRelayState(true);

            try
            {
                _audioFileReader = new AudioFileReader(filePath);
                
                _waveOut = new WaveOutEvent();
                _waveOut.DesiredLatency = 200;
                _waveOut.Init(_audioFileReader);

                _waveOut.PlaybackStopped += (s, e) =>
                {
                    StopLoopback();
                };

                _waveOut.Play();

                _volumeTimer = new System.Windows.Threading.DispatcherTimer();
                _volumeTimer.Interval = TimeSpan.FromMilliseconds(50);
                var random = new Random();
                _volumeTimer.Tick += (s, e) =>
                {
                    if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        // Simulate audio pulse for the UI
                        VolumeChanged?.Invoke(this, random.NextDouble() * 0.5 + 0.3);
                    }
                };
                _volumeTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start file playback: {ex.Message}");
                StopLoopback();
            }
        }

        public double GetPlaybackPosition()
        {
            if (_audioFileReader != null) return _audioFileReader.CurrentTime.TotalSeconds;
            if (_openedReaders.Count > 0) return _openedReaders.Sum(r => r.CurrentTime.TotalSeconds);
            return 0;
        }

        public double GetPlaybackDuration()
        {
            if (_audioFileReader != null) return _audioFileReader.TotalTime.TotalSeconds;
            if (_openedReaders.Count > 0) return _openedReaders.Sum(r => r.TotalTime.TotalSeconds);
            return 0;
        }
        
        public void SetPlaybackPosition(double seconds)
        {
            if (_audioFileReader != null)
            {
                try
                {
                    _audioFileReader.CurrentTime = TimeSpan.FromSeconds(seconds);
                }
                catch { }
            }
        }

        public void EnsureDummyAudioFile(string filePath)
        {
            try
            {
                string? dir = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                if (!System.IO.File.Exists(filePath))
                {
                    // Generate 2 seconds of 440Hz beep at 16000Hz, 16-bit mono
                    int sampleRate = 16000;
                    short frequency = 440;
                    double durationSeconds = 2.0;
                    int numSamples = (int)(sampleRate * durationSeconds);
                    byte[] waveData = new byte[numSamples * 2];

                    for (int i = 0; i < numSamples; i++)
                    {
                        double t = (double)i / sampleRate;
                        short sample = (short)(Math.Sin(2 * Math.PI * frequency * t) * 16384);
                        waveData[i * 2] = (byte)(sample & 0xFF);
                        waveData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                    }

                    using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                    using (var bw = new System.IO.BinaryWriter(fs))
                    {
                        // RIFF header
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                        bw.Write(36 + waveData.Length);
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                        // fmt chunk
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                        bw.Write(16); // Subchunk1Size
                        bw.Write((short)1); // AudioFormat (PCM)
                        bw.Write((short)1); // NumChannels
                        bw.Write(sampleRate);
                        bw.Write(sampleRate * 2); // ByteRate
                        bw.Write((short)2); // BlockAlign
                        bw.Write((short)16); // BitsPerSample

                        // data chunk
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                        bw.Write(waveData.Length);
                        bw.Write(waveData);
                    }
                    System.Diagnostics.Debug.WriteLine($"[Dummy Audio] Generated dummy WAV file at: {filePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dummy Audio] Failed to generate dummy WAV: {ex.Message}");
            }
        }

        public void StopLoopback()
        {
            VolumeChanged?.Invoke(this, 0);
            SetPttRelayState(false);

            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_bufferedWaveProvider != null)
            {
                _bufferedWaveProvider.ClearBuffer();
                _bufferedWaveProvider = null;
            }

            if (_volumeTimer != null)
            {
                _volumeTimer.Stop();
                _volumeTimer = null;
            }

            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }

            foreach (var reader in _openedReaders)
            {
                try { reader.Dispose(); } catch { }
            }
            _openedReaders.Clear();
        }

        public void Dispose()
        {
            StopLoopback();
        }
    }
}
