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
