using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace TCPViewerPro;

// Local Authenticode trust verification. Does not execute executables, download
// reputation data, or run PowerShell. Revocation is not queried online.
internal static class SignatureVerifier
{
    internal sealed record Result(string Category, string Status, string Signer, string TechnicalResult);

    private static readonly Guid VerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionID, ref WINTRUST_DATA data);

    internal static Result Check(string path)
    {
        if (!File.Exists(path))
            return new("Unavailable", "File not accessible", "Unavailable", "The file could not be accessed.");

        IntPtr pathPtr = IntPtr.Zero;
        IntPtr filePtr = IntPtr.Zero;
        try
        {
            pathPtr = Marshal.StringToCoTaskMemUni(path);
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = pathPtr,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };
            filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, filePtr, false);
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = 2,       // WTD_UI_NONE
                fdwRevocationChecks = 0, // WTD_REVOKE_NONE
                dwUnionChoice = 1,    // WTD_CHOICE_FILE
                pFile = filePtr,
                dwStateAction = 0,    // WTD_STATEACTION_IGNORE
                dwProvFlags = 0x00001000 | 0x00000010, // CACHE_ONLY_URL_RETRIEVAL | REVOCATION_CHECK_NONE
                dwUIContext = 0
            };
            var action = VerifyV2;
            int trust = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            if (trust == TRUST_E_NOSIGNATURE)
                return new("Unsigned", "No trusted Authenticode signature", "None", "TRUST_E_NOSIGNATURE");
            if (trust != 0)
                return new("Unverified", "Signature validation failed or cannot be completed", "Unknown", $"WinVerifyTrust 0x{unchecked((uint)trust):X8}");

            // A trusted file might have an external *catalog* signature. The
            // embedded certificate API cannot always identify its signer.
            // Do not use the spoofable VERSIONINFO CompanyName as signer proof.
            try
            {
                using var raw = X509Certificate.CreateFromSignedFile(path);
                using var cert = new X509Certificate2(raw);
                string signer = cert.GetNameInfo(X509NameType.SimpleName, false);
                string subject = cert.SubjectName.Name ?? "";
                bool microsoft = HasMicrosoftOrganization(subject);
                return microsoft
                    ? new("Verified Microsoft", "Trusted Authenticode signature (offline)", signer, "WinVerifyTrust succeeded; revocation not checked")
                    : new("Verified third party", "Trusted Authenticode signature (offline)", signer, "WinVerifyTrust succeeded; revocation not checked");
            }
            catch
            {
                return new("Trusted signer unknown", "Trusted by Windows, signer not extractable", "Not available (possibly catalog signed)",
                    "WinVerifyTrust succeeded; signer unavailable from embedded certificate API");
            }
        }
        catch (Exception ex)
        {
            return new("Unverified", "Signature check could not be completed", "Unknown", ex.Message);
        }
        finally
        {
            if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
            if (pathPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(pathPtr);
        }
    }

    private static bool HasMicrosoftOrganization(string subject)
    {
        // The certificate is already trusted. Require the signer certificate's
        // Organization (not its display name or file CompanyName) to match.
        // Split along RDN separators; this handles normal Microsoft code-signing certs.
        foreach (string part in subject.Split(','))
        {
            string value = part.Trim();
            if (value.StartsWith("O=", StringComparison.OrdinalIgnoreCase) &&
                value[2..].Trim().Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
