using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StackSuite.Services
{
    public class SftpService : IDisposable
    {
        private SftpClient? _client;

        public bool IsConnected => _client?.IsConnected == true;

        public void Connect(string host, int port, string username, string password)
        {
            Disconnect();
            _client = new SftpClient(host, port, username, password);
            _client.Connect();
        }

        public void Disconnect()
        {
            if (_client != null)
            {
                if (_client.IsConnected)
                    _client.Disconnect();
                _client.Dispose();
                _client = null;
            }
        }

        public async Task<IList<SftpFile>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            return await Task.Run(() =>
                _client.ListDirectory(path)
                       .OfType<SftpFile>()
                       .ToList(), cancellationToken);
        }

        public async Task DownloadFileAsync(string remotePath, string localPath, IProgress<ulong>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            using var file = File.OpenWrite(localPath);
            await Task.Run(() =>
            {
                _client.DownloadFile(remotePath, file, bytes => progress?.Report(bytes));
            }, cancellationToken);
        }

        public async Task UploadFileAsync(string localPath, string remotePath, IProgress<ulong>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            using var file = File.OpenRead(localPath);
            await Task.Run(() =>
            {
                _client.UploadFile(file, remotePath, true, bytes => progress?.Report(bytes));
            }, cancellationToken);
        }

        public async Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            await Task.Run(() =>
            {
                _client.DeleteFile(remotePath);
            }, cancellationToken);
        }

        public async Task DeleteDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            await Task.Run(() =>
            {
                // Recursively delete all files and subdirectories
                var files = _client.ListDirectory(remotePath);
                foreach (var file in files)
                {
                    if (file.Name == "." || file.Name == "..")
                        continue;

                    if (file.IsDirectory)
                    {
                        DeleteDirectoryRecursive(file.FullName, cancellationToken);
                    }
                    else
                    {
                        _client.DeleteFile(file.FullName);
                    }
                }
                _client.DeleteDirectory(remotePath);
            }, cancellationToken);
        }

        private void DeleteDirectoryRecursive(string remotePath, CancellationToken cancellationToken)
        {
            var files = _client!.ListDirectory(remotePath);
            foreach (var file in files)
            {
                if (file.Name == "." || file.Name == "..")
                    continue;

                if (file.IsDirectory)
                {
                    DeleteDirectoryRecursive(file.FullName, cancellationToken);
                }
                else
                {
                    _client.DeleteFile(file.FullName);
                }
            }
            _client.DeleteDirectory(remotePath);
        }

        public async Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            if (_client == null || !_client.IsConnected)
                throw new InvalidOperationException("SFTP client is not connected.");

            await Task.Run(() =>
            {
                _client.CreateDirectory(remotePath);
            }, cancellationToken);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}