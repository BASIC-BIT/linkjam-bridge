using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LinkJam.Companion.Services
{
    public class CarabinerClient : IDisposable
    {
        private Process? _carabinerProcess;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly string _host = "127.0.0.1";
        private readonly int _port = 17000;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed = false;

        public event EventHandler<double>? TempoChanged;
        public event EventHandler<int>? PeersChanged;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public bool IsConnected => _tcpClient?.Connected ?? false;

        public async Task StartAsync()
        {
            // Try to connect to an existing Carabiner instance
            try
            {
                await ConnectAsync();
                return;
            }
            catch
            {
                // No existing instance, start a new one
            }

            if (!IsCarabinerRunning())
            {
                StartCarabinerProcess();
            }

            // Try to connect with retries
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await ConnectAsync();
                    return; // Success
                }
                catch (Exception ex)
                {
                    if (i == maxRetries - 1)
                    {
                        throw new Exception(
                            $"Failed to connect to Carabiner.\n" +
                            "Check if Windows Firewall is blocking port 17000.\n" +
                            $"Error: {ex.Message}", ex);
                    }
                    await Task.Delay(1000);
                }
            }
        }

        private bool IsCarabinerRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("carabiner");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void StartCarabinerProcess()
        {
            try
            {
                var carabinerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ThirdParty", "carabiner.exe");
                
                if (!File.Exists(carabinerPath))
                {
                    // Create ThirdParty directory if it doesn't exist
                    var thirdPartyDir = Path.GetDirectoryName(carabinerPath);
                    if (thirdPartyDir != null && !Directory.Exists(thirdPartyDir))
                    {
                        Directory.CreateDirectory(thirdPartyDir);
                    }
                    
                    throw new FileNotFoundException(
                        $"Carabiner executable not found at: {carabinerPath}\n\n" +
                        "Please download Carabiner from:\n" +
                        "https://github.com/Deep-Symmetry/carabiner/releases\n\n" +
                        "1. Download the Windows release (carabiner-windows.zip)\n" +
                        "2. Extract carabiner.exe\n" +
                        "3. Place it in the ThirdParty folder:\n" +
                        $"   {thirdPartyDir}");
                }

                _carabinerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = carabinerPath,
                        Arguments = $"--port {_port}",
                        UseShellExecute = false,
                        CreateNoWindow = true,  // Hide window but capture output
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                _carabinerProcess.ErrorDataReceived += (s, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"Carabiner ERROR: {e.Data}");
                };

                _carabinerProcess.Start();
                _carabinerProcess.BeginErrorReadLine();
                
                // Check if process exited immediately
                System.Threading.Thread.Sleep(500);
                if (_carabinerProcess.HasExited)
                {
                    throw new Exception($"Carabiner exited immediately with code {_carabinerProcess.ExitCode}. " +
                        "Missing Visual C++ Runtime? Download: https://aka.ms/vs/17/release/vc_redist.x64.exe");
                }
                
                System.Threading.Thread.Sleep(1500);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start Carabiner: {ex.Message}", ex);
            }
        }

        private async Task ConnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_host, _port);
                _stream = _tcpClient.GetStream();
                // Use ASCII encoding to avoid BOM issues with Carabiner
                _reader = new StreamReader(_stream, Encoding.ASCII);
                _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

                ConnectionStatusChanged?.Invoke(this, true);

                _ = Task.Run(ReadResponsesAsync);
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, false);
                throw new Exception($"Failed to connect to Carabiner: {ex.Message}", ex);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task ReadResponsesAsync()
        {
            try
            {
                while (!_disposed && _reader != null && _tcpClient?.Connected == true)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line != null)
                    {
                        ProcessResponse(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from Carabiner: {ex.Message}");
            }
        }

        private void ProcessResponse(string response)
        {
            try
            {
                var json = JObject.Parse(response);
                
                if (json["bpm"] != null)
                {
                    var bpm = json["bpm"]!.Value<double>();
                    TempoChanged?.Invoke(this, bpm);
                }
                
                if (json["peers"] != null)
                {
                    var peers = json["peers"]!.Value<int>();
                    PeersChanged?.Invoke(this, peers);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing Carabiner response: {ex.Message}");
            }
        }

        public async Task<double> GetTempoAsync()
        {
            // Request status from Carabiner
            await SendCommandAsync("status");
            await Task.Delay(100);
            // TODO: Parse the response to get actual BPM
            return 174.0;
        }

        public async Task SetTempoAsync(double bpm)
        {
            // Carabiner expects: bpm <float>
            await SendCommandAsync($"bpm {bpm}");
        }

        public async Task SetBeatTimeAsync(double beats, long whenMs)
        {
            var whenMicros = whenMs * 1000;
            // Carabiner expects: beat-at-time <beat> <when> [quantum]
            await SendCommandAsync($"beat-at-time {beats} {whenMicros}");
        }

        public async Task ForceDownbeatAsync(long whenMs)
        {
            var whenMicros = whenMs * 1000;
            // Carabiner expects: force-beat-at-time <beat> <when> [quantum]
            await SendCommandAsync($"force-beat-at-time 0 {whenMicros} 4");
        }

        public async Task EnableStartStopSyncAsync(bool enable)
        {
            // Carabiner expects: enable-start-stop-sync or disable-start-stop-sync
            var command = enable ? "enable-start-stop-sync" : "disable-start-stop-sync";
            await SendCommandAsync(command);
        }

        private async Task SendCommandAsync(string command)
        {
            if (_writer == null || !IsConnected)
            {
                throw new InvalidOperationException("Not connected to Carabiner");
            }

            await _writer.WriteLineAsync(command);
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            try
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _stream?.Dispose();
                _tcpClient?.Dispose();
                
                if (_carabinerProcess != null && !_carabinerProcess.HasExited)
                {
                    _carabinerProcess.Kill();
                    _carabinerProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during CarabinerClient disposal: {ex.Message}");
            }
        }
    }
}