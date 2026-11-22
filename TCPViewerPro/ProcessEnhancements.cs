using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TCPViewerPro;

public sealed partial class MainForm
{
    // Classification requires a successful WinVerifyTrust check and an
    // extractable signer certificate. Unavailable/catalog-only signers stay
    // visible and are never silently classified as Microsoft.
    private sealed record ProcessMetadata(
        string Name, string Path, string Company, string Category,
        string Signer, string SignatureStatus, string SignatureDetail,
        long Length, DateTime WriteTimeUtc);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags,
        StringBuilder name, ref int capacity);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private static string TryReadImagePath(int pid)
    {
        if (pid <= 4) return "";
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(32768);
            int length = sb.Capacity;
            return QueryFullProcessImageName(handle, 0, sb, ref length) ? sb.ToString() : "";
        }
        finally { CloseHandle(handle); }
    }

    private ProcessMetadata ReadProcessMetadata(int pid)
    {
        string name = pid switch { 0 => "System Idle Process", 4 => "System", _ => "(unavailable)" };
        string path = "";
        if (pid > 4)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                name = proc.ProcessName;
                try { path = proc.MainModule?.FileName ?? ""; } catch { }
            }
            catch { }
            if (path.Length == 0) path = TryReadImagePath(pid);
        }
        if (path.Length == 0)
            return new ProcessMetadata(name, "", "Unknown", "Unavailable", "Unavailable",
                "Executable path not accessible", "Process may be protected or exited", 0, DateTime.MinValue);

        long length = 0;
        DateTime modified = DateTime.MinValue;
        try
        {
            var info = new FileInfo(path);
            length = info.Length;
            modified = info.LastWriteTimeUtc;
        }
        catch { }
        lock (fileMetadataLock)
        {
            if (fileMetadata.TryGetValue(path, out var cached) &&
                cached.Length == length && cached.WriteTimeUtc == modified)
                return cached with { Name = name };
        }

        string company = "Unknown";
        try
        {
            string? rawCompany = FileVersionInfo.GetVersionInfo(path).CompanyName;
            if (!string.IsNullOrWhiteSpace(rawCompany)) company = rawCompany.Trim();
        }
        catch { }
        var signature = SignatureVerifier.Check(path);
        var result = new ProcessMetadata(name, path, company, signature.Category,
            signature.Signer, signature.Status, signature.TechnicalResult, length, modified);
        lock (fileMetadataLock)
        {
            if (fileMetadata.Count > 2500) fileMetadata.Clear();
            fileMetadata[path] = result;
        }
        return result;
    }

    private void HandleShortcut(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
            RefreshData(manual: true);
        else if (e.Control && e.KeyCode == Keys.F)
        {
            tabs.SelectedIndex = 0;
            search.Focus();
            search.SelectAll();
        }
        else if (e.Control && e.KeyCode == Keys.P) ToggleMonitoring();
        else if (e.Control && e.KeyCode == Keys.M)
        {
            tabs.SelectedIndex = 0;
            publisherFilterBox.SelectedIndex = publisherFilterBox.SelectedIndex == 1 ? 0 : 1;
        }
        else if (e.Control && e.KeyCode == Keys.S) ExportCsv();
        else return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void SortConnectionHeader(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0) return;
        string property = grid.Columns[e.ColumnIndex].DataPropertyName;
        if (string.IsNullOrEmpty(property)) return;
        if (sortProperty == property) sortAscending = !sortAscending;
        else { sortProperty = property; sortAscending = true; }
        ApplyFilter();
    }

    private IEnumerable<ConnectionRow> SortConnections(IEnumerable<ConnectionRow> source)
    {
        // Keep PID and port values numeric. Plain string sorting makes 10000
        // appear before 443 and prevents efficient investigation.
        Func<ConnectionRow, object> key = sortProperty switch
        {
            "Pid" => r => r.Pid,
            "LocalPort" => r => r.LocalPort,
            "RemotePort" => r => r.RemotePort,
            "Protocol" => r => r.Protocol,
            "Company" => r => r.Company,
            "PublisherCategory" => r => r.PublisherCategory,
            "Signer" => r => r.Signer,
            "Group" => r => r.Process,
            "LocalAddress" => r => r.LocalAddress,
            "RemoteAddress" => r => r.RemoteAddress,
            "State" => r => r.State,
            "Path" => r => r.Path,
            _ => r => r.Process
        };
        var comparer = Comparer<object>.Create((a, b) =>
        {
            if (a is int x && b is int y) return x.CompareTo(y);
            return StringComparer.OrdinalIgnoreCase.Compare(a?.ToString(), b?.ToString());
        });
        return sortAscending ? source.OrderBy(key, comparer) : source.OrderByDescending(key, comparer);
    }

    private int processSortColumn = 2;
    private bool processSortAscending = false;

    private void SortProcessHeader(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0) return;
        if (processSortColumn == e.ColumnIndex) processSortAscending = !processSortAscending;
        else { processSortColumn = e.ColumnIndex; processSortAscending = true; }
        ApplyProcessSort();
    }

    private void ApplyProcessSort()
    {
        if (processGrid.Columns.Count == 0 || processGrid.Rows.Count <= 1) return;
        processGrid.Sort(processGrid.Columns[processSortColumn],
            processSortAscending ? System.ComponentModel.ListSortDirection.Ascending
                                 : System.ComponentModel.ListSortDirection.Descending);
    }

    private ContextMenuStrip BuildConnectionContextMenu()
    {
        var menu = new ContextMenuStrip { BackColor = panel2, ForeColor = Color.White };
        menu.Items.Add("Process properties", null, (_, _) => InspectSelectedProcess());
        menu.Items.Add("Show only this process", null, (_, _) =>
        {
            if (Selected() is { } r) search.Text = r.Process;
        });
        menu.Items.Add("Hide verified Microsoft processes", null, (_, _) => publisherFilterBox.SelectedIndex = 1);
        menu.Items.Add("Show all publishers", null, (_, _) => publisherFilterBox.SelectedIndex = 0);
        menu.Items.Add("Open executable folder", null, (_, _) => OpenSelectedFileLocation());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copy remote IP", null, (_, _) =>
        {
            if (Selected() is { } r) Clipboard.SetText(r.RemoteAddress);
        });
        menu.Items.Add("Copy local endpoint", null, (_, _) =>
        {
            if (Selected() is { } r) Clipboard.SetText($"{r.LocalAddress}:{r.LocalPort}");
        });
        menu.Items.Add("Copy remote endpoint", null, (_, _) =>
        {
            if (Selected() is { } r) Clipboard.SetText($"{r.RemoteAddress}:{r.RemotePort}");
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Resolve remote DNS", null, async (_, _) => await LookupSelectedDns());
        menu.Items.Add("Close selected TCP connection", null, (_, _) => CloseSelectedTcp());
        menu.Items.Add("Block remote IP (confirm)", null, (_, _) => BlockSelectedIp());
        menu.Items.Add("End owning process (confirm)", null, (_, _) => KillSelectedProcess());
        return menu;
    }

    private void InspectSelectedProcess()
    {
        if (Selected() is { } row) ShowProcessProperties(row);
        else MessageBox.Show("Select a connection first.");
    }

    private void ShowProcessProperties(ConnectionRow r) => OpenProcessInspector(r);

    private void OpenSelectedFileLocation()
    {
        if (Selected() is not { } r || r.Path.Length == 0 || !File.Exists(r.Path))
        {
            MessageBox.Show("The executable path is not accessible for this process.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{r.Path}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) { MessageBox.Show("Could not open executable location:\n" + ex.Message); }
    }
}
