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

Console.WriteLine($"All {passed} security checks passed.");
