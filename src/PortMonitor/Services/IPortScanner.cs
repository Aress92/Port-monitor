using PortMonitor.Models;

namespace PortMonitor.Services;

public interface IPortScanner
{
    Task<List<PortEntry>> GetActivePortsAsync();
}
