using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using PortMonitor.Helpers;
using PortMonitor.Models;

namespace PortMonitor.Services;

public class PortScannerService : IPortScanner
{
    public Task<List<PortEntry>> GetActivePortsAsync()
    {
        return Task.Run(() =>
        {
            var entries = new List<PortEntry>();
            entries.AddRange(GetTcpPorts());
            entries.AddRange(GetUdpPorts());
            return entries.OrderBy(e => e.Port).ToList();
        });
    }

    private static List<PortEntry> GetTcpPorts()
    {
        var entries = new List<PortEntry>();
        int size = 0;

        NativeMethods.GetExtendedTcpTable(nint.Zero, ref size, true,
            NativeMethods.AF_INET, NativeMethods.TCP_TABLE_OWNER_PID_ALL);

        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            uint result = NativeMethods.GetExtendedTcpTable(buffer, ref size, true,
                NativeMethods.AF_INET, NativeMethods.TCP_TABLE_OWNER_PID_ALL);

            if (result != 0) return entries;

            int rowCount = Marshal.ReadInt32(buffer);
            nint rowPtr = buffer + Marshal.SizeOf<NativeMethods.MIB_TCPTABLE_OWNER_PID>();
            int rowSize = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);
                var state = (NativeMethods.TcpState)row.dwState;
                int localPort = (ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                string localAddr = new IPAddress(row.dwLocalAddr).ToString();
                string processName = GetProcessName((int)row.dwOwningPid);

                entries.Add(new PortEntry
                {
                    Port = localPort,
                    Protocol = "TCP",
                    Pid = (int)row.dwOwningPid,
                    ProcessName = processName,
                    LocalAddress = localAddr,
                    Status = state.ToString()
                });

                rowPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return entries;
    }

    private static List<PortEntry> GetUdpPorts()
    {
        var entries = new List<PortEntry>();
        int size = 0;

        NativeMethods.GetExtendedUdpTable(nint.Zero, ref size, true,
            NativeMethods.AF_INET, NativeMethods.UDP_TABLE_OWNER_PID);

        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            uint result = NativeMethods.GetExtendedUdpTable(buffer, ref size, true,
                NativeMethods.AF_INET, NativeMethods.UDP_TABLE_OWNER_PID);

            if (result != 0) return entries;

            int rowCount = Marshal.ReadInt32(buffer);
            nint rowPtr = buffer + Marshal.SizeOf<NativeMethods.MIB_UDPTABLE_OWNER_PID>();
            int rowSize = Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(rowPtr);
                int localPort = (ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                string localAddr = new IPAddress(row.dwLocalAddr).ToString();
                string processName = GetProcessName((int)row.dwOwningPid);

                entries.Add(new PortEntry
                {
                    Port = localPort,
                    Protocol = "UDP",
                    Pid = (int)row.dwOwningPid,
                    ProcessName = processName,
                    LocalAddress = localAddr,
                    Status = "\u2014"
                });

                rowPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return entries;
    }

    private static string GetProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return "<nieznany>";
        }
    }
}
