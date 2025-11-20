using System;
using System.Runtime.InteropServices;

namespace SocketGhost.Core.Interop
{
    public static class WinIpHelper
    {
        // https://docs.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedtcptable
        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            bool bOrder,
            int ulAf,
            TCP_TABLE_CLASS TableClass,
            uint Reserved = 0);

        public const int AF_INET = 2;    // IPv4
        public const int AF_INET6 = 23;  // IPv6

        public enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            public uint dwState;
            public uint dwLocalAddr;
            public int dwLocalPort;
            public uint dwRemoteAddr;
            public int dwRemotePort;
            public int dwOwningPid;
        }

        // Helper to convert port to host byte order
        public static ushort PortToHostOrder(int port)
        {
            return (ushort)(((port & 0xFF) << 8) | ((port & 0xFF00) >> 8));
        }
    }
}
