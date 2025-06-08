using Renci.SshNet;
using Renci.SshNet.Common;
using StackSuite.ViewModels;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static System.Net.Mime.MediaTypeNames;

namespace StackSuite.Services
{
    /// <summary>
    /// A service for SSH connections. Supports both one-off command execution and an interactive shell.
    /// </summary>
    public class SshService : IDisposable
    {
        private SshClient? _client;
        private ShellStream? _shellStream;
        private bool _isDisposed = false;

        /// <summary>
        /// Indicates whether the underlying SSH client is connected.
        /// </summary>
        public bool IsConnected => _client?.IsConnected == true;

        /// <summary>
        /// Establishes a new SSH connection (username/password).
        /// </summary>
        /// <param name="host">Hostname or IP</param>
        /// <param name="port">Port (usually 22)</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        public void Connect(string host, int port, string username, string password)
        {
            Disconnect(); // in case a previous connection existed

            _client = new SshClient(host, port, username, password);
            _client.HostKeyReceived += OnHostKeyReceived;
            _client.ErrorOccurred += OnErrorOccurred;

            _client.Connect();
            if (!_client.IsConnected)
                throw new InvalidOperationException("SSH client failed to connect.");
        }

        /// <summary>
        /// Disconnects and disposes any existing SSH client/shell.
        /// </summary>
        public void Disconnect()
        {
            if (_shellStream != null)
            {
                _shellStream.Dispose();
                _shellStream = null;
            }

            if (_client != null)
            {
                if (_client.IsConnected)
                    _client.Disconnect();

                _client.Dispose();
                _client = null;
            }
        }

        /// <summary>
        /// Executes a single command on the remote host and returns the full output.
        /// </summary>
        /// <param name="commandText">The command to run (e.g. "ls -la")</param>
        /// <param name="timeoutMillis">How long to wait (in milliseconds) before timing out.</param>
        /// <returns>Standard output (and standard error concatenated).</returns>
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

                // Combine both stdout and stderr:
                var result = new StringBuilder();
                if (!string.IsNullOrEmpty(cmd.Result))
                    result.Append(cmd.Result);
                if (!string.IsNullOrEmpty(cmd.Error))
                    result.AppendLine().Append(cmd.Error);

                return result.ToString();
            });
        }

        /// <summary>
        /// Opens an interactive shell stream. Caller can Read() and Write() to the returned ShellStream.
        /// </summary>
        /// <param name="terminalName">e.g. "xterm" or "vt100"</param>
        /// <param name="cols">Number of columns</param>
        /// <param name="rows">Number of rows</param>
        /// <param name="width">Pixel width (optional)</param>
        /// <param name="height">Pixel height (optional)</param>
        /// <returns>A ShellStream if successful.</returns>
        public ShellStream CreateShellStream(
            string terminalName = "xterm",
            uint cols = 80,
            uint rows = 24,
            uint width = 800,
            uint height = 600)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SSH client is not connected.");

            // If there's already an open shell, dispose it first:
            _shellStream?.Dispose();

            // Create a new shell stream with requested dimensions:
            _shellStream = _client.CreateShellStream(terminalName, cols, rows, width, height, 4096);

            return _shellStream;
        }

        /// <summary>
        /// Writes raw text (including newline if desired) to the interactive shell.
        /// </summary>
        /// <param name="text">Text to send, e.g. "ls -l\n"</param>
        public void SendToShell(string text)
        {
            if (_shellStream == null || !_shellStream.CanWrite)
            {
                // Optionally, log or notify the user/UI
                throw new InvalidOperationException("ShellStream is not initialized or not writable.");
            }

            _shellStream.Write(text);
            _shellStream.Flush();
        }
        /// <summary>
        /// Attempts to read up to <paramref name="maxLength"/> bytes of available data from the shell,
        /// returning whatever is currently buffered. Non-blocking.
        /// </summary>
        /// <param name="maxLength">Maximum number of bytes to read.</param>
        /// <returns>The text that was available to read.</returns>
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

        /// <summary>
        /// Event raised if the SSH client reports an error.
        /// </summary>
        private void OnErrorOccurred(object sender, ExceptionEventArgs e)
        {
            // You can log or rethrow here. For now, just rethrow on the UI thread if needed:
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"SSH error: {e.Exception.Message}", "SSH Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        /// <summary>
        /// Event raised on host key validation. Currently accepts any key.
        /// For production, you should verify against a known fingerprint.
        /// </summary>
        private void OnHostKeyReceived(object sender, HostKeyEventArgs e)
        {
            // e.CanTrust = true; // accept all host keys for now
            // In a more secure scenario, compare e.HostKey with a stored fingerprint and set e.CanTrust accordingly.
            e.CanTrust = true;
        }

        /// <summary>
        /// Disposes the client and shell.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            Disconnect();
            _isDisposed = true;
        }
    }
}
