using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SocketGhost.Core.Interop;

namespace SocketGhost.Core
{
    public class PidResolver
    {
        public Task<int?> ResolvePidAsync(IPEndPoint local, IPEndPoint remote)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Task.FromResult<int?>(null);
            }

            return Task.Run(() =>
            {
                try
                {
                    return GetPidForConnection(local, remote);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PID resolution failed: {ex.Message}");
                    return null;
                }
            });
        }

        private int? GetPidForConnection(IPEndPoint local, IPEndPoint remote)
        {
            // Currently only supporting IPv4 for MVP simplicity
            if (local.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return null;
            }

            int bufferSize = 0;
            // First call to get size
            WinIpHelper.GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, WinIpHelper.AF_INET, WinIpHelper.TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);

            IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                uint ret = WinIpHelper.GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, WinIpHelper.AF_INET, WinIpHelper.TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);
                if (ret != 0)
                {
                    return null;
                }

                // MIB_TCPTABLE_OWNER_PID structure:
                // DWORD dwNumEntries
                // MIB_TCPROW_OWNER_PID table[ANY_SIZE]
                
                int numEntries = Marshal.ReadInt32(tcpTablePtr);
                IntPtr rowPtr = IntPtr.Add(tcpTablePtr, 4);
                int rowSize = Marshal.SizeOf(typeof(WinIpHelper.MIB_TCPROW_OWNER_PID));

                // Convert endpoints to uint/int for comparison
                // Note: Windows stores IP as uint (little endian) and Port as network byte order (big endian)
                // But GetExtendedTcpTable returns ports in network byte order? Let's check.
                // Actually, dwLocalPort in struct is in network byte order.
                
                // Local endpoint from proxy perspective is the Client's Remote endpoint (Client -> Proxy)
                // Wait, we need to match the socket connection from the App to the Proxy.
                // The Proxy sees: Client IP:Port (Remote) -> Proxy IP:Port (Local)
                // The App sees: App IP:Port (Local) -> Proxy IP:Port (Remote)
                // So we need to find a row where:
                // Row.Local == Client.Remote (App's local)
                // Row.Remote == Client.Local (App's remote, i.e., Proxy)
                
                // Wait, let's clarify "local" and "remote" args passed to this function.
                // In ProxyServer.OnRequest:
                // e.HttpClient.RemoteEndPoint is the App's IP:Port (Client)
                // e.HttpClient.LocalEndPoint is the Proxy's IP:Port (Server)
                
                // So we are looking for a TCP row where:
                // Row.LocalAddr == e.HttpClient.RemoteEndPoint.Address
                // Row.LocalPort == e.HttpClient.RemoteEndPoint.Port
                // Row.RemoteAddr == e.HttpClient.LocalEndPoint.Address
                // Row.RemotePort == e.HttpClient.LocalEndPoint.Port

                long targetLocalAddr = BitConverter.ToInt32(local.Address.GetAddressBytes(), 0);
                // Port in struct is network byte order? Usually yes.
                // Let's try matching ports carefully.
                
                for (int i = 0; i < numEntries; i++)
                {
                    WinIpHelper.MIB_TCPROW_OWNER_PID row = Marshal.PtrToStructure<WinIpHelper.MIB_TCPROW_OWNER_PID>(rowPtr);
                    
                    // Check if this row matches our connection
                    // We match the "Local" side of the row to the "Remote" side of the proxy connection (the App)
                    
                    // IP comparison
                    if (row.dwLocalAddr == targetLocalAddr)
                    {
                        // Port comparison - handle network byte order
                        ushort rowLocalPort = WinIpHelper.PortToHostOrder(row.dwLocalPort);
                        
                        if (rowLocalPort == local.Port)
                        {
                            // Found a match on local side (App side)
                            // Optionally check remote side (Proxy side) for stricter match
                            return row.dwOwningPid;
                        }
                    }

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }

                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTablePtr);
            }
        }
    }
}
