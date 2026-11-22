# TCP Viewer Pro

A Windows network connection monitor and process-investigation desktop app built using C# WinForms.

**Current source version: 6.4.0** (small usability release for testing updates from v6.3.0)

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
- About page with the running version, release downloads, source code, and privacy information

Some sidebar modules are planned placeholders, **not yet implemented**.

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
- For **in-app automatic updates**, run the executable named **`TCPViewerPro.exe`** in a writable folder. The current updater (including v6.3.0 and v6.4.0) refuses to replace other names. Version-labelled downloads are for identifying, keeping and manually launching different versions; a filename alone does not establish the version of an installed program.
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

## Important security and testing notes

The project has built successfully on GitHub Actions, but the executable still needs manual runtime/security testing on Windows before broad distribution.

Offline signature verification does not check online certificate revocation; a valid signature alone does not prove software is safe. Endpoint snapshots do not provide packet capture or per-process bandwidth measurement. Changing firewall rules or ending processes can disrupt applications. No automatic connection-data upload is implemented.

See `TEST_CHECKLIST.txt`, `UPDATE_TEST_CHECKLIST.txt` and `RELEASE_CHECKLIST.txt`.
