# TCP Viewer Pro

A Windows network connection monitor and process-investigation desktop app built using C# WinForms.

**Current source version: 6.2.0** (Matrix Green theme and cyber-cat icon)

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

A Windows CI build workflow also publishes downloadable artifacts when it succeeds. **Build artifacts are not GitHub Releases**.

## Automatic updates

The default update repository is `techsupport993-creator/TCPviewerPRO`. Updates require a newer **GitHub Release** containing a compatible Windows EXE and valid SHA-256 digest/checksum. Uploading source alone does not publish an update. The application asks before installing.

## Important security and testing notes

The source has not yet been compiled or runtime-tested in the authoring environment. Validate the Windows build before distributing it.

Offline signature verification does not check online certificate revocation; a valid signature alone does not prove software is safe. Endpoint snapshots do not provide packet capture or per-process bandwidth measurement. Changing firewall rules or ending processes can disrupt applications. No automatic connection-data upload is implemented.

See `TEST_CHECKLIST.txt`, `UPDATE_TEST_CHECKLIST.txt` and `RELEASE_CHECKLIST.txt`.
