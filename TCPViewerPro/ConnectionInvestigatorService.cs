using System.Net;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// An immutable snapshot of evidence visible to TCP Viewer Pro when the user
/// selected a connection. It is NOT a live network-capture event or verdict.
/// </summary>
internal sealed record ConnectionInvestigation(
    string Protocol, string Process, int Pid,
    string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort,
    string State, string PublisherCategory, string Signer, string Company,
    string SignatureStatus, string SignatureDetail, string ExecutablePath,
    DateTime ProcessStartedUtc, DateTime CapturedAt, DateTime? FirstObservedAt);

internal sealed record InvestigationProcessDetails(
    string ParentPid, string CommandLine, string StartedUtc,
    string FileSha256, string CollectionStatus);

/// <summary>
/// Deterministic, offline explanations and privacy-aware text report formatting.
/// Port numbers and code-signing status are clues, not malware determinations.
/// </summary>
internal static class ConnectionInvestigatorService
{
    internal static bool HasRemotePeer(ConnectionInvestigation connection)
    {
        if (connection.RemotePort == 0 || !IPAddress.TryParse(connection.RemoteAddress, out var remote))
            return false;
        if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        return !remote.Equals(IPAddress.Any) && !remote.Equals(IPAddress.IPv6Any);
    }

    internal static string AddressType(ConnectionInvestigation connection)
    {
        if (!HasRemotePeer(connection)) return "No remote peer in this snapshot";
        if (!IPAddress.TryParse(connection.RemoteAddress, out var address)) return "Unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return "Loopback (this computer)";
        return SecurityGuards.ClassifyAddressScope(address.ToString()) switch
        {
            "PRIVATE / LOCAL" => "Private, local or shared-network address",
            "PUBLIC" => "Public internet address",
            "OTHER" => "Reserved or special-purpose address",
            _ => "Unknown address type"
        };
    }

    internal static string Endpoint(string address, int port)
    {
        bool ipv6 = IPAddress.TryParse(address, out var ip) &&
                    ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        return ipv6 ? $"[{address}]:{port}" : $"{address}:{port}";
    }

    internal static string PortHint(ConnectionInvestigation connection)
    {
        if (!HasRemotePeer(connection)) return "No remote service port is available.";
        return connection.RemotePort switch
        {
            443 => "Port 443 commonly carries HTTPS traffic; the port alone does not prove encryption or trust.",
            80 => "Port 80 is commonly used for HTTP, but any service can use this port.",
            53 => "Port 53 is commonly used for DNS. This does not prove what data was exchanged.",
            22 => "Port 22 commonly carries SSH remote-access traffic. It is not automatically suspicious.",
            3389 => "Port 3389 commonly carries Remote Desktop traffic. Verify whether it is expected.",
            445 => "Port 445 is commonly used for SMB file sharing. Verify whether it is expected.",
            123 => "Port 123 commonly carries NTP time-synchronization traffic.",
            _ => "A port number identifies an endpoint, not the application protocol or safety of its traffic."
        };
    }

    internal static string Explain(ConnectionInvestigation connection)
    {
        string app = string.IsNullOrWhiteSpace(connection.Process) ? "An unidentified process" : connection.Process;
        if (!HasRemotePeer(connection))
            return $"{app} has a {connection.Protocol} endpoint without a remote peer in this snapshot. " +
                   "For example, listening TCP sockets and bound UDP sockets do not necessarily represent an outgoing connection.";

        string peer = AddressType(connection);
        string state = connection.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase)
            ? $" The captured TCP state is {connection.State}."
            : " UDP does not use TCP connection states.";
        return $"{app} is associated with a {connection.Protocol} endpoint to a {peer.ToLowerInvariant()}." +
               state + " " + PortHint(connection);
    }

    internal static string ExplainSignature(ConnectionInvestigation connection)
    {
        return connection.PublisherCategory switch
        {
            "Verified Microsoft" => "The executable had an offline-valid Microsoft Authenticode signature at capture time. Certificate revocation was not checked.",
            "Verified third party" => "The executable had an offline-valid third-party Authenticode signature at capture time. Certificate revocation was not checked.",
            "Unsigned" => "The executable did not have a verified digital signature. Unsigned software is not necessarily malicious.",
            "Unverified" => "Windows did not establish a valid executable signature. This alone cannot determine whether the process is malicious.",
            _ => "A verified publisher identity was unavailable for this process. That does not establish whether it is safe or unsafe."
        };
    }

    internal static string NextSteps(ConnectionInvestigation connection)
    {
        if (!HasRemotePeer(connection))
            return "If this endpoint is unexpected, confirm which application owns it and whether it should listen or bind on this computer.";
        if (connection.Pid <= 0 || string.IsNullOrWhiteSpace(connection.ExecutablePath))
            return "Process identity is incomplete. Try refreshing and checking the process in Windows Task Manager before drawing conclusions.";
        return "If you recognize the application and expected activity, no action may be needed. " +
               "If not, inspect its executable path and signer, check the process in Windows, and compare activity over time. " +
               "Do not block or terminate a process based on a port or IP classification alone.";
    }

    private static string OneLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unavailable";
        return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    internal static string FormatReport(
        ConnectionInvestigation connection, InvestigationProcessDetails? details,
        bool redactSensitive)
    {
        string privateField(string field) => redactSensitive ? "[REDACTED]" : OneLine(field);
        var sb = new StringBuilder();
        sb.AppendLine("TCP VIEWER PRO v6.7.0 - CONNECTION INVESTIGATOR");
        sb.AppendLine("Local evidence snapshot - not a security verdict");
        sb.AppendLine("Captured: " + connection.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Observation: " + (connection.FirstObservedAt is DateTime first
            ? first.ToString("yyyy-MM-dd HH:mm:ss") + " (first seen in this app session)"
            : "No first-seen event recorded in this app session"));
        sb.AppendLine();
        sb.AppendLine("EXPLANATION");
        sb.AppendLine(Explain(connection));
        sb.AppendLine("Address classification: " + AddressType(connection));
        sb.AppendLine("Publisher explanation: " + ExplainSignature(connection));
        sb.AppendLine("Suggested next steps: " + NextSteps(connection));
        sb.AppendLine();
        sb.AppendLine("CAPTURED ENDPOINT AND PROCESS");
        sb.AppendLine("Process: " + OneLine(connection.Process));
        sb.AppendLine("PID: " + connection.Pid);
        sb.AppendLine("Protocol: " + OneLine(connection.Protocol));
        sb.AppendLine("State at capture: " + OneLine(connection.State));
        sb.AppendLine("Local endpoint: " + privateField(Endpoint(connection.LocalAddress, connection.LocalPort)));
        sb.AppendLine("Remote endpoint: " + (HasRemotePeer(connection)
            ? privateField(Endpoint(connection.RemoteAddress, connection.RemotePort))
            : "None reported"));
        sb.AppendLine("Publisher category: " + OneLine(connection.PublisherCategory));
        sb.AppendLine("Signer: " + OneLine(connection.Signer));
        sb.AppendLine("Company metadata (not verified): " + OneLine(connection.Company));
        sb.AppendLine("Signature status: " + OneLine(connection.SignatureStatus));
        sb.AppendLine("Signature details: " +
                      (redactSensitive ? "[REDACTED]" : OneLine(connection.SignatureDetail)));
        sb.AppendLine("Executable path: " + privateField(connection.ExecutablePath));
        sb.AppendLine("Process start from snapshot (UTC): " + (connection.ProcessStartedUtc == DateTime.MinValue
            ? "Unavailable" : connection.ProcessStartedUtc.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine();
        sb.AppendLine("OPTIONAL LIVE PROCESS DETAILS");
        if (details is null)
            sb.AppendLine("Not collected yet.");
        else
        {
            sb.AppendLine("Collection status: " + OneLine(details.CollectionStatus));
            sb.AppendLine("Parent PID: " + OneLine(details.ParentPid));
            sb.AppendLine("Command line: " + privateField(details.CommandLine));
            sb.AppendLine("Started (UTC): " + OneLine(details.StartedUtc));
            sb.AppendLine("On-disk executable SHA-256: " + OneLine(details.FileSha256));
        }
        sb.AppendLine();
        sb.AppendLine("LIMITATIONS");
        sb.AppendLine("- A port or public/private IP classification is not a threat score.");
        sb.AppendLine("- An offline-valid signature is not proof of safety; certificate revocation is not checked.");
        sb.AppendLine("- Process state and endpoints may have changed since the snapshot.");
        sb.AppendLine("- The on-disk file hash may not equal the image loaded into process memory.");
        sb.AppendLine("- The app does not capture packet contents, measure traffic volume or query online reputation.");
        sb.AppendLine(redactSensitive
            ? "- Sharing protection enabled: endpoints, paths, command lines and signature detail were redacted."
            : "- Sharing protection disabled: this report may expose IP addresses, user paths and command lines.");
        return sb.ToString();
    }
}
