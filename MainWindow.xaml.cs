using Microsoft.Win32;
using Renci.SshNet.Sftp;
using StackSuite.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace StackSuite
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<DeviceInfo> _results;
        private readonly ICollectionView _resultsView;
        private CancellationTokenSource? _cts;
        private readonly Dictionary<TabItem, SftpService> _sftpSessions = new();

        public MainWindow()
        {
            InitializeComponent();

            _results = new ObservableCollection<DeviceInfo>();
            _resultsView = CollectionViewSource.GetDefaultView(_results);
            _resultsView.Filter = FilterOffline;
            NetTesterResults.ItemsSource = _results;

            CancelButton.IsEnabled = false;

            var adapters = System.Net.NetworkInformation.NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(nic => new
                {
                    Id = nic.Id,
                    Name = $"{nic.Name} ({nic.Description})"
                })
                .ToList();

            InterfaceComboBox.ItemsSource = adapters;
            InterfaceComboBox.SelectedIndex = 0;

        }

        private void NetTesterResults_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var prop = typeof(DeviceInfo).GetProperty(e.PropertyName);
            if (prop != null)
            {
                var displayName = prop.GetCustomAttribute<DisplayNameAttribute>();
                if (displayName != null)
                    e.Column.Header = displayName.DisplayName;
            }
        }
        private void NetTesterRow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not DataGridRow row || row.Item is not DeviceInfo device)
                return;

            var vendor = device.Vendor?.ToLower() ?? "";
            var type = device.DeviceType?.ToLower() ?? "";
            var ports = device.OpenPorts?.Split(',').Select(p => p.Trim()).ToList() ?? new();

            bool isApple = vendor.Contains("apple") || vendor.Contains("mac");
            bool isWorkstationOrServer = type.Contains("workstation") || type.Contains("server");

            if (row.ContextMenu is not ContextMenu menu)
                return;

            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                string header = item.Header?.ToString() ?? "";

                if (header.Contains("RDP"))
                {
                    item.IsEnabled = ports.Contains("3389") ||
                                     (!isApple && isWorkstationOrServer);
                }
                else if (header.Contains("SSH"))
                {
                    item.IsEnabled = ports.Contains("22");
                }
                else if (header.Contains("Web"))
                {
                    item.IsEnabled = ports.Contains("80") || ports.Contains("443");
                }
                else if (header.Contains("UNC"))
                {
                    item.IsEnabled = isWorkstationOrServer && !isApple;
                }
            }
        }

        private void OpenInSsh_Click(object sender, RoutedEventArgs e)
        {
            if (NetTesterResults.SelectedItem is not DeviceInfo device)
                return;

            var host = !string.IsNullOrWhiteSpace(device.ResolvedHost) && device.ResolvedHost != "N/A"
                ? device.ResolvedHost
                : device.DisplayName;

            MessageBox.Show($"[Placeholder] Would launch SSH to: {host}", "SSH", MessageBoxButton.OK, MessageBoxImage.Information);

            // Future: Launch your SSH console or use PuTTY / Renci.SshNet
        }
        private void NetTesterResults_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (NetTesterResults.SelectedItems.Count == 0)
                    return;

                var properties = typeof(DeviceInfo).GetProperties()
                    .Where(p => p.CanRead)
                    .ToList();

                var lines = new List<string>();

                foreach (var item in NetTesterResults.SelectedItems)
                {
                    if (item is not DeviceInfo device)
                        continue;

                    var values = properties.Select(p =>
                        p.GetValue(device)?.ToString()?.Replace("\t", " ") ?? string.Empty);

                    lines.Add(string.Join("\t", values));
                }

                // Join all selected rows with newlines
                string output = string.Join(Environment.NewLine, lines);
                Clipboard.SetText(output);

                e.Handled = true;
            }
        }

        private void OpenUnc_Click(object sender, RoutedEventArgs e)
        {
            if (NetTesterResults.SelectedItem is not DeviceInfo device)
                return;

            var host = !string.IsNullOrWhiteSpace(device.ResolvedHost) && device.ResolvedHost != "N/A"
                ? device.ResolvedHost
                : device.DisplayName;

            var uncPath = $"\\\\{host}\\C$";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uncPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open UNC path {uncPath}.\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenInRdp_Click(object sender, RoutedEventArgs e)
        {
            if (NetTesterResults.SelectedItem is not DeviceInfo device)
                return;

            var host = !string.IsNullOrWhiteSpace(device.ResolvedHost) && device.ResolvedHost != "N/A"
                ? device.ResolvedHost
                : device.DisplayName;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "mstsc",
                    Arguments = $"/v:{host}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open RDP to {host}.\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenHttp_Click(object sender, RoutedEventArgs e)
        {
            if (NetTesterResults.SelectedItem is not DeviceInfo device)
                return;

            // Prefer resolved hostname if it's valid
            var host = !string.IsNullOrWhiteSpace(device.ResolvedHost) && device.ResolvedHost != "N/A"
                ? device.ResolvedHost
                : device.DisplayName;

            // Determine whether to use HTTP or HTTPS
            bool hasHttps = device.OpenPorts?.Contains("443") == true;
            string scheme = hasHttps ? "https" : "http";

            var url = $"{scheme}://{host}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open web UI for {host}.\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = (WindowState == WindowState.Maximized)
                          ? WindowState.Normal
                          : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e) =>
            Close();

        private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void TreeViewItem_ReadMe_Selected(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            string readmeUrl = "https://github.com/keatonstacks/StackSuite/blob/master/README.md"; // Adjust as needed

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = readmeUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open README online:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NetworkTestingTreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainTabControl.SelectedIndex = 1;
        }
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Prompt for a file path
            var dlg = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "NetworkScanResults.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var writer = new StreamWriter(dlg.FileName, false, Encoding.UTF8);

                // Use reflection to get headers from DisplayNameAttribute (or fall back to property name)
                var props = typeof(DeviceInfo).GetProperties();
                var headers = props
                    .Select(p => p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? p.Name);
                writer.WriteLine(string.Join(",", headers));

                // Write each DeviceInfo as CSV row
                foreach (var item in _results)
                {
                    var values = props.Select(p =>
                    {
                        var v = p.GetValue(item)?.ToString() ?? "";
                        // Escape quotes (" -> "") and wrap in quotes
                        return $"\"{v.Replace("\"", "\"\"")}\"";
                    });
                    writer.WriteLine(string.Join(",", values));
                }

                MessageBox.Show($"Exported {_results.Count} rows to:\n{dlg.FileName}",
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

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text or CSV|*.txt;*.csv|All Files|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            // Read all non-empty lines
            var entries = File.ReadAllLines(dlg.FileName)
                              .Select(l => l.Trim())
                              .Where(l => !string.IsNullOrWhiteSpace(l));

            // Kick off the scan just like ScanButton_Click
            ScanButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ScanProgressBar.Visibility = Visibility.Visible;
            _results.Clear();
            _cts = new CancellationTokenSource();

            try
            {
                await foreach (var info in NetTesterService.ScanHostsAsync(
                                        entries,
                                        options: null,
                                        cancellationToken: _cts.Token))
                {
                    _ = Dispatcher.BeginInvoke(
                        new Action(() => _results.Add(info)));
                }
            }
            catch (OperationCanceledException) { /* cancelled */ }
            finally
            {
                ScanProgressBar.Visibility = Visibility.Collapsed;
                ScanButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            // Read and trim the host/range input
            var hostOrIp = NetTesterHostIP.Text.Trim();

            // Pull the selected adapter’s Id (or null if none)
            var selectedAdapter = InterfaceComboBox.SelectedItem as dynamic;
            string? adapterId = selectedAdapter?.Id as string;

            // UI state: disable scan, enable cancel, show progress, clear results
            ScanButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ScanProgressBar.Visibility = Visibility.Visible;
            _results.Clear();

            _cts = new CancellationTokenSource();
            try
            {
                // Run on threadpool so we can await the IAsyncEnumerable
                await Task.Run(async () =>
                {
                    if (string.IsNullOrEmpty(hostOrIp))
                    {
                        // No IP input → discover local subnet on chosen adapter
                        await foreach (var info in NetTesterService.DiscoverDevicesAsync(
                                                adapterId,
                                                options: null,
                                                cancellationToken: _cts.Token))
                        {
                            _ = Dispatcher.BeginInvoke(
                                new Action(() => _results.Add(info)));
                        }
                    }
                    else
                    {
                        // IP or range provided → scan that host/range
                        await foreach (var info in NetTesterService.ScanAsyncEnumerable(
                                                hostOrIp,
                                                options: null,
                                                cancellationToken: _cts.Token))
                        {
                            _ = Dispatcher.BeginInvoke(
                                new Action(() => _results.Add(info)));
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // user cancelled
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Scan failed: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Restore UI
                ScanProgressBar.Visibility = Visibility.Collapsed;
                ScanButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            _cts?.Cancel();
            ScanProgressBar.Visibility = Visibility.Collapsed;
        }

        private bool FilterOffline(object obj)
        {
            if (obj is DeviceInfo di)
            {
                if (ShowOfflineToggle.IsChecked == true)
                    return true;

                return !string.Equals(
                    di.Status,
                    "Offline",
                    StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        private void SshSessionTree_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (SshSessionTree.SelectedItem is TreeViewItem item && item.Tag is TabItem tab)
            {
                SshSessionTabControl.SelectedItem = tab;
            }
        }

        private void SftpSessionTree_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (SftpSessionTree.SelectedItem is TreeViewItem item && item.Tag is TabItem tab)
            {
                SftpSessionTabControl.SelectedItem = tab;
            }
        }

        private void ShowOfflineToggle_Changed(object sender, RoutedEventArgs e)
        {
            _resultsView.Refresh();
        }

        private void ConnectSshSession_Click(object sender, RoutedEventArgs e)
        {
            string host = SshHostInput.Text.Trim();
            string user = SshUsernameInput.Text.Trim();
            string password = SshPasswordInput.Password.Trim(); // You can use this securely later

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
            {
                MessageBox.Show("Please enter both host and username for SSH.");
                return;
            }

            string title = $"SSH: {user}@{host}";

            var newTab = new TabItem
            {
                Header = title,
                HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeaderTemplate"),
                Content = new TextBox
                {
                    Text = $"[Placeholder] SSH session to {user}@{host}",
                    FontFamily = new FontFamily("Consolas"),
                    Background = Brushes.Black,
                    Foreground = Brushes.LightGreen,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(8),
                    Padding = new Thickness(10)
                }
            };

            SshSessionTabControl.Items.Add(newTab);
            SshSessionTabControl.SelectedItem = newTab;

            // Add to TreeView under parent node
            TreeViewItem rootNode;

            if (SshSessionTree.Items.Count == 0)
            {
                rootNode = new TreeViewItem
                {
                    Header = "📡 Active SSH Sessions",
                    IsExpanded = true
                };
                SshSessionTree.Items.Add(rootNode);
            }
            else
            {
                rootNode = SshSessionTree.Items[0] as TreeViewItem;
            }

            var sessionNode = new TreeViewItem
            {
                Header = $"{user}@{host}",
                Tag = newTab
            };

            rootNode?.Items.Add(sessionNode);
        }

        private async void ConnectSftpSession_Click(object sender, RoutedEventArgs e)
        {
            string host = SftpHostInput.Text.Trim();
            string user = SftpUsernameInput.Text.Trim();
            string password = SftpPasswordInput.Password.Trim();

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
            {
                MessageBox.Show("Please enter both host and username for SFTP.");
                return;
            }

            var sftpService = new SftpService();
            try
            {
                sftpService.Connect(host, 22, user, password);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SFTP connection failed: {ex.Message}");
                sftpService.Dispose();
                return;
            }

            // Modern DataGrid for file browser
            var fileGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = true,
                CanUserResizeColumns = true,
                CanUserSortColumns = true,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 8, 0, 0),
                Height = 320
            };

            fileGrid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            fileGrid.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new System.Windows.Data.Binding("Type"), Width = 100 });
            fileGrid.Columns.Add(new DataGridTextColumn { Header = "Size", Binding = new System.Windows.Data.Binding("Length"), Width = 100 });
            fileGrid.Columns.Add(new DataGridTextColumn { Header = "Modified", Binding = new System.Windows.Data.Binding("LastWriteTime"), Width = 180 });

            var pathBox = new TextBox { IsReadOnly = true, Margin = new Thickness(0, 8, 0, 0) };
            var statusText = new TextBlock { Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 0) };

            async Task LoadDir(string path)
            {
                try
                {
                    pathBox.Text = path;
                    var files = await sftpService.ListDirectoryAsync(path);
                    // Filter out "." and ".." and project to an anonymous type for the grid
                    var displayFiles = files
                        .Where(f => f.Name != "." && f.Name != "..")
                        .Select(f => new
                        {
                            f.Name,
                            Type = f.IsDirectory ? "Folder" : "File",
                            f.Length,
                            LastWriteTime = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            SftpFile = f // Keep reference for navigation/download
                        })
                        .ToList();

                    fileGrid.ItemsSource = displayFiles;
                    statusText.Text = $"Listed {displayFiles.Count} items";
                }
                catch (Exception ex)
                {
                    statusText.Text = $"Error: {ex.Message}";
                }
            }

            fileGrid.MouseDoubleClick += async (s, args) =>
            {
                var item = fileGrid.SelectedItem;
                if (item != null)
                {
                    var typeProp = item.GetType().GetProperty("Type");
                    var sftpFileProp = item.GetType().GetProperty("SftpFile");
                    if (typeProp != null && sftpFileProp != null)
                    {
                        var typeValue = typeProp.GetValue(item) as string;
                        if (typeValue == "Folder")
                        {
                            var sftpFile = sftpFileProp.GetValue(item) as SftpFile;
                            if (sftpFile != null)
                                await LoadDir(sftpFile.FullName);
                        }
                    }
                }
            };

            // Up button
            var upBtn = new Button { Content = "Up", Margin = new Thickness(0, 0, 8, 0) };
            upBtn.Click += async (s, args) =>
            {
                var parent = System.IO.Path.GetDirectoryName(pathBox.Text.TrimEnd('/')) ?? "/";
                await LoadDir(parent.Replace('\\', '/'));
            };

            // Download button
            var downloadBtn = new Button { Content = "Download", Margin = new Thickness(0, 0, 8, 0) };
            downloadBtn.Click += async (s, args) =>
            {
                var item = fileGrid.SelectedItem;
                if (item != null)
                {
                    var typeProp = item.GetType().GetProperty("Type");
                    var sftpFileProp = item.GetType().GetProperty("SftpFile");
                    if (typeProp != null && sftpFileProp != null)
                    {
                        var typeValue = typeProp.GetValue(item) as string;
                        if (typeValue == "File")
                        {
                            var file = sftpFileProp.GetValue(item) as SftpFile;
                            var dlg = new SaveFileDialog { FileName = file?.Name };
                            if (dlg.ShowDialog() == true && file != null)
                            {
                                await sftpService.DownloadFileAsync(file.FullName, dlg.FileName);
                                statusText.Text = $"Downloaded {file.Name}";
                            }
                        }
                    }
                }
            };

            // Upload button
            var uploadBtn = new Button { Content = "Upload" };
            uploadBtn.Click += async (s, args) =>
            {
                var dlg = new OpenFileDialog();
                if (dlg.ShowDialog() == true)
                {
                    string remotePath = pathBox.Text.TrimEnd('/') + "/" + System.IO.Path.GetFileName(dlg.FileName);
                    await sftpService.UploadFileAsync(dlg.FileName, remotePath);
                    await LoadDir(pathBox.Text);
                    statusText.Text = $"Uploaded {System.IO.Path.GetFileName(dlg.FileName)}";
                }
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            buttonPanel.Children.Add(upBtn);
            buttonPanel.Children.Add(downloadBtn);
            buttonPanel.Children.Add(uploadBtn);

            var contentPanel = new StackPanel();
            contentPanel.Children.Add(pathBox);
            contentPanel.Children.Add(fileGrid);
            contentPanel.Children.Add(buttonPanel);
            contentPanel.Children.Add(statusText);

            var newTab = new TabItem
            {
                Header = $"SFTP: {user}@{host}",
                HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeaderTemplate"),
                Content = contentPanel
            };

            SftpSessionTabControl.Items.Add(newTab);
            SftpSessionTabControl.SelectedItem = newTab;

            // Track the session for cleanup
            _sftpSessions[newTab] = sftpService;

            // Add to TreeView under parent node
            TreeViewItem rootNode;
            if (SftpSessionTree.Items.Count == 0)
            {
                rootNode = new TreeViewItem
                {
                    Header = "📁 Active SFTP Sessions",
                    IsExpanded = true
                };
                SftpSessionTree.Items.Add(rootNode);
            }
            else
            {
                rootNode = SftpSessionTree.Items[0] as TreeViewItem;
            }

            var sessionNode = new TreeViewItem
            {
                Header = $"{user}@{host}",
                Tag = newTab
            };
            rootNode?.Items.Add(sessionNode);

            // Initial directory load
            await LoadDir("/");
        }

        private void SshTreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainTabControl.SelectedItem = MainTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => t.Header?.ToString().Contains("SSH") == true);
        }

        private void SftpTreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainTabControl.SelectedItem = MainTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => t.Header?.ToString().Contains("SFTP") == true);
        }
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var tabItem = FindParent<TabItem>(button);
                var tabControl = FindParent<TabControl>(button);

                if (tabItem == null || tabControl == null)
                    return;

                // Prevent closing the static connection tab (index 0)
                if (tabControl.Items.IndexOf(tabItem) == 0)
                    return;

                // Remove corresponding TreeViewItem
                if (tabControl == SftpSessionTabControl)
                {
                    RemoveSessionTreeItem(SftpSessionTree, tabItem);
                    if (_sftpSessions.TryGetValue(tabItem, out var svc))
                    {
                        svc.Dispose();
                        _sftpSessions.Remove(tabItem);
                    }
                }
                else if (tabControl == SshSessionTabControl)
                {
                    RemoveSessionTreeItem(SshSessionTree, tabItem);
                }

                tabControl.Items.Remove(tabItem);
            }
        }
        private void RemoveSessionTreeItem(TreeView treeView, TabItem tab)
        {
            foreach (var root in treeView.Items.OfType<TreeViewItem>())
            {
                foreach (var child in root.Items.OfType<TreeViewItem>().ToList())
                {
                    if (child.Tag == tab)
                    {
                        root.Items.Remove(child);
                        return;
                    }
                }
            }
        }

        // Helper method to find parent of given type in visual tree
        public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typed) return typed;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

    }
}
