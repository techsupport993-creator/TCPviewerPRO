using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TCPViewerPro;

/// <summary>
/// Opt-in GitHub Releases update checks. No update host is preconfigured,
/// no executable is downloaded or launched without user confirmation.
/// Each download requires a SHA-256 supplied by GitHub's release-asset digest
/// (or a published .sha256 companion) and is verified before execution.
/// </summary>
public sealed partial class MainForm
{
    private sealed class UpdatePreferences
    {
        public string Repository { get; set; } = "techsupport993-creator/TCPviewerPRO";
        public bool AutoCheck { get; set; } = true;
        public DateTimeOffset LastCheckedUtc { get; set; } = DateTimeOffset.MinValue;
        // Cache update checks for the exact EXE and version that was checked.
        // Multiple copies must not inherit each other's 24-hour suppression.
        public string LastCheckedExecutablePath { get; set; } = "";
        public string LastCheckedExecutableVersion { get; set; } = "";
    }

    private sealed record AvailableRelease(Version Version, string Tag,
        string AssetUrl, string Sha256, long Size, string Notes);

    private static readonly string UpdatePreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TCPViewerPro", "update-settings.json");

    private readonly UpdatePreferences updatePrefs = new();
    private Label? updateStatusLabel;
    private bool isCheckingUpdates;

    private static readonly HttpClient UpdateHttp = CreateUpdateHttpClient();

    private static HttpClient CreateUpdateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("TCPViewerPro",
                typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "6.4.2"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private void LoadUpdatePreferences()
    {
        try
        {
            if (!File.Exists(UpdatePreferencesPath)) return;
            var saved = JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(UpdatePreferencesPath));
            if (saved is null) return;
            updatePrefs.Repository = saved.Repository ?? "";
            updatePrefs.AutoCheck = saved.AutoCheck;
            updatePrefs.LastCheckedUtc = saved.LastCheckedUtc;
            updatePrefs.LastCheckedExecutablePath = saved.LastCheckedExecutablePath ?? "";
            updatePrefs.LastCheckedExecutableVersion = saved.LastCheckedExecutableVersion ?? "";
        }
        catch { /* Malformed settings must not prevent the network monitor opening. */ }
    }

    private void SaveUpdatePreferences()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UpdatePreferencesPath)!);
            File.WriteAllText(UpdatePreferencesPath, JsonSerializer.Serialize(updatePrefs,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save update settings:\n" + ex.Message,
                "Update settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BuildUpdateSettingsSection(FlowLayoutPanel scroll)
    {
        scroll.Controls.Add(new Label
        {
            Text = "APPLICATION UPDATES", Font = new Font("Segoe UI Semibold", 17F),
            ForeColor = cyan, AutoSize = true, Margin = new Padding(4, 13, 4, 8)
        });
        scroll.Controls.Add(new Label
        {
            Text = "Automatically check GitHub Releases when launching the app (at most once every 24 hours " +
                   "per running copy/version). Installing always requires your approval.",
            ForeColor = muted, Width = 580, Height = 42, Margin = new Padding(4, 0, 4, 6)
        });
        scroll.Controls.Add(new Label
        {
            Text = "GitHub update repository (owner/repository):", ForeColor = Color.White,
            AutoSize = true, Margin = new Padding(4, 4, 4, 5)
        });
        var repoBox = new TextBox
        {
            Text = updatePrefs.Repository, Width = 340, BackColor = panel2,
            ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "example-owner/tcp-viewer-pro",
            Margin = new Padding(4, 0, 4, 7)
        };
        scroll.Controls.Add(repoBox);
        var autoBox = new CheckBox
        {
            Text = "Automatically check when the app starts", Checked = updatePrefs.AutoCheck,
            ForeColor = Color.White, AutoSize = true, Margin = new Padding(7, 4, 4, 9)
        };
        autoBox.CheckedChanged += (_, _) =>
        {
            updatePrefs.AutoCheck = autoBox.Checked;
            SaveUpdatePreferences();
        };
        scroll.Controls.Add(autoBox);
        scroll.Controls.Add(new Label
        {
            Text = "Multiple copies: only the EXE you opened will update. Other downloaded EXEs remain " +
                   "unchanged. Close any other windows using the same EXE before installing.",
            ForeColor = muted, Width = 600, Height = 42, Margin = new Padding(6, 0, 4, 10)
        });

        var buttons = new FlowLayoutPanel
        {
            Width = 490, Height = 43, WrapContents = false, BackColor = panel,
            Margin = new Padding(4, 2, 4, 6)
        };
        var saveButton = new Button
        {
            Text = "SAVE SOURCE", Width = 132, Height = 32,
            BackColor = panel2, ForeColor = cyan, FlatStyle = FlatStyle.Flat
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += (_, _) =>
        {
            string repo = repoBox.Text.Trim();
            if (repo.Length > 0 && !ValidRepo(repo))
            {
                MessageBox.Show(this, "Enter a GitHub repository as owner/repository, with no URL or spaces.",
                    "Invalid update source", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            updatePrefs.Repository = repo;
            updatePrefs.LastCheckedUtc = DateTimeOffset.MinValue;
            SaveUpdatePreferences();
            SetUpdateStatus(repo.Length == 0
                ? "Update source disabled. No automatic network requests will be made."
                : $"Saved: {repo}. Click CHECK NOW to search for a new release.");
        };
        var checkButton = new Button
        {
            Text = "CHECK NOW", Width = 132, Height = 32,
            BackColor = panel2, ForeColor = green, FlatStyle = FlatStyle.Flat
        };
        checkButton.FlatAppearance.BorderSize = 0;
        checkButton.Click += async (_, _) =>
        {
            if (repoBox.Text.Trim() != updatePrefs.Repository)
            {
                MessageBox.Show(this, "Click SAVE SOURCE first to save the repository you entered.");
                return;
            }
            await CheckForUpdatesAsync(silent: false);
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(checkButton);
        scroll.Controls.Add(buttons);

        updateStatusLabel = new Label
        {
            Text = updatePrefs.Repository.Length == 0
                ? "Not configured. Enter the GitHub repository that will publish your EXE releases."
                : $"Release source: {updatePrefs.Repository}",
            ForeColor = muted, Height = 36, Width = 600,
            Margin = new Padding(5, 0, 4, 12)
        };
        scroll.Controls.Add(updateStatusLabel);
    }

    private static bool ValidRepo(string repo) => Regex.IsMatch(repo,
        @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,99}/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}\z");

    private void SetUpdateStatus(string message)
    {
        if (updateStatusLabel is not null && !updateStatusLabel.IsDisposed)
            updateStatusLabel.Text = message;
    }

    private async Task AutoCheckForUpdatesAsync()
    {
        if (!updatePrefs.AutoCheck || !ValidRepo(updatePrefs.Repository)) return;
        string currentPath = Path.GetFullPath(Application.ExecutablePath);
        string version = (typeof(MainForm).Assembly.GetName().Version ??
            new Version(0, 0)).ToString();
        bool sameCopy = UpdateInstanceGuard.SameExePath(
            currentPath, updatePrefs.LastCheckedExecutablePath);
        if (sameCopy &&
            updatePrefs.LastCheckedExecutableVersion.Equals(version, StringComparison.Ordinal) &&
            DateTimeOffset.UtcNow - updatePrefs.LastCheckedUtc < TimeSpan.FromHours(24))
            return;
        await CheckForUpdatesAsync(silent: true);
    }

    private async Task CheckForUpdatesAsync(bool silent)
    {
        if (isCheckingUpdates) return;
        string repo = updatePrefs.Repository;
        if (!ValidRepo(repo))
        {
            if (!silent)
                MessageBox.Show(this, "Set a valid GitHub owner/repository in SETTINGS first.",
                    "Updates not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        isCheckingUpdates = true;
        SetUpdateStatus("Checking for new releases...");
        try
        {
            AvailableRelease release = await FindLatestReleaseAsync(repo);
            updatePrefs.LastCheckedUtc = DateTimeOffset.UtcNow;
            updatePrefs.LastCheckedExecutablePath = Path.GetFullPath(Application.ExecutablePath);
            updatePrefs.LastCheckedExecutableVersion = (typeof(MainForm).Assembly.GetName().Version ??
                new Version(0, 0)).ToString();
            SaveUpdatePreferences();
            Version currentVersion = typeof(MainForm).Assembly.GetName().Version ?? new Version(0, 0);
            if (release.Version <= currentVersion)
            {
                SetUpdateStatus($"Up to date (installed {currentVersion}; latest {release.Version}).");
                if (!silent) MessageBox.Show(this, "You're running the latest published version.",
                    "No update needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetUpdateStatus($"Update available: v{release.Version}. Install when you're ready.");
            string summary = release.Notes.Length > 500 ? release.Notes[..500] + "..." : release.Notes;
            var accept = MessageBox.Show(this,
                $"New TCP Viewer Pro release: {release.Tag}\n" +
                $"Installed: {currentVersion}\n\n" +
                $"Publisher: github.com/{repo}\n\n{summary}\n\n" +
                $"This file will be updated:\n{Application.ExecutablePath}\n\n" +
                "Other downloaded copies are NOT updated. The app will restart. " +
                "Only trust release repositories controlled by the developer.",
                "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (accept != DialogResult.Yes) return;
            // Two windows opened from the *same* file can keep it locked.
            // Different EXEs in other locations are independent copies.
            var otherInstances = UpdateInstanceGuard.FindOtherRunningCopies(
                Application.ExecutablePath, Environment.ProcessId);
            if (otherInstances.Count != 0)
            {
                string msg = UpdateInstanceGuard.BlockingMessage(otherInstances,
                    Path.GetFileName(Application.ExecutablePath));
                SetUpdateStatus("Close other windows using this executable before installing.");
                MessageBox.Show(this, msg, "Other copy still running",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            await DownloadAndStageUpdateAsync(release, repo);
        }
        catch (Exception ex)
        {
            SetUpdateStatus("Update check failed: " + ex.Message);
            if (!silent)
                MessageBox.Show(this, "Update failed:\n" + ex.Message,
                    "Update checker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { isCheckingUpdates = false; }
    }

    private static async Task<AvailableRelease> FindLatestReleaseAsync(string repo)
    {
        var uri = new Uri($"https://api.github.com/repos/{repo}/releases/latest");
        using var response = await UpdateHttp.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("No public GitHub release found for that repository.");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse((ReadOnlyMemory<byte>)
            await ReadBoundedContentAsync(response.Content, 1024 * 1024));
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"\Av[0-9]+\.[0-9]+\.[0-9]+\z") ||
            !Version.TryParse(tag[1..], out Version? version))
            throw new InvalidOperationException("Latest release tag must be exactly vMAJOR.MINOR.PATCH.");

        string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        JsonElement? exeAsset = null;
        JsonElement? shaAsset = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            if (name.Equals("TCPViewerPro.exe", StringComparison.OrdinalIgnoreCase)) exeAsset = asset;
            if (name.Equals("TCPViewerPro.exe.sha256", StringComparison.OrdinalIgnoreCase)) shaAsset = asset;
        }
        if (!exeAsset.HasValue)
            throw new InvalidOperationException("Latest release does not contain TCPViewerPro.exe.");

        var binary = exeAsset.Value;
        string url = binary.GetProperty("browser_download_url").GetString() ?? "";
        ValidateReleaseUrl(url, repo, tag, "TCPViewerPro.exe");
        long size = binary.GetProperty("size").GetInt64();
        if (size < 50_000 || size > 400_000_000)
            throw new InvalidOperationException("Unexpected update executable size.");
        string digest = binary.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
        string sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? digest[7..] : "";

        if (!ValidSha256(sha) && shaAsset.HasValue)
        {
            string shaUrl = shaAsset.Value.GetProperty("browser_download_url").GetString() ?? "";
            ValidateReleaseUrl(shaUrl, repo, tag, "TCPViewerPro.exe.sha256");
            using var shaResponse = await UpdateHttp.GetAsync(shaUrl);
            shaResponse.EnsureSuccessStatusCode();
            string shaText = Encoding.UTF8.GetString(await ReadBoundedContentAsync(shaResponse.Content, 4096));
            sha = SecurityGuards.ReadReleaseSha256Manifest(shaText) ?? "";
        }
        if (!ValidSha256(sha))
            throw new InvalidOperationException("Release is missing a usable SHA-256 digest. " +
                "Include the GitHub asset digest or TCPViewerPro.exe.sha256.");
        return new AvailableRelease(version, tag, url, sha, size, notes);
    }

    private static void ValidateReleaseUrl(string url, string repo, string tag, string filename)
    {
        if (!SecurityGuards.IsExpectedReleaseAssetUrl(url, repo, tag, filename))
            throw new InvalidOperationException(
                "Release URL is not the expected GitHub asset for this repository, tag and filename.");
    }

    private static async Task<byte[]> ReadBoundedContentAsync(HttpContent content, int limit)
    {
        if (content.Headers.ContentLength is long length && length > limit)
            throw new InvalidOperationException("GitHub response is larger than expected.");
        await using var stream = await content.ReadAsStreamAsync();
        using var data = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer.AsMemory())) != 0)
        {
            if (data.Length + count > limit)
                throw new InvalidOperationException("GitHub response exceeds allowed size.");
            data.Write(buffer, 0, count);
        }
        return data.ToArray();
    }

    private static bool ValidSha256(string sha) => Regex.IsMatch(sha, @"\A[0-9a-fA-F]{64}\z");

    private async Task DownloadAndStageUpdateAsync(AvailableRelease release, string repo)
    {
        string? stagedPath = null;
        try
        {
            string updateDir = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "TCPViewerPro", "Updates",
                "v" + release.Version, Environment.ProcessId + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(updateDir);
            stagedPath = Path.Combine(updateDir, "TCPViewerPro.exe");

            SetUpdateStatus($"Downloading {release.Tag}...");
            using (var response = await UpdateHttp.GetAsync(release.AssetUrl,
                HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long length && length != release.Size)
                    throw new InvalidOperationException("Release size does not match the published asset.");
                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = new FileStream(stagedPath, FileMode.Create,
                    FileAccess.Write, FileShare.None, 65536, useAsync: true);
                var buffer = new byte[65536];
                long total = 0;
                while (true)
                {
                    int bytes = await input.ReadAsync(buffer);
                    if (bytes == 0) break;
                    total += bytes;
                    if (total > release.Size || total > 400_000_000)
                        throw new InvalidOperationException("Download exceeds the expected release size.");
                    await output.WriteAsync(buffer.AsMemory(0, bytes));
                }
                if (total != release.Size)
                    throw new InvalidOperationException("Download is incomplete.");
            }

            SetUpdateStatus("Checking downloaded SHA-256...");
            await using (var read = File.OpenRead(stagedPath))
            {
                byte[] hash = await SHA256.HashDataAsync(read);
                string actual = Convert.ToHexString(hash);
                if (!actual.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Checksum mismatch. Update aborted.");
            }
            using (var stream = File.OpenRead(stagedPath))
            {
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                    throw new InvalidOperationException("Downloaded update is not a Windows executable.");
            }

            string original = Application.ExecutablePath;
            var oldSignature = SignatureVerifier.Check(original);
            var newSignature = SignatureVerifier.Check(stagedPath);
            bool priorTrusted = oldSignature.Category.StartsWith("Verified", StringComparison.OrdinalIgnoreCase);
            if (priorTrusted && (!newSignature.Category.StartsWith("Verified", StringComparison.OrdinalIgnoreCase) ||
                 !SecurityGuards.SameSignerFingerprint(
                    oldSignature.CertificateFingerprintSha256, newSignature.CertificateFingerprintSha256)))
                throw new InvalidOperationException(
                    "The update's verified signing certificate does not match the installed application. " +
                    "Automatic update blocked; certificate rotation requires a separately verified manual upgrade.");

            if (!priorTrusted)
            {
                if (MessageBox.Show(this,
                    $"The currently installed application is not publisher-verified.\n" +
                    $"The new release's SHA-256 matches github.com/{repo}, " +
                    "but that alone cannot prove the developer's identity.\n\n" +
                    $"New release signature: {newSignature.Category}\n" +
                    "Only continue if this is a GitHub repository you trust.\n\nInstall anyway?",
                    "Unverified update publisher", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }

            var process = new ProcessStartInfo(stagedPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(stagedPath)!
            };
            process.ArgumentList.Add("--apply-update");
            process.ArgumentList.Add(original);
            process.ArgumentList.Add(release.Sha256);
            process.ArgumentList.Add(Environment.ProcessId.ToString());
            if (Process.Start(process) is null)
                throw new InvalidOperationException("Could not launch the staged updater.");
            SetUpdateStatus("Installing update and restarting...");
            Close(); // The staged EXE waits for this process to exit before replacing it.
        }
        catch
        {
            // Leave the previously installed app untouched when checks fail.
            if (stagedPath is not null)
            {
                try { File.Delete(stagedPath); } catch { }
            }
            throw;
        }
    }
}
