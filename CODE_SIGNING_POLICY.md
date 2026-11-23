# TCP Viewer Pro — Code signing policy

**Status: unsigned; preparing to apply for trusted signing.** This page is a
security and release plan, **not** a claim of a signed binary or approval by
any signing provider.

## Public release and scope

- Project: TCP Viewer Pro, a GPL-3.0 Windows network connection monitor.
- Official source and release workflow: this public GitHub repository.
- Current published release: v6.6.2, **unsigned**.
- Intended signed artifacts: the project's self-contained Windows x64
  `TCPViewerPro.exe` and version-labelled copy built from this source.
- Existing releases and their SHA-256 checksums are immutable. Signing a
  compiled file changes its bytes; a newly signed release will get a **new
  version**, a new checksum, and a new tag. We will not silently replace v6.6.2.

## Trust goals and current limitations

We intend to seek free signing for eligible open-source software through
[SignPath Foundation](https://signpath.org/). The foundation has **not
accepted this project**, and there is currently **no SignPath signature**.
If accepted, the verified certificate publisher would be **SignPath
Foundation**, not the app brand or a personally verified developer.

Eligibility is subject to [SignPath Foundation's conditions](https://signpath.org/terms).
The project must undergo provider review, including its security-tool
features. This app observes connections and provides explicit Windows
network/process management actions. Its proposed future active scanner is
not implemented. This description is provided for truthful eligibility review
and does not imply that a provider will accept the project.

Alternative: an individually or organizationally validated code-signing
identity from a recognized certificate authority or Microsoft's Azure
Artifact Signing. Identity verification and paid service configuration
are necessary in that route.

## Signing authority and change control

- **Source authors and maintainers:** repository collaborators authorized to
  create changes through their GitHub accounts.
- **Code reviewers:** an authorized maintainer reviews untrusted contributions
  before they reach protected release source. No contributor can certify
  their own unreviewed outside contribution.
- **Signing approver:** an explicitly authorized maintainer, configured in
  the signing provider. This role is **not yet provisioned**.
- All maintainers/reviewers/approvers with source or signing access must use
  multifactor authentication.
- No signing token, certificate private key, PFX, or password will be stored
  in the repository, release assets, or logs.
- The signing provider holds the production key when using a managed service.
- A signing request must originate from an auditable automated build linked
  to a specific public source commit and require authorized approval.

## Release sequence (only after approval and configuration)

1. Review the source, build configuration, and dependency inventory.
2. Run the repository's Windows regression tests and build a clean, versioned
   executable using an audited GitHub Actions workflow.
3. Submit the validated build artifact to the approved signing service with
   provenance metadata. Require a deliberate approval step.
4. Verify the Authenticode signature, timestamp and expected certificate
   identity on the exact final executable; abort on failure.
5. Compute SHA-256 **after signing** and only then create the matching generic
   and version-labelled release downloads.
6. Publish the immutable release, SHA-256 checksums, source commit link,
   signed/unsigned status and verification instructions.
7. Update PC Technician Hub's download endpoint and checksum to the new
   version **after** release verification. Never present an unsigned build as
   signed.
8. Test automatic update from an older unsigned release. After a user has a
   verified signed installation, future signed updates must preserve the
   expected certificate fingerprint or require a separately verified migration.

The existing GitHub Actions variable `REQUIRE_SIGNED_RELEASE` is a
verification guard only. It must **not** be enabled until a real signing
step exists *before* the verification/checksum/release sequence.

## Privacy and user consent

TCP Viewer Pro reads local TCP/UDP connections and process metadata for
diagnostics. Network diagnostics are user-initiated. The optional update
check contacts the selected release repository, and updates require
approval. The application's current security description states that it
does not upload diagnostic results automatically.

The software must not make hidden system configuration changes. Windows
Firewall and process-control operations require explicit user action and
the relevant Windows permissions. See [SECURITY.md](SECURITY.md) and the
[project privacy page](https://pc-technician-hub.pctechhub-tools.workers.dev/privacy.html).
Before exporting any log, users should review it for sensitive IP
addresses, hostnames, or paths.

## How users will verify a future signed release

On Windows, after downloading a *future signed* release:

```powershell
Get-AuthenticodeSignature .\TCPViewerPro.exe |
    Format-List Status,SignerCertificate,TimeStamperCertificate
Get-FileHash .\TCPViewerPro.exe -Algorithm SHA256
```

Require `Status : Valid`, verify the publisher from the certificate and
the release documentation, and check the full published SHA-256 digest.
A valid signature establishes publisher/integrity properties; it does
**not** certify the software is malware-free or guarantee SmartScreen
warnings will disappear immediately.

## Provider status

- SignPath Foundation application: **not yet submitted**.
- Signing identity/approval roles: **not yet provisioned**.
- Production Windows release signing: **not enabled**.
- No production release should be described as signed until its actual
  Authenticode signature has been verified.
