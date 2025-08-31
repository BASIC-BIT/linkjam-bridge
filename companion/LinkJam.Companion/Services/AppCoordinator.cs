using System;
using System.Threading.Tasks;
using LinkJam.Companion.Models;

namespace LinkJam.Companion.Services
{
    public class AppCoordinator : IDisposable
    {
        private readonly CarabinerClient _carabinerClient;
        private readonly AuthorityClient _authorityClient;
        private readonly BoundaryScheduler _boundaryScheduler;
        private bool _disposed = false;

        public event EventHandler<ConnectionStatus>? StatusChanged;
        public event EventHandler<BoundaryInfo>? BoundaryInfoUpdated;
        public event EventHandler<string>? LogMessage;
        public event EventHandler<int>? PeersChanged;

        public CarabinerClient Carabiner => _carabinerClient;
        public AuthorityClient Authority => _authorityClient;
        public BoundaryScheduler Scheduler => _boundaryScheduler;

        public AppCoordinator()
        {
            _carabinerClient = new CarabinerClient();
            _authorityClient = new AuthorityClient();
            _boundaryScheduler = new BoundaryScheduler(_carabinerClient, _authorityClient);

            _carabinerClient.ConnectionStatusChanged += OnCarabinerConnectionChanged;
            _carabinerClient.PeersChanged += OnPeersChanged;
            
            _authorityClient.ConnectionStatusChanged += OnAuthorityConnectionChanged;
            _authorityClient.ErrorOccurred += OnError;
            
            _boundaryScheduler.StatusChanged += OnSchedulerStatusChanged;
            _boundaryScheduler.BoundaryInfoUpdated += OnBoundaryInfoUpdated;
            _boundaryScheduler.LogMessage += OnLogMessage;
        }

        public async Task ConnectAsync(string serverUrl, string roomId, string djName)
        {
            try
            {
                LogMessage?.Invoke(this, "Starting Carabiner...");
                await _carabinerClient.StartAsync();
                
                LogMessage?.Invoke(this, $"Connecting to Authority at {serverUrl}...");
                await _authorityClient.ConnectAsync(serverUrl, roomId, djName);
                
                _boundaryScheduler.StartUpdating();
                
                LogMessage?.Invoke(this, "Successfully connected to LinkJam system");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                LogMessage?.Invoke(this, "Disconnecting...");
                await _authorityClient.DisconnectAsync();
                LogMessage?.Invoke(this, "Disconnected");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Disconnect error: {ex.Message}");
            }
        }

        private void OnCarabinerConnectionChanged(object? sender, bool connected)
        {
            LogMessage?.Invoke(this, connected ? "Carabiner connected" : "Carabiner disconnected");
        }

        private void OnAuthorityConnectionChanged(object? sender, ConnectionStatus status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private void OnSchedulerStatusChanged(object? sender, ConnectionStatus status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private void OnBoundaryInfoUpdated(object? sender, BoundaryInfo info)
        {
            BoundaryInfoUpdated?.Invoke(this, info);
        }

        private void OnPeersChanged(object? sender, int peers)
        {
            PeersChanged?.Invoke(this, peers);
            LogMessage?.Invoke(this, $"Link peers: {peers}");
        }

        private void OnError(object? sender, string error)
        {
            LogMessage?.Invoke(this, $"Error: {error}");
        }

        private void OnLogMessage(object? sender, string message)
        {
            LogMessage?.Invoke(this, message);
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            try
            {
                _boundaryScheduler?.Dispose();
                _authorityClient?.Dispose();
                _carabinerClient?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during AppCoordinator disposal: {ex.Message}");
            }
        }
    }
}