using System.Net;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// Evidence from one Windows IP Helper snapshot. These are socket-table rows,
/// not packets, byte counters, validated remote services or threat findings.
/// </summary>
internal sealed record InvestigationSocket(
    string Protocol, string State, string LocalAddress, int LocalPort,
    string RemoteAddress, int RemotePort);

/// <summary>
/// An event observed while TCP Viewer Pro was open. Matching a PID alone does
/// not establish that a historical event belongs to the same process instance.
/// </summary>
internal sealed record InvestigationEvent(
    DateTime SeenAt, string RemoteAddress, int RemotePort, string State);

internal sealed record InvestigationStatistics(
    int EndpointCount, int TcpCount, int UdpCount,
    int EstablishedCount, int ListeningCount,
    int UniqueRemoteAddresses, int Ipv6Count, int ObservationCount);

/// <summary>
/// Deterministic, local-only aggregation and technician report formatting.
/// Safe to execute without admin rights or any network access.
/// </summary>
internal static class ConnectionInvestigatorForensics
{
    private static bool HasRealRemote(string address, int port)
    {
        if (port <= 0 || !IPAddress.TryParse(address, out var ip)) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any);
    }

    internal static InvestigationStatistics Summarize(
        IReadOnlyList<InvestigationSocket> sockets,
        IReadOnlyList<InvestigationEvent> observations)
    {
        return new(
            sockets.Count,
            sockets.Count(x => x.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase)),
            sockets.Count(x => x.Protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase)),
            sockets.Count(x => x.State.Equals("ESTABLISHED", StringComparison.OrdinalIgnoreCase)),
            sockets.Count(x => x.State.Equals("LISTENING", StringComparison.OrdinalIgnoreCase)),
            sockets.Where(x => HasRealRemote(x.RemoteAddress, x.RemotePort))
                   .Select(x => x.RemoteAddress).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            sockets.Count(x => IPAddress.TryParse(x.LocalAddress, out var ip) &&
                ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6),
            observations.Count);
    }

    internal static string FormatDeepReport(
        ConnectionInvestigation selected,
        InvestigationProcessDetails? details,
        IReadOnlyList<InvestigationSocket> sockets,
        IReadOnlyList<InvestigationEvent> observations,
        bool redactSensitive)
    {
        var stats = Summarize(sockets, observations);
        var sb = new StringBuilder(ConnectionInvestigatorService.FormatReport(
            selected, details, redactSensitive));

        sb.AppendLine();
        sb.AppendLine("SUPER ADVANCED / PROCESS SOCKET SNAPSHOT");
        sb.AppendLine("The following counters describe only the selected process PID in the captured Windows table.");
        sb.AppendLine($"Endpoints: {stats.EndpointCount}  TCP: {stats.TcpCount}  UDP: {stats.UdpCount}");
        sb.AppendLine($"TCP ESTABLISHED: {stats.EstablishedCount}  LISTENING: {stats.ListeningCount}");
        sb.AppendLine($"Distinct reported remote IPs: {stats.UniqueRemoteAddresses}  IPv6 local endpoints: {stats.Ipv6Count}");
        sb.AppendLine($"PID-matched events retained in this app session: {stats.ObservationCount}");
        sb.AppendLine();
        sb.AppendLine("OTHER ENDPOINTS FOR SAME PID (capture-time)");
        int shown = 0;
        foreach (var socket in sockets.Take(250))
        {
            // Always redact both addresses and port numbers in share mode. Even
            // a private port plus state could be sensitive system inventory.
            string local = redactSensitive ? "[REDACTED]" :
                ConnectionInvestigatorService.Endpoint(socket.LocalAddress, socket.LocalPort);
            string remote = redactSensitive ? "[REDACTED]" :
                ConnectionInvestigatorService.Endpoint(socket.RemoteAddress, socket.RemotePort);
            sb.AppendLine($"{socket.Protocol,-4} {socket.State,-14} {local} -> {remote}");
            shown++;
        }
        if (sockets.Count > shown)
            sb.AppendLine($"... {sockets.Count - shown} other endpoints omitted (250-row cap)");
        if (sockets.Count == 0) sb.AppendLine("No related endpoints in captured snapshot.");

        sb.AppendLine();
        sb.AppendLine("SESSION OBSERVATIONS FOR SAME PID (newest first)");
        int eventsShown = 0;
        foreach (var observation in observations.OrderByDescending(x => x.SeenAt).Take(200))
        {
            string remote = redactSensitive ? "[REDACTED]" :
                ConnectionInvestigatorService.Endpoint(observation.RemoteAddress, observation.RemotePort);
            sb.AppendLine($"{observation.SeenAt:yyyy-MM-dd HH:mm:ss}  {observation.State,-14}  {remote}");
            eventsShown++;
        }
        if (observations.Count > eventsShown)
            sb.AppendLine($"... {observations.Count - eventsShown} additional observations omitted (200-row cap)");
        if (observations.Count == 0) sb.AppendLine("No PID-matched events in current session history.");

        sb.AppendLine();
        sb.AppendLine("INTERPRETATION BOUNDARIES");
        sb.AppendLine("- The endpoint counts are not bandwidth measurements; they do not reveal bytes sent/received.");
        sb.AppendLine("- TCP states are an instantaneous snapshot and can change or disappear.");
        sb.AppendLine("- UDP BOUND sockets have no corresponding remote peer in the IP Helper UDP table.");
        sb.AppendLine("- A session observation is first SEEN by the app, not proof that a connection began then.");
        sb.AppendLine("- PID-matched historical events can belong to another process after PID reuse.");
        sb.AppendLine("- These values do not identify packet contents, domains visited or malware.");
        return sb.ToString();
    }
}
