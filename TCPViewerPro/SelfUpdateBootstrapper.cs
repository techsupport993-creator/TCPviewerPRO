using System.Diagnostics;
using System.Security.Cryptography;

namespace TCPViewerPro;

/// <summary>
/// Runs inside the just-downloaded, checksum-verified copy of TCPViewerPro.exe.
/// Waiting for the old application to exit allows a single-file EXE to replace
/// itself without cmd.exe, a helper package, Python, or PowerShell.
/// </summary>
internal static class SelfUpdateBootstrapper
{
    public static bool TryHandle(string[] args)
    {
        if (args.Length == 0 || args[0] != "--apply-update") return false;
        try
        {
            if (args.Length != 4) throw new ArgumentException("Missing updater arguments.");
            string destination = Path.GetFullPath(args[1]);
            string expected = args[2];
            if (!int.TryParse(args[3], out int oldPid) || oldPid <= 0)
                throw new ArgumentException("Invalid previous process ID.");
            if (!Path.GetFileName(destination).Equals("TCPViewerPro.exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Updates can only replace TCPViewerPro.exe.");
            if (expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("Invalid SHA-256 expected value.");

            string self = Path.GetFullPath(Environment.ProcessPath ?? Application.ExecutablePath);
            string updateRoot = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TCPViewerPro", "Updates")) + Path.DirectorySeparatorChar;
            if (!self.StartsWith(updateRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Updater is not running from its staging folder.");
            if (destination.StartsWith(updateRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot update a staged executable.");

            using (var file = File.OpenRead(self))
            {
                string hash = Convert.ToHexString(SHA256.HashData(file));
                if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Staged update checksum did not match.");
            }

            try
            {
                using var previous = Process.GetProcessById(oldPid);
                previous.WaitForExit(30_000);
                if (!previous.HasExited)
                    throw new TimeoutException("The current TCP Viewer Pro window is still open.");
            }
            catch (ArgumentException) { /* The original process already exited. */ }

            string temporaryDestination = destination + ".update-pending";
            string backupDestination = destination + ".previous";
            try
            {
                // Copy to the target volume first. File.Replace swaps the new
                // executable in one filesystem operation while keeping backup.
                File.Copy(self, temporaryDestination, overwrite: true);
                Exception? lastError = null;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    try
                    {
                        if (File.Exists(destination))
                            File.Replace(temporaryDestination, destination, backupDestination, true);
                        else
                            File.Move(temporaryDestination, destination);
                        lastError = null;
                        break;
                    }
                    catch (IOException ex) { lastError = ex; Thread.Sleep(500); }
                    catch (UnauthorizedAccessException ex) { lastError = ex; break; }
                }
                if (lastError is not null) throw lastError;
            }
            finally
            {
                try { if (File.Exists(temporaryDestination)) File.Delete(temporaryDestination); }
                catch { }
            }

            Process.Start(new ProcessStartInfo(destination)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(destination)!
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Automatic installation failed. The previous executable may still be present.\n\n" +
                ex.Message + "\n\nDownload the release manually if necessary.",
                "TCP Viewer Pro updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return true;
    }
}
