using System.Runtime.InteropServices;

namespace PortMonitor.Helpers;

internal static class NativeMethods
{
    private const string IphlpapiDll = "iphlpapi.dll";

    public const int AF_INET = 2;
    public const int TCP_TABLE_OWNER_PID_ALL = 5;
    public const int UDP_TABLE_OWNER_PID = 1;

    [DllImport(IphlpapiDll, SetLastError = true)]
    public static extern uint GetExtendedTcpTable(
        nint pTcpTable, ref int pdwSize, bool bOrder,
        int ulAf, int tableClass, uint reserved = 0);

    [DllImport(IphlpapiDll, SetLastError = true)]
    public static extern uint GetExtendedUdpTable(
        nint pUdpTable, ref int pdwSize, bool bOrder,
        int ulAf, int tableClass, uint reserved = 0);

    public enum TcpState
    {
        Closed = 1,
        Listen = 2,
        SynSent = 3,
        SynReceived = 4,
        Established = 5,
        FinWait1 = 6,
        FinWait2 = 7,
        CloseWait = 8,
        Closing = 9,
        LastAck = 10,
        TimeWait = 11,
        DeleteTcb = 12
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_TCPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_UDPROW_OWNER_PID
    {
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_UDPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
    }
}
