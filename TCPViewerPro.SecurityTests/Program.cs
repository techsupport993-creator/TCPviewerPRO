using System.Net;
using TCPViewerPro;

int passed = 0;
void Assert(bool condition, string scenario)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + scenario);
    Console.WriteLine("PASS " + scenario);
    passed++;
}

string repo = "techsupport993-creator/TCPviewerPRO";
string tag = "v6.4.2";
string exe = $"https://github.com/{repo}/releases/download/{tag}/TCPViewerPro.exe";
string sha = new string('a', 64);

Assert(SecurityGuards.IsExpectedReleaseAssetUrl(exe, repo, tag, "TCPViewerPro.exe"), "valid official release URL");
Assert(SecurityGuards.IsExpectedReleaseAssetUrl(
    $"https://github.com/{repo}/releases/download/{tag}/TCPViewerPro.exe.sha256",
    repo, tag, "TCPViewerPro.exe.sha256"), "valid checksum asset URL");
Assert(!SecurityGuards.IsExpectedReleaseAssetUrl(exe + "?mirror=1", repo, tag, "TCPViewerPro.exe"), "query string disallowed");
Assert(!SecurityGuards.IsExpectedReleaseAssetUrl(exe.Replace("github.com", "github.com.attacker.test"),
    repo, tag, "TCPViewerPro.exe"), "lookalike host rejected");
Assert(!SecurityGuards.IsExpectedReleaseAssetUrl(exe.Replace("/v6.4.2/", "/v6.4.1/"),
    repo, tag, "TCPViewerPro.exe"), "wrong version refused");
Assert(!SecurityGuards.IsExpectedReleaseAssetUrl(exe.Replace("TCPViewerPro.exe", "Malware.exe"),
    repo, tag, "TCPViewerPro.exe"), "wrong executable refused");
Assert(!SecurityGuards.IsExpectedReleaseAssetUrl(exe.Replace("/TCPviewerPRO/", "/TCPviewerPRO-alt/"),
    repo, tag, "TCPViewerPro.exe"), "repo-name prefix confusion refused");
Assert(!SecurityGuards.IsExpectedReleaseAssetUrl("http://github.com/techsupport993-creator/TCPviewerPRO/releases/download/v6.4.2/TCPViewerPro.exe",
    repo, tag, "TCPViewerPro.exe"), "plaintext HTTP rejected");
Assert(!SecurityGuards.IsExpectedReleaseAssetUrl(exe, repo, "invalid-version", "TCPViewerPro.exe"),
    "malformed release tag rejected");

Assert(SecurityGuards.ReadReleaseSha256Manifest(sha + "  TCPViewerPro.exe\n") == sha,
    "checksum manifest accepted");
Assert(SecurityGuards.ReadReleaseSha256Manifest(sha + " *TCPViewerPro.exe\r\n") == sha,
    "binary checksum manifest accepted");
Assert(SecurityGuards.ReadReleaseSha256Manifest(sha + "  Other.exe") is null, "wrong checksum filename refused");
Assert(SecurityGuards.ReadReleaseSha256Manifest("not-a-hash TCPViewerPro.exe") is null, "invalid checksum refused");
Assert(SecurityGuards.ReadReleaseSha256Manifest(sha + "  TCPViewerPro.exe\n" + sha + "  Other.exe") is null,
    "extra checksum lines refused");

Assert(SecurityGuards.SameSignerFingerprint(sha, sha.ToUpperInvariant()), "identical signer fingerprint accepted");
Assert(!SecurityGuards.SameSignerFingerprint(sha, new string('b', 64)), "different signer cert rejected");
Assert(!SecurityGuards.SameSignerFingerprint("", sha), "missing signer fingerprint rejected");

Assert(!SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("127.0.0.1")), "loopback IP rejected");
Assert(!SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("0.0.0.0")), "unspecified IP rejected");
Assert(!SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("255.255.255.255")), "broadcast IP rejected");
Assert(!SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("224.0.0.1")), "multicast IP rejected");
Assert(!SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("169.254.22.2")), "link-local IPv4 rejected");
Assert(!SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("ff02::1")), "IPv6 multicast rejected");
Assert(!SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("::1")), "IPv6 loopback rejected");
Assert(SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("1.1.1.1")), "ordinary IPv4 accepted");
Assert(SecurityGuards.IsBlockableRemoteAddress(IPAddress.Parse("2001:4860:4860::8888")), "ordinary IPv6 accepted");
Assert(SecurityGuards.IsPrivateOrSharedAddress(IPAddress.Parse("192.168.0.1")), "LAN IPv4 flagged for extra warning");
Assert(SecurityGuards.IsPrivateOrSharedAddress(IPAddress.Parse("fc00::1")), "private IPv6 flagged for extra warning");
Assert(!SecurityGuards.IsPrivateOrSharedAddress(IPAddress.Parse("8.8.8.8")), "public address not flagged");

// Address classification must not present reserved or LAN traffic as public internet activity.
Assert(SecurityGuards.ClassifyAddressScope("169.254.10.20") == "PRIVATE / LOCAL",
    "IPv4 link-local address is local");
Assert(SecurityGuards.ClassifyAddressScope("192.168.1.9") == "PRIVATE / LOCAL",
    "private LAN IP is local");
Assert(SecurityGuards.ClassifyAddressScope("100.64.12.14") == "PRIVATE / LOCAL",
    "carrier shared IP is local/shared");
Assert(SecurityGuards.ClassifyAddressScope("192.0.2.10") == "OTHER",
    "documentation IP is not public");
Assert(SecurityGuards.ClassifyAddressScope("203.0.113.10") == "OTHER",
    "TEST-NET-3 is not public");
Assert(SecurityGuards.ClassifyAddressScope("224.0.0.1") == "OTHER",
    "IPv4 multicast is not public");
Assert(SecurityGuards.ClassifyAddressScope("2001:db8::1") == "OTHER",
    "IPv6 documentation address is not public");
Assert(SecurityGuards.ClassifyAddressScope("fe80::1%2") == "PRIVATE / LOCAL",
    "IPv6 scoped link-local address is local");
Assert(SecurityGuards.ClassifyAddressScope("1.1.1.1") == "PUBLIC",
    "ordinary public IPv4 is public");
Assert(SecurityGuards.ClassifyAddressScope("invalid-ip") == "UNKNOWN",
    "invalid IP stays unknown");

// The current TCP state must not affect connection identity.
string endpoint = SecurityGuards.EndpointIdentity("TCP", 1234, "192.168.1.10", 50123, "1.1.1.1", 443);
Assert(endpoint == SecurityGuards.EndpointIdentity("TCP", 1234, "192.168.1.10", 50123, "1.1.1.1", 443),
    "same endpoint identity is stable across refreshes");
Assert(endpoint != SecurityGuards.EndpointIdentity("TCP", 1234, "192.168.1.10", 50124, "1.1.1.1", 443),
    "changed local port creates a separate endpoint");


﻿// Connection Investigator v6.7.0: deterministic explanations, evidence, and redaction.
var investigation = new ConnectionInvestigation(
    "TCP", "ExampleBrowser", 4321,
    "192.168.0.197", 51555, "203.0.113.40", 443,
    "ESTABLISHED", "Verified third party", "Example Signer", "Example Company",
    "Valid", @"Inspected at C:\Users\TestUser\AppData\Local\Example",
    @"C:\Users\TestUser\Apps\browser.exe",
    new DateTime(2026, 11, 23, 20, 0, 0, DateTimeKind.Utc),
    new DateTime(2026, 11, 23, 20, 10, 0),
    new DateTime(2026, 11, 23, 20, 5, 0));
var investigationDetails = new InvestigationProcessDetails(
    "31", @"C:\Users\TestUser\Apps\browser.exe --user-data-dir=C:\Users\TestUser\Private",
    "2026-11-23 20:00:00", new string('F', 64), "Identity verified");

Assert(ConnectionInvestigatorService.HasRemotePeer(investigation), "TCP remote endpoint detected");
Assert(ConnectionInvestigatorService.AddressType(investigation) == "Reserved or special-purpose address",
    "special-purpose IP is not described as a normal public host");
Assert(ConnectionInvestigatorService.Explain(investigation).Contains("Port 443") &&
       ConnectionInvestigatorService.Explain(investigation).Contains("does not prove"),
    "HTTPS hint avoids assuming verified secure traffic");
Assert(ConnectionInvestigatorService.ExplainSignature(investigation).Contains("revocation was not checked"),
    "verified signature is qualified as an offline check");
Assert(ConnectionInvestigatorService.Endpoint("fe80::1%3", 8080) == "[fe80::1%3]:8080",
    "scoped IPv6 endpoints use readable bracket formatting");
Assert(ConnectionInvestigatorService.Endpoint("192.168.1.1", 443) == "192.168.1.1:443",
    "IPv4 endpoint formatting stays readable");

var redactedInvestigation = ConnectionInvestigatorService.FormatReport(investigation, investigationDetails, true);
Assert(redactedInvestigation.Contains("Local endpoint: [REDACTED]") &&
       redactedInvestigation.Contains("Remote endpoint: [REDACTED]") &&
       redactedInvestigation.Contains("Executable path: [REDACTED]") &&
       redactedInvestigation.Contains("Command line: [REDACTED]"),
    "sharing mode redacts both endpoints, executable path and command line");
Assert(!redactedInvestigation.Contains("192.168.0.197") &&
       !redactedInvestigation.Contains("203.0.113.40") &&
       !redactedInvestigation.Contains("TestUser") &&
       !redactedInvestigation.Contains("--user-data-dir") &&
       !redactedInvestigation.Contains(@"C:\Users\"),
    "sharing mode does not leak private IPs, username or path in raw signature details");
Assert(redactedInvestigation.Contains("first seen in this app session"),
    "first observed is accurately scoped to the current session");

var fullInvestigation = ConnectionInvestigatorService.FormatReport(investigation, investigationDetails, false);
Assert(fullInvestigation.Contains("192.168.0.197:51555") &&
       fullInvestigation.Contains("203.0.113.40:443") &&
       fullInvestigation.Contains("TestUser"),
    "local advanced mode includes original evidence only when unredacted");
Assert(fullInvestigation.Contains("not a security verdict", StringComparison.OrdinalIgnoreCase),
    "reports explicitly avoid malware verdicts");

var boundUdp = investigation with
{
    Protocol = "UDP", RemoteAddress = "0.0.0.0", RemotePort = 0, State = "BOUND"
};
Assert(!ConnectionInvestigatorService.HasRemotePeer(boundUdp),
    "bound UDP socket is not mistaken for an active remote connection");
Assert(ConnectionInvestigatorService.Explain(boundUdp).Contains("without a remote peer"),
    "bound UDP explanation does not imply outbound traffic");
Assert(ConnectionInvestigatorService.FormatReport(boundUdp, null, true).Contains("Remote endpoint: None reported"),
    "no remote endpoint displays an accurate neutral statement");

var localLoopback = investigation with { RemoteAddress = "127.0.0.1", RemotePort = 60000 };
Assert(ConnectionInvestigatorService.AddressType(localLoopback) == "Loopback (this computer)",
    "localhost is identified as a connection on the same PC");


// Super Advanced: aggregate process-owned socket rows and session-only observations.
var socketRows = new[]
{
    new InvestigationSocket("TCP", "ESTABLISHED", "192.168.4.3", 52212, "1.1.1.1", 443),
    new InvestigationSocket("TCP", "ESTABLISHED", "192.168.4.3", 52214, "1.1.1.1", 443),
    new InvestigationSocket("TCP", "LISTENING", "0.0.0.0", 80, "0.0.0.0", 0),
    new InvestigationSocket("UDP", "BOUND", "fe80::1%7", 5353, "::", 0),
};
var observedRows = new[]
{
    new InvestigationEvent(new DateTime(2026, 11, 23, 20, 10, 0), "8.8.8.8", 443, "ESTABLISHED"),
    new InvestigationEvent(new DateTime(2026, 11, 23, 20, 11, 0), "203.0.113.88", 443, "TIME-WAIT")
};
var socketStats = ConnectionInvestigatorForensics.Summarize(socketRows, observedRows);
Assert(socketStats.EndpointCount == 4 && socketStats.TcpCount == 3 && socketStats.UdpCount == 1,
    "Super Advanced categorizes captured TCP and UDP sockets");
Assert(socketStats.EstablishedCount == 2 && socketStats.ListeningCount == 1 &&
       socketStats.UniqueRemoteAddresses == 1 && socketStats.Ipv6Count == 1,
    "Super Advanced counts states, unique remote addresses and IPv6 without counting listeners as peers");
Assert(socketStats.ObservationCount == 2,
    "Super Advanced reports number of session observations without inventing timing");
var safeDeep = ConnectionInvestigatorForensics.FormatDeepReport(investigation, investigationDetails,
    socketRows, observedRows, redactSensitive: true);
Assert(safeDeep.Contains("SUPER ADVANCED / PROCESS SOCKET SNAPSHOT") &&
       safeDeep.Contains("Endpoints: 4") && safeDeep.Contains("SESSION OBSERVATIONS FOR SAME PID"),
    "Super Advanced produces a useful detailed technician report");
Assert(!safeDeep.Contains("192.168.4.3") && !safeDeep.Contains("1.1.1.1") &&
       !safeDeep.Contains("8.8.8.8") && !safeDeep.Contains("203.0.113.88") &&
       !safeDeep.Contains("fe80::1") && !safeDeep.Contains("TestUser"),
    "Super Advanced share-safe report does not leak addresses or usernames");
var rawDeep = ConnectionInvestigatorForensics.FormatDeepReport(investigation, investigationDetails,
    socketRows, observedRows, redactSensitive: false);
Assert(rawDeep.Contains("192.168.4.3:52212") && rawDeep.Contains("[fe80::1%7]:5353") &&
       rawDeep.Contains("8.8.8.8:443"), "unredacted technician report contains captured socket and event details");
Assert(rawDeep.Contains("PID-matched historical events can belong to another process after PID reuse"),
    "session observations clearly warn about PID reuse");

Assert(SecurityGuards.IsCriticalWindowsProcess("lsass", @"C:\Windows\System32\lsass.exe"), "LSASS protected");
Assert(SecurityGuards.IsCriticalWindowsProcess("svchost", @"C:\Windows\System32\svchost.exe"), "svchost protected");
Assert(SecurityGuards.IsCriticalWindowsProcess("notepad", ""), "inaccessible process identity protected");
Assert(!SecurityGuards.IsCriticalWindowsProcess("notepad", Path.Combine(Path.GetTempPath(), "notepad.exe")),
    "normal executable not overblocked");

string path = Path.Combine(Path.GetTempPath(), "Process.exe");
DateTime now = DateTime.UtcNow;
Assert(SecurityGuards.IsSameProcessSnapshot("Process", path, now, "process", path, now),
    "unchanged process identity accepted");
Assert(!SecurityGuards.IsSameProcessSnapshot("Process", path, now, "Process", path, now.AddSeconds(1)),
    "PID reuse detected through creation time");
Assert(!SecurityGuards.IsSameProcessSnapshot("Process", path, now, "Process", path + "2", now),
    "path swap detected");
Assert(!SecurityGuards.IsSameProcessSnapshot("Process", path, DateTime.MinValue, "Process", path, now),
    "unavailable process creation time refused");

Assert(SecurityGuards.ProtectCsvFormula("=2+2") == "'=2+2", "CSV formula neutralized");
Assert(SecurityGuards.ProtectCsvFormula("\t@SUM(1,2)") == "'\t@SUM(1,2)", "CSV tab-prefixed formula neutralized");
Assert(SecurityGuards.ProtectCsvFormula("Safe process") == "Safe process", "regular CSV value unchanged");


// Multiple-copy updater regressions: updates must be scoped to one exact file.
string baseDir = Path.Combine(Path.GetTempPath(), "TCPViewerPro-UpdateTests");
string firstCopy = Path.Combine(baseDir, "copy-a", "TCPViewerPro.exe");
string equivalent = Path.Combine(baseDir, "copy-a", ".", "TCPViewerPro.exe");
string secondCopy = Path.Combine(baseDir, "copy-b", "TCPViewerPro.exe");
string numberedCopy = Path.Combine(baseDir, "copy-a", "TCPViewerPro (1).exe");
Assert(UpdateInstanceGuard.SameExePath(firstCopy, equivalent),
    "same executable path with dot component detected");
Assert(!UpdateInstanceGuard.SameExePath(firstCopy, secondCopy),
    "identically named EXEs in separate folders stay independent");
Assert(!UpdateInstanceGuard.SameExePath(firstCopy, numberedCopy),
    "numbered downloads are separate files");
Assert(!UpdateInstanceGuard.SameExePath(firstCopy, ""),
    "missing stored executable path triggers fresh check");
Assert(UpdateInstanceGuard.MutexNameForExecutable(firstCopy) ==
       UpdateInstanceGuard.MutexNameForExecutable(equivalent),
    "equivalent paths share an update lock");
Assert(UpdateInstanceGuard.MutexNameForExecutable(firstCopy) !=
       UpdateInstanceGuard.MutexNameForExecutable(secondCopy),
    "different executable paths receive distinct update locks");
Assert(UpdateInstanceGuard.BlockingMessage(
    new[] { new UpdateInstanceGuard.RunningCopy(1234, false) }, "TCPViewerPro.exe")
        .Contains("1234"), "lock warning identifies blocking PID");
if (OperatingSystem.IsWindows() && Environment.ProcessPath is { } runningExe)
{
    var withSelf = UpdateInstanceGuard.FindOtherRunningCopies(runningExe, -1);
    Assert(withSelf.Any(x => x.Pid == Environment.ProcessId),
        "find active instance using the current EXE path");
    var withoutSelf = UpdateInstanceGuard.FindOtherRunningCopies(
        runningExe, Environment.ProcessId);
    Assert(!withoutSelf.Any(x => x.Pid == Environment.ProcessId),
        "exclude the updating process from running-copy detection");
}


// Network Diagnostics v6.5: only properly formed hosts can be passed to DNS or Ping.
Assert(NetworkDiagnosticsService.TryNormalizeHost(" 1.1.1.1 ", out var pingHost)
       && pingHost == "1.1.1.1", "ping target trims whitespace safely");
Assert(NetworkDiagnosticsService.TryNormalizeHost("example.com", out _),
    "normal DNS hostname accepted");
Assert(NetworkDiagnosticsService.TryNormalizeHost("::1", out _),
    "IPv6 address accepted");
Assert(NetworkDiagnosticsService.TryNormalizeHost("my-device.local", out _),
    "local DNS hostname accepted");
Assert(!NetworkDiagnosticsService.TryNormalizeHost("", out _),
    "empty diagnostic target refused");
Assert(!NetworkDiagnosticsService.TryNormalizeHost("https://example.com/path", out _),
    "URL is not accepted as a hostname");
Assert(!NetworkDiagnosticsService.TryNormalizeHost("example.com:443", out _),
    "host-and-port string refused");
Assert(!NetworkDiagnosticsService.TryNormalizeHost("example.com\nother.com", out _),
    "newline injection refused");
Assert(!NetworkDiagnosticsService.TryNormalizeHost(new string('a', 254), out _),
    "overlong hostname refused");

Assert(NetworkDiagnosticsService.FormatSpeed(-1) == "Unknown",
    "unavailable adapter link speed shown as unknown");
Assert(NetworkDiagnosticsService.FormatSpeed(100_000_000) == "100 Mbps",
    "100 Mbps link speed formatted correctly");
Assert(NetworkDiagnosticsService.FormatSpeed(1_000_000_000) == "1 Gbps",
    "gigabit adapter speed formatted correctly");

Assert(!NetworkDiagnosticsService.IsUsableGateway(IPAddress.Parse("0.0.0.0")),
    "unspecified IPv4 gateway ignored");
Assert(!NetworkDiagnosticsService.IsUsableGateway(IPAddress.Parse("127.0.0.1")),
    "loopback gateway ignored");
Assert(!NetworkDiagnosticsService.IsUsableGateway(IPAddress.Parse("224.0.0.1")),
    "multicast gateway ignored");
Assert(!NetworkDiagnosticsService.IsUsableGateway(IPAddress.Parse("ff02::1")),
    "IPv6 multicast gateway ignored");
Assert(NetworkDiagnosticsService.IsUsableGateway(IPAddress.Parse("192.168.1.1")),
    "ordinary IPv4 router gateway accepted");

var noGateway = new NetworkDiagnosticsService.AdapterSnapshot(
    "Disconnected", "Down", "Ethernet", "None", "None", "None", "None",
    "Unavailable", "Unknown", new[] { IPAddress.Parse("192.168.5.1") });
var wifiGateway = new NetworkDiagnosticsService.AdapterSnapshot(
    "Wi-Fi", "Up", "Wireless80211", "192.168.1.100", "None",
    "192.168.1.1", "192.168.1.1", "Unavailable", "866 Mbps",
    new[] { IPAddress.Parse("192.168.1.1") });
var virtualGateway = new NetworkDiagnosticsService.AdapterSnapshot(
    "Virtual Adapter", "Up", "Tunnel", "10.0.0.20", "None", "10.0.0.1",
    "None", "Unavailable", "100 Mbps", new[] { IPAddress.Parse("10.0.0.1") });
var selectedGateway = NetworkDiagnosticsService.ChooseGateway(
    new[] { noGateway, virtualGateway, wifiGateway });
Assert(selectedGateway?.Adapter.Name == "Wi-Fi" &&
       selectedGateway?.Gateway.Equals(IPAddress.Parse("192.168.1.1")) == true,
    "gateway prefers active physical network adapter");
Assert(NetworkDiagnosticsService.ChooseGateway(new[] { noGateway }) is null,
    "offline adapters do not supply active gateway");
Assert(NetworkDiagnosticsService.FormatAdapterReport(new[] { wifiGateway })
        .Contains("192.168.1.1") &&
       NetworkDiagnosticsService.FormatAdapterReport(new[] { wifiGateway })
        .Contains("Wi-Fi"), "adapter report includes reported gateway and adapter");


var ok = NetworkHealthService.SummarizePing("Internet test", "1.1.1.1", 3,
    new long[] { 10, 12, 11 });
Assert(ok.Level == NetworkHealthService.HealthLevel.Good &&
       ok.Headline.Contains("all 3") && ok.Details.Contains("11 ms"),
    "plain-English internet ping success");
Assert(ok.Explanation.Contains("does not measure download speed"),
    "successful ping does not imply speed test");
var partial = NetworkHealthService.SummarizePing("Local router", "192.168.0.1", 3,
    new long[] { 4 });
Assert(partial.Level == NetworkHealthService.HealthLevel.Attention &&
       partial.Details.Contains("loss: 66%"),
    "partial router ping reports packet loss without false healthy label");
var silent = NetworkHealthService.SummarizePing("Internet test", "1.1.1.1", 2,
    Array.Empty<long>());
Assert(silent.Level == NetworkHealthService.HealthLevel.Attention &&
       silent.Explanation.Contains("block ping"),
    "silent test explicitly notes firewall blocking");
var goodDns = NetworkHealthService.SummarizeDns(2);
Assert(goodDns.Level == NetworkHealthService.HealthLevel.Good &&
       goodDns.Headline.Contains("worked"),
    "successful DNS summarized for beginners");
Assert(NetworkHealthService.SummarizeDns(0).Level ==
       NetworkHealthService.HealthLevel.Attention,
    "no DNS addresses triggers attention");
Assert(NetworkHealthService.SummarizeDns(0, "Name not known").Level ==
       NetworkHealthService.HealthLevel.Attention,
    "DNS error summarized without claiming malware");
var health = new NetworkHealthService.HealthResult(
    new[] { ok, partial, goodDns },
    Array.Empty<NetworkDiagnosticsService.AdapterSnapshot>(),
    new DateTime(2026, 11, 22));
Assert(health.Headline.Contains("attention") &&
       health.ToReport().Split("Local router", StringSplitOptions.None).Length == 2 &&
       health.ToReport().Contains("Internet test"),
    "beginner report preserves exact checks without raw adapter duplication");
var greenHealth = new NetworkHealthService.HealthResult(
    new[] { ok, goodDns }, Array.Empty<NetworkDiagnosticsService.AdapterSnapshot>(),
    DateTime.Now);
Assert(greenHealth.Headline.Contains("passed") &&
       greenHealth.Explanation.Contains("not a full internet speed"),
    "overall summary limits certainty to tests that actually ran");
Console.WriteLine($"All {passed} security checks passed.");
