using Microsoft.Win32;
using Renci.SshNet.Sftp;
using StackSuite.Services;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace StackSuite.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        
        // ─────────────────────────────────────────────────────────────────────────────
        // A) TAB NAVIGATION PROPERTIES & COMMANDS (new)
        // ─────────────────────────────────────────────────────────────────────────────

        private int _selectedTabIndex;
        /// <summary>
        /// 0 = Home, 1 = Network Tester, 2 = SSH Console, 3 = SFTP Client
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged(nameof(SelectedTabIndex));
                }
            }
        }

        public ICommand GoToHomeTabCommand { get; }
        public ICommand GoToNetTesterTabCommand { get; }
        public ICommand GoToSshTabCommand { get; }
        public ICommand GoToSftpTabCommand { get; }

        // ─────────────────────────────────────────────────────────────────────────────
        // B) NET TESTER FIELDS, PROPERTIES & COMMANDS
        // ─────────────────────────────────────────────────────────────────────────────

        public ObservableCollection<DeviceInfo> Results { get; } = new();
        public ICollectionView ResultsView { get; }

        private string _hostOrIp = "";
        public string HostOrIp
        {
            get => _hostOrIp;
            set
            {
                if (_hostOrIp != value)
                {
                    _hostOrIp = value;
                    OnPropertyChanged(nameof(HostOrIp));
                }
            }
        }

        private bool _showOffline;
        public bool ShowOffline
        {
            get => _showOffline;
            set
            {
                if (_showOffline != value)
                {
                    _showOffline = value;
                    OnPropertyChanged(nameof(ShowOffline));
                    ResultsView.Refresh();
                }
            }
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (_isScanning != value)
                {
                    _isScanning = value;
                    OnPropertyChanged(nameof(IsScanning));
                    OnPropertyChanged(nameof(IsCancelEnabled));
                }
            }
        }

        public bool IsCancelEnabled => IsScanning;

        public ObservableCollection<NetworkAdapterViewModel> Adapters { get; } = new();
        private NetworkAdapterViewModel? _selectedAdapter;
        public NetworkAdapterViewModel? SelectedAdapter
        {
            get => _selectedAdapter;
            set
            {
                if (_selectedAdapter != value)
                {
                    _selectedAdapter = value;
                    OnPropertyChanged(nameof(SelectedAdapter));
                }
            }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                if (Math.Abs(_progressValue - value) > 0.001)
                {
                    _progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        private Visibility _progressVisibility = Visibility.Collapsed;
        public Visibility ProgressVisibility
        {
            get => _progressVisibility;
            set
            {
                if (_progressVisibility != value)
                {
                    _progressVisibility = value;
                    OnPropertyChanged(nameof(ProgressVisibility));
                }
            }
        }

        private CancellationTokenSource? _cts;

        // “OpenIn…” availability flags (enable/disable menu items)
        private bool _canOpenInRdp = true;
        private bool _canOpenInSsh = true;
        private bool _canOpenHttp = true;
        private bool _canOpenUnc = true;
        private bool _canConnectSsh = true; // used by ConnectSshSessionCommand

        public bool CanOpenInRdp
        {
            get => _canOpenInRdp;
            set
            {
                if (_canOpenInRdp != value)
                {
                    _canOpenInRdp = value;
                    OnPropertyChanged(nameof(CanOpenInRdp));
                    (OpenInRdpCommand as RelayCommand<DeviceInfo>)?.RaiseCanExecuteChanged();
                }
            }
        }
        public bool CanOpenInSsh
        {
            get => _canOpenInSsh;
            set
            {
                if (_canOpenInSsh != value)
                {
                    _canOpenInSsh = value;
                    OnPropertyChanged(nameof(CanOpenInSsh));
                    (OpenInSshCommand as RelayCommand<DeviceInfo>)?.RaiseCanExecuteChanged();
                }
            }
        }
        public bool CanOpenHttp
        {
            get => _canOpenHttp;
            set
            {
                if (_canOpenHttp != value)
                {
                    _canOpenHttp = value;
                    OnPropertyChanged(nameof(CanOpenHttp));
                    (OpenHttpCommand as RelayCommand<DeviceInfo>)?.RaiseCanExecuteChanged();
                }
            }
        }
        public bool CanOpenUnc
        {
            get => _canOpenUnc;
            set
            {
                if (_canOpenUnc != value)
                {
                    _canOpenUnc = value;
                    OnPropertyChanged(nameof(CanOpenUnc));
                    (OpenUncCommand as RelayCommand<DeviceInfo>)?.RaiseCanExecuteChanged();
                }
            }
        }
        public bool CanConnectSsh
        {
            get => _canConnectSsh;
            set
            {
                if (_canConnectSsh != value)
                {
                    _canConnectSsh = value;
                    OnPropertyChanged(nameof(CanConnectSsh));
                    (ConnectSshSessionCommand as RelayCommand<object?>)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand ScanCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }

        public ICommand OpenInRdpCommand { get; }
        public ICommand OpenInSshCommand { get; }
        public ICommand OpenHttpCommand { get; }
        public ICommand OpenUncCommand { get; }
        public ICommand ConnectSshSessionCommand { get; }
        public ICommand NetTesterResultsKeyDownCommand { get; }

        // ─────────────────────────────────────────────────────────────────────────────
        // C) SSH SESSION FIELDS, PROPERTIES & COMMANDS
        // ─────────────────────────────────────────────────────────────────────────────

        private string _newSshHost = "";
        public string NewSshHost
        {
            get => _newSshHost;
            set
            {
                if (_newSshHost != value)
                {
                    _newSshHost = value;
                    OnPropertyChanged(nameof(NewSshHost));
                    OnPropertyChanged(nameof(CanAddSshSession));
                    (AddSshSessionCommand as RelayCommand<object?>)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _newSshUsername = "";
        public string NewSshUsername
        {
            get => _newSshUsername;
            set
            {
                if (_newSshUsername != value)
                {
                    _newSshUsername = value;
                    OnPropertyChanged(nameof(NewSshUsername));
                    OnPropertyChanged(nameof(CanAddSshSession));
                    (AddSshSessionCommand as RelayCommand<object?>)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _newSshPassword = "";
        public string NewSshPassword
        {
            get => _newSshPassword;
            set
            {
                if (_newSshPassword != value)
                {
                    _newSshPassword = value;
                    OnPropertyChanged(nameof(NewSshPassword));
                    OnPropertyChanged(nameof(CanAddSshSession));
                    (AddSshSessionCommand as RelayCommand<object?>)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanAddSshSession =>
            !string.IsNullOrWhiteSpace(NewSshHost) &&
            !string.IsNullOrWhiteSpace(NewSshUsername) &&
            !string.IsNullOrWhiteSpace(NewSshPassword);

        public ObservableCollection<SshSessionViewModel> SshSessions { get; } = new();
        private SshSessionViewModel? _selectedSshSession;
        public SshSessionViewModel? SelectedSshSession
        {
            get => _selectedSshSession;
            set
            {
                if (_selectedSshSession != value)
                {
                    _selectedSshSession = value;
                    OnPropertyChanged(nameof(SelectedSshSession));
                }
            }
        }

        public ICommand AddSshSessionCommand { get; }

        // ─────────────────────────────────────────────────────────────────────────────
        // D) SFTP SESSION FIELDS, PROPERTIES & COMMANDS
        // ─────────────────────────────────────────────────────────────────────────────

        private string _sftpHost = "";
        public string SftpHost
        {
            get => _sftpHost;
            set
            {
                if (_sftpHost != value)
                {
                    _sftpHost = value;
                    OnPropertyChanged(nameof(SftpHost));
                    OnPropertyChanged(nameof(CanAddSftpSession));
                }
            }
        }

        private string _sftpUsername = "";
        public string SftpUsername
        {
            get => _sftpUsername;
            set
            {
                if (_sftpUsername != value)
                {
                    _sftpUsername = value;
                    OnPropertyChanged(nameof(SftpUsername));
                    OnPropertyChanged(nameof(CanAddSftpSession));
                }
            }
        }

        private string _sftpPassword = "";
        public string SftpPassword
        {
            get => _sftpPassword;
            set
            {
                if (_sftpPassword == value) return;
                _sftpPassword = value;
                OnPropertyChanged(nameof(SftpPassword));

                // Notify that CanAddSftpSession may have changed
                OnPropertyChanged(nameof(CanAddSftpSession));

                // Also tell the RelayCommand that its CanExecute might have changed
                (AddSftpSessionCommand as RelayCommand<object?>)?.RaiseCanExecuteChanged();
            }
        }

        public bool CanAddSftpSession =>
            !string.IsNullOrWhiteSpace(SftpHost) &&
            !string.IsNullOrWhiteSpace(SftpUsername) &&
            !string.IsNullOrWhiteSpace(SftpPassword);

        public ObservableCollection<SftpSessionViewModel> SftpSessions { get; } = new();
        private SftpSessionViewModel? _selectedSftpSession;
        public SftpSessionViewModel? SelectedSftpSession
        {
            get => _selectedSftpSession;
            set
            {
                if (_selectedSftpSession != value)
                {
                    _selectedSftpSession = value;
                    OnPropertyChanged(nameof(SelectedSftpSession));
                }
            }
        }

        public ICommand AddSftpSessionCommand { get; }

        // ─────────────────────────────────────────────────────────────────────────────
        // E) CONSTRUCTOR: INITIALIZE EVERYTHING
        // ─────────────────────────────────────────────────────────────────────────────

        public MainWindowViewModel()
        {
            // 1) Tab navigation commands (new)
            GoToHomeTabCommand = new RelayCommand<object?>(_ => SelectedTabIndex = 0);
            GoToNetTesterTabCommand = new RelayCommand<object?>(_ => SelectedTabIndex = 1);
            GoToSshTabCommand = new RelayCommand<object?>(_ => SelectedTabIndex = 2);
            GoToSftpTabCommand = new RelayCommand<object?>(_ => SelectedTabIndex = 3);

            // 2) NET TESTER setup
            ResultsView = CollectionViewSource.GetDefaultView(Results);
            ResultsView.Filter = FilterOffline;

            ScanCommand = new RelayCommand<object?>(async _ => await ScanAsync(), _ => !IsScanning);
            CancelCommand = new RelayCommand<object?>(_ => CancelScan(), _ => IsScanning);
            ImportCommand = new RelayCommand<object?>(async _ => await ImportAsync(), _ => !IsScanning);
            ExportCommand = new RelayCommand<object?>(_ => Export(), _ => Results.Any());

            OpenInRdpCommand = new RelayCommand<DeviceInfo>(OnOpenInRdp, _ => CanOpenInRdp);
            OpenInSshCommand = new RelayCommand<DeviceInfo>(OnOpenInSsh, _ => CanOpenInSsh);
            OpenHttpCommand = new RelayCommand<DeviceInfo>(OnOpenHttp, _ => CanOpenHttp);
            OpenUncCommand = new RelayCommand<DeviceInfo>(OnOpenUnc, _ => CanOpenUnc);
            ConnectSshSessionCommand = new RelayCommand<object?>(OnConnectSsh, _ => CanConnectSsh);
            NetTesterResultsKeyDownCommand = new RelayCommand<DeviceInfo>(OnNetTesterResultsKeyDown);

            LoadAdapters();

            // 3) SSH session command
            AddSshSessionCommand = new RelayCommand<object?>(_ => AddSshSession(), _ => CanAddSshSession);

            // 4) SFTP session command
            AddSftpSessionCommand = new RelayCommand<object?>(_ => AddSftpSession(), _ => CanAddSftpSession);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // F) NET TESTER METHODS
        // ─────────────────────────────────────────────────────────────────────────────

        private void LoadAdapters()
        {
            Adapters.Clear();
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(nic => new NetworkAdapterViewModel
                {
                    Id = nic.Id,
                    Name = $"{nic.Name} ({nic.Description})"
                })
                .ToList();

            foreach (var adapter in adapters)
                Adapters.Add(adapter);

            SelectedAdapter = Adapters.FirstOrDefault();
        }

        private bool FilterOffline(object obj)
        {
            if (obj is DeviceInfo di)
            {
                if (ShowOffline) return true;
                return !string.Equals(di.Status, "Offline", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private async Task ScanAsync()
        {
            IsScanning = true;
            ProgressVisibility = Visibility.Visible;
            Results.Clear();
            _cts = new CancellationTokenSource();

            try
            {
                var adapterId = SelectedAdapter?.Id;
                var options = new NetTesterOptions();

                if (string.IsNullOrWhiteSpace(HostOrIp))
                {
                    await foreach (var info in NetTesterService.DiscoverDevicesAsync(adapterId, options, _cts.Token))
                        Results.Add(info);
                }
                else
                {
                    await foreach (var info in NetTesterService.ScanAsyncEnumerable(HostOrIp, options, _cts.Token))
                        Results.Add(info);
                }
            }
            catch (OperationCanceledException)
            {
                // user cancelled
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Scan failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
                ProgressVisibility = Visibility.Collapsed;
                _cts = null;
            }
        }

        private void CancelScan()
        {
            _cts?.Cancel();
            IsScanning = false;
            ProgressVisibility = Visibility.Collapsed;
        }

        private async Task ImportAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Text or CSV|*.txt;*.csv|All Files|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            var entries = File.ReadAllLines(dlg.FileName)
                              .Select(line => line.Trim())
                              .Where(line => !string.IsNullOrWhiteSpace(line));

            IsScanning = true;
            ProgressVisibility = Visibility.Visible;
            Results.Clear();
            _cts = new CancellationTokenSource();

            try
            {
                await foreach (var info in NetTesterService.ScanHostsAsync(entries, options: null, cancellationToken: _cts.Token))
                    Results.Add(info);
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsScanning = false;
                ProgressVisibility = Visibility.Collapsed;
                _cts = null;
            }
        }

        private void Export()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "NetworkScanResults.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var writer = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8);

                var props = typeof(DeviceInfo).GetProperties();
                var headers = props
                    .Select(p => p.GetCustomAttributes(typeof(DisplayNameAttribute), false)
                                  .OfType<DisplayNameAttribute>().FirstOrDefault()?.DisplayName
                                  ?? p.Name);
                writer.WriteLine(string.Join(",", headers));

                foreach (var item in Results)
                {
                    var values = props.Select(p =>
                    {
                        var v = p.GetValue(item)?.ToString() ?? "";
                        return $"\"{v.Replace("\"", "\"\"")}\"";
                    });
                    writer.WriteLine(string.Join(",", values));
                }

                MessageBox.Show($"Exported {Results.Count} rows to:\n{dlg.FileName}",
                                "Export Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void OnOpenInRdp(DeviceInfo? dev)
        {
            if (dev == null || string.IsNullOrWhiteSpace(dev.ReplyIP)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "mstsc",
                    Arguments = $"/v:{dev.ReplyIP}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch RDP:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenInSsh(DeviceInfo? dev)
        {
            if (dev == null || string.IsNullOrWhiteSpace(dev.ReplyIP)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ssh",
                    Arguments = dev.ReplyIP,
                    UseShellExecute = false
                });
            }
            catch
            {
                // fallback to PuTTY
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "putty.exe",
                        Arguments = $"-ssh {dev.ReplyIP}",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch SSH:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnOpenHttp(DeviceInfo? dev)
        {
            if (dev == null || string.IsNullOrWhiteSpace(dev.ReplyIP)) return;

            try
            {
                var url = $"http://{dev.ReplyIP}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open browser:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenUnc(DeviceInfo? dev)
        {
            if (dev == null || string.IsNullOrWhiteSpace(dev.ReplyIP)) return;

            try
            {
                var unc = $"\\\\{dev.ReplyIP}\\";
                Process.Start(new ProcessStartInfo
                {
                    FileName = unc,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open UNC share:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnConnectSsh(object? parameter)
        {
            // This was the old “Connect SSH” in Net Tester. Not used elsewhere in MVVM.
            MessageBox.Show("SSH Connect pressed. Implement SSH logic here.", "SSH Connect", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnNetTesterResultsKeyDown(DeviceInfo? selectedDevice)
        {
            if (selectedDevice == null) return;
            OnOpenInRdp(selectedDevice);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // G) SSH SESSION METHODS
        // ─────────────────────────────────────────────────────────────────────────────

        private async void AddSshSession()
        {
            var connectionKey = $"{NewSshUsername}@{NewSshHost}";
            var tabTitle = $"SSH: {connectionKey}";

            var sshVm = new SshSessionViewModel(removeCallback: vm =>
            {
                var cast = (SshSessionViewModel)vm;
                // Disconnect and remove:
                cast.Service?.Dispose();
                SshSessions.Remove(cast);
                if (SelectedSshSession == cast)
                    SelectedSshSession = null;
            })
            {
                ConnectionKey = connectionKey,
                TabTitle = tabTitle
            };

            // Start the connection & interactive shell in background:
            await Task.Run(async () =>
            {
                try
                {
                    await sshVm.StartInteractiveShellAsync(host: NewSshHost,
                                      username: NewSshUsername,
                                      password: NewSshPassword);

                    // Once connected, switch back to UI thread to add the tab:
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SshSessions.Add(sshVm);
                        SelectedSshSession = sshVm;

                        // Clear input fields:
                        NewSshHost = "";
                        NewSshUsername = "";
                        NewSshPassword = "";
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"SSH connection failed:\n\n{ex.Message}\n\nPlease verify credentials and reachability.",
                            "SSH Connection Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    });
                }
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // H) SFTP SESSION METHODS
        // ─────────────────────────────────────────────────────────────────────────────

        private void AddSftpSession()
{
    var connectionKey = $"{SftpUsername}@{SftpHost}";
    var tabTitle = $"SFTP: {connectionKey}";

    var sftpVm = new SftpSessionViewModel(removeCallback: vm =>
    {
        var cast = (SftpSessionViewModel)vm;
        // Dispose the service and remove from collection
        cast.Service?.Dispose();
        SftpSessions.Remove(cast);
        if (SelectedSftpSession == cast)
            SelectedSftpSession = null;
    })
    {
        ConnectionKey = connectionKey,
        TabTitle = tabTitle
    };

    // Do NOT set IsConnected yet; we’ll wait until AFTER loading the directory.

    Task.Run(async () =>
    {
        try
        {
            var service = new SftpService();
            service.Connect(SftpHost, 22, SftpUsername, SftpPassword);
            sftpVm.Service = service;

            // 1) Load the directory first (still on background thread)
            await LoadSftpDirectoryAsync(sftpVm, "/");

            // 2) Only when directory loading is complete, go back to UI thread:
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Now flip IsConnected = true so “X” is enabled
                sftpVm.IsConnected = true;

                // Wire up commands and insert into UI
                WireUpSftpCommands(sftpVm);
                SftpSessions.Add(sftpVm);
                SelectedSftpSession = sftpVm;

                // Clear the input fields
                SftpHost = "";
                SftpUsername = "";
                SftpPassword = "";
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"SFTP connection failed:\n\n{ex.Message}\n\nPlease check:\n" +
                    "• Host/IP address is correct\n" +
                    "• Port 22 is accessible\n" +
                    "• Username and password are valid\n" +
                    "• SSH service is running on target host",
                    "Connection Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            });
        }
    });
}


        private async Task LoadSftpDirectoryAsync(SftpSessionViewModel sftpVm, string path)
        {
            if (sftpVm.Service == null || !sftpVm.Service.IsConnected)
                return;

            try
            {
                sftpVm.StatusText = $"Listing {path}...";
                var files = await sftpVm.Service.ListDirectoryAsync(path);
                sftpVm.DirectoryItems.Clear();

                foreach (var f in files)
                {
                    if (f.Name == ".") continue;

                    var vm = new SftpFileViewModel
                    {
                        SftpFile = f,
                        DisplayName = f.Name == ".."
                            ? "📁 .."
                            : f.IsDirectory
                                ? $"📁 {f.Name}"
                                : $"{GetFileIcon(f.Name)}{f.Name}",
                        Type = f.IsDirectory ? "Folder" : "File",
                        FormattedSize = f.IsDirectory ? "" : FormatFileSize(f.Length),
                        FormattedDate = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Permissions = f.GroupCanWrite ? "rw-" : "r--"
                    };
                    sftpVm.DirectoryItems.Add(vm);
                }

                sftpVm.CurrentPath = path;
                sftpVm.StatusText = $"Listed {sftpVm.DirectoryItems.Count(vm => !vm.SftpFile.IsDirectory)} files, {sftpVm.DirectoryItems.Count(vm => vm.SftpFile.IsDirectory)} folders";
            }
            catch (Exception ex)
            {
                sftpVm.StatusText = $"Error loading directory: {ex.Message}";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Failed to load directory '{path}':\n{ex.Message}",
                        "Directory Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                });
            }
        }

        private void WireUpSftpCommands(SftpSessionViewModel sftpVm)
        {
            // Up one level
            sftpVm.UpDirectoryCommand = new RelayCommand<object?>(async _ =>
            {
                var current = sftpVm.CurrentPath.TrimEnd('/');
                if (string.IsNullOrEmpty(current) || current == "/")
                    return;

                var parent = current.Contains('/')
                                 ? current.Substring(0, current.LastIndexOf('/'))
                                 : "/";
                if (string.IsNullOrEmpty(parent))
                    parent = "/";

                await LoadSftpDirectoryAsync(sftpVm, parent);
            }, _ => sftpVm.Service?.IsConnected == true);

            // Refresh
            sftpVm.RefreshDirectoryCommand = new RelayCommand<object?>(async _ =>
            {
                await LoadSftpDirectoryAsync(sftpVm, sftpVm.CurrentPath);
            }, _ => sftpVm.Service?.IsConnected == true);

            // Navigate into folder (double-click)
            sftpVm.NavigateDirectoryCommand = new RelayCommand<object?>(async param =>
            {
                if (param is not SftpFileViewModel selected) return;
                if (!selected.SftpFile.IsDirectory) return;

                var name = selected.SftpFile.Name;
                string nextPath;
                if (name == "..")
                {
                    var curr = sftpVm.CurrentPath.TrimEnd('/');
                    if (curr == "/" || string.IsNullOrEmpty(curr)) return;

                    nextPath = curr.Contains('/')
                        ? curr.Substring(0, curr.LastIndexOf('/'))
                        : "/";
                    if (string.IsNullOrEmpty(nextPath)) nextPath = "/";
                }
                else
                {
                    nextPath = selected.SftpFile.FullName;
                }

                await LoadSftpDirectoryAsync(sftpVm, nextPath);
            },
            param => sftpVm.Service?.IsConnected == true && param is SftpFileViewModel);

            // ───── Download (with progress & cancellation) ─────
            sftpVm.DownloadCommand = new RelayCommand<object?>(async param =>
            {
                if (param is not IList list) return;
                var items = list.Cast<SftpFileViewModel>()
                                .Where(vm => !vm.SftpFile.IsDirectory)
                                .ToList();
                if (!items.Any())
                {
                    MessageBox.Show("Please select one or more files to download.",
                                    "No Files Selected",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                    return;
                }

                // 1) Prepare cancellation & progress reporting
                sftpVm._transferCts = new CancellationTokenSource();
                sftpVm.IsTransferInProgress = true;
                var progress = new Progress<ProgressReport>(report =>
                {
                    double pct = report.TotalBytes == 0
                        ? 0
                        : report.BytesTransferred / (double)report.TotalBytes * 100;
                    sftpVm.TransferProgressPercent = pct;

                    // ← Now uses human-readable MB/KB via your FormatFileSize()
                    sftpVm.TransferStatusText =
                        $"{FormatFileSize(report.BytesTransferred)} / {FormatFileSize(report.TotalBytes)} ({pct:0.##}% completed)";
                });

                try
                {
                    if (items.Count == 1)
                    {
                        var file = items.First().SftpFile;
                        var dlg = new SaveFileDialog { FileName = file.Name };
                        if (dlg.ShowDialog() != true) return;

                        sftpVm.StatusText = $"Downloading {file.Name}…";
                        await sftpVm.Service!.DownloadFileAsync(
                            file.FullName,
                            dlg.FileName,
                            progress,
                            sftpVm._transferCts.Token
                        );
                        sftpVm.StatusText = $"Downloaded {file.Name}";
                    }
                    else
                    {
                        var folderDlg = new System.Windows.Forms.FolderBrowserDialog();
                        if (folderDlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                        sftpVm.StatusText = $"Downloading {items.Count} files…";
                        var tasks = items.Select(vm =>
                            sftpVm.Service!.DownloadFileAsync(
                                vm.SftpFile.FullName,
                                Path.Combine(folderDlg.SelectedPath, vm.SftpFile.Name),
                                progress,
                                sftpVm._transferCts.Token
                            ));
                        await Task.WhenAll(tasks);
                        sftpVm.StatusText = $"Downloaded {items.Count} files";
                    }
                }
                catch (OperationCanceledException)
                {
                    sftpVm.StatusText = "Download canceled.";
                }
                catch (Exception ex)
                {
                    sftpVm.StatusText = $"Download failed: {ex.Message}";
                }
                finally
                {
                    sftpVm.IsTransferInProgress = false;
                    sftpVm._transferCts?.Dispose();
                    sftpVm._transferCts = null;
                }
            },
            param => sftpVm.Service?.IsConnected == true);


            // Upload
            // ───── Upload (with progress & cancellation) ─────
            sftpVm.UploadCommand = new RelayCommand<object?>(async _ =>
            {
                var dlg = new OpenFileDialog { Multiselect = true };
                if (dlg.ShowDialog() != true) return;

                // 1) Prepare cancellation & progress reporting
                sftpVm._transferCts = new CancellationTokenSource();
                sftpVm.IsTransferInProgress = true;
                var progress = new Progress<ProgressReport>(report =>
                {
                    double pct = report.TotalBytes == 0
                        ? 0
                        : report.BytesTransferred / (double)report.TotalBytes * 100;
                    sftpVm.TransferProgressPercent = pct;
                    sftpVm.TransferStatusText =
                        $"{FormatFileSize(report.BytesTransferred)} / {FormatFileSize(report.TotalBytes)} ({pct:0.##}% completed)";
                });

                try
                {
                    sftpVm.StatusText = $"Uploading {dlg.FileNames.Length} files…";
                    var tasks = dlg.FileNames.Select(localPath =>
                    {
                        var fileName = Path.GetFileName(localPath);
                        var remotePath = $"{sftpVm.CurrentPath.TrimEnd('/')}/{fileName}";
                        return sftpVm.Service!.UploadFileAsync(
                            localPath,
                            remotePath,
                            progress,
                            sftpVm._transferCts.Token
                        );
                    });
                    await Task.WhenAll(tasks);
                    await LoadSftpDirectoryAsync(sftpVm, sftpVm.CurrentPath);
                    sftpVm.StatusText = $"Uploaded {dlg.FileNames.Length} files";
                }
                catch (OperationCanceledException)
                {
                    sftpVm.StatusText = "Upload canceled.";
                }
                catch (Exception ex)
                {
                    sftpVm.StatusText = $"Upload failed: {ex.Message}";
                    MessageBox.Show($"Upload failed:\n{ex.Message}",
                                    "Upload Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
                finally
                {
                    sftpVm.IsTransferInProgress = false;
                    sftpVm._transferCts?.Dispose();
                    sftpVm._transferCts = null;
                }
            },
            _ => sftpVm.Service?.IsConnected == true);

            // Delete
            sftpVm.DeleteCommand = new RelayCommand<object?>(async param =>
            {
                if (param is not IList list) return;
                var items = list.Cast<SftpFileViewModel>().ToList();
                if (!items.Any()) return;

                var result = MessageBox.Show(
                    $"Are you sure you want to delete {items.Count} item(s)?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );
                if (result != MessageBoxResult.Yes) return;

                try
                {
                    foreach (var vm in items)
                    {
                        var f = vm.SftpFile;
                        if (f.IsDirectory)
                            await sftpVm.Service!.DeleteDirectoryAsync(f.FullName);
                        else
                            await sftpVm.Service!.DeleteFileAsync(f.FullName);
                    }
                    await LoadSftpDirectoryAsync(sftpVm, sftpVm.CurrentPath);
                    sftpVm.StatusText = $"Deleted {items.Count} items";
                }
                catch (Exception ex)
                {
                    sftpVm.StatusText = $"Delete failed: {ex.Message}";
                    MessageBox.Show($"Delete operation failed:\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, param => sftpVm.Service?.IsConnected == true);

            // Create New Folder
            sftpVm.CreateFolderCommand = new RelayCommand<object?>(async _ =>
            {
                string folderName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter folder name:", "New Folder", "New Folder"
                );
                if (string.IsNullOrWhiteSpace(folderName)) return;

                var newFolderPath = $"{sftpVm.CurrentPath.TrimEnd('/')}/{folderName}";
                try
                {
                    await sftpVm.Service!.CreateDirectoryAsync(newFolderPath);
                    await LoadSftpDirectoryAsync(sftpVm, sftpVm.CurrentPath);
                    sftpVm.StatusText = $"Created folder '{folderName}'";
                }
                catch (Exception ex)
                {
                    sftpVm.StatusText = $"Failed to create folder: {ex.Message}";
                    MessageBox.Show($"Failed to create folder:\n{ex.Message}", "Create Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, _ => sftpVm.Service?.IsConnected == true);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // I) HELPER FOR FILE ICONS & SIZES (unchanged)
        // ─────────────────────────────────────────────────────────────────────────────

        private string GetFileIcon(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".txt" or ".log" => "📄 ",
                ".pdf" => "📕 ",
                ".doc" or ".docx" => "📘 ",
                ".xls" or ".xlsx" => "📗 ",
                ".zip" or ".rar" or ".7z" => "📦 ",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼 ",
                ".mp3" or ".wav" or ".flac" => "🎵 ",
                ".mp4" or ".avi" or ".mkv" => "🎬 ",
                ".exe" or ".msi" => "⚙️ ",
                ".sh" or ".bat" or ".cmd" => "📜 ",
                _ => "📄 "
            };
        }

        /// <summary>
        /// Convert an unsigned byte count into a human-readable string (B, KB, MB, GB, TB).
        /// </summary>
        private string FormatFileSize(ulong bytes)
        {
            if (bytes == 0UL)
                return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Convert a signed byte count into a human-readable string by forwarding to the ulong overload.
        /// Negative values are treated as zero.
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            // guard against negative long values
            return FormatFileSize((ulong)Math.Max(bytes, 0));
        }


        // ─────────────────────────────────────────────────────────────────────────────
        // J) INotifyPropertyChanged IMPLEMENTATION
        // ─────────────────────────────────────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    // ─────────────────────────────────────────────────────────────────────────────
    // 10) NetworkAdapterViewModel (unchanged)
    // ─────────────────────────────────────────────────────────────────────────────

    public class NetworkAdapterViewModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }


    // ─────────────────────────────────────────────────────────────────────────────
    // 11) RelayCommand<T> (unchanged)
    // ─────────────────────────────────────────────────────────────────────────────

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;
        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            var param = parameter is T ? (T?)parameter : default;
            return _canExecute == null || _canExecute(param);
        }

        public void Execute(object? parameter)
        {
            var param = parameter is T ? (T?)parameter : default;
            _execute(param);
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }


    // ─────────────────────────────────────────────────────────────────────────────
    // 12) SftpFileViewModel (unchanged)
    // ─────────────────────────────────────────────────────────────────────────────

    public class SftpFileViewModel
    {
        public SftpFile SftpFile { get; set; } = null!;
        public string DisplayName { get; set; } = "";
        public string Type { get; set; } = "";
        public string FormattedSize { get; set; } = "";
        public string FormattedDate { get; set; } = "";
        public string Permissions { get; set; } = "";
    }
}
