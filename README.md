# TCP Viewer Pro

A Windows network connection monitor and process-investigation desktop app built using C# WinForms.

**Current source version: 6.2.0** (Matrix Green theme and cyber-cat icon)

## Download TCP Viewer Pro (Windows 10/11, x64)

**[Download the latest TCPViewerPro.exe](https://github.com/techsupport993-creator/TCPviewerPRO/releases/latest/download/TCPViewerPro.exe)**

Or browse the **[GitHub Releases page](https://github.com/techsupport993-creator/TCPviewerPRO/releases)** to see version notes and download the matching `TCPViewerPro.exe.sha256` checksum.

Extracting ZIP files or installing Python or the .NET runtime is **not required** for the self-contained EXE. Windows may display an unknown-publisher warning until code signing is configured; verify the release checksum and download only from this repository.

## Features

- Live IPv4/IPv6 TCP and UDP endpoints with owning-process PIDs
- Publisher filtering and offline Windows Authenticode trust checks
- Process inspection including SHA-256, executable path, signature, parent PID and command line (where accessible)
- Connection-state colors, selectable themes, pause/resume, saved table preferences
- Connection history, new-connection alerts, reverse DNS, CSV exports
- Confirmed IPv4 TCP disconnect and Windows Firewall outbound-IP blocking
- Optional GitHub Releases update checks with SHA-256 verification and explicit approval

Some sidebar modules are planned placeholders, **not yet implemented**.

## Build (Windows 10/11)

Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), download or clone this repository, and double-click `Build-TCPViewerPro.bat`.

Alternatively:

```powershell
dotnet restore TCPViewerPro/TCPViewerPro.csproj --configfile NuGet.Config -r win-x64
dotnet publish TCPViewerPro/TCPViewerPro.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

The result is designed as a self-contained, single-file Windows x64 executable.

GitHub Actions builds a standalone Windows x64 executable and stores an Actions build artifact. When the version in `TCPViewerPro/TCPViewerPro.csproj` has not been released yet, the workflow also creates a public GitHub Release containing `TCPViewerPro.exe` and `TCPViewerPro.exe.sha256`.

To publish a future version, increment `<Version>` and `<FileVersion>` in the project file, commit the change to `main`, and confirm the workflow succeeds. The public download link above always points to the latest published release. Tags must match the project version (for example `v6.3.0`).

## Automatic updates

The default update repository is `techsupport993-creator/TCPviewerPRO`. Updates require a newer **GitHub Release** containing a compatible Windows EXE and valid SHA-256 digest/checksum. Uploading source alone does not publish an update. The application asks before installing.

## Important security and testing notes

The project has built successfully on GitHub Actions, but the executable still needs manual runtime/security testing on Windows before broad distribution.

Offline signature verification does not check online certificate revocation; a valid signature alone does not prove software is safe. Endpoint snapshots do not provide packet capture or per-process bandwidth measurement. Changing firewall rules or ending processes can disrupt applications. No automatic connection-data upload is implemented.

See `TEST_CHECKLIST.txt`, `UPDATE_TEST_CHECKLIST.txt` and `RELEASE_CHECKLIST.txt`.
