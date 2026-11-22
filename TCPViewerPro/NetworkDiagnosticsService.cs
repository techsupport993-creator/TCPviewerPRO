using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// Read-only local network diagnostics. Network requests only happen when a
/// user explicitly runs a ping, DNS lookup, gateway test, or the combined test.
/// No shell processes, administrative privileges, or third-party endpoints.
/// </summary>
internal static class NetworkDiagnosticsService
{
    internal sealed record AdapterSnapshot(
        string Name,
        string Status,
        string Kind,
        string IPv4,
        string IPv6,
        string Gateway,
        string Dns,
        string Mac,
        string Speed,
        IReadOnlyList<IPAddress> Gateways);

    internal static bool TryNormalizeHost(string? value, out string host)
    {
        host = (value ?? "").Trim();
        if (host.Length is < 1 or > 253 ||
            host.Any(char.IsWhiteSpace) ||
            host.Contains('/') || host.Contains('\\') ||
            host.Contains("://", StringComparison.Ordinal))
            return false;

        return IPAddress.TryParse(host, out _) ||
               Uri.CheckHostName(host) == UriHostNameType.Dns;
    }

    internal static string FormatSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0) return "Unknown";
        if (bitsPerSecond >= 1_000_000_000)
            return $"{bitsPerSecond / 1_000_000_000d:0.##} Gbps";
        if (bitsPerSecond >= 1_000_000)
            return $"{bitsPerSecond / 1_000_000d:0.##} Mbps";
        return $"{bitsPerSecond / 1_000d:0.##} Kbps";
    }

    internal static IReadOnlyList<AdapterSnapshot> GetAdapters()
    {
        var results = new List<AdapterSnapshot>();
        foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
        {
            // The interface can disappear or change while it is inspected.
            try
            {
                IPInterfaceProperties properties = network.GetIPProperties();
                var ipv4 = properties.UnicastAddresses
                    .Select(x => x.Address)
                    .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                    .Select(x => x.ToString()).Distinct().ToArray();
                var ipv6 = properties.UnicastAddresses
                    .Select(x => x.Address)
                    .Where(x => x.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(x => x.ToString()).Distinct().ToArray();
                var gateways = properties.GatewayAddresses
                    .Select(x => x.Address)
                    .Where(IsUsableGateway).Distinct().ToArray();
                var dns = properties.DnsAddresses
                    .Select(x => x.ToString()).Distinct().ToArray();

                byte[] mac = network.GetPhysicalAddress().GetAddressBytes();
                string macText = mac.Length == 0
                    ? "Unavailable"
                    : string.Join(":", mac.Select(x => x.ToString("X2")));

                results.Add(new AdapterSnapshot(
                    network.Name,
                    network.OperationalStatus.ToString(),
                    network.NetworkInterfaceType.ToString(),
                    JoinOrNone(ipv4),
                    JoinOrNone(ipv6),
                    JoinOrNone(gateways.Select(x => x.ToString())),
                    JoinOrNone(dns),
                    macText,
                    FormatSpeed(network.Speed),
                    gateways));
            }
            catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException
                                      or NotSupportedException)
            {
                results.Add(new AdapterSnapshot(
                    network.Name,
                    network.OperationalStatus.ToString(),
                    network.NetworkInterfaceType.ToString(),
                    "Unavailable", "Unavailable", "Unavailable",
                    "Unavailable", "Unavailable", "Unknown",
                    Array.Empty<IPAddress>()));
            }
        }

        return results.OrderBy(x => x.Status == "Up" ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        string[] nonempty = values.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return nonempty.Length == 0 ? "None" : string.Join(", ", nonempty);
    }

    internal static bool IsUsableGateway(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any))
            return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return !address.Equals(IPAddress.Broadcast) && !address.IsIPv6Multicast;
        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
               !address.IsIPv6Multicast;
    }

    internal static (AdapterSnapshot Adapter, IPAddress Gateway)? ChooseGateway(
        IReadOnlyList<AdapterSnapshot> adapters)
    {
        // Prefer active wired/wireless adapters and IPv4 gateways. This is
        // the first reported gateway, not a definitive Windows route decision.
        foreach (var adapter in adapters
            .Where(x => x.Status.Equals("Up", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Kind is "Ethernet" or "Wireless80211" ? 0 : 1))
        {
            var gateway = adapter.Gateways
                .Where(IsUsableGateway)
                .OrderBy(x => x.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .FirstOrDefault();
            if (gateway is not null) return (adapter, gateway);
        }
        return null;
    }

    internal static async Task<string> LookupDnsAsync(string? target, CancellationToken cancellation)
    {
        if (!TryNormalizeHost(target, out string host))
            throw new ArgumentException("Enter a hostname or IP address without a URL, path or port.");

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellation);
        var output = new StringBuilder();
        output.AppendLine($"DNS LOOKUP  {host}");
        output.AppendLine($"Resolved addresses: {addresses.Length}");
        foreach (IPAddress address in addresses.Distinct())
            output.AppendLine($"  {address.AddressFamily}: {address}");
        if (addresses.Length == 0)
            output.AppendLine("No addresses were returned by the configured system DNS resolver.");
        output.AppendLine("Results come from Windows name resolution; this is not a DNS traffic monitor.");
        return output.ToString().TrimEnd();
    }

    internal static async Task<string> PingAsync(string? target, int attempts,
        CancellationToken cancellation)
    {
        if (!TryNormalizeHost(target, out string host))
            throw new ArgumentException("Enter a hostname or IP address without a URL, path or port.");
        if (attempts is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(attempts), "Use 1–4 ping attempts.");

        IPAddress address;
        if (IPAddress.TryParse(host, out IPAddress? ip))
            address = ip;
        else
        {
            IPAddress[] resolved = await Dns.GetHostAddressesAsync(host, cancellation);
            address = resolved.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)
                ?? resolved.FirstOrDefault()
                ?? throw new InvalidOperationException("DNS returned no addresses for the ping target.");
        }

        var output = new StringBuilder();
        output.AppendLine($"PING  {host} ({address})");
        int received = 0;
        var times = new List<long>();
        for (int i = 0; i < attempts; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            using var ping = new Ping();
            try
            {
                PingReply reply = await ping.SendPingAsync(address, 1500).WaitAsync(cancellation);
                if (reply.Status == IPStatus.Success)
                {
                    received++;
                    times.Add(reply.RoundtripTime);
                    output.AppendLine($"  Reply {i + 1}: {reply.RoundtripTime} ms");
                }
                else
                    output.AppendLine($"  Reply {i + 1}: {reply.Status}");
            }
            catch (PingException ex)
            {
                output.AppendLine($"  Reply {i + 1}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        output.AppendLine($"Received: {received}/{attempts}; loss: {(attempts - received) * 100 / attempts}%");
        if (times.Count != 0)
            output.AppendLine($"Latency min / avg / max: {times.Min()} / {times.Average():0.#} / {times.Max()} ms");
        output.AppendLine("ICMP traffic may be blocked by a firewall; no reply does not prove the host is offline.");
        return output.ToString().TrimEnd();
    }

    internal static async Task<string> PingGatewayAsync(
        IReadOnlyList<AdapterSnapshot> adapters, CancellationToken cancellation)
    {
        var chosen = ChooseGateway(adapters);
        if (chosen is null)
            return "GATEWAY CHECK\nNo usable gateway was reported by an active network adapter.";

        var (adapter, address) = chosen.Value;
        string pingText = await PingAsync(address.ToString(), 2, cancellation);
        return $"GATEWAY CHECK\nAdapter: {adapter.Name}\n" +
               "Testing the adapter's reported gateway (not necessarily the OS-selected route).\n" +
               pingText;
    }

    internal static string FormatAdapterReport(IReadOnlyList<AdapterSnapshot> adapters)
    {
        var output = new StringBuilder();
        output.AppendLine($"NETWORK ADAPTERS  {adapters.Count} found");
        foreach (AdapterSnapshot adapter in adapters)
        {
            output.AppendLine();
            output.AppendLine($"{adapter.Name}  [{adapter.Status}]  {adapter.Kind}");
            output.AppendLine($"  IPv4: {adapter.IPv4}");
            output.AppendLine($"  IPv6: {adapter.IPv6}");
            output.AppendLine($"  Gateway: {adapter.Gateway}");
            output.AppendLine($"  DNS: {adapter.Dns}");
            output.AppendLine($"  MAC: {adapter.Mac}");
            output.AppendLine($"  Link speed: {adapter.Speed} (reported, not a throughput test)");
        }
        return output.ToString().TrimEnd();
    }
}
