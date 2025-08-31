using System;
using System.Drawing;
using System.Windows.Forms;
using LinkJam.Companion.Models;
using LinkJam.Companion.Services;

namespace LinkJam.Companion
{
    public partial class MainForm : Form
    {
        private NotifyIcon _trayIcon = null!;
        private ContextMenuStrip _trayMenu = null!;
        private AppCoordinator _coordinator = null!;
        
        private TextBox _serverUrlTextBox = null!;
        private TextBox _roomIdTextBox = null!;
        private TextBox _djNameTextBox = null!;
        private Button _connectButton = null!;
        private Label _statusLabel = null!;
        private Label _bpmLabel = null!;
        private Label _bpiLabel = null!;
        private Label _barBeatLabel = null!;
        private Label _boundaryLabel = null!;
        private Label _peersLabel = null!;
        private RichTextBox _logTextBox = null!;
        
        private ConnectionStatus _currentStatus = ConnectionStatus.Disconnected;
        private BoundaryInfo? _currentBoundary;
        private System.Windows.Forms.Timer _updateTimer = null!;

        public MainForm()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeCoordinator();
            LoadSettings();
            StartUpdateTimer();
        }

        private void InitializeComponent()
        {
            this.Text = "LinkJam Companion";
            this.Size = new Size(500, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(10)
            };

            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            mainPanel.Controls.Add(new Label { Text = "Server URL:", TextAlign = ContentAlignment.MiddleRight }, 0, row);
            _serverUrlTextBox = new TextBox { Dock = DockStyle.Fill, Text = "http://localhost:3000" };
            mainPanel.Controls.Add(_serverUrlTextBox, 1, row++);

            mainPanel.Controls.Add(new Label { Text = "Room ID:", TextAlign = ContentAlignment.MiddleRight }, 0, row);
            _roomIdTextBox = new TextBox { Dock = DockStyle.Fill, Text = "main" };
            mainPanel.Controls.Add(_roomIdTextBox, 1, row++);

            mainPanel.Controls.Add(new Label { Text = "DJ Name:", TextAlign = ContentAlignment.MiddleRight }, 0, row);
            _djNameTextBox = new TextBox { Dock = DockStyle.Fill, Text = "DJ" };
            mainPanel.Controls.Add(_djNameTextBox, 1, row++);

            _connectButton = new Button 
            { 
                Text = "Connect", 
                Height = 30,
                BackColor = Color.FromArgb(102, 126, 234),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _connectButton.Click += ConnectButton_Click;
            mainPanel.Controls.Add(_connectButton, 1, row++);

            var statusPanel = new Panel { Height = 120, Dock = DockStyle.Fill };
            var statusGroupBox = new GroupBox 
            { 
                Text = "Status", 
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            
            var statusLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(5)
            };

            _statusLabel = new Label { Text = "DISCONNECTED", Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            statusLayout.Controls.Add(new Label { Text = "Status:" }, 0, 0);
            statusLayout.Controls.Add(_statusLabel, 1, 0);

            _bpmLabel = new Label { Text = "--" };
            statusLayout.Controls.Add(new Label { Text = "BPM:" }, 0, 1);
            statusLayout.Controls.Add(_bpmLabel, 1, 1);

            _bpiLabel = new Label { Text = "--" };
            statusLayout.Controls.Add(new Label { Text = "BPI:" }, 0, 2);
            statusLayout.Controls.Add(_bpiLabel, 1, 2);

            _barBeatLabel = new Label { Text = "--:--" };
            statusLayout.Controls.Add(new Label { Text = "Bar:Beat:" }, 0, 3);
            statusLayout.Controls.Add(_barBeatLabel, 1, 3);

            _boundaryLabel = new Label { Text = "--.-s" };
            statusLayout.Controls.Add(new Label { Text = "Next Boundary:" }, 0, 4);
            statusLayout.Controls.Add(_boundaryLabel, 1, 4);

            statusGroupBox.Controls.Add(statusLayout);
            statusPanel.Controls.Add(statusGroupBox);
            mainPanel.SetColumnSpan(statusPanel, 2);
            mainPanel.Controls.Add(statusPanel, 0, row++);

            _peersLabel = new Label { Text = "Link Peers: 0", Dock = DockStyle.Fill };
            mainPanel.SetColumnSpan(_peersLabel, 2);
            mainPanel.Controls.Add(_peersLabel, 0, row++);

            var logGroupBox = new GroupBox 
            { 
                Text = "Activity Log", 
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            
            _logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 249, 250),
                Font = new Font("Consolas", 9),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            
            logGroupBox.Controls.Add(_logTextBox);
            mainPanel.SetColumnSpan(logGroupBox, 2);
            mainPanel.SetRowSpan(logGroupBox, 4);
            mainPanel.Controls.Add(logGroupBox, 0, row);

            this.Controls.Add(mainPanel);

            this.FormClosing += MainForm_FormClosing;
        }

        private void InitializeTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Show", null, (s, e) => ShowWindow());
            _trayMenu.Items.Add("Connect", null, (s, e) => ConnectButton_Click(s, e));
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            _trayIcon = new NotifyIcon
            {
                Text = "LinkJam Companion",
                Icon = SystemIcons.Application,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void InitializeCoordinator()
        {
            _coordinator = new AppCoordinator();
            _coordinator.StatusChanged += OnStatusChanged;
            _coordinator.BoundaryInfoUpdated += OnBoundaryInfoUpdated;
            _coordinator.LogMessage += OnLogMessage;
            _coordinator.PeersChanged += OnPeersChanged;
        }

        private void LoadSettings()
        {
            _serverUrlTextBox.Text = Properties.Settings.Default.ServerUrl ?? "http://localhost:3000";
            _roomIdTextBox.Text = Properties.Settings.Default.RoomId ?? "main";
            _djNameTextBox.Text = Properties.Settings.Default.DjName ?? Environment.UserName;
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.ServerUrl = _serverUrlTextBox.Text;
            Properties.Settings.Default.RoomId = _roomIdTextBox.Text;
            Properties.Settings.Default.DjName = _djNameTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void StartUpdateTimer()
        {
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = 100;
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentBoundary != null)
            {
                var msUntilBoundary = (_currentBoundary.NextBoundary - DateTime.Now).TotalMilliseconds;
                if (msUntilBoundary > 0)
                {
                    _boundaryLabel.Text = $"{msUntilBoundary / 1000:F1}s";
                }
                
                _barBeatLabel.Text = $"{_currentBoundary.CurrentBar}:{_currentBoundary.CurrentBeat}";
            }
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (_currentStatus == ConnectionStatus.Disconnected)
            {
                try
                {
                    _connectButton.Enabled = false;
                    SaveSettings();
                    
                    await _coordinator.ConnectAsync(
                        _serverUrlTextBox.Text,
                        _roomIdTextBox.Text,
                        _djNameTextBox.Text);
                    
                    _connectButton.Text = "Disconnect";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Connection failed: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _connectButton.Enabled = true;
                }
            }
            else
            {
                await _coordinator.DisconnectAsync();
                _connectButton.Text = "Connect";
            }
        }

        private void OnStatusChanged(object? sender, ConnectionStatus status)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnStatusChanged(sender, status));
                return;
            }

            _currentStatus = status;
            _statusLabel.Text = status.ToString().ToUpper();
            
            switch (status)
            {
                case ConnectionStatus.Disconnected:
                    _statusLabel.ForeColor = Color.Red;
                    _trayIcon.Icon = SystemIcons.Error;
                    break;
                case ConnectionStatus.Connecting:
                    _statusLabel.ForeColor = Color.Orange;
                    break;
                case ConnectionStatus.Connected:
                    _statusLabel.ForeColor = Color.Blue;
                    _trayIcon.Icon = SystemIcons.Information;
                    break;
                case ConnectionStatus.Armed:
                    _statusLabel.ForeColor = Color.DarkOrange;
                    break;
                case ConnectionStatus.Locked:
                    _statusLabel.ForeColor = Color.Green;
                    _trayIcon.Icon = SystemIcons.Shield;
                    break;
            }
            
            UpdateTrayTooltip();
        }

        private void OnBoundaryInfoUpdated(object? sender, BoundaryInfo info)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnBoundaryInfoUpdated(sender, info));
                return;
            }

            _currentBoundary = info;
            _bpmLabel.Text = $"{info.BeatMs * info.IntervalMs / 60000:F1}";
            _bpiLabel.Text = $"{(int)(info.IntervalMs / info.BeatMs)}";
        }

        private void OnPeersChanged(object? sender, int peers)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnPeersChanged(sender, peers));
                return;
            }

            _peersLabel.Text = $"Link Peers: {peers}";
        }

        private void OnLogMessage(object? sender, string message)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnLogMessage(sender, message));
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logTextBox.AppendText($"[{timestamp}] {message}\n");
            _logTextBox.ScrollToCaret();
            
            while (_logTextBox.Lines.Length > 100)
            {
                var lines = _logTextBox.Lines;
                _logTextBox.Lines = lines.Skip(1).ToArray();
            }
        }

        private void UpdateTrayTooltip()
        {
            var status = _currentStatus.ToString();
            var room = _roomIdTextBox.Text;
            _trayIcon.Text = $"LinkJam Companion\nRoom: {room}\nStatus: {status}";
        }

        private void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.ShowBalloonTip(2000, "LinkJam Companion", 
                    "Application minimized to tray", ToolTipIcon.Info);
            }
        }

        private void ExitApplication()
        {
            _updateTimer?.Stop();
            _coordinator?.Dispose();
            _trayIcon?.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Dispose();
                _coordinator?.Dispose();
                _trayIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}