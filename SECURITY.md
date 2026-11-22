# TCP Viewer Pro security policy

## Supported releases

Keep the latest published release installed. Older versions may lack updater, firewall-action, and process-safety fixes.

## What the application does

- Reads local TCP/UDP ownership data and process metadata for diagnostics. **No automatic upload of connection or process data** is implemented.
- Offers explicit-confirmation actions to close a selected IPv4 TCP connection, terminate an ordinary user application, or create a Windows Defender Firewall outbound rule.
- Runs as the current user (`asInvoker`); firewall changes use a separate Windows administrator/UAC prompt.
- Checks an optionally configured GitHub Releases repository for a newer version. Downloading/installing needs user approval and verifies the published SHA-256 digest.
- Uses offline Windows Authenticode checks; **certificate revocation is not checked online**. A verified signature does not mean a program is malware-free.

## Controls added for v6.4.2

- Process termination refuses critical Windows processes and files under the Windows system directory, requires an accessible executable path, and rechecks process name, path, and start time before termination.
- Firewall rule creation uses the Windows System32 `netsh.exe` path, an argument list (not a shell command string), explicit system-wide warnings, and checks its exit status. Loopback, unspecified, broadcast, multicast, and link-local addresses are rejected; local/private targets get an extra warning.
- IPv4 TCP disconnect checks that the connection still belongs to the selected PID before attempting it. A network connection can still change between validation and action.
- GitHub release EXE/sha256 URLs must match the configured repo, exact release tag, and expected asset filename. Metadata and checksum manifest sizes are limited. SHA-256 manifest parsing is tied to the EXE filename.
- A previously Authenticode-verified app can auto-update only to an EXE with the same verified signer certificate fingerprint; signed publisher changes require a separately verified manual migration.
- CSV fields that might be interpreted by spreadsheets as formulas are neutralized before export.
- PR and release builds run the dependency-free `TCPViewerPro.SecurityTests` regression suite.

## Known limitations and remaining work

- **Unsigned releases have no cryptographic developer-identity proof.** A SHA-256 published by the same GitHub account does not protect against that account being compromised. Obtain a trusted signing identity before widespread redistribution.
- The in-app GitHub update-source setting can be changed. Only configure a repository you independently trust; a different repository can publish different executables.
- Checksums and local Windows API verification do not replace antivirus scanning or independent code review. Offline signature checks cannot establish current revocation status.
- The process, endpoint, and firewall checks reduce accidental harm but are subject to operating-system races, administrator permissions, and Windows networking behavior.
- No unattended/hidden process termination, remote administration, or silent update installation is intended.

## Report a security issue

For a sensitive vulnerability, contact the repository maintainer privately via GitHub's private vulnerability reporting feature **if it is enabled**. Avoid posting exploit details, credentials, or private host data in public GitHub issues. For non-sensitive defects, use a normal issue with reproduction steps, the release version, and redacted logs.

## Safe verification

Run `dotnet run --project TCPViewerPro.SecurityTests/TCPViewerPro.SecurityTests.csproj -c Release` for deterministic guard checks. On a disposable/test Windows computer, verify that the app still monitors TCP/UDP, that firewall operations require a UAC prompt and clear warning, and that the updater preserves the `.previous` backup. Do not test process termination against essential Windows processes or disconnect a network connection you depend on.
