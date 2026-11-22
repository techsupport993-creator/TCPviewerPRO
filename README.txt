TCP VIEWER PRO v6.2 — MATRIX GREEN + CAT ICON + UPDATES
=======================================================

NEW IN v6.2
-----------
- Green cyber-cat Windows executable icon (also shown in the taskbar/title bar)
- Application version 6.2.0.0
- Update preferences in SETTINGS: GitHub owner/repository source
- Automatic checks at startup, at most once every 24 hours (optional)
- CHECK NOW button for manual release lookup
- Explicit approval before downloading or installing an update
- Mandatory SHA-256 verification of the downloaded executable
- Refuses publisher changes when the installed application is Authenticode-trusted
- Single-EXE self-updater: stages a new EXE, exits, replaces the old EXE,
  keeps TCPViewerPro.exe.previous as rollback backup, and restarts
- No Python, PowerShell, or command prompt required when running the app

HOW TO BUILD
------------
1. Extract this ZIP to its own folder.
2. Install the official .NET 8 SDK if it is not already installed:
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0
3. Double-click Build-TCPViewerPro.bat and allow first-time NuGet restore.
4. The builder copies the self-contained TCPViewerPro.exe into this folder.

HOW TO CONFIGURE UPDATES
------------------------
1. You must own or trust a public GitHub repository used for releases.
   No arbitrary update server or repository has been invented for this app.
2. Open TCP Viewer Pro -> SETTINGS -> APPLICATION UPDATES.
3. Enter the source as OWNER/REPOSITORY (not a full URL).
   Example: yourusername/tcpviewerpro
4. Click SAVE SOURCE and then CHECK NOW.
5. Enable or disable startup checks with the checkbox. The app never
   downloads or installs an update without confirmation.

HOW TO PUBLISH AN UPDATE
------------------------
1. Change <Version> and <FileVersion> in TCPViewerPro.csproj, such as 6.3.0.
2. Build the standalone EXE with Build-TCPViewerPro.bat.
3. Create a GitHub Release, tag v6.3.0, in your chosen repository.
4. Upload the binary named exactly TCPViewerPro.exe.
5. The update checker reads the GitHub release asset's sha256: digest, or
   an additional asset named TCPViewerPro.exe.sha256 with the expected hash.
   GitHub's releases API may provide the asset digest directly. If not,
   create the .sha256 file yourself and attach it to the SAME release.
6. On a computer with an older version, use SETTINGS -> CHECK NOW.

SECURITY AND LIMITATIONS
------------------------
- GitHub release source must be entered by the user; updater is inactive
  until a repository is configured. No auto-updates are available by default.
- HTTPS plus a checksum published by the same GitHub release DOES NOT
  independently authenticate the software publisher. Production releases
  should be code-signed and published from a controlled GitHub account.
- Existing trusted signer is required to match the new trusted signer.
- When running a self-built/unsigned app, the user sees an extra warning.
- A machine without internet access can use the app normally without updates.
- Updates need write access to the EXE's installation folder. If installed
  in Program Files without the correct permissions, automatic replacement
  can fail; download the release manually or run a proper installer.
- The previous EXE is backed up as TCPViewerPro.exe.previous on successful
  replacement. Old preferences are retained under %LOCALAPPDATA%\TCPViewerPro.
- The updater does not run as a background Windows service and will not
  silently install or execute unknown code.
- Windows executable building and live self-update were NOT tested in this
  Linux environment. Test carefully on a secondary Windows PC before relying on it.

OTHER FEATURES RETAINED
-----------------------
Matrix Green/Cyber Blue theme switch; custom connection colors; red .exe
highlighting; pause/resume; direct Windows TCP/UDP endpoint ownership;
verified publisher filtering; saved table layout; process grouping;
process inspector and hashing; CSV exports; DNS lookup; firewall IP block;
TCP disconnect; connection history; sidebar navigation.
