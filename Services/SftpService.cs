using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.IO;

namespace StackSuite.Services
{
    public interface ISftpService : IDisposable
    {
        bool IsConnected { get; }
        void Connect(string host, int port, string username, string password);
        Task ConnectAsync(string host, int port, string username, string password, CancellationToken cancellationToken = default);
        void Disconnect();
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        Task<IList<SftpFile>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);
        Task DownloadFileAsync(string remotePath, string localPath, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default);
        Task UploadFileAsync(string localPath, string remotePath, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default);
        Task DeleteDirectoryAsync(string remotePath, CancellationToken cancellationToken = default);
        Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default);
    }

    public class ProgressReport
    {
        public ulong TotalBytes { get; init; }
        public ulong BytesTransferred { get; init; }
        public double ThroughputBytesPerSecond { get; init; }
        public TimeSpan Elapsed { get; init; }
    }

    internal class ProgressTracker
    {
        private readonly ulong _totalBytes;
        private readonly DateTime _start;
        private DateTime _lastTime;
        private ulong _lastBytes;

        public ProgressTracker(ulong totalBytes)
        {
            _totalBytes = totalBytes;
            _start = _lastTime = DateTime.UtcNow;
            _lastBytes = 0;
        }

        public ProgressReport Report(ulong transferred)
        {
            var now = DateTime.UtcNow;
            var deltaTime = (now - _lastTime).TotalSeconds;
            var deltaBytes = transferred - _lastBytes;
            var rate = deltaTime > 0 ? deltaBytes / deltaTime : 0;

            _lastTime = now;
            _lastBytes = transferred;

            return new ProgressReport
            {
                TotalBytes = _totalBytes,
                BytesTransferred = transferred,
                ThroughputBytesPerSecond = rate,
                Elapsed = now - _start
            };
        }
    }

    public class SftpService : ISftpService
    {
        private SftpClient? _client;
        private readonly ILogger<SftpService> _logger;
        public SftpService()
            : this(NullLogger<SftpService>.Instance) { }
        public SftpService(ILogger<SftpService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsConnected => _client?.IsConnected == true;

        public void Connect(string host, int port, string username, string password)
            => ConnectAsync(host, port, username, password, CancellationToken.None)
               .GetAwaiter().GetResult();

        public async Task ConnectAsync(string host, int port, string username, string password, CancellationToken cancellationToken = default)
        {
            await DisconnectAsync(cancellationToken).ConfigureAwait(false);
            _client = new SftpClient(host, port, username, password);

            try
            {
                await Task.Run(() => _client.Connect(), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("SFTP connected to {Host}:{Port}", host, port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SFTP {Host}:{Port}", host, port);
                throw;
            }
        }

        public void Disconnect()
            => DisconnectAsync(CancellationToken.None)
               .GetAwaiter().GetResult();

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null) return;

            try
            {
                await Task.Run(() =>
                {
                    if (_client.IsConnected) _client.Disconnect();
                    _client.Dispose();
                    _client = null;
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("SFTP disconnected cleanly.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during SFTP disconnect.");
            }
        }

        public void Dispose()
        {
            if (_client != null && _client.IsConnected)
                _client.Disconnect();
            _client?.Dispose();
            _client = null;
        }

        public async Task<IList<SftpFile>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return _client!
                        .ListDirectory(path)
                        .OfType<SftpFile>()
                        .ToList();
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list directory '{Path}'", path);
                throw;
            }
        }

        public async Task DownloadFileAsync(
        string remotePath,
        string localPath,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            try
            {
                var attrs = _client!.GetAttributes(remotePath);
                var tracker = new ProgressTracker((ulong)attrs.Size);

                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                using var fs = File.OpenWrite(localPath);
                await Task.Run(() =>
                {
                    _client.DownloadFile(remotePath, fs, bytes =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(tracker.Report((ulong)bytes));
                    });
                }, cancellationToken).ConfigureAwait(false);

                double sizeMb = attrs.Size / (1024d * 1024d);
                _logger.LogInformation(
                    "Downloaded '{Remote}' → '{Local}' ({SizeMb:0.##} MB)",
                    remotePath,
                    localPath,
                    sizeMb
                );
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Download of '{Remote}' was canceled.", remotePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading '{Remote}'", remotePath);
                throw;
            }
        }


        public async Task UploadFileAsync(
        string localPath,
        string remotePath,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
            {
                EnsureConnected();
                try
                {
                    using var fs = File.OpenRead(localPath);
                    var tracker = new ProgressTracker((ulong)fs.Length);

                    await Task.Run(() =>
                    {
                        _client!.UploadFile(fs, remotePath, true, bytes =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            progress?.Report(tracker.Report((ulong)bytes));
                        });
                    }, cancellationToken).ConfigureAwait(false);

                    double sizeMb = fs.Length / (1024d * 1024d);
                    _logger.LogInformation(
                        "Uploaded '{Local}' → '{Remote}' ({SizeMb:0.##} MB)",
                        localPath,
                        remotePath,
                        sizeMb
                    );
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Upload of '{Local}' was canceled.", localPath);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading '{Local}'", localPath);
                    throw;
                }
            }


        public async Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _client!.DeleteFile(remotePath);
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Deleted file '{Remote}'", remotePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file '{Remote}'", remotePath);
                throw;
            }
        }

        public async Task DeleteDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            try
            {
                await Task.Run(() =>
                {
                    RecursiveDelete(remotePath, cancellationToken);
                    _client!.DeleteDirectory(remotePath);
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Deleted directory '{Remote}'", remotePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting directory '{Remote}'", remotePath);
                throw;
            }
        }

        private void RecursiveDelete(string path, CancellationToken cancellationToken)
        {
            foreach (var entry in _client!.ListDirectory(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Name is "." or "..") continue;

                if (entry.IsDirectory)
                    RecursiveDelete(entry.FullName, cancellationToken);
                else
                    _client.DeleteFile(entry.FullName);
            }
        }

        public async Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _client!.CreateDirectory(remotePath);
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Created directory '{Remote}'", remotePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating directory '{Remote}'", remotePath);
                throw;
            }
        }

        private void EnsureConnected()
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");
        }
    }
}
