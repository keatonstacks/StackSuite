using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;
using System.Windows;

namespace StackSuite.Services
{
    public class SshService : IDisposable
    {
        private SshClient? _client;
        private ShellStream? _shellStream;
        private bool _isDisposed;

        public bool IsConnected => _client?.IsConnected == true;

        public void Connect(string host, int port, string username, string password)
        {
            Disconnect();

            _client = new SshClient(host, port, username, password);
            _client.HostKeyReceived += OnHostKeyReceived;
            _client.ErrorOccurred += OnErrorOccurred;

            _client.Connect();
            if (!_client.IsConnected)
                throw new InvalidOperationException("SSH client failed to connect.");
        }

        public void Disconnect()
        {
            _shellStream?.Dispose();
            _shellStream = null;

            if (_client != null)
            {
                if (_client.IsConnected)
                    _client.Disconnect();

                _client.Dispose();
                _client = null;
            }
        }

        public async Task<string> ExecuteCommandAsync(string commandText, int timeoutMillis = 30000)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SSH client is not connected.");

            return await Task.Run(() =>
            {
                using var cmd = _client.CreateCommand(commandText);
                cmd.CommandTimeout = TimeSpan.FromMilliseconds(timeoutMillis);

                var asyncResult = cmd.BeginExecute();
                while (!asyncResult.IsCompleted)
                {
                    Thread.Sleep(50);
                }
                cmd.EndExecute(asyncResult);

                var result = new StringBuilder();
                if (!string.IsNullOrEmpty(cmd.Result))
                    result.Append(cmd.Result);
                if (!string.IsNullOrEmpty(cmd.Error))
                    result.AppendLine().Append(cmd.Error);

                return result.ToString();
            });
        }

        public ShellStream CreateShellStream(
            string terminalName = "xterm",
            uint cols = 80,
            uint rows = 24,
            uint width = 800,
            uint height = 600)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SSH client is not connected.");

            _shellStream?.Dispose();
            _shellStream = _client.CreateShellStream(terminalName, cols, rows, width, height, 4096);

            return _shellStream;
        }

        public void SendToShell(string text)
        {
            if (_shellStream == null || !_shellStream.CanWrite)
                throw new InvalidOperationException("ShellStream is not initialized or not writable.");

            _shellStream.Write(text);
            _shellStream.Flush();
        }

        public string ReadFromShell(int maxLength = 1024)
        {
            if (_shellStream == null)
                throw new InvalidOperationException("ShellStream is not initialized.");
            if (!_shellStream.DataAvailable)
                return string.Empty;

            byte[] buffer = new byte[maxLength];
            int bytesRead = _shellStream.Read(buffer, 0, maxLength);

            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
#nullable disable
        private void OnErrorOccurred(object sender, ExceptionEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"SSH error: {e.Exception.Message}", "SSH Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void OnHostKeyReceived(object sender, HostKeyEventArgs e)
        {
            e.CanTrust = true;
        }
#nullable enable
        public void Dispose()
        {
            if (_isDisposed) return;

            Disconnect();
            _isDisposed = true;
        }
    }
}