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

        public void StartLoopback()
        {
            StopLoopback(); // Ensure any existing loopback is stopped

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

        public void StartFilePlayback(string filePath)
        {
            StopLoopback();

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

        public void StopLoopback()
        {
            VolumeChanged?.Invoke(this, 0);

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
        }

        public void Dispose()
        {
            StopLoopback();
        }
    }
}
