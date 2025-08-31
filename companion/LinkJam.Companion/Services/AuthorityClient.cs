using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LinkJam.Companion.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LinkJam.Companion.Services
{
    public class AuthorityClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private bool _disposed = false;
        private string _roomId = "main";
        private string _djName = "DJ";
        private string _serverUrl = "";
        private long _clockOffset = 0;
        private readonly List<long> _offsetSamples = new();
        private readonly object _offsetLock = new();

        public event EventHandler<TempoState>? TempoStateReceived;
        public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public long ClockOffset => _clockOffset;
        public string RoomId => _roomId;
        public string DjName => _djName;

        public async Task ConnectAsync(string serverUrl, string roomId, string djName)
        {
            if (IsConnected)
            {
                await DisconnectAsync();
            }

            _serverUrl = serverUrl;
            _roomId = roomId;
            _djName = djName;

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _webSocket = new ClientWebSocket();
                
                var wsUrl = $"{_serverUrl.Replace("http://", "ws://").Replace("https://", "wss://")}/ws/{roomId}";
                ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Connecting);
                
                await _webSocket.ConnectAsync(new Uri(wsUrl), _cancellationTokenSource.Token);
                ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Connected);

                Console.WriteLine($"Connected to Authority at {wsUrl}");

                _ = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token));
                
                await PerformTimeSyncAsync();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Disconnected);
                throw;
            }
        }

        private async Task PerformTimeSyncAsync()
        {
            const int syncCount = 8;
            _offsetSamples.Clear();

            for (int i = 0; i < syncCount; i++)
            {
                try
                {
                    var t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var ping = new WebSocketMessage
                    {
                        Type = "time_sync_ping",
                        Payload = new TimeSyncPing { T0Client = t0 }
                    };

                    var tcs = new TaskCompletionSource<TimeSyncPong>();
                    
                    var timeSyncHandler = new EventHandler<string>((sender, json) =>
                    {
                        try
                        {
                            var msg = JsonConvert.DeserializeObject<WebSocketMessage>(json);
                            if (msg?.Type == "time_sync_pong" && msg.Payload != null)
                            {
                                var pong = JObject.FromObject(msg.Payload).ToObject<TimeSyncPong>();
                                if (pong != null && pong.T0Client == t0)
                                {
                                    tcs.TrySetResult(pong);
                                }
                            }
                        }
                        catch { }
                    });

                    TimeSyncPongReceived += timeSyncHandler;
                    
                    await SendMessageAsync(ping);
                    
                    var pongTask = tcs.Task;
                    var timeoutTask = Task.Delay(2000);
                    
                    if (await Task.WhenAny(pongTask, timeoutTask) == pongTask)
                    {
                        var pong = await pongTask;
                        var t3 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var rtt = t3 - t0;
                        var offset = pong.T1Server - (t0 + rtt / 2);
                        
                        lock (_offsetLock)
                        {
                            _offsetSamples.Add(offset);
                        }
                    }
                    
                    TimeSyncPongReceived -= timeSyncHandler;
                    
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Time sync sample {i} failed: {ex.Message}");
                }
            }

            lock (_offsetLock)
            {
                if (_offsetSamples.Count > 0)
                {
                    var sortedOffsets = _offsetSamples.OrderBy(x => x).ToList();
                    _clockOffset = sortedOffsets[sortedOffsets.Count / 2];
                    Console.WriteLine($"Time sync complete. Clock offset: {_clockOffset}ms");
                }
            }
        }

        private event EventHandler<string>? TimeSyncPongReceived;

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            var messageBuilder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer.Array!, 0, result.Count));
                        
                        if (result.EndOfMessage)
                        {
                            var json = messageBuilder.ToString();
                            messageBuilder.Clear();
                            ProcessMessage(json);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken);
                        ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Disconnected);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when cancellation is requested - don't log as error
                Console.WriteLine("WebSocket receive loop cancelled");
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Connection was closed, this is expected during disconnect
                Console.WriteLine("WebSocket connection closed");
            }
            catch (Exception ex)
            {
                // Only report as error if we're not in the process of disconnecting
                if (!cancellationToken.IsCancellationRequested)
                {
                    ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
                    ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Disconnected);
                }
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<WebSocketMessage>(json);
                if (message == null) return;

                switch (message.Type)
                {
                    case "tempo_state":
                        if (message.Payload != null)
                        {
                            var tempoState = JObject.FromObject(message.Payload).ToObject<TempoState>();
                            if (tempoState != null)
                            {
                                TempoStateReceived?.Invoke(this, tempoState);
                            }
                        }
                        break;
                        
                    case "time_sync_pong":
                        TimeSyncPongReceived?.Invoke(this, json);
                        break;
                        
                    case "error":
                        var error = message.Payload?.ToString() ?? "Unknown error";
                        ErrorOccurred?.Invoke(this, error);
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Message processing error: {ex.Message}");
            }
        }

        public async Task SendTempoProposalAsync(double bpm)
        {
            var proposal = new TempoProposal
            {
                RoomId = _roomId,
                Bpm = bpm,
                ProposedBy = _djName,
                ClientMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var message = new WebSocketMessage
            {
                Type = "tempo_proposal",
                Payload = proposal
            };

            await SendMessageAsync(message);
        }

        private async Task SendMessageAsync(object message)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to Authority");
            }

            await _sendLock.WaitAsync();
            try
            {
                var json = JsonConvert.SerializeObject(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket!.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                // Cancel the receive loop first
                _cancellationTokenSource?.Cancel();
                
                // Then close the WebSocket if it's still open
                if (_webSocket?.State == WebSocketState.Open || _webSocket?.State == WebSocketState.CloseReceived)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - we're disconnecting anyway
                Console.WriteLine($"Warning during disconnect: {ex.Message}");
            }
            finally
            {
                ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Disconnected);
            }
        }

        public long GetServerTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _clockOffset;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            try
            {
                DisconnectAsync().Wait(TimeSpan.FromSeconds(5));
                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
                _sendLock?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during AuthorityClient disposal: {ex.Message}");
            }
        }
    }
}