using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using StackSuite.Services;

namespace StackSuite
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<DeviceInfo> _results;
        private readonly ICollectionView _resultsView;
        private CancellationTokenSource? _cts;

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
                .Select(nic => new {
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

        private void ShowOfflineToggle_Changed(object sender, RoutedEventArgs e)
        {
            _resultsView.Refresh();
        }
    }
}
