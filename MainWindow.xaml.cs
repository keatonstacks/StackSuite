using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StackSuite.ViewModels;

namespace StackSuite
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();

            if (DataContext is MainWindowViewModel vm)
            {
                vm.SftpSessions.CollectionChanged += SftpSessions_CollectionChanged;
            }
        }

        private void SshSessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                SshPasswordInput.Password = "";
            }
        }

        private void SftpSessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                SftpPasswordInput.Password = "";
            }
        }

        private void SftpSessionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainWindowViewModel vm && e.NewValue is SftpSessionViewModel chosen)
            {
                vm.SelectedSftpSession = chosen;
            }
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
            MainTabControl.SelectedIndex = 1;
        }

        private void SshTreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var sshTab = MainTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => t.Header?.ToString().Contains("SSH") == true);

            if (sshTab != null)
                MainTabControl.SelectedItem = sshTab;
        }

        private void SftpTreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var sftpTab = MainTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => t.Header?.ToString().Contains("SFTP") == true);

            if (sftpTab != null)
                MainTabControl.SelectedItem = sftpTab;
        }

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

        // --- FIXED: Pattern matching ensures session is non-null for the switch ---
        private void SshTerminalBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((DataContext as MainWindowViewModel)?.SelectedSshSession is not SshSessionViewModel session)
                return;

            switch (e.Key)
            {
                case Key.Enter:
                    session.SendInput("\r");
                    e.Handled = true;
                    break;

                case Key.Back:
                    session.SendInput("\b");
                    e.Handled = true;
                    break;

                case Key.Tab:
                    session.SendInput("\t");
                    e.Handled = true;
                    break;

                case Key.Up:
                    session.SendInput("\x1B[A");
                    e.Handled = true;
                    break;

                case Key.Down:
                    session.SendInput("\x1B[B");
                    e.Handled = true;
                    break;

                case Key.Left:
                    session.SendInput("\x1B[D");
                    e.Handled = true;
                    break;

                case Key.Right:
                    session.SendInput("\x1B[C");
                    e.Handled = true;
                    break;

                case Key.C:
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        session.SendInput("\x03");
                        e.Handled = true;
                    }
                    break;
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