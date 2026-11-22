using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// Checks for a running instance that is using the exact EXE being replaced.
/// Different copies in different folders may be updated independently, but a
/// shared EXE must not be replaced while another instance is still running.
/// </summary>
internal static class UpdateInstanceGuard
{
    internal sealed record RunningCopy(int Pid, bool IdentityUncertain);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle, uint flags, StringBuilder executablePath, ref int pathLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr processHandle);

    internal static bool SameExePath(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            return false;
        try
        {
            return Path.GetFullPath(first).Equals(
                Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    internal static string MutexNameForExecutable(string executablePath)
    {
        string fullPath = Path.GetFullPath(executablePath).ToUpperInvariant();
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath));
        return @"Local\TCPViewerPro.Update." + Convert.ToHexString(digest);
    }

    internal static IReadOnlyList<RunningCopy> FindOtherRunningCopies(
        string executablePath, int currentPid)
    {
        string exeName = Path.GetFileNameWithoutExtension(executablePath);
        var matches = new List<RunningCopy>();
        if (string.IsNullOrEmpty(exeName)) return matches;
        foreach (var process in Process.GetProcessesByName(exeName))
        {
            using (process)
            {
                int pid;
                try { pid = process.Id; }
                catch { continue; }
                if (pid == currentPid) continue;

                string? processPath = TryQueryFullPath(pid);
                if (processPath is null)
                {
                    // If an EXE with the same name cannot be inspected, do not
                    // blindly assume it is safe to replace the selected target.
                    matches.Add(new RunningCopy(pid, IdentityUncertain: true));
                }
                else if (SameExePath(processPath, executablePath))
                {
                    matches.Add(new RunningCopy(pid, IdentityUncertain: false));
                }
            }
        }
        return matches;
    }

    private static string? TryQueryFullPath(int pid)
    {
        IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new StringBuilder(32768);
            int length = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref length)
                ? buffer.ToString()
                : null;
        }
        finally { CloseHandle(handle); }
    }

    internal static string BlockingMessage(IReadOnlyList<RunningCopy> instances, string filename)
    {
        string pids = string.Join(", ", instances.Select(x => x.Pid));
        string uncertain = instances.Any(x => x.IdentityUncertain)
            ? "\nSome processes could not be inspected, so updating was stopped as a precaution."
            : "";
        return $"Another instance may be using {filename} (PID: {pids})." +
               "\n\nClose the other window(s), then try the update again." +
               "\nOnly the copy you launch is updated. Files in other folders or with other names remain unchanged." +
               uncertain;
    }
}
