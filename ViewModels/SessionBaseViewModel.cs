// SessionBaseViewModel.cs (updated setters)
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace StackSuite.ViewModels
{
    /// <summary>
    /// A base class for an active connection (SSH or SFTP).
    /// </summary>
    public abstract class SessionBaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// A unique key for this session (e.g. "user@host").
        /// </summary>
        // Changed setter to public so external initializers can assign
        public string ConnectionKey { get; set; } = "";

        /// <summary>
        /// The display name for the tab header (e.g. "SFTP: user@host").
        /// </summary>
        // Changed setter to public so external initializers can assign
        public string TabTitle { get; set; } = "";

        /// <summary>
        /// Indicates whether the session is still connected.
        /// </summary>
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); }
        }

        /// <summary>
        /// Command to close (dispose) this session.
        /// </summary>
        public ICommand CloseSessionCommand { get; }

        protected SessionBaseViewModel(Action<SessionBaseViewModel> removeCallback)
        {
            // When CloseSessionCommand executes, it will invoke removeCallback(this)
            CloseSessionCommand = new RelayCommand<object?>(
                _ => removeCallback(this)
            );
        }

        /// <summary>
        /// Call this to clean up any resources (e.g. Disconnect/Dispose).
        /// </summary>
        public abstract Task DisconnectAsync();
    }
}
