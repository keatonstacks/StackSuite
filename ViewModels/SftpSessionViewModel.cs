using StackSuite.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace StackSuite.ViewModels
{
    public class SftpSessionViewModel : SessionBaseViewModel
    {
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

        private bool _isTransferInProgress;
        public bool IsTransferInProgress
        {
            get => _isTransferInProgress;
            set
            {
                if (_isTransferInProgress != value)
                {
                    _isTransferInProgress = value;
                    OnPropertyChanged(nameof(IsTransferInProgress));
                    (CancelTransferCommand as RelayCommand<object?>)?.RaiseCanExecuteChanged();
                }
            }
        }

        private double _transferProgressPercent;
        public double TransferProgressPercent
        {
            get => _transferProgressPercent;
            set
            {
                if (Math.Abs(_transferProgressPercent - value) > 0.1)
                {
                    _transferProgressPercent = value;
                    OnPropertyChanged(nameof(TransferProgressPercent));
                }
            }
        }

        private string _transferStatusText = "";
        public string TransferStatusText
        {
            get => _transferStatusText;
            set
            {
                if (_transferStatusText != value)
                {
                    _transferStatusText = value;
                    OnPropertyChanged(nameof(TransferStatusText));
                }
            }
        }

        internal CancellationTokenSource? _transferCts;
        public ICommand CancelTransferCommand { get; set; }
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
            IsConnected = false;

            UpDirectoryCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            RefreshDirectoryCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            NavigateDirectoryCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            DownloadCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            UploadCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            DeleteCommand = new RelayCommand<object?>(async _ => { }, _ => false);
            CreateFolderCommand = new RelayCommand<object?>(async _ => { }, _ => false);

            CancelTransferCommand = new RelayCommand<object?>(
                _ => _transferCts?.Cancel(),
                _ => IsTransferInProgress
            );

        }

        public override Task DisconnectAsync()
        {
            IsConnected = false;
            Service?.Disconnect();
            Service = null;
            return Task.CompletedTask;
        }

    }

}
