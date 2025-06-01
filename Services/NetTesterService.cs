using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace StackSuite.Services
{
    public class NetTesterOptions
    {
        public int PingTimeoutMs { get; set; } = 300;
        public int PortTimeoutMs { get; set; } = 300;
        public int MaxConcurrentScans { get; set; } = 20;
        public int ArpRetryCount { get; set; } = 3;
        public int ArpRetryDelayMs { get; set; } = 100;
        public IList<int> PortsToScan { get; set; } = new List<int> { 21, 22, 23, 80, 443 };
    }

    public class DeviceInfo
    {
        [DisplayName("Timestamp")] public string TimeStamp { get; set; } = string.Empty;
        [DisplayName("IP Address")] public string DisplayName { get; set; } = string.Empty;
        [DisplayName("Resolved Host")] public string ResolvedHost { get; set; } = string.Empty;
        [DisplayName("Status")] public string Status { get; set; } = string.Empty;
        [DisplayName("Open Ports")] public string OpenPorts { get; set; } = string.Empty;
        [DisplayName("Latency")] public string Latency { get; set; } = string.Empty;
        [DisplayName("TTL")] public string TTL { get; set; } = string.Empty;
        [DisplayName("Reply IP")] public string ReplyIP { get; set; } = string.Empty;
        [DisplayName("MAC Address")] public string MACAddress { get; set; } = string.Empty;
        [DisplayName("Vendor")] public string Vendor { get; set; } = string.Empty;
        [DisplayName("Device Type")] public string DeviceType { get; set; } = string.Empty;
    }

    public partial class NetTesterService
    {
        private static readonly Dictionary<string, string> _ouiMap;
        private static readonly Dictionary<string, string> _vendorTypeLookup;

        // 1) Compile-time regex for range parsing
        [GeneratedRegex(@"^(?<base>\d+\.\d+\.\d+\.)(?<start>\d+)-(?<end>\d+)$", RegexOptions.Compiled)]
        private static partial Regex RangeRegex();

        // 2) Compile-time regex to strip non-hex characters
        [GeneratedRegex(@"[^A-Fa-f0-9]")]
        private static partial Regex NonHexRegex();

        [LibraryImport("iphlpapi.dll")]
        public static partial int SendARP(int destIP, int srcIP, [Out] byte[] macAddress, ref int macAddressLength);

        static NetTesterService()
        {
            _ouiMap = new(StringComparer.OrdinalIgnoreCase);
            _vendorTypeLookup = new(StringComparer.OrdinalIgnoreCase);
            LoadOuiDatabase();
            LoadVendorTypeMappings();
        }

        private static void LoadOuiDatabase()
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                          .FirstOrDefault(n => n.EndsWith("oui.csv", StringComparison.OrdinalIgnoreCase))
                       ?? throw new FileNotFoundException("Embedded oui.csv not found.");
            using var stream = asm.GetManifestResourceStream(name)!;
            using var rdr = new StreamReader(stream);
            while (!rdr.EndOfStream)
            {
                var line = rdr.ReadLine();
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("Registry", StringComparison.OrdinalIgnoreCase))
                    continue;

                var cols = line.Split(',');
                if (cols.Length < 3) continue;

                var prefix = cols[1].Trim().Replace("-", "").ToUpperInvariant();
                var vendor = cols[2].Trim('"');
                if (!_ouiMap.ContainsKey(prefix))
                    _ouiMap[prefix] = vendor;
            }
        }

        private static void LoadVendorTypeMappings()
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                          .FirstOrDefault(n => n.EndsWith("VendorMappings.xml", StringComparison.OrdinalIgnoreCase))
                       ?? throw new FileNotFoundException("Embedded VendorMappings.xml not found.");
            using var stream = asm.GetManifestResourceStream(name)!;
            var doc = XDocument.Load(stream);
            foreach (var elem in doc.Root!.Elements("Vendor"))
            {
                var key = (string?)elem.Attribute("name");
                var type = (string?)elem.Attribute("type");
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(type))
                    _vendorTypeLookup[key!] = type!;
            }
        }

        public static async IAsyncEnumerable<DeviceInfo> ScanAsyncEnumerable(
            string hostEntry,
            NetTesterOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            options ??= new NetTesterOptions();
            var targets = ParseHostEntries(hostEntry).ToList();
            var semaphore = new SemaphoreSlim(options.MaxConcurrentScans);
            var tasks = new List<Task<DeviceInfo>>();

            foreach (var ip in targets)
            {
                await semaphore.WaitAsync(cancellationToken);
                tasks.Add(Task.Run(async () =>
                {
                    try { return await ProcessHostAsync(ip, options, cancellationToken); }
                    finally { semaphore.Release(); }
                }, cancellationToken));
            }

            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks);
                tasks.Remove(done);
                yield return await done;
            }
        }

        private static IEnumerable<string> ParseHostEntries(string entry)
        {
            entry = entry.Trim();
            var m = RangeRegex().Match(entry);
            if (m.Success
                && int.TryParse(m.Groups["start"].Value, out var s)
                && int.TryParse(m.Groups["end"].Value, out var e))
            {
                var baseIp = m.Groups["base"].Value;
                return Enumerable.Range(s, e - s + 1).Select(i => baseIp + i);
            }
            return new[] { entry };
        }

        private static async Task<DeviceInfo> ProcessHostAsync(
            string ip,
            NetTesterOptions options,
            CancellationToken cancellationToken)
        {
            var info = new DeviceInfo
            {
                TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                DisplayName = ip,
                ResolvedHost = "N/A",
                Status = "Offline",
                Latency = string.Empty,
                TTL = string.Empty,
                ReplyIP = string.Empty,
                OpenPorts = string.Empty
            };

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, options.PingTimeoutMs);
                if (reply.Status != IPStatus.Success)
                    return info;

                info.Status = "Online";
                info.Latency = reply.RoundtripTime + " ms";
                info.TTL = reply.Options?.Ttl.ToString() ?? string.Empty;
                info.ReplyIP = reply.Address.ToString();
                try
                {
                    info.ResolvedHost = (await Dns.GetHostEntryAsync(reply.Address)).HostName;
                }
                catch { }

                var portTasks = options.PortsToScan
                    .Select(p => IsTcpPortOpen(ip, p, options.PortTimeoutMs, cancellationToken)
                                 .ContinueWith(t => (Port: p, Open: t.Result), cancellationToken));
                var results = await Task.WhenAll(portTasks);
                var open = results.Where(r => r.Open).Select(r => r.Port);
                info.OpenPorts = open.Any() ? string.Join(", ", open) : string.Empty;

                info.MACAddress = await Task.Run(() => GetRemoteMacAddress(ip, options));
                info.Vendor = GetVendor(info.MACAddress);
                info.DeviceType = GetDeviceType(info.Vendor);
            }
            catch (OperationCanceledException)
            {
                info.Status = "Canceled";
            }
            catch (Exception ex)
            {
                info.Status = "Error";
                info.Latency = ex.Message;
            }

            return info;
        }

        private static string GetRemoteMacAddress(string ipAddress, NetTesterOptions options)
        {
            try
            {
                ForceArpEntry(ipAddress);
                var addr = IPAddress.Parse(ipAddress);
                var dest = BitConverter.ToInt32(addr.GetAddressBytes(), 0);
                var mac = new byte[6];
                var len = mac.Length;

                for (int i = 0; i < options.ArpRetryCount; i++)
                {
                    if (SendARP(dest, 0, mac, ref len) == 0 && len > 0)
                        break;
                    Thread.Sleep(options.ArpRetryDelayMs);
                }

                return len > 0
                    ? BitConverter.ToString(mac, 0, len)
                    : "N/A";
            }
            catch { return "N/A"; }
        }

        private static void ForceArpEntry(string ipAddress)
        {
            try
            {
                using var udp = new UdpClient();
                udp.Connect(ipAddress, 1);
                udp.Send(Array.Empty<byte>(), 0);
            }
            catch { }
        }

        private static async Task<bool> IsTcpPortOpen(
            string host,
            int port,
            int timeout,
            CancellationToken ct)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                var result = await Task.WhenAny(task, Task.Delay(timeout, ct));
                return result == task && client.Connected;
            }
            catch { return false; }
        }

        public static async IAsyncEnumerable<DeviceInfo> ScanHostsAsync(
            IEnumerable<string> hostEntries,
            NetTesterOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            options ??= new NetTesterOptions();

            // 1) Expand ranges and dedupe
            var targets = hostEntries
                .SelectMany(e => ParseHostEntries(e))
                .Distinct()
                .ToList();

            // 2) Bounded parallelism
            var semaphore = new SemaphoreSlim(options.MaxConcurrentScans);
            var tasks = new List<Task<DeviceInfo>>();

            foreach (var ip in targets)
            {
                await semaphore.WaitAsync(cancellationToken);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        return await ProcessHostAsync(ip, options, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            // 3) Yield as completed
            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks);
                tasks.Remove(done);
                yield return await done;
            }
        }

        private static string GetVendor(string mac)
        {
            var cleaned = NonHexRegex().Replace(mac, "").ToUpperInvariant();
            if (cleaned.Length < 6) return "N/A";
            var oui = cleaned[..6];
            return _ouiMap.TryGetValue(oui, out var v) ? v : "Unknown Vendor";
        }

        private static string GetDeviceType(string vendor)
        {
            foreach (var kvp in _vendorTypeLookup)
                if (vendor.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            return "Unknown";
        }

        /// <summary>
        /// Discover all hosts on the selected adapter's IPv4 subnets.
        /// </summary>
        public static async IAsyncEnumerable<DeviceInfo> DiscoverDevicesAsync(
    string? adapterId,
    NetTesterOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            options ??= new NetTesterOptions();

            // 1) Build list of all target IP strings
            var hosts = new List<string>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    (string.IsNullOrEmpty(adapterId) || nic.Id == adapterId));

            foreach (var nic in interfaces)
            {
                var props = nic.GetIPProperties();
                foreach (var uni in props.UnicastAddresses
                            .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork
                                     && u.IPv4Mask != null))
                {
                    var ipBytes = uni.Address.GetAddressBytes();
                    var maskBytes = uni.IPv4Mask.GetAddressBytes();

                    // Convert to uint in network byte order
                    uint ipInt = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(ipBytes, 0));
                    uint maskInt = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(maskBytes, 0));
                    uint network = ipInt & maskInt;
                    uint broadcast = network | ~maskInt;

                    // Skip network and broadcast addresses
                    for (uint x = network + 1; x < broadcast; x++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var addrBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)x));
                        hosts.Add(new IPAddress(addrBytes).ToString());
                    }
                }
            }

            // 2) Scan them with bounded concurrency
            var semaphore = new SemaphoreSlim(options.MaxConcurrentScans);
            var tasks = new List<Task<DeviceInfo>>();

            foreach (var host in hosts)
            {
                await semaphore.WaitAsync(cancellationToken);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // reuse your existing logic
                        return await ProcessHostAsync(host, options, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            // 3) Yield results as they complete
            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks);
                tasks.Remove(done);
                yield return await done;
            }
        }
    }
}
