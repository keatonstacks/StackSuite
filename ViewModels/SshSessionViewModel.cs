// SshSessionViewModel.cs (revamped with input readiness check)
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using StackSuite.Services;

namespace StackSuite.ViewModels
{
    public class SshSessionViewModel : SessionBaseViewModel
    {
        // =================================================================================
        // Inherited Members (no longer shadowed):
        //   string ConnectionKey { get; set; }
        //   string TabTitle       { get; set; }
        //   bool   IsConnected    { get; set; }
        // =================================================================================

        private bool _shellReady;
        /// <summary>
        /// Indicates when the shell stream has been created and is writable.
        /// </summary>
        public bool ShellReady
        {
            get => _shellReady;
            private set
            {
                if (_shellReady != value)
                {
                    _shellReady = value;
                    OnPropertyChanged(nameof(ShellReady));
                }
            }
        }

        private string _terminalContent = string.Empty;
        public string TerminalContent
        {
            get => _terminalContent;
            set
            {
                if (_terminalContent != value)
                {
                    _terminalContent = value;
                    OnPropertyChanged(nameof(TerminalContent));
                }
            }
        }

        private CancellationTokenSource? _shellReadCts;

        public ObservableCollection<SshSessionViewModel> SubSessions { get; } = new();

        public SshService? Service { get; set; }

        public SshSessionViewModel(Action<SessionBaseViewModel> removeCallback)
            : base(removeCallback)
        {
            IsConnected = true;
            ShellReady = false;
        }

        /// <summary>
        /// Sends user input to the shell only if ready.
        /// </summary>
        public void SendInput(string input)
        {
            if (!ShellReady || Service == null || !Service.IsConnected)
                return;

            try
            {
                Service.SendToShell(input);
            }
            catch
            {
                // Suppress errors when shell isn't ready
            }
        }

        public override Task DisconnectAsync()
        {
            IsConnected = false;
            ShellReady = false;
            _shellReadCts?.Cancel();
            Service?.Disconnect();
            Service = null;
            return Task.CompletedTask;
        }

        public async Task StartInteractiveShellAsync(string host, string username, string password)
        {
            if (Service != null && Service.IsConnected)
                return;

            var svc = new SshService();
            svc.Connect(host, 22, username, password);
            Service = svc;

            // Create the shell stream and mark ready
            Service.CreateShellStream();
            ShellReady = true;

            // Start reading output
            _shellReadCts = new CancellationTokenSource();
            _ = Task.Run(() => ReadShellLoop(_shellReadCts.Token));
            await Task.CompletedTask;
        }

        private async Task ReadShellLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && Service != null && Service.IsConnected)
            {
                try
                {
                    string output = Service.ReadFromShell();
                    if (!string.IsNullOrEmpty(output))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            TerminalContent += output;
                        });
                    }
                    await Task.Delay(50, token);
                }
                catch
                {
                    // suppressed
                }
            }
        }
    }
}
