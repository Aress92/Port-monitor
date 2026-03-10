namespace PortMonitor.Models;

public class PortEntry
{
    public int Port { get; init; }
    public string Protocol { get; init; } = string.Empty;  // "TCP" or "UDP"
    public int Pid { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string LocalAddress { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;  // "LISTEN", "ESTABLISHED", etc.
}
