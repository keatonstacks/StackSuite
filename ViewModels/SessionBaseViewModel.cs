using System.ComponentModel;
using System.Windows.Input;

namespace StackSuite.ViewModels
{
    public abstract class SessionBaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public string ConnectionKey { get; set; } = "";
        public string TabTitle { get; set; } = "";

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); }
        }

        public ICommand CloseSessionCommand { get; }

        protected SessionBaseViewModel(Action<SessionBaseViewModel> removeCallback)
        {
            CloseSessionCommand = new RelayCommand<object?>(
                _ => removeCallback(this)
            );
        }
        public abstract Task DisconnectAsync();
    }
}
