using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TCPViewerPro;

internal static class IpHelperTables
{
    const int AF_INET = 2;
    const int AF_INET6 = 23;
    const int TCP_TABLE_OWNER_PID_ALL = 5;
    const int UDP_TABLE_OWNER_PID = 1;
    const uint ERROR_INSUFFICIENT_BUFFER = 122;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);

    internal sealed record TcpEndpoint(IPAddress LocalAddress, int LocalPort, IPAddress RemoteAddress, int RemotePort, int State, int Pid);
    internal sealed record UdpEndpoint(IPAddress LocalAddress, int LocalPort, int Pid);

    public static IReadOnlyList<TcpEndpoint> GetTcp4()
    {
        IntPtr buffer = IntPtr.Zero;
        try
        {
            int size = 0;
            uint rc = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (rc != ERROR_INSUFFICIENT_BUFFER && rc != 0) throw new InvalidOperationException($"GetExtendedTcpTable failed: {rc}");
            buffer = Marshal.AllocHGlobal(size);
            rc = GetExtendedTcpTable(buffer, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (rc != 0) throw new InvalidOperationException($"GetExtendedTcpTable failed: {rc}");

            int count = Marshal.ReadInt32(buffer);
            const int rowSize = 24; // MIB_TCPROW_OWNER_PID
            var rows = new List<TcpEndpoint>(count);
            for (int i = 0; i < count; i++)
            {
                int o = IntPtr.Size == 8 ? 4 + i * rowSize : 4 + i * rowSize;
                int state = Marshal.ReadInt32(buffer, o);
                uint localAddr = unchecked((uint)Marshal.ReadInt32(buffer, o + 4));
                uint localPort = unchecked((uint)Marshal.ReadInt32(buffer, o + 8));
                uint remoteAddr = unchecked((uint)Marshal.ReadInt32(buffer, o + 12));
                uint remotePort = unchecked((uint)Marshal.ReadInt32(buffer, o + 16));
                int pid = Marshal.ReadInt32(buffer, o + 20);
                rows.Add(new TcpEndpoint(new IPAddress(BitConverter.GetBytes(localAddr)), DecodePort(localPort),
                    new IPAddress(BitConverter.GetBytes(remoteAddr)), DecodePort(remotePort), state, pid));
            }
            return rows;
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
    }

    public static IReadOnlyList<UdpEndpoint> GetUdp4()
    {
        IntPtr buffer = IntPtr.Zero;
        try
        {
            int size = 0;
            uint rc = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
            if (rc != ERROR_INSUFFICIENT_BUFFER && rc != 0) throw new InvalidOperationException($"GetExtendedUdpTable failed: {rc}");
            buffer = Marshal.AllocHGlobal(size);
            rc = GetExtendedUdpTable(buffer, ref size, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
            if (rc != 0) throw new InvalidOperationException($"GetExtendedUdpTable failed: {rc}");

            int count = Marshal.ReadInt32(buffer);
            const int rowSize = 12; // MIB_UDPROW_OWNER_PID
            var rows = new List<UdpEndpoint>(count);
            for (int i = 0; i < count; i++)
            {
                int o = 4 + i * rowSize;
                uint localAddr = unchecked((uint)Marshal.ReadInt32(buffer, o));
                uint localPort = unchecked((uint)Marshal.ReadInt32(buffer, o + 4));
                int pid = Marshal.ReadInt32(buffer, o + 8);
                rows.Add(new UdpEndpoint(new IPAddress(BitConverter.GetBytes(localAddr)), DecodePort(localPort), pid));
            }
            return rows;
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
    }


    public static IReadOnlyList<TcpEndpoint> GetTcp6()
    {
        IntPtr buffer = IntPtr.Zero;
        try
        {
            int size = 0;
            uint rc = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
            if (rc != ERROR_INSUFFICIENT_BUFFER && rc != 0) throw new InvalidOperationException($"GetExtendedTcpTable IPv6 failed: {rc}");
            if (size == 0) return Array.Empty<TcpEndpoint>();
            buffer = Marshal.AllocHGlobal(size);
            rc = GetExtendedTcpTable(buffer, ref size, true, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
            if (rc != 0) throw new InvalidOperationException($"GetExtendedTcpTable IPv6 failed: {rc}");
            int count = Marshal.ReadInt32(buffer);
            const int rowSize = 56; // MIB_TCP6ROW_OWNER_PID
            var rows = new List<TcpEndpoint>(count);
            for (int i = 0; i < count; i++)
            {
                int o = 4 + i * rowSize;
                byte[] local = new byte[16], remote = new byte[16];
                Marshal.Copy(IntPtr.Add(buffer, o), local, 0, 16);
                uint localScopeId = unchecked((uint)IPAddress.NetworkToHostOrder(Marshal.ReadInt32(buffer, o + 16)));
                uint localPort = unchecked((uint)Marshal.ReadInt32(buffer, o + 20));
                Marshal.Copy(IntPtr.Add(buffer, o + 24), remote, 0, 16);
                uint remoteScopeId = unchecked((uint)IPAddress.NetworkToHostOrder(Marshal.ReadInt32(buffer, o + 40)));
                uint remotePort = unchecked((uint)Marshal.ReadInt32(buffer, o + 44));
                int state = Marshal.ReadInt32(buffer, o + 48);
                int pid = Marshal.ReadInt32(buffer, o + 52);
                rows.Add(new TcpEndpoint(new IPAddress(local, localScopeId), DecodePort(localPort),
                    new IPAddress(remote, remoteScopeId), DecodePort(remotePort), state, pid));
            }
            return rows;
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
    }

    public static IReadOnlyList<UdpEndpoint> GetUdp6()
    {
        IntPtr buffer = IntPtr.Zero;
        try
        {
            int size = 0;
            uint rc = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AF_INET6, UDP_TABLE_OWNER_PID, 0);
            if (rc != ERROR_INSUFFICIENT_BUFFER && rc != 0) throw new InvalidOperationException($"GetExtendedUdpTable IPv6 failed: {rc}");
            if (size == 0) return Array.Empty<UdpEndpoint>();
            buffer = Marshal.AllocHGlobal(size);
            rc = GetExtendedUdpTable(buffer, ref size, true, AF_INET6, UDP_TABLE_OWNER_PID, 0);
            if (rc != 0) throw new InvalidOperationException($"GetExtendedUdpTable IPv6 failed: {rc}");
            int count = Marshal.ReadInt32(buffer);
            const int rowSize = 28; // MIB_UDP6ROW_OWNER_PID
            var rows = new List<UdpEndpoint>(count);
            for (int i = 0; i < count; i++)
            {
                int o = 4 + i * rowSize;
                byte[] local = new byte[16];
                Marshal.Copy(IntPtr.Add(buffer, o), local, 0, 16);
                uint localScopeId = unchecked((uint)IPAddress.NetworkToHostOrder(Marshal.ReadInt32(buffer, o + 16)));
                uint localPort = unchecked((uint)Marshal.ReadInt32(buffer, o + 20));
                int pid = Marshal.ReadInt32(buffer, o + 24);
                rows.Add(new UdpEndpoint(new IPAddress(local, localScopeId), DecodePort(localPort), pid));
            }
            return rows;
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
    }


    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern uint SetTcpEntry(ref MibTcpRow row);

    [StructLayout(LayoutKind.Sequential)]
    struct MibTcpRow
    {
        public int State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
    }

    public static uint CloseTcp4(IPAddress local, int localPort, IPAddress remote, int remotePort)
    {
        if (local.AddressFamily != AddressFamily.InterNetwork || remote.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("IPv4 addresses are required.");
        var row = new MibTcpRow
        {
            State = 12, // MIB_TCP_STATE_DELETE_TCB
            LocalAddress = BitConverter.ToUInt32(local.GetAddressBytes(), 0),
            LocalPort = HostToNetworkPort(localPort),
            RemoteAddress = BitConverter.ToUInt32(remote.GetAddressBytes(), 0),
            RemotePort = HostToNetworkPort(remotePort)
        };
        return SetTcpEntry(ref row);
    }

    static uint HostToNetworkPort(int port)
    {
        ushort network = unchecked((ushort)IPAddress.HostToNetworkOrder(unchecked((short)port)));
        return network;
    }

    static int DecodePort(uint networkOrderPort)
    {
        // The port is stored in network byte order in the low WORD of the DWORD.
        ushort raw = unchecked((ushort)(networkOrderPort & 0xffff));
        return (ushort)IPAddress.NetworkToHostOrder(unchecked((short)raw));
    }

    public static string TcpStateName(int state) => state switch
    {
        1 => "CLOSED", 2 => "LISTENING", 3 => "SYN-SENT", 4 => "SYN-RECEIVED",
        5 => "ESTABLISHED", 6 => "FIN-WAIT-1", 7 => "FIN-WAIT-2", 8 => "CLOSE-WAIT",
        9 => "CLOSING", 10 => "LAST-ACK", 11 => "TIME-WAIT", 12 => "DELETE-TCB",
        _ => $"UNKNOWN ({state})"
    };
}
