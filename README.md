# TCP Viewer Pro

A Windows network connection monitor and process-investigation desktop app built using C# WinForms.

**Current source version: 6.6.2** (Midnight Dark theme, Security Center and Connection History refresh)

## Download TCP Viewer Pro (Windows 10/11, x64)

**[Download the latest TCPViewerPro.exe](https://github.com/techsupport993-creator/TCPviewerPRO/releases/latest/download/TCPViewerPro.exe)**

Or browse the **[GitHub Releases page](https://github.com/techsupport993-creator/TCPviewerPRO/releases)** to download files labeled with their version, such as `TCPViewerPro_v6.4.0.exe`, and the corresponding `.sha256` checksum. Releases also retain `TCPViewerPro.exe` for compatibility with older automatic updaters. Both EXEs for a given release contain identical bytes.

Extracting ZIP files or installing Python or the .NET runtime is **not required** for the self-contained EXE. Windows may display an unknown-publisher warning until code signing is configured; verify the release checksum and download only from this repository.

## Features

- Live IPv4/IPv6 TCP and UDP endpoints with owning-process PIDs
- Publisher filtering and offline Windows Authenticode trust checks
- Process inspection including SHA-256, executable path, signature, parent PID and command line (where accessible)
- Connection-state colors, selectable themes, pause/resume, saved table preferences
- Connection history, new-connection alerts, reverse DNS, CSV exports
- Confirmed IPv4 TCP disconnect and Windows Firewall outbound-IP blocking
- Optional GitHub Releases update checks with SHA-256 verification and explicit approval
- Theme-aware Process Inspector connections table, including recoloring when the theme changes
- Network Diagnostics: ping latency and packet loss, DNS lookup, gateway reachability, adapter IP/MAC/speed and DNS configuration, copy/save local reports
- About page with the running version, release downloads, source code, and privacy information

Some sidebar modules are planned placeholders, **not yet implemented**.

## Update reliability improvements in v6.4.3

When multiple copies of TCP Viewer Pro are present, **each EXE is its own installation** and can be a different version. The automatic updater replaces only the **currently opened executable**, keeping its existing filename; it does not silently replace or delete other copies in Downloads or other folders.

- **Independent update checks:** The 24-hour auto-check cache now includes the full running EXE path and its installed version. Checking a new or older copy is not suppressed just because a different copy was checked recently.
- **Two windows from the same EXE:** Before downloading an update, the app checks for other windows running from the same executable file. If found, it asks you to close the other windows and retry instead of failing after the original closes.
- **During installation:** The staged updater checks for other running instances again and uses a destination-specific named mutex. Two separate update operations cannot replace the same EXE concurrently. It does not terminate other processes.
- **Different EXE files:** Version-labelled files such as `TCPViewerPro_v6.2.0.exe`, browser duplicates like `TCPViewerPro (1).exe`, and copies in different directories are independent. Updating one does not update the others.
- Added Windows regression tests for file identity, per-target mutex naming, and detection of running copies.

**Test on Windows:** Make two files in separate folders (for example, v6.2.0 and v6.4.2). Open the older EXE and update it to v6.4.3. Reopen the other file; its version should remain unchanged. Then launch the same EXE twice, attempt its next update, and verify you receive a clear request to close the extra instance rather than an unexplained installation failure. The app remains unsigned; do not turn off Windows protections to run it.

## New in v6.6.2 — Midnight Dark + activity views

- **Midnight Dark** joins Matrix Green and Cyber Blue in Settings. The neutral charcoal palette has blue/silver accents, applies immediately across the app, and is saved for the next launch. Existing users keep their selected theme.
- **Security Center** now uses a readable, sortable-looking (but deliberately unsorted to preserve newest-first ordering) activity table with columns for observation time, application, PID, remote endpoint, TCP state and address type. Search by application/PID/address/state; filter public, private/local, or other addresses; copy selected event or export visible rows as CSV. Address classification is **not** a malware verdict.
- **Connection History** now has structured columns, searchable history and state filters, Copy Selected, Export CSV (currently visible rows), and a **confirmation before clearing**. All activity stays in memory unless the user exports it; existing exported files are never deleted.
- **Performance:** Activity grids update when new endpoints are observed or the user changes view/filter, not continuously without changes. Up to 2,000 observations are retained for the current session; Security Center displays up to 300 matches.
- **Privacy:** No remote telemetry, background upload, automatic process termination or changes to the updater have been introduced.
- **What to test on Windows:** Change between all three themes, restart to check persistence, visit Security Center and Connection History during active browsing, try the search and filters, export a CSV and inspect it, verify Copy Selected, clear the log (including Cancel), and resize the window at 100% and 150% display scale.

## Fixed in v6.6.1 — clearer Simple Mode result cards

- Corrects a layout bug visible in v6.6.0: the green "Responded" status appeared above the test name and explanatory text was clipped.
- Each card now has dedicated rows in this order: **Test name → Result → Plain-English explanation**, with enough space for the explanation.
- All checks and Advanced Mode are unchanged. The fix is cosmetic; no additional network activity or permissions are needed.
- Verify on Windows at your usual display scaling, then switch between Matrix Green and Cyber Blue to check readability.

## New in v6.6.0 — Simple / Advanced diagnostics

The Network Diagnostics page now opens in **SIMPLE** mode, with an **ADVANCED** button at the top right. Both modes share test results; changing modes does not rerun any network probes.

**Simple mode (default)**
- Click **CHECK CONNECTION** to explicitly run a small local test: a reported gateway (if present), three ICMP pings of Cloudflare's public IP `1.1.1.1`, and a DNS lookup for `example.com`.
- See three plain-English status cards (Local router, Internet test, Website names) with green checkmarks for full replies and yellow cautions for failures or missing information.
- An overall summary describes what passed and what may need attention. A failed ICMP ping **does not prove the internet or device is offline**—firewalls can block pings. This is **not a speed test or malware scanner**.
- **CANCEL**, **COPY REPORT**, and **SAVE REPORT** remain available. Reports stay local unless manually shared.

**Advanced mode**
- Keeps the individual Ping, DNS, Gateway, Refresh Adapters, and Run All controls, editable host fields, raw console-style output, and full adapter table.
- Shows detailed IPv4/IPv6 addresses, gateways, configured DNS servers, MAC addresses and reported link speeds. Advanced tests remain opt-in.

**Report cleanup**
- Initial on-screen instructions are no longer exported as if they were results.
- Adapter inventory appears **only once** in a saved report, after results have been collected.

**Windows test checklist**
1. Start v6.6.0, open **NETWORK DIAGNOSTICS**, and verify **SIMPLE** is selected automatically.
2. Click **CHECK CONNECTION**. Check that three cards and overall summary change from *Not checked* to meaningful results.
3. Click **ADVANCED**, confirm individual tests and adapter table are still present, then switch back and verify the summary is preserved.
4. Test **CANCEL** during a check. Export a report and verify startup instructions and duplicate adapter lists are absent.
5. Change between Cyber Blue and Matrix Green themes. Verify contrast and readable statuses in both modes.
6. From v6.5.0, update through **SETTINGS → APPLICATION UPDATES** and confirm the new view loads.

## New in v6.5.0 — Network Diagnostics

Open **NETWORK DIAGNOSTICS** in the left sidebar (under **DIAGNOSTIC TOOLS**, no longer a planned placeholder).

- **PING TEST:** enter a hostname or IP address and send up to three ICMP echo requests. Displays round-trip latency, packet loss, and status. ICMP timeouts do not necessarily mean a system is unreachable.
- **DNS LOOKUP:** resolve a hostname through the computer's configured Windows DNS resolver, displaying IPv4/IPv6 addresses.
- **GATEWAY TEST:** finds a gateway advertised by an active network adapter (preferring a physical wired/wireless adapter) and tries two pings. The displayed gateway is the adapter-reported gateway, not a proof of which route Windows would select.
- **REFRESH ADAPTERS:** show online/offline adapters, types, IPv4/IPv6 addresses, gateways, DNS servers, MAC addresses and reported link speed. Reported adapter speed is **not** measured Internet bandwidth.
- **RUN ALL:** execute the above tests on demand, with failures displayed separately so one failed test does not prevent other diagnostics.
- **CANCEL:** cancel an in-progress diagnostic; operations also time out automatically.
- **COPY REPORT / SAVE REPORT:** explicitly copy or export a human-readable `.txt` report for IT troubleshooting. Reports may include private IP addresses, MAC addresses and DNS servers; review before sharing.

No administrator privileges, external API keys, Python, system shell commands, port scans or background data uploads are required. **Ping and DNS queries only occur after the user clicks a test button.** Internet connectivity and DNS resolver configuration determine which test results are available.

### Suggested Windows test

1. Start `TCPViewerPro.exe` and open **NETWORK DIAGNOSTICS**.
2. Select **REFRESH ADAPTERS**. Confirm your Ethernet/Wi-Fi adapter, IPv4 address, gateway and DNS servers appear.
3. Keep `1.1.1.1` in **PING HOST / IP**, then click **PING TEST**. Ping may time out on networks that block ICMP.
4. Keep `example.com` in **DNS HOST**, click **DNS LOOKUP**, and verify that addresses are returned.
5. Click **GATEWAY TEST** and **RUN ALL**. Verify the window remains responsive and a missing gateway shows a clear message.
6. Click **COPY REPORT** and **SAVE REPORT**. Check the resulting text includes diagnostics and the local adapter inventory.
7. Change between Matrix Green and Cyber Blue themes. Check buttons, report output and adapter table remain legible.
8. Verify automatic updating from v6.4.3 to v6.5.0 still works.

The GitHub Actions build and regression checks validate code, but individual Windows adapter, DNS and gateway behavior requires testing on a real computer.

## Security improvements in v6.4.2

- Safer process termination: system-process protection and PID, start-time and executable identity checks.
- Safer firewall changes: explicit system-wide warnings, the Windows System32 `netsh.exe`, structured arguments, and exit-status verification.
- Improved update validation: exact release-asset URLs, limited metadata sizes and strict checksum parsing.
- Signed-update continuity: if the installed app is signed and verified, the updated EXE must use the same certificate fingerprint.
- CSV formula-injection protection for reports opened in spreadsheet software.
- Automated security regression tests in GitHub Actions before publishing new releases.

**The current release is not digitally code-signed.** See [CODE_SIGNING.md](CODE_SIGNING.md) for certificate, timestamping, and safe GitHub workflow requirements, and [SECURITY.md](SECURITY.md) for the security model and known limitations. A successful build or matching SHA-256 checksum alone does not mean Windows considers the software trusted.

You can run the dependency-free tests with:

```powershell
dotnet run --project TCPViewerPro.SecurityTests/TCPViewerPro.SecurityTests.csproj -c Release
```

## Build (Windows 10/11)

Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), download or clone this repository, and double-click `Build-TCPViewerPro.bat`.

Alternatively:

```powershell
dotnet restore TCPViewerPro/TCPViewerPro.csproj --configfile NuGet.Config -r win-x64
dotnet publish TCPViewerPro/TCPViewerPro.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

The result is designed as a self-contained, single-file Windows x64 executable.

GitHub Actions builds a standalone Windows x64 executable and stores an Actions build artifact. When the version in `TCPViewerPro/TCPViewerPro.csproj` has not been released yet, the workflow also creates a public GitHub Release containing a version-labelled EXE and checksum as well as the stable `TCPViewerPro.exe` and `TCPViewerPro.exe.sha256` updater assets.

To publish a future version, increment `<Version>` and `<FileVersion>` in the project file, commit the change to `main`, and confirm the workflow succeeds. The public download link above always points to the latest published release. Tags must match the project version (for example `v6.3.0`).

## Filename and automatic updater compatibility

- Public download filenames include the full version, for example `TCPViewerPro_v6.2.0.exe`, `TCPViewerPro_v6.3.0.exe` and `TCPViewerPro_v6.4.0.exe`.
- **Updater fix in v6.4.1:** When an older version checks GitHub for updates, it downloads the newest patched installer. The installer now supports `TCPViewerPro.exe`, version-labelled names such as `TCPViewerPro_v6.2.0.exe`, and Windows numbered duplicates such as `TCPViewerPro (1).exe` or `TCPViewerPro_v6.2.0 (1).exe`. It replaces the originally launched file **without renaming it**, if the folder is writable. Unrelated EXE names remain blocked.
- To keep multiple auto-updatable installations, create separate folders named `v6.2.0`, `v6.3.0` and so on, each containing a file named `TCPViewerPro.exe`. Do not overwrite your existing versions.

## Automatic updates

The default update repository is `techsupport993-creator/TCPviewerPRO`. Updates require a newer **GitHub Release** containing a compatible Windows EXE and valid SHA-256 digest/checksum. Uploading source alone does not publish an update. The application asks before installing.

## Updating from 6.2.0 to 6.3.0

To test the complete update flow on a Windows computer, keep the existing v6.2.0 executable in a **writable folder**, close other copies of the app, then select **Settings > Application Updates > CHECK NOW**. When v6.3.0 is published, approve the update; the app should download, verify, replace the old EXE, restart, and retain your theme and settings. On success, check **About** for version 6.3.0. If the app is installed in a protected folder such as Program Files, replace the EXE manually instead. Do not perform the first upgrade test on a critical computer; retain a backup.

## What's new in v6.3.0

- Process Inspector uses the same theme-aware, resizable network endpoints table as the main dashboard.
- Inspector windows update colors if you switch between Matrix Green and Cyber Blue.
- New About page shows the installed version and links directly to official releases and source code.
- Version is visible in the status bar.

## v6.4.0 changes

- The main window title now shows the installed version.
- The About page adds **COPY APP INFO**, which copies basic version, theme, OS and update-source details without copying connections or usernames.
- The Process Inspector adds **COPY ENDPOINTS** for the current process snapshot (tab-separated text).
- No changes to the v6.3.0 updater installation algorithm, so the v6.3.0 to v6.4.0 upgrade tests the existing updater.

## Verify an automatic upgrade from v6.3.0

1. Keep a backup of your original `TCPViewerPro.exe` and run the **v6.3.0** copy from a folder you can write to (for example `Documents\\TCPViewerProTest`), **not** `Program Files`.
2. Open **Settings > Application Updates > CHECK NOW** in v6.3.0; the expected result is **v6.4.0 available**. If the app already says v6.4.0, you started the new executable and haven't tested an upgrade.
3. Approve the update prompt and, for an unsigned build, review the publisher warning carefully. The updater should verify the SHA-256, replace the executable, and restart.
4. Verify the new window title says **v6.4.0** and the About page shows **6.4.0**.
5. Close/reopen the app; check your saved Matrix Green/Cyber Blue theme and table layout. In the original EXE folder, confirm the rollback copy `TCPViewerPro.exe.previous` exists.
6. Test **COPY APP INFO** and **COPY ENDPOINTS**. If any step fails, keep your backup and report the exact message. Do not disable antivirus or Windows security to force the update.

This test is not complete until it succeeds on a real Windows PC; a successful GitHub build only verifies compilation and packaging.

## v6.4.1 — Fix for automatic updates from renamed downloads

Versions 6.2.0–6.4.0 could check for, download and verify a newer release, but updating failed when the original EXE was named `TCPViewerPro_v6.2.0.exe` or `TCPViewerPro (1).exe`. The downloaded update's bootstrapper had a hard-coded destination name check.

**v6.4.1 removes that naming limitation** for recognized TCP Viewer Pro versioned and numbered EXE names. The staged updater still validates its SHA-256 digest, requires the target to exist outside its staging directory, waits for the running process to exit, and preserves a `.previous` rollback copy. The original filename remains unchanged, even if it says an earlier version.

### Test from an older version

1. Back up your original v6.2.0/v6.3.0/v6.4.0 EXE and launch it from a writable folder, not Program Files.
2. Keep its current name (for example `TCPViewerPro_v6.2.0.exe`); you should **not** need to rename it.
3. Open **Settings → Application Updates → CHECK NOW** and choose v6.4.1 after verifying the source is your trusted GitHub release. The updater asks for approval and may warn that the EXE is unsigned.
4. If installation succeeds, the application restarts. **About** and the title bar should show **6.4.1**, even if the file on disk still has an older version in its name.
5. Confirm your settings are preserved and the original folder contains a `.previous` backup.

A successful GitHub Actions build validates compilation but does not prove the replacement-and-restart sequence works on your PC; this test must be done on Windows.

## Important security and testing notes

The project has built successfully on GitHub Actions, but the executable still needs manual runtime/security testing on Windows before broad distribution.

Offline signature verification does not check online certificate revocation; a valid signature alone does not prove software is safe. Endpoint snapshots do not provide packet capture or per-process bandwidth measurement. Changing firewall rules or ending processes can disrupt applications. No automatic connection-data upload is implemented.

See `TEST_CHECKLIST.txt`, `UPDATE_TEST_CHECKLIST.txt` and `RELEASE_CHECKLIST.txt`.
