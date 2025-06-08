using Microsoft.Win32;
using System;
using System.Collections.Specialized;          // ← for NotifyCollectionChangedEventArgs
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using StackSuite.Services;
using StackSuite.ViewModels;

namespace StackSuite
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 1) Set DataContext
            DataContext = new MainWindowViewModel();

            // 2) Subscribe to SftpSessions.CollectionChanged so we can clear the PasswordBox
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SftpSessions.CollectionChanged += SftpSessions_CollectionChanged;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // A) When a new SFTP session is added, clear the visible PasswordBox
        // ─────────────────────────────────────────────────────────────────────────────
        private void SshSessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                SshPasswordInput.Password = "";
            }
        }

        private void SftpSessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Only act when items are added
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // Clear the PasswordBox so that 'Connect' can be clicked again
                SftpPasswordInput.Password = "";
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // B) TreeView selection → switch to the correct tab
        // ─────────────────────────────────────────────────────────────────────────────
        private void SftpSessionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainWindowViewModel vm && e.NewValue is SftpSessionViewModel chosen)
            {
                vm.SelectedSftpSession = chosen;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 1) Net Tester DataGrid: Auto‐generate column headers via DisplayNameAttribute
        // ─────────────────────────────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────────────────────────────
        // 2) Net Tester DataGrid: Enable/disable context‐menu items based on DeviceInfo
        // ─────────────────────────────────────────────────────────────────────────────
        private void NetTesterRow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not DataGridRow row || row.Item is not DeviceInfo device)
                return;

            var vendor = device.Vendor?.ToLower() ?? "";
            var type = device.DeviceType?.ToLower() ?? "";
            var ports = device.OpenPorts?
                             .Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
                             .ToList()
                          ?? new();

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

        // ─────────────────────────────────────────────────────────────────────────────
        // 3) Window‐Chrome / Title Bar Handlers (unchanged)
        // ─────────────────────────────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────────────────────────────
        // 4) "Home" TreeView Navigation Handlers (unchanged)
        // ─────────────────────────────────────────────────────────────────────────────
        private void TreeViewItem_ReadMe_Selected(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            string readmeUrl = "https://github.com/keatonstacks/StackSuite/blob/master/README.md";
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
            // Switch to “Network Tester” tab (index 1)
            MainTabControl.SelectedIndex = 1;
        }

        private void SshTreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Find the first TabItem whose Header contains “SSH”
            var sshTab = MainTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => t.Header?.ToString().Contains("SSH") == true);

            if (sshTab != null)
                MainTabControl.SelectedItem = sshTab;
        }

        private void SftpTreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Find the first TabItem whose Header contains “SFTP”
            var sftpTab = MainTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => t.Header?.ToString().Contains("SFTP") == true);

            if (sftpTab != null)
                MainTabControl.SelectedItem = sftpTab;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 5) SFTP / SSH PasswordBox: keep ViewModel.SftpPassword in sync
        // ─────────────────────────────────────────────────────────────────────────────
        private void SftpPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && sender is PasswordBox pb)
            {
                vm.SftpPassword = pb.Password;
            }
        }
        private void SshPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && sender is PasswordBox pb)
            {
                vm.NewSshPassword = pb.Password;
            }
        }
        // ─────────────────────────────────────────────────────────────────────────────
        // 6) Helper Method: Find a parent of a given type in the Visual Tree
        // ─────────────────────────────────────────────────────────────────────────────
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
        private void SshTerminalBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var vm = (DataContext as MainWindowViewModel)?.SelectedSshSession;
            if (vm == null) return;

            switch (e.Key)
            {
                case Key.Enter:
                    vm.SendInput("\r");
                    e.Handled = true;
                    break;

                case Key.Back:
                    // send a true backspace
                    vm.SendInput("\b");
                    e.Handled = true;
                    break;

                case Key.Tab:
                    vm.SendInput("\t");
                    e.Handled = true;
                    break;

                case Key.Up:
                    vm.SendInput("\x1B[A");
                    e.Handled = true;
                    break;

                case Key.Down:
                    vm.SendInput("\x1B[B");
                    e.Handled = true;
                    break;

                case Key.Left:
                    vm.SendInput("\x1B[D");
                    e.Handled = true;
                    break;

                case Key.Right:
                    vm.SendInput("\x1B[C");
                    e.Handled = true;
                    break;

                case Key.C:
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        vm.SendInput("\x03"); // Ctrl+C
                        e.Handled = true;
                    }
                    break;

                    // Let SPACE fall through into PreviewTextInput
            }
        }
        private void SshSessionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainWindowViewModel vm && e.NewValue is SshSessionViewModel sshVm)
            {
                vm.SelectedSshSession = sshVm;
            }
        }

        private void SshTerminalBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            var session = vm?.SelectedSshSession;
            if (session == null)
                return;

            session.SendInput(e.Text);
            e.Handled = true;
        }
    }
}
