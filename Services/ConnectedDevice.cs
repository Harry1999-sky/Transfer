namespace LanTransfer.Services;

public class ConnectedDevice
{
    public string Ip { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastSeen { get; set; }
}
