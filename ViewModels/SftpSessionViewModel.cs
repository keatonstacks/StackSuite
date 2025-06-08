// SftpSessionViewModel.cs (revamped)
// ==================================
// **Changelog:**
// 1. Removed shadowed ConnectionKey, TabTitle, IsConnected members.
// 2. Utilizes inherited properties from SessionBaseViewModel.
// 3. Initial commands are disabled until Service.IsConnected is true.

using Renci.SshNet.Sftp;
using StackSuite.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace StackSuite.ViewModels
{
    /// <summary>
    /// Represents an active SFTP session. Inherits close logic from SessionBaseViewModel.
    /// </summary>
    public class SftpSessionViewModel : SessionBaseViewModel
    {
        // =================================================================================
        // Inherited Members (no longer shadowed):
        //   string ConnectionKey { get; set; }
        //   string TabTitle       { get; set; }
        //   bool   IsConnected    { get; set; }
        // =================================================================================

        private string _currentPath = "/";
        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (_currentPath != value)
                {
                    _currentPath = value;
                    OnPropertyChanged(nameof(CurrentPath));
                }
            }
        }

        public ObservableCollection<SftpFileViewModel> DirectoryItems { get; } = new();

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private SftpService? _service;
        public SftpService? Service
        {
            get => _service;
            set
            {
                if (_service != value)
                {
                    _service = value;
                    OnPropertyChanged(nameof(Service));
                }
            }
        }

        // Commands will be wired up after connection succeeds
        public ICommand UpDirectoryCommand { get; set; }
        public ICommand RefreshDirectoryCommand { get; set; }
        public ICommand NavigateDirectoryCommand { get; set; }
        public ICommand DownloadCommand { get; set; }
        public ICommand UploadCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand CreateFolderCommand { get; set; }

        public SftpSessionViewModel(Action<SessionBaseViewModel> removeCallback)
            : base(removeCallback)
        {
            // Start disconnected: close button disabled until IsConnected = true
            IsConnected = false;

            // Initialize commands disabled by default
            UpDirectoryCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            RefreshDirectoryCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            NavigateDirectoryCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            DownloadCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            UploadCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            DeleteCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            CreateFolderCommand = new RelayCommand<object?>(async _ => { }, _ => false);
        }

        public override Task DisconnectAsync()
        {
            // Mark disconnected and call Dispose on service
            IsConnected = false;
            Service?.Disconnect();
            Service = null;
            return Task.CompletedTask;
        }
    }
}
