using System.ComponentModel;
using System.Diagnostics;

namespace PortMonitor.Services;

public class ProcessService
{
    public static (bool Success, string? Error) KillProcess(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            string name = process.ProcessName;
            process.Kill();
            return (true, null);
        }
        catch (ArgumentException)
        {
            return (false, "Proces juz nie istnieje.");
        }
        catch (Win32Exception)
        {
            return (false, "Brak uprawnien. Uruchom aplikacje jako Administrator.");
        }
        catch (InvalidOperationException)
        {
            return (false, "Proces juz nie istnieje.");
        }
    }
}
