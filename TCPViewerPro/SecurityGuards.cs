using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace TCPViewerPro;

/// <summary>
/// Testable guardrails for security-sensitive UI actions and release handling.
/// These checks supplement Windows access controls; they are not malware verdicts.
/// </summary>
internal static class SecurityGuards
{
    private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "smss", "csrss", "wininit", "winlogon",
        "lsass", "lsaiso", "services", "svchost", "fontdrvhost",
        "registry", "secure system", "memory compression"
    };

    internal static bool IsCriticalWindowsProcess(string? name, string? path)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        string normalizedName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4] : name;
        if (CriticalProcessNames.Contains(normalizedName)) return true;

        if (string.IsNullOrWhiteSpace(path)) return true; // Fail closed when identity is unavailable.
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        try
        {
            if (windows.Length == 0) return false;
            string root = Path.GetFullPath(windows).TrimEnd(Path.DirectorySeparatorChar) +
                          Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    internal static bool IsSameProcessSnapshot(string? expectedName, string? expectedPath,
        DateTime expectedStartUtc, string? currentName, string? currentPath, DateTime currentStartUtc)
    {
        if (string.IsNullOrWhiteSpace(expectedName) || string.IsNullOrWhiteSpace(expectedPath) ||
            string.IsNullOrWhiteSpace(currentPath) || expectedStartUtc == DateTime.MinValue ||
            currentStartUtc == DateTime.MinValue) return false;
        try
        {
            return expectedName.Equals(currentName, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFullPath(expectedPath).Equals(Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase) &&
                expectedStartUtc.ToUniversalTime() == currentStartUtc.ToUniversalTime();
        }
        catch { return false; }
    }

    internal static bool IsBlockableRemoteAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();
            // Exclude unspecified, broadcast, multicast, reserved and link-local addresses.
            return bytes[0] is > 0 and < 224 &&
                   !(bytes[0] == 169 && bytes[1] == 254);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !address.Equals(IPAddress.IPv6Any) && !address.IsIPv6Multicast &&
                   !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal;
        return false;
    }

    internal static bool IsPrivateOrSharedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = address.GetAddressBytes();
            return b[0] == 10 ||
                (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                (b[0] == 192 && b[1] == 168) ||
                (b[0] == 100 && b[1] is >= 64 and <= 127);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (address.GetAddressBytes()[0] & 0xFE) == 0xFC; // fc00::/7
        return false;
    }

    /// <summary>
    /// Display classification only, not an indicator of whether traffic is safe.
    /// Reserved and documentation ranges must never be mislabeled as public internet hosts.
    /// </summary>
    internal static string EndpointIdentity(string protocol, int pid, string localAddress,
        int localPort, string remoteAddress, int remotePort) =>
        $"{protocol}|{pid}|{localAddress}:{localPort}|{remoteAddress}:{remotePort}";

    internal static string ClassifyAddressScope(string? value)
    {
        if (!IPAddress.TryParse(value, out IPAddress? address)) return "UNKNOWN";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal || IsPrivateOrSharedAddress(address))
            return "PRIVATE / LOCAL";

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = address.GetAddressBytes();
            if (b[0] == 169 && b[1] == 254) return "PRIVATE / LOCAL";
            if (b[0] == 0 || b[0] >= 224 ||
                (b[0] == 192 && b[1] == 0 && b[2] == 2) || // TEST-NET-1
                (b[0] == 198 && b[1] == 51 && b[2] == 100) || // TEST-NET-2
                (b[0] == 203 && b[1] == 0 && b[2] == 113) || // TEST-NET-3
                (b[0] == 198 && (b[1] == 18 || b[1] == 19))) // benchmarking
                return "OTHER";
            return "PUBLIC";
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            byte[] b = address.GetAddressBytes();
            if (address.IsIPv6Multicast ||
                (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8)) // documentation
                return "OTHER";
            return "PUBLIC";
        }
        return "UNKNOWN";
    }

    internal static bool IsExpectedReleaseAssetUrl(string? url, string? repo, string? tag, string file)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(repo) ||
            string.IsNullOrWhiteSpace(tag)) return false;
        if (!Regex.IsMatch(repo, @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,99}/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}\z") ||
            !Regex.IsMatch(tag, @"\Av[0-9]+\.[0-9]+\.[0-9]+\z")) return false;
        if (file is not ("TCPViewerPro.exe" or "TCPViewerPro.exe.sha256")) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
               uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0 &&
               uri.Fragment.Length == 0 &&
               uri.AbsolutePath.Equals($"/{repo}/releases/download/{tag}/{file}",
                   StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ReadReleaseSha256Manifest(string? content)
    {
        if (content is null || content.Length > 4096) return null;
        Match match = Regex.Match(content,
            @"\A[ \t]*(?<hash>[a-fA-F0-9]{64})[ \t]+\*?TCPViewerPro\.exe[ \t]*(?:\r?\n)?\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["hash"].Value.ToLowerInvariant() : null;
    }

    internal static bool SameSignerFingerprint(string? before, string? after)
    {
        return before is { Length: 64 } && after is { Length: 64 } &&
               Regex.IsMatch(before, @"\A[a-fA-F0-9]{64}\z") &&
               Regex.IsMatch(after, @"\A[a-fA-F0-9]{64}\z") &&
               before.Equals(after, StringComparison.OrdinalIgnoreCase);
    }

    internal static string ProtectCsvFormula(string value)
    {
        if (value.Length == 0) return value;
        string trimmed = value.TrimStart(' ', '\t', '\r', '\n');
        return trimmed.Length != 0 && "=+-@".Contains(trimmed[0]) ? "'" + value : value;
    }
}
