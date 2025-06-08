using System.ComponentModel;
using System.Runtime.CompilerServices;

public class DeviceInfo : INotifyPropertyChanged
{
    private string _timeStamp = "";
    private string _displayName = "";
    private string _resolvedHost = "";
    private string _status = "";
    private string _openPorts = "";
    private string _latency = "";
    private string _ttl = "";
    private string _replyIP = "";
    private string _macAddress = "";
    private string _vendor = "";
    private string _deviceType = "";

    [DisplayName("Timestamp")]
    public string TimeStamp
    {
        get => _timeStamp;
        set => SetField(ref _timeStamp, value);
    }

    [DisplayName("IP Address")]
    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    [DisplayName("Resolved Host")]
    public string ResolvedHost
    {
        get => _resolvedHost;
        set => SetField(ref _resolvedHost, value);
    }

    [DisplayName("Status")]
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    [DisplayName("Open Ports")]
    public string OpenPorts
    {
        get => _openPorts;
        set => SetField(ref _openPorts, value);
    }

    [DisplayName("Latency")]
    public string Latency
    {
        get => _latency;
        set => SetField(ref _latency, value);
    }

    [DisplayName("TTL")]
    public string TTL
    {
        get => _ttl;
        set => SetField(ref _ttl, value);
    }

    [DisplayName("Reply IP")]
    public string ReplyIP
    {
        get => _replyIP;
        set => SetField(ref _replyIP, value);
    }

    [DisplayName("MAC Address")]
    public string MACAddress
    {
        get => _macAddress;
        set => SetField(ref _macAddress, value);
    }

    [DisplayName("Vendor")]
    public string Vendor
    {
        get => _vendor;
        set => SetField(ref _vendor, value);
    }

    [DisplayName("Device Type")]
    public string DeviceType
    {
        get => _deviceType;
        set => SetField(ref _deviceType, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}