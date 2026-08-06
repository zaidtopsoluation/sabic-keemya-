using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MySqlConnector;

namespace Keemya.Frontend.Services
{
    public class SirenCommunicationService
    {
        private static readonly Lazy<SirenCommunicationService> _instance =
            new Lazy<SirenCommunicationService>(() => new SirenCommunicationService());

        public static SirenCommunicationService Instance => _instance.Value;

        // UI bound logs
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        // Event for notifying parsed real-time C2030 telemetry
        public event Action<string, byte, byte, byte, byte>? StandardStatusReceived;
        public event Action<string, byte, byte, byte, byte>? InstantStatusReceived;
        public event Action<string, byte, byte, byte, byte, byte, byte>? ActiveStatusReceived;
        public event Action<string, byte, byte, byte, byte>? WeatherReceived;
        public event Action<string, byte, byte, byte, byte>? ComprehensiveTempReceived;
        public event Action<string, byte, byte>? BatteryAcReceived;
        public event Action<string, byte, byte>? BatteryTempReceived;
        public event Action<string, string>? SirenStatusChanged; // sirenName, statusString (ONLINE, WARNING, OFFLINE)

        // ────────────────────────────────────────────────────────────────────
        // SPEED TUNING CONSTANTS
        // These were the source of the 10-20s command latency. A real C2030
        // siren replies in well under 100ms on a healthy serial/TCP link, so
        // the old 2000ms/1000ms waits were almost pure dead time that got
        // multiplied across every siren in a broadcast. Tune here if needed.
        // ────────────────────────────────────────────────────────────────────
        private const int SerialAckTimeoutMs = 800;          // was 2000ms — normal-path ACK wait
        private const int SerialOfflineAckTimeoutMs = 250;   // was 300ms  — ACK wait for sirens already known offline
        private const int TcpAckTimeoutMs = 800;             // was 1000ms (hardcoded) — ACK wait over TCP
        private const int AutoDetectCooldownMs = 60000;      // NEW — prevents auto-detect from re-triggering (and hogging the serial lock) on every single failed send
        private const int UserCommandLockTimeoutMs = 8000;   // was 30000ms — ceiling to acquire serial lock for a user command
        private const int PollingLockTimeoutMs = 2000;        // was 3000ms — ceiling to acquire serial lock for background polling
        private const int SerialPollInterDeviceDelayMs_Many = 500;  // was 2000ms — gap between polling >5 serial sirens
        private const int SerialPollInterDeviceDelayMs_Few = 300;   // was 1000ms — gap between polling <=5 serial sirens

        // TCP Server variables
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _tcpCts;
        private readonly ConcurrentDictionary<string, TcpClient> _connectedClients = new ConcurrentDictionary<string, TcpClient>();

        // Pending TCP ACK waits — keyed by siren IP. Tracks TaskCompletionSource and expected sent frame.
        private readonly ConcurrentDictionary<string, (TaskCompletionSource<bool> Tcs, byte[] SentFrame)> _pendingTcpAcks = new();

        // Notification state-change trackers
        private static readonly ConcurrentDictionary<string, bool> _activeIntrusions = new();
        private static readonly ConcurrentDictionary<string, bool> _activeAcLosses = new();
        private static readonly ConcurrentDictionary<string, bool> _activeLowBattery = new();
        private static readonly ConcurrentDictionary<string, bool> _activeOfflines = new();

        // Consecutive failure counter — siren must fail this many polls in a row before OFFLINE is declared
        private const int OfflineThreshold = 3;
        private static readonly ConcurrentDictionary<string, int> _failureCounters = new();

        // Live in-memory cache of siren states
        private readonly ConcurrentDictionary<string, SirenStatusCacheItem> _sirenCache = new();

        // Serial Port variables
        private SerialPort? _serialPort;
        private string _serialPortName = "COM4";
        private int _serialBaudRate = 9600; // Default — actual value loaded from DB (match your physical siren's DIP switch setting)
        private CancellationTokenSource? _serialReadCts;

        // Ensures only one serial send+read cycle runs at a time
        private readonly SemaphoreSlim _serialLock = new SemaphoreSlim(1, 1);
        private volatile bool _isReadingResponse = false;
        private volatile bool _isAutoDetecting = false;
        private volatile bool _activeReadIsUserInitiated = false;
        private volatile bool _cancelPendingRead = false;
        private volatile bool _hasSuccessfulSerialComm = false;
        private DateTime _lastUserCommandTime = DateTime.MinValue;
        private DateTime _lastSerialFrameSentTime = DateTime.MinValue;

        // Auto-detect throttling — protects against a single unresponsive siren
        // repeatedly triggering a full port/baud scan and starving the serial lock
        private DateTime _lastAutoDetectAttempt = DateTime.MinValue;

        private SirenCommunicationService()
        {
            Log("Communication Service Initialized.");
            StartTcpServer();
            LoadSerialConfig();
            StartGlobalPolling();
        }

        public void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formatted = $"[{timestamp}] {message}";

            // Also print to terminal/console for real-time debugging
            Console.WriteLine(formatted);

            try
            {
                string logPath = "C:\\Users\\HP\\Desktop\\sabic-keemya-\\siren_comm.log";
                if (!System.IO.Directory.Exists("C:\\Users\\HP\\Desktop\\sabic-keemya-"))
                {
                    logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "siren_comm.log");
                }
                System.IO.File.AppendAllText(logPath, formatted + Environment.NewLine);
            }
            catch {}

            // Ensure thread safety for UI binding
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Logs.Add(formatted);
                // Limit log size to 100 entries to prevent memory growth
                if (Logs.Count > 100)
                {
                    Logs.RemoveAt(0);
                }
            });
        }

        // Returns true if we're allowed to kick off an auto-detect scan right now.
        // Prevents a single flaky siren from causing repeated full-port/baud scans
        // that hold the serial lock and stall every other command behind them.
        private bool TryBeginAutoDetectThrottle()
        {
            var now = DateTime.Now;
            if ((now - _lastAutoDetectAttempt).TotalMilliseconds < AutoDetectCooldownMs)
            {
                return false;
            }
            _lastAutoDetectAttempt = now;
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // TCP Server Logic
        // ────────────────────────────────────────────────────────────────────
        public void StartTcpServer()
        {
            _tcpCts = new CancellationTokenSource();
            Task.Run(() => RunTcpServerAsync(_tcpCts.Token));
        }

        private async Task RunTcpServerAsync(CancellationToken token)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, 5555);
                _tcpListener.Start();
                Log("⚡ [TCP Server] Netty Replica Listening on Port 5555...");

                while (!token.IsCancellationRequested)
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync(token);
                    var clientIp = ((IPEndPoint?)tcpClient.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
                    
                    // Track connected siren client
                    _connectedClients[clientIp] = tcpClient;
                    Log($"🔌 [TCP Server] Siren Device connected from IP: {clientIp}");

                    // Handle device reading in background
                    _ = Task.Run(() => HandleTcpClientAsync(tcpClient, clientIp, token));
                }
            }
            catch (Exception ex)
            {
                Log($"❌ [TCP Server] Error: {ex.Message}");
            }
        }

        private async Task HandleTcpClientAsync(TcpClient client, string ip, CancellationToken token)
        {
            var stream = client.GetStream();
            byte[] buffer = new byte[1024];

            // Accumulate bytes across multiple TCP reads until a complete frame arrives
            var frameBuffer = new System.Collections.Generic.List<byte>();

            try
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break; // Client disconnected

                    // Append received chunk to the frame buffer
                    for (int i = 0; i < bytesRead; i++)
                        frameBuffer.Add(buffer[i]);

                    string chunkHex = string.Join(" ", buffer.Take(bytesRead).Select(b => b.ToString("X2")));
                    Log($"📥 [TCP Server] Chunk from {ip} ({bytesRead} bytes): {chunkHex}");

                    // Safety cap — discard if buffer grows unreasonably large
                    if (frameBuffer.Count > 256)
                    {
                        Log($"⚠️ [TCP Server] Buffer overflow from {ip} — discarding {frameBuffer.Count} bytes.");
                        frameBuffer.Clear();
                        continue;
                    }

                    // Check if a complete frame has arrived (contains ETX 0x03)
                    int etxIndex = frameBuffer.IndexOf(0x03);
                    if (etxIndex >= 0)
                    {
                        // Extract ONLY bytes up to and including ETX — slice precisely
                        byte[] completeFrame = frameBuffer.Take(etxIndex + 1).ToArray();
                        string frameHex = string.Join(" ", completeFrame.Select(b => b.ToString("X2")));
                        Log($"📦 [TCP Server] Complete frame assembled from {ip} ({completeFrame.Length} bytes): {frameHex}");

                        // Keep any leftover bytes AFTER ETX for the next frame
                        var leftover = frameBuffer.Skip(etxIndex + 1).ToList();
                        frameBuffer.Clear();
                        frameBuffer.AddRange(leftover);

                        // Process the clean complete frame
                        ProcessSirenResponse(ip, completeFrame, "TCP/IP");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"🔌 [TCP Server] Connection closed for {ip}: {ex.Message}");
            }
            finally
            {
                _connectedClients.TryRemove(ip, out _);
                client.Close();
                Log($"🔌 [TCP Server] Device offline: {ip}");
            }
        }

        public async Task<bool> SendTcpCommandAsync(string ip, byte[] frame, bool expectsAck = true)
        {
            // Remove port if present in IP config (e.g. 192.168.1.50:5555 -> 192.168.1.50)
            string cleanIp = ip.Split(':')[0];

            if (!_connectedClients.TryGetValue(cleanIp, out var client) || !client.Connected)
            {
                Log($"⚠️ [TCP Sender] No active connection found for siren IP: {cleanIp}");
                return false;
            }

            try
            {
                // Register a pending ACK waiter with the expected sent frame (avoid race condition)
                var ackTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingTcpAcks[cleanIp] = (ackTcs, frame);

                var stream = client.GetStream();
                await stream.WriteAsync(frame, 0, frame.Length);
                await stream.FlushAsync();

                string hex = string.Join(" ", frame.Select(b => b.ToString("X2")));
                Log($"📤 [TCP Sender] Sent frame to {cleanIp}: {hex}");
                
                if (!expectsAck)
                {
                    _pendingTcpAcks.TryRemove(cleanIp, out _);
                    Log($"✅ [TCP Sender] Command does not require ACK. Fast-completing.");
                    return true;
                }

                Log($"⏳ [TCP Sender] Waiting for ACK from {cleanIp} ({TcpAckTimeoutMs}ms timeout)...");

                // Wait for a valid siren response
                bool ackReceived = await Task.WhenAny(ackTcs.Task, Task.Delay(TcpAckTimeoutMs)) == ackTcs.Task
                                   && ackTcs.Task.Result;

                _pendingTcpAcks.TryRemove(cleanIp, out _);

                if (ackReceived)
                {
                    Log($"✅ [TCP Sender] ACK confirmed from {cleanIp} via TCP/IP.");
                    return true;
                }
                else
                {
                    Log($"⚠️ [TCP Sender] ACK timeout — siren at {cleanIp} did not reply (wrong address or offline).");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _pendingTcpAcks.TryRemove(cleanIp, out _);
                Log($"❌ [TCP Sender] Failed to transmit over TCP to {cleanIp}: {ex.Message}");
                return false;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Serial Port Logic
        // ────────────────────────────────────────────────────────────────────
        private void LoadSerialConfig()
        {
            try
            {
                string connStr = AppConfig.ConnectionString;
                using var conn = new MySqlConnection(connStr);
                conn.Open();

                // Read port name and baud rate for the CURRENT workstation from the database
                string sql = "SELECT PortName, BaudRate FROM WorkstationSerialConfigs WHERE StationName = @StationName LIMIT 1";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@StationName", AppConfig.StationName);
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    _serialPortName = rdr.GetString(0);
                    int dbBaud = rdr.GetInt32(1);
                    if (dbBaud == 1200 || dbBaud == 9600)
                        _serialBaudRate = dbBaud;
                    else
                        Log($"⚠️ [Serial Config] Invalid baud rate {dbBaud} in DB — keeping default {_serialBaudRate}.");

                    Log($"⚙️ [Serial Config] Loaded: {_serialPortName} @ {_serialBaudRate} baud for workstation {AppConfig.StationName}");
                }
            }
            catch
            {
                Log($"⚙️ [Serial Config] DB table missing or unseeded for {AppConfig.StationName}. Using defaults: {_serialPortName} @ {_serialBaudRate} baud");
            }
        }

        public async Task<string?> AutoDetectSerialPortAsync()
        {
            if (AppConfig.StationName != "Admin ECC") return null;

            if (_isAutoDetecting)
            {
                Log("🔍 [Auto-Detect] Port scanning is already in progress.");
                return null;
            }

            _isAutoDetecting = true;
            Log("🔍 [Auto-Detect] Starting automatic serial port detection...");

            // 1. Get all siren addresses from DB to test, starting with wildcard
            var testFrames = new List<(string Name, byte[] Frame)>();
            
            // Always prioritize wildcard frame first to quickly scan on any active port
            byte[] wildcardFrame = new byte[15];
            wildcardFrame[0] = 0x02;
            wildcardFrame[1] = 0x8F; wildcardFrame[2] = 0x8F; wildcardFrame[3] = 0x8F;
            wildcardFrame[4] = 0x8F; wildcardFrame[5] = 0x8F; wildcardFrame[6] = 0x8F; wildcardFrame[7] = 0x8F;
            wildcardFrame[8] = 0x80; wildcardFrame[9] = 0x80;
            wildcardFrame[10] = 0xA3;
            wildcardFrame[11] = 0x03;
            byte wildcardXor = 0;
            for (int i = 0; i <= 11; i++) wildcardXor ^= wildcardFrame[i];
            wildcardFrame[12] = (byte)(0x80 | (wildcardXor >> 4));
            wildcardFrame[13] = (byte)(0x80 | (wildcardXor & 0x0F));
            wildcardFrame[14] = 0x0D;
            testFrames.Add(("Default Wildcard", wildcardFrame));

            try
            {
                using var conn = new MySqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();
                using var cmd = new MySqlCommand("SELECT Name, AreaCode, AddressCode FROM SirenDevices", conn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    string name = rdr.GetString(0);
                    string areaCode = rdr.IsDBNull(1) ? "000" : rdr.GetString(1);
                    string addressCode = rdr.IsDBNull(2) ? "0000" : rdr.GetString(2);

                    // Construct 15-byte protocol frame for 0x23 (Instant Status Check)
                    byte[] frame = new byte[15];
                    frame[0] = 0x02;

                    string area = areaCode.PadLeft(3, '0');
                    frame[1] = (byte)(0x80 | (area[0] - '0'));
                    frame[2] = (byte)(0x80 | (area[1] - '0'));
                    frame[3] = (byte)(0x80 | (area[2] - '0'));

                    string addr = addressCode.PadLeft(4, '0');
                    frame[4] = (byte)(0x80 | (addr[0] - '0'));
                    frame[5] = (byte)(0x80 | (addr[1] - '0'));
                    frame[6] = (byte)(0x80 | (addr[2] - '0'));
                    frame[7] = (byte)(0x80 | (addr[3] - '0'));

                    frame[8] = 0x80;
                    frame[9] = 0x80;
                    frame[10] = 0xA3; // 0x23 | 0x80
                    frame[11] = 0x03;

                    byte xor = 0;
                    for (int i = 0; i <= 11; i++) xor ^= frame[i];
                    frame[12] = (byte)(0x80 | (xor >> 4));
                    frame[13] = (byte)(0x80 | (xor & 0x0F));
                    frame[14] = 0x0D;

                    testFrames.Add((name, frame));
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ [Auto-Detect] Database query failed: {ex.Message}");
            }

            string[] availablePorts = SerialPort.GetPortNames();
            if (availablePorts.Length == 0)
            {
                Log("❌ [Auto-Detect] No active COM ports found on this machine.");
                _isAutoDetecting = false;
                return null;
            }

            Log($"🔍 [Auto-Detect] Found {availablePorts.Length} port(s): {string.Join(", ", availablePorts)}");

            // Acquire lock to avoid conflict with normal transmission
            if (!await _serialLock.WaitAsync(10000))
            {
                Log("⚠️ [Auto-Detect] Serial port lock is busy. Aborting scan.");
                _isAutoDetecting = false;
                return null;
            }

            try
            {
                // Temporarily close and dispose active serial port
                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                            _serialPort.Close();
                    }
                    catch {}
                    _serialPort.Dispose();
                    _serialPort = null;
                }

                // Determine bauds to test (try current default/configured baud first)
                int defaultBaud = _serialBaudRate;
                int altBaud = _serialBaudRate == 1200 ? 9600 : 1200;
                int[] baudsToTest = new int[] { defaultBaud, altBaud };

                foreach (string portName in availablePorts)
                {
                    foreach (int baudRate in baudsToTest)
                    {
                        Log($"🔍 [Auto-Detect] Testing {portName} @ {baudRate} baud...");
                        using var testPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                        testPort.ReadTimeout = 1000;
                        testPort.WriteTimeout = 1000;

                        try
                        {
                            testPort.Open();

                            foreach (var tf in testFrames)
                            {
                                Log($"🔍 [Auto-Detect]   Pinging with address of '{tf.Name}'...");
                                testPort.DiscardInBuffer();
                                testPort.DiscardOutBuffer();

                                testPort.Write(tf.Frame, 0, tf.Frame.Length);

                                // Wait up to 1 second for any incoming byte
                                var sw = System.Diagnostics.Stopwatch.StartNew();
                                bool replied = false;
                                while (sw.ElapsedMilliseconds < 1000)
                                {
                                    if (testPort.BytesToRead > 0)
                                    {
                                        replied = true;
                                        break;
                                    }
                                    await Task.Delay(50);
                                }

                                if (replied)
                                {
                                    Log($"✨ [Auto-Detect] SUCCESS! Siren responded on port {portName} @ {baudRate} baud using address of '{tf.Name}'.");
                                    _serialPortName = portName;
                                    _serialBaudRate = baudRate;
                                    
                                    // Reinitialize main serial port to this config
                                    if (_serialPort != null)
                                    {
                                        try
                                        {
                                            if (_serialPort.IsOpen) _serialPort.Close();
                                        }
                                        catch {}
                                        _serialPort.Dispose();
                                    }
                                    _serialPort = new SerialPort(_serialPortName, _serialBaudRate, Parity.None, 8, StopBits.One);
                                    _serialPort.ReadTimeout = 5000;
                                    _serialPort.WriteTimeout = 5000;

                                    // Save to database
                                    _ = Task.Run(() => SaveSerialPortConfigAsync(portName, baudRate));

                                    return portName;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"🔍 [Auto-Detect] Port {portName} @ {baudRate} baud check failed: {ex.Message}");
                        }
                        finally
                        {
                            if (testPort.IsOpen) testPort.Close();
                        }
                    }
                }

                Log("❌ [Auto-Detect] No responding sirens found on any available COM port.");
                return null;
            }
            finally
            {
                _serialLock.Release();
                _isAutoDetecting = false;
            }
        }

        private async Task SaveSerialPortConfigAsync(string portName, int baudRate)
        {
            if (AppConfig.StationName != "Admin ECC") return;

            try
            {
                using var conn = new MySqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();
                
                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS `WorkstationSerialConfigs` (
                        `StationName` VARCHAR(255) NOT NULL,
                        `PortName` VARCHAR(50) NOT NULL,
                        `BaudRate` INT NOT NULL,
                        PRIMARY KEY (`StationName`)
                    );";
                using (var createCmd = new MySqlCommand(createTableSql, conn))
                {
                    await createCmd.ExecuteNonQueryAsync();
                }

                string insertSql = @"
                    INSERT INTO WorkstationSerialConfigs (StationName, PortName, BaudRate) 
                    VALUES (@StationName, @PortName, @BaudRate)
                    ON DUPLICATE KEY UPDATE PortName = @PortName, BaudRate = @BaudRate;";
                using (var insertCmd = new MySqlCommand(insertSql, conn))
                {
                    insertCmd.Parameters.AddWithValue("@StationName", AppConfig.StationName);
                    insertCmd.Parameters.AddWithValue("@PortName", portName);
                    insertCmd.Parameters.AddWithValue("@BaudRate", baudRate);
                    await insertCmd.ExecuteNonQueryAsync();
                }
                
                Log($"⚙️ [Serial Config] Saved to DB for {AppConfig.StationName}: {portName} @ {baudRate} baud");
            }
            catch (Exception ex)
            {
                Log($"❌ [Serial Config] Failed to save config to DB: {ex.Message}");
            }
        }

        public async Task SendBinaryCancelPacketsAsync()
        {
            _lastUserCommandTime = DateTime.Now;
            InterruptSerialRead();

            await _serialLock.WaitAsync();
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    if (_serialPort != null && _serialPort.PortName == _serialPortName)
                    {
                        _serialPort.Open();
                    }
                    else
                    {
                        if (_serialPort != null)
                        {
                            try
                            {
                                if (_serialPort.IsOpen) _serialPort.Close();
                            }
                            catch {}
                            _serialPort.Dispose();
                        }
                        _serialPort = new SerialPort(_serialPortName, _serialBaudRate, Parity.None, 8, StopBits.One);
                        _serialPort.ReadTimeout = 2000;
                        _serialPort.WriteTimeout = 2000;
                        _serialPort.Open();
                    }
                }

                // Clear buffers before transmission to flush stale bytes
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                // Send Binary Clear (0x06, 0x00, 0x06) 5 times to cut through noise/ground shift
                for (int i = 0; i < 5; i++)
                {
                    byte[] binClear = new byte[] { 0x06, 0x00, 0x06 };
                    _serialPort.Write(binClear, 0, binClear.Length);
                    Log($"📤 [Serial Sender] Dispatched binary direct CLEAR (06 00 06) [Attempt {i + 1}/5]");
                    await Task.Delay(150);
                }

                await Task.Delay(400);

                // Send Binary Siren Off (0x06, 0x1B, 0x21) 3 times
                for (int i = 0; i < 3; i++)
                {
                    byte[] binSirenOff = new byte[] { 0x06, 0x1B, 0x21 };
                    _serialPort.Write(binSirenOff, 0, binSirenOff.Length);
                    Log($"📤 [Serial Sender] Dispatched binary direct SIREN OFF (06 1B 21) [Attempt {i + 1}/3]");
                    await Task.Delay(150);
                }

                await Task.Delay(400);

                // Send Binary Test Clear (0x06, 0x1E, 0x24) 3 times
                for (int i = 0; i < 3; i++)
                {
                    byte[] binTestClear = new byte[] { 0x06, 0x1E, 0x24 };
                    _serialPort.Write(binTestClear, 0, binTestClear.Length);
                    Log($"📤 [Serial Sender] Dispatched binary direct TEST CLEAR (06 1E 24) [Attempt {i + 1}/3]");
                    await Task.Delay(150);
                }
            }
            catch (Exception ex)
            {
                Log($"❌ [Serial Sender] Error sending binary cancel packets: {ex.Message}");
            }
            finally
            {
                _serialLock.Release();
            }
        }

        private byte[] BuildWildcardFrame(byte commandHex)
        {
            byte[] frame = new byte[15];
            frame[0] = 0x02; // STX

            // Area Code (wildcards)
            frame[1] = 0x8F;
            frame[2] = 0x8F;
            frame[3] = 0x8F;

            // Address Code (wildcards)
            frame[4] = 0x8F;
            frame[5] = 0x8F;
            frame[6] = 0x8F;
            frame[7] = 0x8F;

            // Command bytes
            frame[8] = 0x80;
            frame[9] = 0x80;
            frame[10] = (byte)(0x80 | commandHex);

            frame[11] = 0x03; // ETX

            // BCN Checksum
            byte xorSum = 0;
            for (int i = 0; i <= 11; i++)
            {
                xorSum ^= frame[i];
            }
            frame[12] = (byte)(0x80 | (xorSum >> 4));
            frame[13] = (byte)(0x80 | (xorSum & 0x0F));
            frame[14] = 0x0D; // CR

            return frame;
        }

        public async Task<bool> SendWildcardClearAsync()
        {
            if (AppConfig.StationName != "Admin ECC")
            {
                Log("📥 [Remote Station] Relaying wildcard clear to Admin ECC...");
                try
                {
                    using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                    {
                        await connection.OpenAsync();
                        string sql = @"
                            INSERT INTO PendingCommands (Id, TargetSirenName, IpAddress, Redundant, FrameHex, TrackStatus, IsUserInitiated, Created, Status)
                            VALUES (@Id, '__WILDCARD_CLEAR__', '', 0, '', 0, 1, @Created, 'PENDING');";
                        using (var cmd = new MySqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                            cmd.Parameters.AddWithValue("@Created", DateTime.Now);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    Log("✅ [Remote Station] Wildcard clear successfully queued in database.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"❌ [Remote Station] Failed to queue wildcard clear: {ex.Message}");
                    return false;
                }
            }

            // Send binary cancel packets first for immediate C2030 board response
            await SendBinaryCancelPacketsAsync();
            await Task.Delay(950);

            // Send wildcard CLEAR for Group 0 (0x00)
            byte[] frame00 = BuildWildcardFrame(0x00);
            Log("📤 [Serial Sender] Dispatched global wildcard CLEAR Group 0 (0x00) command.");
            bool res1 = await SendSerialCommandAsync(frame00, expectsAck: false, isUserInitiated: true);
            await Task.Delay(950);

            // Send wildcard SIREN OFF (0x1B) command to disable audio power amplifiers
            byte[] frame1B = BuildWildcardFrame(0x1B);
            Log("📤 [Serial Sender] Dispatched global wildcard SIREN OFF (0x1B) command.");
            bool resOff = await SendSerialCommandAsync(frame1B, expectsAck: false, isUserInitiated: true);
            await Task.Delay(950);

            // Send wildcard CLEAR for Group 1 (0x10)
            byte[] frame10 = BuildWildcardFrame(0x10);
            Log("📤 [Serial Sender] Dispatched global wildcard CLEAR Group 1 (0x10) command.");
            bool res2 = await SendSerialCommandAsync(frame10, expectsAck: false, isUserInitiated: true);
            await Task.Delay(950);

            // Send wildcard CLEAR for Group 3 (0x30)
            byte[] frame30 = BuildWildcardFrame(0x30);
            Log("📤 [Serial Sender] Dispatched global wildcard CLEAR Group 3 (0x30) command.");
            bool res3 = await SendSerialCommandAsync(frame30, expectsAck: false, isUserInitiated: true);
            await Task.Delay(950);

            // Send wildcard CANCEL/TEST CLEAR (0x1E) to clear LEDs
            byte[] frame1E = BuildWildcardFrame(0x1E);
            Log("📤 [Serial Sender] Dispatched global wildcard TEST CLEAR (0x1E) command.");
            bool res4 = await SendSerialCommandAsync(frame1E, expectsAck: false, isUserInitiated: true);

            return res1 && resOff && res2 && res3 && res4;
        }

        public async Task<bool> SendSerialCommandAsync(byte[] frame, bool expectsAck = true, bool isUserInitiated = true)
        {
            if (AppConfig.StationName != "Admin ECC")
            {
                Log($"📥 [Remote Station] Relaying raw serial command to Admin ECC via database queue...");
                try
                {
                    using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                    {
                        await connection.OpenAsync();
                        string sql = @"
                            INSERT INTO PendingCommands (Id, TargetSirenName, IpAddress, Redundant, FrameHex, TrackStatus, IsUserInitiated, Created, Status)
                            VALUES (@Id, '__RAW_SERIAL__', '', 0, @FrameHex, @TrackStatus, @IsUserInitiated, @Created, 'PENDING');";
                        using (var cmd = new MySqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                            cmd.Parameters.AddWithValue("@FrameHex", BitConverter.ToString(frame).Replace("-", ""));
                            cmd.Parameters.AddWithValue("@TrackStatus", expectsAck ? 1 : 0);
                            cmd.Parameters.AddWithValue("@IsUserInitiated", isUserInitiated ? 1 : 0);
                            cmd.Parameters.AddWithValue("@Created", DateTime.Now);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    Log($"✅ [Remote Station] Raw serial command successfully queued in database.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"❌ [Remote Station] Failed to queue raw serial command: {ex.Message}");
                    return false;
                }
            }

            if (isUserInitiated)
            {
                InterruptSerialRead();
            }

            // For user-initiated commands, wait up to UserCommandLockTimeoutMs to transmit.
            // For background polling, wait PollingLockTimeoutMs.
            int lockTimeout = isUserInitiated ? UserCommandLockTimeoutMs : PollingLockTimeoutMs;
            if (!await _serialLock.WaitAsync(lockTimeout))
            {
                Log($"⚠️ [Serial Sender] Busy — serial port lock could not be acquired within {lockTimeout / 1000}s. Skipping command.");
                return false;
            }

            try
            {
                // Check if port is already open or configured
                if (_serialPort == null || _serialPort.PortName != _serialPortName)
                {
                    if (_serialPort != null)
                    {
                        try
                        {
                            if (_serialPort.IsOpen)
                                _serialPort.Close();
                        }
                        catch {}
                        _serialPort.Dispose();
                    }

                    _serialPort = new SerialPort(_serialPortName, _serialBaudRate, Parity.None, 8, StopBits.One);
                    _serialPort.ReadTimeout = 2000; // 2 seconds is extremely generous and prevents long delays
                    _serialPort.WriteTimeout = 2000;
                }

                if (!_serialPort.IsOpen)
                {
                    int retries = 3;
                    while (retries > 0)
                    {
                        try
                        {
                            _serialPort.Open();
                            Log($"🔌 [Serial] Port {_serialPortName} opened successfully.");
                            break;
                        }
                        catch (UnauthorizedAccessException) when (retries > 1)
                        {
                            retries--;
                            await Task.Delay(100);
                        }
                    }
                }

                // ⚠️ CRITICAL: Discard any stale bytes left in the RX buffer from a
                // previous response BEFORE sending a new command. Without this, old
                // data gets read back as if it were a fresh reply (false positives).
                int staleBytes = _serialPort.BytesToRead;
                if (staleBytes > 0)
                {
                    _serialPort.DiscardInBuffer();
                    Log($"🧹 [Serial] Flushed {staleBytes} stale byte(s) from RX buffer before send.");
                }

                // ⚠️ CRITICAL: Enforce C2030 inter-command delay requirement of ~850ms (we use 950ms for safety margin)
                var elapsedMs = (DateTime.Now - _lastSerialFrameSentTime).TotalMilliseconds;
                if (elapsedMs < 950)
                {
                    int delayNeeded = 950 - (int)elapsedMs;
                    Log($"⏳ [Serial Sender] Enforcing inter-command delay: delaying transmission by {delayNeeded}ms...");
                    await Task.Delay(delayNeeded);
                }

                _lastSerialFrameSentTime = DateTime.Now;
                _serialPort.Write(frame, 0, frame.Length);
                string hex = string.Join(" ", frame.Select(b => b.ToString("X2")));
                Log($"📤 [Serial Sender] Successfully sent frame: {hex}");

                if (!expectsAck)
                {
                    // Delay to allow the UART transmission to clear the buffer and let the siren process the command cleanly without serial line interference
                    await Task.Delay(950);
                    byte cmdByte = (byte)(frame.Length > 10 ? (frame[10] & 0x7F) : 0);
                    Log($"✅ [Serial Sender] Command {cmdByte:X2}H does not require ACK. Fast-completing.");
                    return true;
                }

                // Wait for the siren's ACK reply and return whether it was valid
                // This is key for redundancy: TCP failover only triggers if siren truly didn't reply
                bool ackReceived = await ReadSerialResponseAsync(_serialPort, frame, isUserInitiated);

                if (!ackReceived && !_hasSuccessfulSerialComm)
                {
                    if (TryBeginAutoDetectThrottle())
                    {
                        Log("⚠️ [Serial Sender] No successful serial communication has occurred yet. Triggering auto-detection...");
                        _ = Task.Run(() => AutoDetectSerialPortAsync());
                    }
                    else
                    {
                        Log("⏳ [Serial Sender] Skipping auto-detect retrigger (cooldown active) — avoids stalling the serial lock for other sirens.");
                    }
                }

                return ackReceived;
            }
            catch (Exception ex)
            {
                Log($"❌ [Serial Sender] Port {_serialPortName} is unavailable or locked: {ex.Message}");

                // Trigger auto-detect asynchronously if the port failed to open or transmit,
                // but respect the cooldown so a persistent fault doesn't repeatedly
                // seize the serial lock and stall the rest of a broadcast.
                if (TryBeginAutoDetectThrottle())
                {
                    _ = Task.Run(() => AutoDetectSerialPortAsync());
                }

                return false;
            }
            finally
            {
                _serialLock.Release(); // Always release in outer finally block!
            }
        }

        // Returns true if a valid ACK frame with matching address was received, false on timeout or error
        private async Task<bool> ReadSerialResponseAsync(SerialPort port, byte[] sentFrame, bool isUserInitiated = true)
        {
            _isReadingResponse = true;
            _activeReadIsUserInitiated = isUserInitiated;
            _cancelPendingRead = false;
            _serialReadCts = new CancellationTokenSource();

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int totalTimeoutMs = SerialAckTimeoutMs;

                if (isUserInitiated && sentFrame != null && sentFrame.Length >= 8)
                {
                    try
                    {
                        string area = $"{(sentFrame[1] & 0x7F)}{(sentFrame[2] & 0x7F)}{(sentFrame[3] & 0x7F)}";
                        string addr = $"{(sentFrame[4] & 0x7F)}{(sentFrame[5] & 0x7F)}{(sentFrame[6] & 0x7F)}{(sentFrame[7] & 0x7F)}";
                        string fullAddr = $"{area}{addr}";

                        var cacheItem = GetCacheItemByAddressOrSource(fullAddr);
                        if (cacheItem != null && !cacheItem.IsOnline)
                        {
                            totalTimeoutMs = SerialOfflineAckTimeoutMs;
                        }
                    }
                    catch
                    {
                    }
                }

                using var timeoutCts = new CancellationTokenSource(totalTimeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _serialReadCts.Token);
                var token = linkedCts.Token;

                var frameBuffer = new System.Collections.Generic.List<byte>();

                while (sw.ElapsedMilliseconds < totalTimeoutMs)
                {
                    token.ThrowIfCancellationRequested();
                    if (_cancelPendingRead)
                    {
                        Log("🔌 [Serial Rx] Read cancelled gracefully (interrupted by user command).");
                        return false;
                    }

                    int remainingTimeout = totalTimeoutMs - (int)sw.ElapsedMilliseconds;
                    if (remainingTimeout <= 0) break;

                    if (port.BytesToRead > 0)
                    {
                        try
                        {
                            // Guaranteed not to block because BytesToRead > 0
                            int b = port.ReadByte();
                            if (b >= 0)
                            {
                                frameBuffer.Add((byte)b);

                                // C2030 frame ends with CR (0x0D) - this ensures we consume the checksum and CR bytes too!
                                if (b == 0x0D)
                                {
                                    byte[] received = frameBuffer.ToArray();
                                    string hex = string.Join(" ", received.Select(x => x.ToString("X2")));
                                    string ascii = new string(received.Select(x => x >= 0x20 && x < 0x7F ? (char)x : '.').ToArray());

                                    Log($"📥 [Serial Rx] Full Frame ({received.Length} bytes)");
                                    Log($"    HEX : {hex}");
                                    Log($"    STR : {ascii}");

                                    // Process and verify address. If mismatch, loop continues to read any other incoming frame
                                    if (ProcessSirenResponse("SerialPort", received, "Serial", sentFrame))
                                    {
                                        return true;
                                    }

                                    frameBuffer.Clear(); // Clear buffer to prepare for next incoming frame
                                }

                                // Safety cap — no valid frame should exceed 128 bytes
                                if (frameBuffer.Count >= 128)
                                {
                                    frameBuffer.Clear();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex is InvalidOperationException || ex is System.IO.IOException)
                            {
                                Log("🔌 [Serial Rx] Read interrupted (port closed or disconnected).");
                            }
                            else
                            {
                                Log($"❌ [Serial Rx] Error reading: {ex.Message}");
                            }
                            return false;
                        }
                    }
                    else
                    {
                        // Wait a tiny bit before polling again. Yields CPU and is fully cancellable.
                        await Task.Delay(15, token);
                    }
                }

                // Handle any partial data left in the buffer on overall timeout
                if (frameBuffer.Count > 0)
                {
                    byte[] partial = frameBuffer.ToArray();
                    string hex = string.Join(" ", partial.Select(x => x.ToString("X2")));
                    string ascii = new string(partial.Select(x => x >= 0x20 && x < 0x7F ? (char)x : '.').ToArray());
                    Log($"📥 [Serial Rx] Partial Frame ({partial.Length} bytes, no CR received)");
                    Log($"    HEX : {hex}");
                    Log($"    STR : {ascii}");

                    // 0xFB is the C2030 NAK byte — siren is alive but rejecting this frame.
                    if (partial.Length == 1 && partial[0] == 0xFB)
                    {
                        Log("❌ [Serial Rx] NAK (0xFB) received — Siren REJECTED the frame. " +
                            "Check that the AreaCode and AddressCode in the DB match the siren's physical DIP switches.");
                        return false;
                    }

                    if (ProcessSirenResponse("SerialPort", partial, "Serial", sentFrame))
                    {
                        return true;
                    }
                }

                Log("⚠️ [Serial Rx] ACK Response Timeout (Siren did not reply or total timeout reached).");
                return false;
            }
            catch (OperationCanceledException)
            {
                if (_cancelPendingRead || (_serialReadCts != null && _serialReadCts.IsCancellationRequested))
                {
                    Log("🔌 [Serial Rx] Read cancelled gracefully (interrupted by user command).");
                }
                else
                {
                    Log("⚠️ [Serial Rx] ACK Response Timeout (Siren did not reply or total timeout reached).");
                }
                return false;
            }
            catch (Exception ex)
            {
                Log($"❌ [Serial Rx] Error reading: {ex.Message}");
                return false;
            }
            finally
            {
                _isReadingResponse = false;
                _activeReadIsUserInitiated = false;
                _cancelPendingRead = false;
                _serialReadCts?.Dispose();
                _serialReadCts = null;
            }
        }

        private void InterruptSerialRead()
        {
            if (!_isReadingResponse || _activeReadIsUserInitiated) return;
            Log("🔌 [Serial] Requesting cancellation of active background serial read...");
            _cancelPendingRead = true;
            try
            {
                _serialReadCts?.Cancel();
            }
            catch {}
            
            // Wait up to 150ms for the read thread to exit cleanly
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_isReadingResponse && sw.ElapsedMilliseconds < 150)
            {
                Thread.Sleep(10);
            }
        }

        private byte[] BuildSirenOnFrame(byte[] originalFrame)
        {
            byte[] frame = new byte[15];
            frame[0] = 0x02; // STX
            
            // Copy area code and address code digits (index 1 to 7)
            Array.Copy(originalFrame, 1, frame, 1, 7);
            
            // Command digit 11 set to Siren On (0x1A)
            frame[8] = 0x80;
            frame[9] = 0x80;
            frame[10] = (byte)(0x80 | 0x1A); // 0x1A = Siren On
            
            frame[11] = 0x03; // ETX
            
            // BCN Checksum
            byte xorSum = 0;
            for (int i = 0; i <= 11; i++)
            {
                xorSum ^= frame[i];
            }
            frame[12] = (byte)(0x80 | (xorSum >> 4));
            frame[13] = (byte)(0x80 | (xorSum & 0x0F));
            frame[14] = 0x0D; // CR
            
            return frame;
        }

        public async Task SendSirenOnSequenceAsync(byte[] targetFrame)
        {
            if (AppConfig.StationName != "Admin ECC") return;

            _lastUserCommandTime = DateTime.Now;
            InterruptSerialRead();

            await _serialLock.WaitAsync();
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    if (_serialPort != null && _serialPort.PortName == _serialPortName)
                    {
                        _serialPort.Open();
                    }
                    else
                    {
                        if (_serialPort != null)
                        {
                            try
                            {
                                if (_serialPort.IsOpen) _serialPort.Close();
                            }
                            catch {}
                            _serialPort.Dispose();
                        }
                        _serialPort = new SerialPort(_serialPortName, _serialBaudRate, Parity.None, 8, StopBits.One);
                        _serialPort.ReadTimeout = 2000;
                        _serialPort.WriteTimeout = 2000;
                        _serialPort.Open();
                    }
                }

                // Clear buffers to avoid stale data
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                // 1. Send Binary Siren On (0x06, 0x1A, 0x20) 3 times for noise resilience
                for (int i = 0; i < 3; i++)
                {
                    byte[] binSirenOn = new byte[] { 0x06, 0x1A, 0x20 };
                    _serialPort.Write(binSirenOn, 0, binSirenOn.Length);
                    Log($"📤 [Serial Sender] Dispatched binary direct SIREN ON (06 1A 20) [Attempt {i + 1}/3]");
                    await Task.Delay(150);
                }

                // 2. Send ASCII Siren On frame
                byte[] asciiSirenOn = BuildSirenOnFrame(targetFrame);
                _serialPort.Write(asciiSirenOn, 0, asciiSirenOn.Length);
                string hex = string.Join(" ", asciiSirenOn.Select(b => b.ToString("X2")));
                Log($"📤 [Serial Sender] Dispatched ASCII SIREN ON frame: {hex}");
            }
            catch (Exception ex)
            {
                Log($"❌ [Serial Sender] Error sending Siren On sequence: {ex.Message}");
            }
            finally
            {
                _serialLock.Release();
            }

            // Enforce C2030 inter-command delay
            await Task.Delay(950);
        }

        // ────────────────────────────────────────────────────────────────────
        // Redundancy Operations (Auto-Failover Logic)
        // ────────────────────────────────────────────────────────────────────
        public async Task<bool> ExecuteTransmitAsync(string sirenName, string ipAddress, bool redundant, byte[] frame, bool trackStatus = true, bool isUserInitiated = true)
        {
            if (AppConfig.StationName != "Admin ECC")
            {
                Log($"📥 [Remote Station] Relaying command for {sirenName} to Admin ECC...");
                try
                {
                    using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                    {
                        await connection.OpenAsync();
                        string sql = @"
                            INSERT INTO PendingCommands (Id, TargetSirenName, IpAddress, Redundant, FrameHex, TrackStatus, IsUserInitiated, Created, Status)
                            VALUES (@Id, @Target, @Ip, @Redundant, @FrameHex, @TrackStatus, @IsUserInitiated, @Created, 'PENDING');";
                        using (var cmd = new MySqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                            cmd.Parameters.AddWithValue("@Target", sirenName);
                            cmd.Parameters.AddWithValue("@Ip", ipAddress ?? "");
                            cmd.Parameters.AddWithValue("@Redundant", redundant ? 1 : 0);
                            cmd.Parameters.AddWithValue("@FrameHex", BitConverter.ToString(frame).Replace("-", ""));
                            cmd.Parameters.AddWithValue("@TrackStatus", trackStatus ? 1 : 0);
                            cmd.Parameters.AddWithValue("@IsUserInitiated", isUserInitiated ? 1 : 0);
                            cmd.Parameters.AddWithValue("@Created", DateTime.Now);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    Log($"✅ [Remote Station] Command successfully queued in database for {sirenName}.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"❌ [Remote Station] Failed to queue command in database: {ex.Message}");
                    return false;
                }
            }

            Log($"🚀 [Redundant Transmit] Activating target: {sirenName}");

            // Check if this command expects an ACK based on Whelen Protocol
            byte cmdByte = (byte)(frame[10] & 0x7F);
            
            // If it is an activation command for a tone or digital voice (not cancel, status, or strobe commands)
            bool isToneActivation = (cmdByte >= 0x01 && cmdByte <= 0x08) || (cmdByte >= 0x31 && cmdByte <= 0x41) || cmdByte == 0x1A;
            if (isToneActivation && isUserInitiated)
            {
                Log($"🔊 [Redundant Transmit] Tone command {cmdByte:X2}H detected. Ensuring Siren power amplifiers are ON first...");
                await SendSirenOnSequenceAsync(frame);
            }

            // Only query and diagnostic status commands return status responses (expects ACK).
            // Tone and setup commands (Wail, Attack, Alert, PA, Siren On/Off) are write-only activations.
            bool expectsAck = (cmdByte == 35 || cmdByte == 31 || cmdByte == 15 || cmdByte == 22 || cmdByte == 33 || cmdByte == 34);

            if (isUserInitiated)
            {
                InterruptSerialRead();
            }

            if (!expectsAck)
            {
                Log($"ℹ️ [Redundant Transmit] Command {cmdByte:X2}H does not return status bytes. Fast-acking.");
            }

            bool hasIp = !string.IsNullOrWhiteSpace(ipAddress);

            // Determine transmission path
            // 1. TCP-only (has IP and not redundant)
            if (hasIp && !redundant)
            {
                Log($"ℹ️ [Redundant Transmit] Routing via TCP-only for {sirenName} (IP: {ipAddress}).");
                bool tcpSuccess = await SendTcpCommandAsync(ipAddress, frame, expectsAck);
                if (tcpSuccess)
                {
                    Log($"✅ [Redundant Transmit] TCP transmit successful for {sirenName}.");
                    if (trackStatus)
                    {
                        TrackSuccess(sirenName, cmdByte);
                    }
                    return true;
                }
                else
                {
                    Log($"❌ [Redundant Transmit] TCP transmit failed for {sirenName}.");
                    if (trackStatus)
                    {
                        TrackFailure(sirenName);
                    }
                    return false;
                }
            }

            // 2. Serial-only or Redundant (Try serial first)
            Log($"ℹ️ [Redundant Transmit] Routing via Serial for {sirenName}.");
            bool serialSuccess = await SendSerialCommandAsync(frame, expectsAck, isUserInitiated);

            if (serialSuccess)
            {
                // Serial ACK confirmed (or not expected) — done, no need to touch TCP unless not redundant
                Log($"✅ [Redundant Transmit] Serial transmit successful for {sirenName}. Done.");

                // If it is redundant and command does not expect ACK, we also transmit via TCP as redundant delivery.
                if (redundant && hasIp && !expectsAck)
                {
                    Log($"ℹ️ [Redundant Transmit] Command {cmdByte:X2}H has no ACK and target has redundant IP. Also transmitting to TCP/IP backup...");
                    await SendTcpCommandAsync(ipAddress, frame, expectsAck);
                }

                if (trackStatus)
                {
                    TrackSuccess(sirenName, cmdByte);
                }
                return true;
            }
            
            // 3. Serial failed (port error, timeout, no ACK)
            if (redundant && hasIp)
            {
                Log($"⚠️ [Redundant Transmit] Serial failed for {sirenName}. Switching to TCP/IP backup...");
                bool tcpSuccess = await SendTcpCommandAsync(ipAddress, frame, expectsAck);
                if (tcpSuccess)
                {
                    Log($"✅ [Redundant Transmit] TCP/IP failover succeeded for {sirenName}.");
                    if (trackStatus)
                    {
                        TrackSuccess(sirenName, cmdByte);
                    }
                    return true;
                }
                else
                {
                    Log($"❌ [Redundant Transmit] ALL channels failed for {sirenName}. Siren may be offline!");
                    if (trackStatus)
                    {
                        TrackFailure(sirenName);
                    }
                    return false;
                }
            }
            else
            {
                Log($"❌ [Redundant Transmit] Serial failed for {sirenName}. Failover aborted: {(redundant ? "No IP address assigned" : "Device is Serial-only")}.");
                if (trackStatus)
                {
                    TrackFailure(sirenName);
                }
                return false;
            }
        }

        public string GetComputedStatus(SirenStatusCacheItem item)
        {
            if (!item.IsTcpOnline && !item.IsSerialOnline)
                return "OFFLINE";

            // If only one channel is online, classify status as WARNING (displays Yellow)
            if ((item.IsTcpOnline && !item.IsSerialOnline) || (!item.IsTcpOnline && item.IsSerialOnline))
                return "WARNING";

            // If both are online, check if there is an active alarm. If so, return WARNING. Otherwise return ONLINE (displays Green).
            if (item.HasAlarm || (item.DcVoltage > 0 && item.DcVoltage < 22.0))
            {
                return "WARNING";
            }

            return "ONLINE";
        }

        private async Task SyncSirenStatusToDbAndNotifyAsync(string sirenName, string statusStr)
        {
            var cacheItem = GetCacheItemByAddressOrSource(sirenName);
            bool acFailed = cacheItem?.HasAcLoss ?? false;
            bool apmFailed = cacheItem?.HasLowBattery ?? false;
            bool doorIntruded = cacheItem?.HasIntrusion ?? false;

            if (cacheItem != null)
            {
                if (cacheItem.LastKnownStatus == statusStr &&
                    cacheItem.LastSyncedAcFailed == acFailed &&
                    cacheItem.LastSyncedApmFailed == apmFailed &&
                    cacheItem.LastSyncedDoorIntruded == doorIntruded)
                {
                    return; // Skip DB write and event notification if nothing has changed
                }
                cacheItem.LastKnownStatus = statusStr;
                cacheItem.LastSyncedAcFailed = acFailed;
                cacheItem.LastSyncedApmFailed = apmFailed;
                cacheItem.LastSyncedDoorIntruded = doorIntruded;
            }

            try
            {
                if (statusStr != "OFFLINE")
                {
                    if (_activeOfflines.TryRemove(sirenName, out _))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Siren Online Restored", $"Siren Device '{sirenName}' is back online and responsive.", "Info");
                    }
                }
                else
                {
                    if (_activeOfflines.TryAdd(sirenName, true))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Siren Offline Alarm", $"Siren Device '{sirenName}' failed to respond on all channels and is offline.", "Danger");
                    }
                }

                // Sync status and telemetry alarms to database
                using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                {
                    await connection.OpenAsync();
                    string sql = "UPDATE SirenDevices SET Status = @Status, AcFailed = @AcFailed, ApmFailed = @ApmFailed, DoorIntruded = @DoorIntruded WHERE Name = @Name";
                    using var command = new MySqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@Status", statusStr);
                    command.Parameters.AddWithValue("@AcFailed", acFailed ? 1 : 0);
                    command.Parameters.AddWithValue("@ApmFailed", apmFailed ? 1 : 0);
                    command.Parameters.AddWithValue("@DoorIntruded", doorIntruded ? 1 : 0);
                    command.Parameters.AddWithValue("@Name", sirenName);
                    await command.ExecuteNonQueryAsync();
                }

                // Fire status changed event
                SirenStatusChanged?.Invoke(sirenName, statusStr);
            }
            catch (Exception ex)
            {
                Log($"❌ [DB Status Sync Error] {ex.Message}");
            }
        }

        private void TrackSuccess(string sirenName, byte cmdByte)
        {
            // Reset consecutive failure counter on any success
            _failureCounters.TryRemove(sirenName, out _);

            // Update cache state to online
            var cacheItem = GetCacheItemByAddressOrSource(sirenName);
            if (cacheItem != null)
            {
                cacheItem.IsOnline = true;
                cacheItem.LastUpdated = DateTime.Now;

                string computedStatus = GetComputedStatus(cacheItem);
                _ = SyncSirenStatusToDbAndNotifyAsync(sirenName, computedStatus);
            }

            // If it is an emergency command (like Wail 0x01, Attack 0x02, SI Test 0x03, Stop 0x1E)
            if (cmdByte == 0x01 || cmdByte == 0x02 || cmdByte == 0x03 || cmdByte == 0x05 || cmdByte == 0x06 || cmdByte == 0x07 || cmdByte == 0x08 || cmdByte == 0x1E)
            {
                string cmdName = cmdByte switch
                {
                    0x01 => "Wail Alert",
                    0x02 => "Attack Pulse",
                    0x03 => "SI Test Command",
                    0x1E => "Cancel/Stop Actions",
                    _ => $"Command (0x{cmdByte:X2})"
                };

                var ns = new NotificationService();
                _ = ns.AddNotificationAsync("Command Dispatched", $"Emergency command '{cmdName}' successfully dispatched to Siren '{sirenName}'.", "Info");
            }
        }

        private void TrackFailure(string sirenName)
        {
            // Increment consecutive failure counter
            int newCount = _failureCounters.AddOrUpdate(sirenName, 1, (_, old) => old + 1);
            Log($"⚠️ [Status] '{sirenName}' consecutive failure count: {newCount}/{OfflineThreshold}");

            // Only declare OFFLINE after OfflineThreshold consecutive failures
            if (newCount < OfflineThreshold)
            {
                Log($"⏳ [Status] '{sirenName}' not yet at threshold — waiting for more failures before declaring OFFLINE.");
                return;
            }

            // Update cache state to offline
            var cacheItem = GetCacheItemByAddressOrSource(sirenName);
            if (cacheItem != null)
            {
                cacheItem.IsOnline = false;
                cacheItem.LastUpdated = DateTime.Now;
            }

            _ = SyncSirenStatusToDbAndNotifyAsync(sirenName, "OFFLINE");
        }

        private void StartGlobalPolling()
        {
            // Populate the cache from the DB immediately upon service startup
            _ = Task.Run(async () =>
            {
                try
                {
                    using var connection = new MySqlConnection(AppConfig.ConnectionString);
                    await connection.OpenAsync();
                    string sql = "SELECT Name, Ip, Redundant, AreaCode, AddressCode, Status FROM SirenDevices";
                    using var command = new MySqlCommand(sql, connection);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string name = reader.GetString(0);
                        string ip = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        bool redundant = reader.GetBoolean(2);
                        string area = reader.IsDBNull(3) ? "000" : reader.GetString(3);
                        string addr = reader.IsDBNull(4) ? "0000" : reader.GetString(4);
                        string status = reader.IsDBNull(5) ? "OFFLINE" : reader.GetString(5);

                        var item = new SirenStatusCacheItem
                        {
                            Name = name,
                            Ip = ip,
                            AreaCode = area,
                            AddressCode = addr,
                            IsOnline = status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase),
                            LastKnownStatus = status.ToUpper(),
                            LastUpdated = DateTime.Now
                        };
                        _sirenCache[name] = item;
                    }
                    Log($"🗄️ Cache populated with {_sirenCache.Count} sirens from the database.");
                }
                catch (Exception ex)
                {
                    Log($"❌ [Cache Initialization Error] {ex.Message}");
                }
            });
            InitializePolling();
            StartStationHeartbeatLoop();
            if (AppConfig.StationName == "Admin ECC")
            {
                StartCommandQueueListener();
            }
        }

        private async Task<bool> PingIpAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "127.0.0.1") return false;
            try
            {
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    var reply = await ping.SendPingAsync(ipAddress, 1000);
                    return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> DetectMyIpAddressAsync(MySqlConnection connection)
        {
            try
            {
                using (var cmd = new MySqlCommand("SELECT SUBSTRING_INDEX(USER(), '@', -1)", connection))
                {
                    var dbIp = await cmd.ExecuteScalarAsync();
                    if (dbIp != null)
                    {
                        string ipStr = dbIp.ToString().Trim();
                        if (ipStr != "localhost" && ipStr != "127.0.0.1" && !string.IsNullOrEmpty(ipStr))
                        {
                            return ipStr;
                        }
                    }
                }
            }
            catch {}

            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        if (ipStr != "127.0.0.1" && !ipStr.StartsWith("169.254"))
                        {
                            return ipStr;
                        }
                    }
                }
            }
            catch {}

            return "127.0.0.1";
        }

        private void StartStationHeartbeatLoop()
        {
            Task.Run(async () =>
            {
                // Wait briefly for startup
                await Task.Delay(5000);

                while (true)
                {
                    try
                    {
                        using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                        {
                            await connection.OpenAsync();

                            // 1. Update our own workstation heartbeat and register dynamic local IP
                            string myIp = await DetectMyIpAddressAsync(connection);
                            string updateLocalSql = @"
                                UPDATE StationStatuses 
                                SET LastHeartbeat = @Now, Status = 'ONLINE', IpAddress = @Ip 
                                WHERE Name = @Name;";
                            using (var cmd = new MySqlCommand(updateLocalSql, connection))
                            {
                                cmd.Parameters.AddWithValue("@Now", DateTime.Now);
                                cmd.Parameters.AddWithValue("@Ip", myIp);
                                cmd.Parameters.AddWithValue("@Name", AppConfig.StationName);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // 2. Query all stations to check heartbeats of remote workstations & ping controllers
                            var stations = new List<(Guid Id, string Name, string Type, string IpAddress, DateTime? LastHeartbeat)>();
                            string selectSql = "SELECT Id, Name, Type, IpAddress, LastHeartbeat FROM StationStatuses";
                            using (var selectCmd = new MySqlCommand(selectSql, connection))
                            using (var reader = await selectCmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    stations.Add((
                                        reader.GetGuid(0),
                                        reader.GetString(1),
                                        reader.GetString(2),
                                        reader.GetString(3),
                                        reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                                    ));
                                }
                            }

                            foreach (var station in stations)
                            {
                                if (station.Name == AppConfig.StationName) continue;

                                string newStatus = "OFFLINE";

                                if (station.Type == "Workstation")
                                {
                                    // Check last heartbeat timestamp
                                    if (station.LastHeartbeat.HasValue && 
                                        (DateTime.Now - station.LastHeartbeat.Value).TotalSeconds <= 15)
                                    {
                                        newStatus = "ONLINE";
                                    }
                                }
                                else if (station.Type == "Controller")
                                {
                                    // Ping IP address
                                    bool isAlive = await PingIpAddressAsync(station.IpAddress);
                                    newStatus = isAlive ? "ONLINE" : "OFFLINE";
                                }

                                // Update status in database if changed
                                string updateStatusSql = @"
                                    UPDATE StationStatuses 
                                    SET Status = @Status 
                                    WHERE Id = @Id;";
                                using (var updateCmd = new MySqlCommand(updateStatusSql, connection))
                                {
                                    updateCmd.Parameters.AddWithValue("@Status", newStatus);
                                    updateCmd.Parameters.AddWithValue("@Id", station.Id);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ [Heartbeat Loop Error] {ex.Message}");
                    }

                    await Task.Delay(5000); // Run every 5 seconds
                }
            });
        }

        private byte[] BuildStatusFrame(string areaCode, string addressCode)
        {
            byte[] frame = new byte[15];
            frame[0] = 0x02;

            string area = areaCode.PadLeft(3, '0');
            frame[1] = (byte)(0x80 | (area[0] - '0'));
            frame[2] = (byte)(0x80 | (area[1] - '0'));
            frame[3] = (byte)(0x80 | (area[2] - '0'));

            string addr = addressCode.PadLeft(4, '0');
            frame[4] = (byte)(0x80 | (addr[0] - '0'));
            frame[5] = (byte)(0x80 | (addr[1] - '0'));
            frame[6] = (byte)(0x80 | (addr[2] - '0'));
            frame[7] = (byte)(0x80 | (addr[3] - '0'));

            frame[8] = 0x80;
            frame[9] = 0x80;
            frame[10] = 0xA3; // 0x23 | 0x80 (Status request)
            frame[11] = 0x03;

            byte xor = 0;
            for (int i = 0; i <= 11; i++) xor ^= frame[i];
            frame[12] = (byte)(0x80 | (xor >> 4));
            frame[13] = (byte)(0x80 | (xor & 0x0F));
            frame[14] = 0x0D;

            return frame;
        }

        private void InitializePolling()
        {
            Task.Run(async () =>
            {
                // Wait briefly for startup logs and serial configurations to load
                await Task.Delay(5000);

                while (true)
                {
                    if (AppConfig.StationName != "Admin ECC")
                    {
                        // Remote workstation: just load status from DB and update local cache
                        try
                        {
                            var list = new List<(string Name, string Status)>();
                            using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                            {
                                await connection.OpenAsync();
                                string sql = "SELECT Name, Status FROM SirenDevices";
                                using var command = new MySqlCommand(sql, connection);
                                using var reader = await command.ExecuteReaderAsync();
                                while (await reader.ReadAsync())
                                {
                                    list.Add((
                                        reader.GetString(0),
                                        reader.GetString(1)
                                    ));
                                }
                            }

                            foreach (var item in list)
                            {
                                var cacheItem = GetCacheItemByAddressOrSource(item.Name);
                                if (cacheItem != null)
                                {
                                    bool isOnline = item.Status == "ONLINE" || item.Status == "WARNING";
                                    cacheItem.IsOnline = isOnline;
                                    cacheItem.IsTcpOnline = isOnline;
                                    cacheItem.IsSerialOnline = isOnline;
                                    cacheItem.LastKnownStatus = item.Status;
                                    cacheItem.LastUpdated = DateTime.Now;

                                    // Raise local event to refresh UI
                                    SirenStatusChanged?.Invoke(item.Name, item.Status);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"❌ [Remote Poller Error] {ex.Message}");
                        }

                        await Task.Delay(10000);
                        continue;
                    }

                    if (DateTime.Now - _lastUserCommandTime < TimeSpan.FromSeconds(5))
                    {
                        Log("⏳ [Global Poller] User command in progress. Suspending background polling briefly...");
                        await Task.Delay(2000);
                        continue;
                    }

                    try
                    {
                        var sirens = new List<(string Name, string Ip, bool Redundant, string AreaCode, string AddressCode)>();
                        
                        using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                        {
                            await connection.OpenAsync();
                            string sql = "SELECT Name, Ip, Redundant, AreaCode, AddressCode FROM SirenDevices";
                            using var command = new MySqlCommand(sql, connection);
                            using var reader = await command.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                sirens.Add((
                                    reader.GetString(0),
                                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    reader.GetBoolean(2),
                                    reader.IsDBNull(3) ? "000" : reader.GetString(3),
                                    reader.IsDBNull(4) ? "0000" : reader.GetString(4)
                                ));
                            }
                        }

                        // Ensure all sirens from DB exist in cache
                        foreach (var s in sirens)
                        {
                            if (!_sirenCache.TryGetValue(s.Name, out var cacheItem))
                            {
                                _sirenCache[s.Name] = new SirenStatusCacheItem
                                {
                                    Name = s.Name,
                                    Ip = s.Ip,
                                    AreaCode = s.AreaCode,
                                    AddressCode = s.AddressCode,
                                    IsOnline = false,
                                    IsTcpOnline = false,
                                    IsSerialOnline = false,
                                    LastKnownStatus = "OFFLINE",
                                    LastUpdated = DateTime.Now
                                };
                            }
                        }

                        // 1. Poll TCP channels in parallel for all IP-enabled sirens
                        var tcpSirens = sirens.Where(s => !string.IsNullOrWhiteSpace(s.Ip)).ToList();
                        var tcpTasks = tcpSirens.Select(async s =>
                        {
                            try
                            {
                                var cacheItem = GetCacheItemByAddressOrSource(s.Name);
                                if (cacheItem != null)
                                {
                                    byte[] frame = BuildStatusFrame(s.AreaCode, s.AddressCode);
                                    bool tcpSuccess = await SendTcpCommandAsync(s.Ip, frame, true);
                                    cacheItem.IsTcpOnline = tcpSuccess;

                                    if (!s.Redundant)
                                    {
                                        cacheItem.IsSerialOnline = false;
                                        cacheItem.IsOnline = tcpSuccess;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"❌ [TCP Poller Error for {s.Name}] {ex.Message}");
                            }
                        });
                        var tcpPromise = Task.WhenAll(tcpTasks);

                        // 2. Poll Serial channels sequentially for Serial-only and Redundant sirens
                        var serialSirensList = sirens.Where(s => string.IsNullOrWhiteSpace(s.Ip) || s.Redundant).ToList();
                        foreach (var s in serialSirensList)
                        {
                            try
                            {
                                var cacheItem = GetCacheItemByAddressOrSource(s.Name);
                                if (cacheItem != null)
                                {
                                    byte[] frame = BuildStatusFrame(s.AreaCode, s.AddressCode);
                                    bool serialSuccess = await SendSerialCommandAsync(frame, true, false);
                                    cacheItem.IsSerialOnline = serialSuccess;

                                    if (string.IsNullOrWhiteSpace(s.Ip))
                                    {
                                        cacheItem.IsTcpOnline = false;
                                        cacheItem.IsOnline = serialSuccess;
                                    }
                                    else
                                    {
                                        // Redundant siren is overall online if either channel responds
                                        cacheItem.IsOnline = cacheItem.IsTcpOnline || serialSuccess;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"❌ [Serial Poller Error for {s.Name}] {ex.Message}");
                            }

                            // Enforce inter-device delay on serial port
                            int delayMs = serialSirensList.Count > 5 ? SerialPollInterDeviceDelayMs_Many : SerialPollInterDeviceDelayMs_Few;
                            await Task.Delay(delayMs);
                        }

                        await tcpPromise;

                        // 3. Compute overall status and sync to database for all sirens
                        foreach (var s in sirens)
                        {
                            var cacheItem = GetCacheItemByAddressOrSource(s.Name);
                            if (cacheItem != null)
                            {
                                cacheItem.LastUpdated = DateTime.Now;
                                string computedStatus = GetComputedStatus(cacheItem);
                                _ = SyncSirenStatusToDbAndNotifyAsync(s.Name, computedStatus);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ [Global Poller Error] {ex.Message}");
                    }

                    // Poll every 10 seconds
                    await Task.Delay(10000);
                }
            });
        }

        // ────────────────────────────────────────────────────────────────────
        // Response Decoding & Status Update
        // ────────────────────────────────────────────────────────────────────
        // Returns true if frame is valid and parsed successfully, false otherwise
        // Returns true if frame is valid, parsed successfully, and address matches the target
        private bool ProcessSirenResponse(string source, byte[] data, string channel, byte[]? sentFrame = null)
        {
            if (data.Length < 4) return false;

            // ── Step 1: Find the true STX (0x02) start marker ──────────────
            // Stale bytes from a previous frame may exist before the real frame start
            int stxIndex = -1;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x02) { stxIndex = i; break; }
            }

            if (stxIndex < 0)
            {
                Log($"❌ [Parser] INVALID frame from {source}: No STX (0x02) marker found — likely garbage/stale bytes.");
                return false;
            }

            if (stxIndex > 0)
            {
                string staleHex = string.Join(" ", data.Take(stxIndex).Select(b => b.ToString("X2")));
                Log($"⚠️ [Parser] Discarded {stxIndex} stale byte(s) before STX: {staleHex}");
            }

            // ── Step 2: Verify CR (0x0D) end marker ────────────────────────
            if (data[data.Length - 1] != 0x0D)
            {
                Log($"❌ [Parser] INVALID frame from {source}: Missing CR (0x0D) end marker — frame may be truncated.");
                return false;
            }

            // ── Step 3: Verify ETX (0x03) exists ─────────────────────────────
            int etxIndex = -1;
            for (int i = stxIndex; i < data.Length; i++)
            {
                if (data[i] == 0x03) { etxIndex = i; break; }
            }

            if (etxIndex < 0)
            {
                Log($"❌ [Parser] INVALID frame from {source}: Missing ETX (0x03) marker.");
                return false;
            }

            // ── Step 4: Extract the clean frame ─────────────────────────────
            byte[] frame = data.Skip(stxIndex).ToArray();

            if (frame.Length < 11)
            {
                Log($"❌ [Parser] INVALID frame from {source}: Frame too short ({frame.Length} bytes).");
                return false;
            }

            // ── Step 4: Validate expected address digits ────────────────
            // In C2030 protocol, bytes 1-3 are Area Code digits, and bytes 4-7 are Address Code digits.
            // Check matches for both Serial and TCP channels to prevent cross-talk.
            byte[]? expectedFrame = sentFrame;

            if (channel == "TCP/IP" && _pendingTcpAcks.TryGetValue(source, out var pending))
            {
                expectedFrame = pending.SentFrame;
            }

            if (expectedFrame != null && expectedFrame.Length >= 8)
            {
                bool addressMismatch = false;
                for (int i = 1; i <= 7; i++)
                {
                    if (frame[i] != expectedFrame[i])
                    {
                        addressMismatch = true;
                        break;
                    }
                }

                if (addressMismatch)
                {
                    string expectedAddr = string.Join(" ", expectedFrame.Skip(1).Take(7).Select(b => b.ToString("X2")));
                    string receivedAddr = string.Join(" ", frame.Skip(1).Take(7).Select(b => b.ToString("X2")));
                    Log($"⚠️ [Parser] Address MISMATCH from {source} via {channel}. Expected: {expectedAddr}, Received: {receivedAddr}. Discarding frame.");
                    return false;
                }
            }

            byte responseAddress = frame[1];
            Log($"🔍 [Parser] Response address byte: 0x{responseAddress:X2}");

            // ── Step 5: Decode status based on expected command ─────────────
            byte cmdByte = 0;
            if (expectedFrame != null && expectedFrame.Length > 10)
            {
                cmdByte = (byte)(expectedFrame[10] & 0x7F);
            }

            string rcvAddress = "";
            if (frame.Length >= 8)
            {
                rcvAddress = $"{(frame[4] & 0x0F)}{(frame[5] & 0x0F)}{(frame[6] & 0x0F)}{(frame[7] & 0x0F)}";
            }

            var cacheItem = GetCacheItemByAddressOrSource(rcvAddress);

            Log($"✨ [Parser] Valid frame confirmed from {source} via {channel} (addr={rcvAddress}, {frame.Length} bytes, for Cmd={cmdByte:X2}H)");

            if (cmdByte == 0x23) // Instant Status Response
            {
                byte statusByte = frame.Length > 12 ? ReconstructByte(frame[11], frame[12]) : (byte)0;
                byte dcVolts = frame.Length > 14 ? ReconstructByte(frame[13], frame[14]) : (byte)0;
                byte cabTemp = (byte)0;
                byte outTemp = (byte)0;

                Log($"📊 [Parser] 23H Instant Status -> StatusByte: 0x{statusByte:X2}, DC Raw: {dcVolts}, CabTemp Raw: {cabTemp}, OutTemp Raw: {outTemp}");
                
                bool intrusion = (statusByte & 0x02) != 0;
                bool acOn = (statusByte & 0x01) != 0;
                bool strobeError = (statusByte & 0x04) != 0;
                bool supervisorError = (statusByte & 0x08) != 0;
                bool fullAlertPass = (statusByte & 0x20) != 0;
                bool partialAlertPass = (statusByte & 0x40) != 0;

                double dcVoltsVal = dcVolts * (35.0 / 255.0);
                bool lowBattery = dcVoltsVal < 22.0 && dcVoltsVal > 0;

                if (cacheItem != null)
                {
                    cacheItem.IsOnline = true;
                    cacheItem.HasIntrusion = intrusion;
                    cacheItem.HasAcLoss = !acOn;
                    cacheItem.HasLowBattery = lowBattery;
                    cacheItem.HasStrobeError = strobeError;
                    cacheItem.HasSupervisorError = supervisorError;
                    cacheItem.HasFullAlertFailure = !fullAlertPass;
                    cacheItem.HasPartialAlertFailure = !partialAlertPass;
                    cacheItem.DcVoltage = dcVoltsVal;
                    cacheItem.LastUpdated = DateTime.Now;

                    // Direct UI telemetry fields
                    cacheItem.StatusByte = statusByte;
                    cacheItem.Intrusion = intrusion;
                    cacheItem.AcOn = acOn;
                    cacheItem.StrobeActive = strobeError;
                    cacheItem.SupervisorMode = supervisorError;
                    cacheItem.FullAlert = fullAlertPass;
                    cacheItem.PartialAlert = partialAlertPass;
                    cacheItem.BiasDetected = (statusByte & 0x80) == 0;
                    cacheItem.AcVoltage = acOn ? 220.0 : 0.0;
                    cacheItem.DynamicAc = acOn;
                    cacheItem.SystemPowerUp = acOn || dcVoltsVal >= 22.0;
                }

                // Handle Intrusion Alert transition
                if (intrusion)
                {
                    if (_activeIntrusions.TryAdd(rcvAddress, true))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Cabinet Intrusion Alert", $"Cabinet door opened/tampered at Siren Address {rcvAddress}.", "Danger");
                    }
                }
                else
                {
                    if (_activeIntrusions.TryRemove(rcvAddress, out _))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Intrusion Alarm Cleared", $"Cabinet door closed/secured at Siren Address {rcvAddress}.", "Info");
                    }
                }

                // Handle AC Power transition
                if (!acOn)
                {
                    if (_activeAcLosses.TryAdd(rcvAddress, true))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("AC Power Loss Warning", $"Siren Address {rcvAddress} has lost main AC Power and is running on backup battery.", "Warning");
                    }
                }
                else
                {
                    if (_activeAcLosses.TryRemove(rcvAddress, out _))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("AC Power Restored", $"Main AC Power supply has been restored at Siren Address {rcvAddress}.", "Info");
                    }
                }

                InstantStatusReceived?.Invoke(rcvAddress, statusByte, dcVolts, cabTemp, outTemp);
            }
            else if (cmdByte == 0x3F) // Active Status Response
            {
                byte activeCmd = frame.Length > 12 ? ReconstructByte(frame[11], frame[12]) : (byte)0;
                byte acVolts = frame.Length > 14 ? ReconstructByte(frame[13], frame[14]) : (byte)0;
                byte dcVolts = frame.Length > 16 ? ReconstructByte(frame[15], frame[16]) : (byte)0;
                byte activeStatus = frame.Length > 18 ? ReconstructByte(frame[17], frame[18]) : (byte)0;
                byte cabTemp = (byte)0;
                byte outTemp = (byte)0;
                
                Log($"📊 [Parser] 3FH Active Status -> Cmd: {activeCmd:X2}, AC: {acVolts}, DC: {dcVolts}, Status: 0x{activeStatus:X2}");
                
                double dcVoltsVal = dcVolts * (35.0 / 255.0);
                bool lowBattery = dcVoltsVal < 22.0 && dcVoltsVal > 0;
                bool intrusion = (activeStatus & 0x08) != 0;
                bool strobeError = (activeStatus & 0x10) != 0;
                bool supervisorError = (activeStatus & 0x40) != 0;
                bool fullAlertPass = (activeStatus & 0x01) != 0;
                bool partialAlertPass = (activeStatus & 0x02) != 0;

                if (cacheItem != null)
                {
                    cacheItem.IsOnline = true;
                    cacheItem.HasIntrusion = intrusion;
                    cacheItem.HasLowBattery = lowBattery;
                    cacheItem.HasStrobeError = strobeError;
                    cacheItem.HasSupervisorError = supervisorError;
                    cacheItem.HasFullAlertFailure = !fullAlertPass;
                    cacheItem.HasPartialAlertFailure = !partialAlertPass;
                    cacheItem.DcVoltage = dcVoltsVal;
                    cacheItem.LastUpdated = DateTime.Now;

                    // Direct UI telemetry fields
                    cacheItem.AcVoltage = acVolts;
                    cacheItem.DynamicAc = acVolts > 0;
                    cacheItem.SystemPowerUp = (acVolts > 0) || dcVoltsVal >= 22.0;
                    cacheItem.FullAlert = fullAlertPass;
                    cacheItem.PartialAlert = partialAlertPass;
                    cacheItem.BiasDetected = (activeStatus & 0x04) != 0;
                    cacheItem.Intrusion = intrusion;
                    cacheItem.StrobeActive = strobeError;
                    cacheItem.SupervisorMode = supervisorError;
                }

                // Handle Intrusion state in Active Status
                if (intrusion)
                {
                    if (_activeIntrusions.TryAdd(rcvAddress, true))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Cabinet Intrusion Alert", $"Cabinet door opened/tampered at Siren Address {rcvAddress}.", "Danger");
                    }
                }
                else
                {
                    if (_activeIntrusions.TryRemove(rcvAddress, out _))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Intrusion Alarm Cleared", $"Cabinet door closed/secured at Siren Address {rcvAddress}.", "Info");
                    }
                }

                // Handle Low Battery transition
                if (lowBattery)
                {
                    if (_activeLowBattery.TryAdd(rcvAddress, true))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Low Battery Warning", $"Siren Address {rcvAddress} backup battery voltage is low ({dcVoltsVal:F1}V).", "Warning");
                    }
                }
                else if (dcVoltsVal >= 23.0)
                {
                    if (_activeLowBattery.TryRemove(rcvAddress, out _))
                    {
                        var ns = new NotificationService();
                        _ = ns.AddNotificationAsync("Battery Voltage Restored", $"Siren Address {rcvAddress} battery voltage is back to normal ({dcVoltsVal:F1}V).", "Info");
                    }
                }

                ActiveStatusReceived?.Invoke(rcvAddress, activeCmd, acVolts, dcVolts, activeStatus, cabTemp, outTemp);
            }
            else if (cmdByte == 0x1F) // Standard Status Response
            {
                byte statusByte = frame.Length > 12 ? ReconstructByte(frame[11], frame[12]) : (byte)0;
                byte dcVolts = frame.Length > 14 ? ReconstructByte(frame[13], frame[14]) : (byte)0;
                byte cabTemp = (byte)0;
                byte outTemp = (byte)0;

                Log($"📊 [Parser] 1FH Standard Status Byte: 0x{statusByte:X2}, DC Raw: {dcVolts}");

                double dcVoltsVal = dcVolts * (35.0 / 255.0);
                bool lowBattery = dcVoltsVal < 22.0 && dcVoltsVal > 0;
                bool fullAlertPass = (statusByte & 0x01) != 0;
                bool partialAlertPass = (statusByte & 0x02) != 0;
                bool rotorOk = (statusByte & 0x04) != 0;
                bool acOn = (statusByte & 0x80) != 0;

                if (cacheItem != null)
                {
                    cacheItem.IsOnline = true;
                    cacheItem.HasLowBattery = lowBattery;
                    cacheItem.HasFullAlertFailure = !fullAlertPass;
                    cacheItem.HasPartialAlertFailure = !partialAlertPass;
                    cacheItem.HasRotorFailure = !rotorOk;
                    cacheItem.HasAcLoss = !acOn;
                    cacheItem.DcVoltage = dcVoltsVal;
                    cacheItem.LastUpdated = DateTime.Now;

                    // Direct UI telemetry fields
                    cacheItem.SirenOn = (statusByte & 0x10) != 0;
                    cacheItem.SystemArmed = (statusByte & 0x20) != 0;
                    cacheItem.FullAlert = fullAlertPass;
                    cacheItem.PartialAlert = partialAlertPass;
                    cacheItem.RotorActive = rotorOk;
                    cacheItem.StoredAc = (statusByte & 0x08) != 0;
                    cacheItem.AcOn = acOn;
                    cacheItem.DynamicAc = acOn;
                    cacheItem.SystemPowerUp = (statusByte & 0x40) != 0 || acOn || dcVoltsVal >= 22.0;
                }

                StandardStatusReceived?.Invoke(rcvAddress, statusByte, dcVolts, cabTemp, outTemp);
            }
            else if (cmdByte == 0x21) // Battery/AC Response
            {
                byte dcVolts = frame.Length > 12 ? ReconstructByte(frame[11], frame[12]) : (byte)0;
                byte acVolts = frame.Length > 14 ? ReconstructByte(frame[13], frame[14]) : (byte)0;

                Log($"📊 [Parser] 21H Battery/AC -> DC Raw: {dcVolts}, AC Raw: {acVolts}");

                if (cacheItem != null)
                {
                    cacheItem.IsOnline = true;
                    cacheItem.DcVoltage = dcVolts * (35.0 / 255.0);
                    cacheItem.HasAcLoss = (acVolts < 10);
                    cacheItem.LastUpdated = DateTime.Now;

                    // Direct UI telemetry fields
                    cacheItem.AcOn = (acVolts >= 10);
                    if (cacheItem.AcOn)
                    {
                        int strippedAc = acVolts & 0x7F;
                        cacheItem.AcVoltage = strippedAc > 0 ? (double)strippedAc : 220.0;
                    }
                    else
                    {
                        cacheItem.AcVoltage = 0.0;
                    }
                    cacheItem.DynamicAc = cacheItem.AcVoltage > 0;
                    cacheItem.SystemPowerUp = cacheItem.AcOn || cacheItem.DcVoltage >= 22.0;
                }

                BatteryAcReceived?.Invoke(rcvAddress, dcVolts, acVolts);
                BatteryTempReceived?.Invoke(rcvAddress, dcVolts, 0);
            }
            else if (cmdByte == 0x22) // Battery/Temperature Response
            {
                byte dcVolts = frame.Length > 12 ? ReconstructByte(frame[11], frame[12]) : (byte)0;
                byte cabTemp = frame.Length > 14 ? ReconstructByte(frame[13], frame[14]) : (byte)0;

                Log($"📊 [Parser] 22H Battery/Temp -> DC Raw: {dcVolts}, Cabinet Temp Raw: {cabTemp}");

                if (cacheItem != null)
                {
                    cacheItem.IsOnline = true;
                    cacheItem.DcVoltage = dcVolts * (35.0 / 255.0);
                    cacheItem.LastUpdated = DateTime.Now;

                    // Direct UI telemetry fields
                    if (cabTemp > 100)
                    {
                        cacheItem.CabTemp = (double)(cabTemp - 100);
                    }
                    else
                    {
                        cacheItem.CabTemp = 0.0;
                    }
                    cacheItem.SystemPowerUp = cacheItem.AcOn || cacheItem.DcVoltage >= 22.0;
                }

                BatteryTempReceived?.Invoke(rcvAddress, dcVolts, cabTemp);
            }
            else if (cmdByte == 0x2A) // Weather Response
            {
                byte outTemp = frame.Length > 12 ? ReconstructByte(frame[11], frame[12]) : (byte)0;
                byte windDir = frame.Length > 14 ? ReconstructByte(frame[13], frame[14]) : (byte)0;
                byte windSpd = frame.Length > 16 ? ReconstructByte(frame[15], frame[16]) : (byte)0;
                byte rain = frame.Length > 18 ? ReconstructByte(frame[17], frame[18]) : (byte)0;
                
                Log($"📊 [Parser] 2AH Weather -> OutTemp: {outTemp}, WindDir: {windDir}, WindSpd: {windSpd}, Rain: {rain}");

                if (cacheItem != null)
                {
                    cacheItem.IsOnline = true;
                    cacheItem.OutTemp = outTemp;
                    cacheItem.LastUpdated = DateTime.Now;
                }

                WeatherReceived?.Invoke(rcvAddress, outTemp, windDir, windSpd, rain);
            }
            else if (cmdByte == 0x2B) // Comprehensive Temp Response
            {
                byte cabTemp = frame.Length > 12 ? ReconstructByte(frame[11], frame[12]) : (byte)0;
                byte outTemp = frame.Length > 14 ? ReconstructByte(frame[13], frame[14]) : (byte)0;
                byte lowPeak = frame.Length > 16 ? ReconstructByte(frame[15], frame[16]) : (byte)0;
                byte highPeak = frame.Length > 18 ? ReconstructByte(frame[17], frame[18]) : (byte)0;
                
                Log($"📊 [Parser] 2BH Temp -> CabTemp: {cabTemp}, OutTemp: {outTemp}, LowPeak: {lowPeak}, HighPeak: {highPeak}");

                if (cacheItem != null)
                {
                    cacheItem.IsOnline = true;
                    cacheItem.CabTemp = cabTemp;
                    cacheItem.OutTemp = outTemp;
                    cacheItem.LastUpdated = DateTime.Now;
                }

                ComprehensiveTempReceived?.Invoke(rcvAddress, cabTemp, outTemp, lowPeak, highPeak);
            }
            else
            {
                // Fallback for generic commands
                byte statusByte = frame.Length > 10 ? frame[10] : (byte)0;
                Log($"📊 [Parser] Generic Status Byte for Cmd {cmdByte:X2}H: 0x{statusByte:X2}");
            }

            // ── Step 6: Resolve pending TCP ACK wait for this IP ─────────────
            if (channel == "TCP/IP" && _pendingTcpAcks.TryGetValue(source, out var pendingTcs))
            {
                pendingTcs.Tcs.TrySetResult(true);
            }

            if (channel == "Serial" || source == "SerialPort")
            {
                _hasSuccessfulSerialComm = true;
            }

            if (cacheItem != null)
            {
                string computedStatus = GetComputedStatus(cacheItem);
                _ = SyncSirenStatusToDbAndNotifyAsync(cacheItem.Name, computedStatus);
            }

            return true;
        }

        private static byte ReconstructByte(byte highByte, byte lowByte)
        {
            return (byte)(((highByte & 0x0F) << 4) | (lowByte & 0x0F));
        }

        public List<SirenStatusCacheItem> GetCachedStatuses()
        {
            return _sirenCache.Values.ToList();
        }

        public SirenStatusCacheItem? GetCacheItemByAddressOrSource(string addressOrSource)
        {
            // First try direct name lookup
            if (_sirenCache.TryGetValue(addressOrSource, out var item))
                return item;

            // Try key matching by parsed AddressCode or source IP or combined Area+Address
            foreach (var val in _sirenCache.Values)
            {
                string combinedAddr = $"{(val.AreaCode ?? "").Trim()}{(val.AddressCode ?? "").Trim()}";
                if (val.Ip == addressOrSource || 
                    (val.AddressCode ?? "").PadLeft(4, '0') == addressOrSource.PadLeft(4, '0') ||
                    val.Name == addressOrSource ||
                    combinedAddr == addressOrSource)
                {
                    return val;
                }
            }
            return null;
        }

        private void StartCommandQueueListener()
        {
            Task.Run(async () =>
            {
                // Wait briefly for startup
                await Task.Delay(5000);

                while (true)
                {
                    try
                    {
                        var pendingList = new List<(string Id, string Target, string Ip, bool Redundant, string FrameHex, bool Track, bool User)>();
                        using (var connection = new MySqlConnection(AppConfig.ConnectionString))
                        {
                            await connection.OpenAsync();
                            string sql = "SELECT Id, TargetSirenName, IpAddress, Redundant, FrameHex, TrackStatus, IsUserInitiated FROM PendingCommands WHERE Status = 'PENDING' ORDER BY Created";
                            using (var cmd = new MySqlCommand(sql, connection))
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    pendingList.Add((
                                        reader.GetGuid(0).ToString(),
                                        reader.GetString(1),
                                        reader.GetString(2),
                                        reader.GetBoolean(3),
                                        reader.GetString(4),
                                        reader.GetBoolean(5),
                                        reader.GetBoolean(6)
                                    ));
                                }
                            }

                            foreach (var item in pendingList)
                            {
                                Log($"📥 [Queue Listener] Processing relayed command: {item.Target} (ID: {item.Id})");

                                bool success = false;

                                if (item.Target == "__RAW_SERIAL__")
                                {
                                    byte[] frame = ConvertHexStringToByteArray(item.FrameHex);
                                    success = await SendSerialCommandAsync(frame, item.Track, item.User);
                                }
                                else if (item.Target == "__WILDCARD_CLEAR__")
                                {
                                    success = await SendWildcardClearAsync();
                                }
                                else
                                {
                                    byte[] frame = ConvertHexStringToByteArray(item.FrameHex);
                                    success = await ExecuteTransmitAsync(item.Target, item.Ip, item.Redundant, frame, item.Track, item.User);
                                }

                                string updateSql = "UPDATE PendingCommands SET Status = @Status WHERE Id = @Id";
                                using (var updateCmd = new MySqlCommand(updateSql, connection))
                                {
                                    updateCmd.Parameters.AddWithValue("@Status", success ? "COMPLETED" : "FAILED");
                                    updateCmd.Parameters.AddWithValue("@Id", item.Id);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ [Queue Listener Error] {ex.Message}");
                    }

                    await Task.Delay(1000);
                }
            });
        }

        private byte[] ConvertHexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }

    public class SirenStatusCacheItem
    {
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string AreaCode { get; set; } = "000";
        public string AddressCode { get; set; } = "0000";
        public bool IsOnline { get; set; } = false;
        public bool IsTcpOnline { get; set; } = false;
        public bool IsSerialOnline { get; set; } = false;
        public string LastKnownStatus { get; set; } = string.Empty;
        public bool LastSyncedAcFailed { get; set; } = false;
        public bool LastSyncedApmFailed { get; set; } = false;
        public bool LastSyncedDoorIntruded { get; set; } = false;
        public bool HasIntrusion { get; set; } = false;
        public bool HasAcLoss { get; set; } = false;
        public bool HasLowBattery { get; set; } = false;
        public bool HasStrobeError { get; set; } = false;
        public bool HasSupervisorError { get; set; } = false;
        public bool HasRotorFailure { get; set; } = false;
        public bool HasFullAlertFailure { get; set; } = false;
        public bool HasPartialAlertFailure { get; set; } = false;
        public double DcVoltage { get; set; } = 0.0;
        public double CabTemp { get; set; } = 0.0;
        public double OutTemp { get; set; } = 0.0;
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;

        // Direct UI telemetry fields
        public bool SirenOn { get; set; } = false;
        public bool AcOn { get; set; } = false;
        public bool DynamicAc { get; set; } = false;
        public bool PartialAlert { get; set; } = false;
        public bool StrobeActive { get; set; } = false;
        public bool SystemArmed { get; set; } = false;
        public bool SupervisorMode { get; set; } = false;
        public bool RotorActive { get; set; } = false;
        public bool StoredAc { get; set; } = false;
        public bool FullAlert { get; set; } = false;
        public bool Intrusion { get; set; } = false;
        public bool BiasDetected { get; set; } = false;
        public bool SystemPowerUp { get; set; } = false;
        public double AcVoltage { get; set; } = 0.0;
        public int? StatusByte { get; set; }

        public bool HasAlarm => HasIntrusion || HasAcLoss || HasLowBattery || HasStrobeError || 
                                HasSupervisorError || HasRotorFailure || HasFullAlertFailure || HasPartialAlertFailure;
    }
}