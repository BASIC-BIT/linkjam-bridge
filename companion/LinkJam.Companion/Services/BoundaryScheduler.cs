using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using LinkJam.Companion.Models;
using Timer = System.Timers.Timer;

namespace LinkJam.Companion.Services
{
    public class BoundaryScheduler : IDisposable
    {
        private readonly CarabinerClient _carabinerClient;
        private readonly AuthorityClient _authorityClient;
        private Timer? _boundaryTimer;
        private TempoState? _currentState;
        private TempoState? _pendingState;
        private readonly object _stateLock = new();
        private bool _disposed = false;
        private double _lastLocalBpm = 0;
        private DateTime _lastProposalTime = DateTime.MinValue;

        public event EventHandler<ConnectionStatus>? StatusChanged;
        public event EventHandler<BoundaryInfo>? BoundaryInfoUpdated;
        public event EventHandler<string>? LogMessage;

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        public BoundaryScheduler(CarabinerClient carabinerClient, AuthorityClient authorityClient)
        {
            _carabinerClient = carabinerClient;
            _authorityClient = authorityClient;
            
            _authorityClient.TempoStateReceived += OnTempoStateReceived;
            _carabinerClient.TempoChanged += OnLocalTempoChanged;
        }

        private void OnTempoStateReceived(object? sender, TempoState state)
        {
            lock (_stateLock)
            {
                _pendingState = state;
                ScheduleBoundaryUpdate();
            }
        }

        private void OnLocalTempoChanged(object? sender, double bpm)
        {
            if (Math.Abs(bpm - _lastLocalBpm) > 0.1 && 
                (DateTime.Now - _lastProposalTime).TotalSeconds > 1)
            {
                _lastLocalBpm = bpm;
                _lastProposalTime = DateTime.Now;
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _authorityClient.SendTempoProposalAsync(bpm);
                        LogMessage?.Invoke(this, $"Proposed BPM change to {bpm:F1}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke(this, $"Failed to send tempo proposal: {ex.Message}");
                    }
                });
            }
        }

        private void ScheduleBoundaryUpdate()
        {
            lock (_stateLock)
            {
                if (_pendingState == null) return;

                var boundary = CalculateNextBoundary(_pendingState);
                var msUntilBoundary = boundary.MsUntilBoundary;

                _boundaryTimer?.Stop();
                _boundaryTimer?.Dispose();

                if (msUntilBoundary > 50)
                {
                    _boundaryTimer = new Timer(msUntilBoundary - 20);
                    _boundaryTimer.Elapsed += async (s, e) => await ApplyPendingStateAsync();
                    _boundaryTimer.AutoReset = false;
                    _boundaryTimer.Start();

                    Status = ConnectionStatus.Armed;
                    StatusChanged?.Invoke(this, Status);
                    LogMessage?.Invoke(this, $"Armed for boundary in {msUntilBoundary:F0}ms");
                }
                else
                {
                    _ = Task.Run(async () => await ApplyPendingStateAsync());
                }

                BoundaryInfoUpdated?.Invoke(this, boundary);
            }
        }

        private async Task ApplyPendingStateAsync()
        {
            try
            {
                lock (_stateLock)
                {
                    if (_pendingState == null) return;
                    _currentState = _pendingState;
                    _pendingState = null;
                }

                await _carabinerClient.SetTempoAsync(_currentState!.Bpm);
                
                var serverNow = _authorityClient.GetServerTime();
                var beatsSinceEpoch = (serverNow - _currentState.EpochMs) / (60000.0 / _currentState.Bpm);
                var barsSinceEpoch = Math.Floor(beatsSinceEpoch / _currentState.Bpi);
                var targetBeat = barsSinceEpoch * _currentState.Bpi;
                
                await _carabinerClient.ForceDownbeatAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                Status = ConnectionStatus.Locked;
                StatusChanged?.Invoke(this, Status);
                LogMessage?.Invoke(this, $"LOCKED at BPM {_currentState.Bpm:F1}, BPI {_currentState.Bpi}");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    UpdateBoundaryInfo();
                });
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Failed to apply tempo state: {ex.Message}");
                Status = ConnectionStatus.Connected;
                StatusChanged?.Invoke(this, Status);
            }
        }

        private BoundaryInfo CalculateNextBoundary(TempoState state)
        {
            var serverNow = _authorityClient.GetServerTime();
            var beatMs = 60000.0 / state.Bpm;
            var intervalMs = state.Bpi * beatMs;
            var elapsed = (serverNow - state.EpochMs) % intervalMs;
            var msUntilBoundary = intervalMs - elapsed;
            
            var totalBeats = Math.Floor((serverNow - state.EpochMs) / beatMs);
            var currentBar = (int)(totalBeats / state.Bpi);
            var currentBeat = (int)(totalBeats % state.Bpi) + 1;
            
            var clientNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nextBoundaryClient = clientNow + (long)msUntilBoundary;

            return new BoundaryInfo
            {
                NextBoundary = DateTimeOffset.FromUnixTimeMilliseconds(nextBoundaryClient).DateTime,
                BeatMs = beatMs,
                IntervalMs = intervalMs,
                CurrentBar = currentBar,
                CurrentBeat = currentBeat,
                MsUntilBoundary = msUntilBoundary
            };
        }

        private void UpdateBoundaryInfo()
        {
            lock (_stateLock)
            {
                if (_currentState != null)
                {
                    var boundary = CalculateNextBoundary(_currentState);
                    BoundaryInfoUpdated?.Invoke(this, boundary);
                }
            }
        }

        public void StartUpdating()
        {
            var updateTimer = new Timer(100);
            updateTimer.Elapsed += (s, e) => UpdateBoundaryInfo();
            updateTimer.Start();
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            _boundaryTimer?.Stop();
            _boundaryTimer?.Dispose();
            
            _authorityClient.TempoStateReceived -= OnTempoStateReceived;
            _carabinerClient.TempoChanged -= OnLocalTempoChanged;
        }
    }
}