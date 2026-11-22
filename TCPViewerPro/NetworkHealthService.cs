using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// Read-only, user-initiated connection health checks. A failed ICMP probe
/// is a signal to investigate, not proof that a device or internet is offline.
/// No telemetry, network changes, elevation, or external service API.
/// </summary>
internal static class NetworkHealthService
{
    internal enum HealthLevel { Good, Attention, Unknown }

    internal sealed record HealthItem(
        string Name, HealthLevel Level, string Headline, string Explanation, string Details);

    internal sealed record HealthResult(
        IReadOnlyList<HealthItem> Items,
        IReadOnlyList<NetworkDiagnosticsService.AdapterSnapshot> Adapters,
        DateTime CheckedAt)
    {
        internal string Headline => Items.All(x => x.Level == HealthLevel.Good)
            ? "These connection checks passed"
            : Items.Any(x => x.Level == HealthLevel.Attention)
                ? "Some results need attention"
                : "More information is needed";

        internal string Explanation => Items.All(x => x.Level == HealthLevel.Good)
            ? "Your gateway, test server and DNS lookup responded. This is a snapshot, not a full internet speed or security test."
            : "See the checks below for what worked and what to try next. A failed ping may simply be blocked by a firewall.";

        internal string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("SIMPLE NETWORK HEALTH CHECK");
            sb.AppendLine($"Checked: {CheckedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(Headline);
            sb.AppendLine(Explanation);
            foreach (var item in Items)
            {
                sb.AppendLine();
                sb.AppendLine($"{item.Name} [{item.Level}] — {item.Headline}");
                sb.AppendLine(item.Explanation);
                sb.AppendLine(item.Details);
            }
            sb.AppendLine();
            sb.AppendLine("Interpretation: ping only measures ICMP reachability; a silent host may still be online.");
            return sb.ToString().TrimEnd();
        }
    }

    internal static HealthItem SummarizePing(
        string name, string target, int sent, IReadOnlyList<long> replies)
    {
        if (sent < 1) throw new ArgumentOutOfRangeException(nameof(sent));
        if (replies.Count > sent) throw new ArgumentException("More replies than probes.");
        if (replies.Count == sent)
        {
            string headline = $"Responded to all {sent} tests";
            string description = name == "Internet test"
                ? "The internet test server replied. This does not measure download speed or guarantee every website works."
                : "The selected network gateway replied. Your local router connection passed this test.";
            return new HealthItem(name, HealthLevel.Good, headline, description,
                $"Target: {target}; replies: {replies.Count}/{sent}; average: {replies.Average():0.#} ms; loss: 0%.");
        }
        string advice = name == "Internet test"
            ? "Some requests did not get a reply. Check Wi-Fi, retry, or test a website. Some networks block ping."
            : "Your gateway did not answer every request. Check the local connection, but remember routers may block ping.";
        return new HealthItem(name, HealthLevel.Attention,
            replies.Count == 0 ? "No ping replies received" : "Some ping requests timed out",
            advice, $"Target: {target}; replies: {replies.Count}/{sent}; loss: {(sent - replies.Count) * 100 / sent}%.");
    }

    internal static HealthItem SummarizeDns(int addressCount, string? problem = null)
    {
        if (problem is not null)
            return new HealthItem("Website names", HealthLevel.Attention,
                "Website name lookup failed",
                "This computer could not resolve the test name. Check your DNS settings or try again.",
                $"DNS test: example.com; reason: {problem}");
        if (addressCount == 0)
            return new HealthItem("Website names", HealthLevel.Attention,
                "No addresses returned",
                "Your DNS lookup returned no addresses. Check network and DNS settings.",
                "DNS test: example.com; 0 addresses.");
        return new HealthItem("Website names", HealthLevel.Good,
            "Website name lookup worked",
            "Windows successfully looked up a website address using its configured DNS resolver.",
            $"DNS test: example.com; addresses returned: {addressCount}.");
    }

    internal static async Task<HealthResult> CheckAsync(CancellationToken cancellationToken)
    {
        // Do not perform any probes during app startup or simply on entering the page.
        var adapters = await Task.Run(NetworkDiagnosticsService.GetAdapters, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<HealthItem>(3);
        var chosen = NetworkDiagnosticsService.ChooseGateway(adapters);
        if (chosen is null)
            results.Add(new HealthItem("Local router", HealthLevel.Unknown,
                "No active gateway found",
                "Windows did not report a usable gateway. Check whether Wi-Fi or Ethernet is connected.",
                "No reported default gateway on an active adapter."));
        else
        {
            var (adapter, address) = chosen.Value;
            results.Add(await TryPingAsync("Local router", address, 2, cancellationToken,
                $"Adapter: {adapter.Name}"));
        }

        results.Add(await TryPingAsync("Internet test", IPAddress.Parse("1.1.1.1"),
            3, cancellationToken, "Cloudflare public IP; ICMP only"));

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync("example.com", cancellationToken);
            results.Add(SummarizeDns(addresses.Length));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            results.Add(SummarizeDns(0, ex.Message));
        }

        return new HealthResult(results, adapters, DateTime.Now);
    }

    private static async Task<HealthItem> TryPingAsync(
        string name, IPAddress address, int attempts, CancellationToken token, string context)
    {
        var times = new List<long>();
        try
        {
            for (int i = 0; i < attempts; i++)
            {
                token.ThrowIfCancellationRequested();
                using var ping = new Ping();
                PingReply reply = await ping.SendPingAsync(address, 1500).WaitAsync(token);
                if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime);
            }
            var summary = SummarizePing(name, address.ToString(), attempts, times);
            return summary with { Details = summary.Details + " " + context };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (PingException ex)
        {
            return new HealthItem(name, HealthLevel.Attention, "Ping could not complete",
                "The network or firewall may be preventing the test; this does not prove the device is offline.",
                $"Target: {address}. {context}. {ex.Message}");
        }
    }
}
