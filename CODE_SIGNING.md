# Code-signing readiness for TCP Viewer Pro

For the public release-approval, privacy, SignPath eligibility and verification plan, see [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md). No production signing provider is connected yet.

TCP Viewer Pro's currently published builds are **unsigned**. SHA-256 release hashes prove that a download matches the bytes GitHub published, but do **not** establish the identity of the publisher or guarantee the app is safe. Do not turn off Smart App Control or Defender to install it.

## Before signing

1. Obtain an Authenticode code-signing identity from a trusted provider (a suitable certificate authority or managed signing service, such as Microsoft Artifact Signing). The developer must complete provider identity verification. A self-signed certificate will not give ordinary Windows PCs publisher trust.
2. Use a signing service or a hardware-backed/private-key-protected certificate. Do not commit a PFX, private key, passwords, or temporary authentication tokens to this repository. Do not upload them as GitHub build artifacts.
3. Restrict production signing to protected GitHub environments and authorized release maintainers. Prefer short-lived identities for cloud signing, with reviews and a documented key-rotation process.
4. Keep the signed release's identity consistent. Since v6.4.2, automatic updates from an already-verified signed installation require the **same signer certificate SHA-256 fingerprint**. Any change in certificate identity requires a separately verified manual upgrade, even if the displayed publisher name stays the same.

## Order of operations for a signed release

**Build → sign the exact EXE → verify its Authenticode signature → compute checksums of the signed EXE → publish both EXE filenames and checksums.**

Signing after calculating the checksum changes the executable bytes and breaks the updater.

With Windows SDK SignTool available and a trusted signing certificate already configured in the Windows certificate store, a maintainer can follow this pattern:

```powershell
signtool sign /sha1 <CERTIFICATE_SHA1_THUMBPRINT> /fd SHA256 /tr <TRUSTED_RFC3161_TIMESTAMP_URL> /td SHA256 ".\TCPViewerPro.exe"
signtool verify /pa /v ".\TCPViewerPro.exe"
Get-FileHash ".\TCPViewerPro.exe" -Algorithm SHA256
```

**These are maintainer instructions, not a working CI signing configuration.** The placeholders require a certificate, approved timestamp service, and secure key setup. Signing has not been enabled automatically.

## GitHub Actions controls

- The release workflow reports the EXE's current Authenticode status before hashing and publishing it.
- Once signing has been configured **before that verification step**, set the GitHub repository Actions variable `REQUIRE_SIGNED_RELEASE` to `true`. The workflow will then fail rather than publish an unsigned build.
- Do **not** set this variable and assume it signs anything; it is a guardrail, not a signing service.
- Do not release an unsigned build while advertising it as signed. Never put a code-signing secret in workflow YAML.

Microsoft references: [SignTool](https://learn.microsoft.com/windows/win32/seccrypto/signtool) and [Authenticode timestamping](https://learn.microsoft.com/windows/win32/seccrypto/time-stamping-authenticode-signatures).

Even correctly signed new software can initially have limited SmartScreen reputation. Keep Windows security enabled and investigate any security alert rather than instructing users to disable it.
