using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TCPViewerPro;

public sealed partial class MainForm
{
    // Keep inspector windows in sync if the user changes the theme while one is open.
    private readonly List<Form> openInspectorDialogs = new();

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr handle, int informationClass,
        IntPtr information, int length, out int returnedLength);

    private static (string ParentPid, string CommandLine) ReadNativeProcessDetails(int pid)
    {
        if (pid <= 4) return ("Unavailable", "Unavailable");
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return ("Access denied or process ended", "Access denied or process ended");
        IntPtr buffer = IntPtr.Zero;
        try
        {
            string parent = "Unavailable";
            string commandLine = "Unavailable";
            try
            {
                int len = IntPtr.Size * 6;
                buffer = Marshal.AllocHGlobal(len);
                if (NtQueryInformationProcess(handle, 0, buffer, len, out _) == 0)
                {
                    IntPtr parentPtr = Marshal.ReadIntPtr(buffer, IntPtr.Size * 5);
                    parent = parentPtr.ToInt64().ToString();
                }
            }
            catch { }
            finally { if (buffer != IntPtr.Zero) { Marshal.FreeHGlobal(buffer); buffer = IntPtr.Zero; } }
            try
            {
                const int size = 131072;
                buffer = Marshal.AllocHGlobal(size);
                // ProcessCommandLineInformation (Windows 8.1+). The return is
                // a local UNICODE_STRING plus its string data.
                if (NtQueryInformationProcess(handle, 60, buffer, size, out _) == 0)
                {
                    int bytes = unchecked((ushort)Marshal.ReadInt16(buffer, 0));
                    IntPtr strPtr = Marshal.ReadIntPtr(buffer, IntPtr.Size == 8 ? 8 : 4);
                    long textAddress = strPtr.ToInt64();
                    long start = buffer.ToInt64();
                    if (bytes >= 0 && bytes < size && bytes % 2 == 0 &&
                        textAddress >= start && textAddress <= start + size - bytes)
                        commandLine = Marshal.PtrToStringUni(strPtr, bytes / 2) ?? "Unavailable";
                }
            }
            catch { }
            return (parent, commandLine);
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            CloseHandle(handle);
        }
    }

    private static string CreateInspectionReport(ConnectionRow selected, int endpointCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TCP VIEWER PRO · PROCESS INVESTIGATION");
        sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();
        sb.AppendLine("Process: " + selected.Process);
        sb.AppendLine("PID: " + selected.Pid);
        var details = ReadNativeProcessDetails(selected.Pid);
        sb.AppendLine("Parent PID: " + details.ParentPid);
        sb.AppendLine("Command line: " + details.CommandLine);
        try
        {
            using var process = Process.GetProcessById(selected.Pid);
            sb.AppendLine("Started: " + process.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        catch { sb.AppendLine("Started: Unavailable"); }
        sb.AppendLine();
        sb.AppendLine("Signature category: " + selected.PublisherCategory);
        sb.AppendLine("Signature status: " + selected.SignatureStatus);
        sb.AppendLine("Signer: " + selected.Signer);
        sb.AppendLine("Signature details: " + selected.SignatureDetail);
        sb.AppendLine("Company (untrusted file metadata): " + selected.Company);
        sb.AppendLine("Executable: " + (selected.Path.Length == 0 ? "Unavailable" : selected.Path));
        if (selected.Path.Length > 0 && File.Exists(selected.Path))
        {
            try
            {
                var file = new FileInfo(selected.Path);
                sb.AppendLine("File size: " + file.Length.ToString("N0") + " bytes");
                sb.AppendLine("Last modified (UTC): " + file.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"));
                using var stream = new FileStream(selected.Path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                sb.AppendLine("SHA-256: " + Convert.ToHexString(SHA256.HashData(stream)));
            }
            catch (Exception ex) { sb.AppendLine("SHA-256: Unavailable (" + ex.Message + ")"); }
        }
        else sb.AppendLine("SHA-256: Unavailable");
        sb.AppendLine();
        sb.AppendLine("Endpoints in current snapshot: " + endpointCount);
        sb.AppendLine("Note: Local Windows trust check does not check online certificate revocation.");
        sb.AppendLine("Signed does not mean safe; unsigned does not necessarily mean malicious.");
        return sb.ToString();
    }

    private async void OpenProcessInspector(ConnectionRow selected)
    {
        // Capture currently displayed snapshot on UI thread; it may change on refresh.
        var endpoints = current.Where(x => x.Pid == selected.Pid).ToList();
        var dialog = new Form
        {
            Text = $"Process Inspector · {selected.Process} (PID {selected.Pid})",
            StartPosition = FormStartPosition.CenterParent, Width = 1050, Height = 780,
            MinimumSize = new Size(800, 550), BackColor = bg, ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(15) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
        var title = new Label
        {
            Text = $"PROCESS INSPECTOR  /  {selected.Process}  /  PID {selected.Pid}",
            Font = new Font("Segoe UI Semibold", 17F), ForeColor = cyan,
            TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill
        };
        layout.Controls.Add(title, 0, 0);
        var report = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Both, WordWrap = false, BackColor = panel,
            ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.2F), Text = "Reading process details and calculating SHA-256…"
        };
        layout.Controls.Add(report, 0, 1);
        // DataGridView gives this inspection window the same dark, theme-aware
        // headers, rows, selection colors and resize behavior as the main tables.
        var endpointsGrid = new DataGridView();
        ConfigureGrid(endpointsGrid);
        endpointsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        endpointsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Protocol", HeaderText = "PROTOCOL", FillWeight = 16, MinimumWidth = 95 });
        endpointsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Local", HeaderText = "LOCAL", FillWeight = 34, MinimumWidth = 210 });
        endpointsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Remote", HeaderText = "REMOTE", FillWeight = 34, MinimumWidth = 210 });
        endpointsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "State", HeaderText = "STATE", FillWeight = 16, MinimumWidth = 115 });
        foreach (var endpoint in endpoints)
            endpointsGrid.Rows.Add(endpoint.Protocol,
                $"{endpoint.LocalAddress}:{endpoint.LocalPort}",
                $"{endpoint.RemoteAddress}:{endpoint.RemotePort}", endpoint.State);
        endpointsGrid.ClearSelection();
        layout.Controls.Add(endpointsGrid, 0, 2);
        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var copy = Button("COPY REPORT", green, bg); copy.Width = 135;
        copy.Click += (_, _) => { if (report.TextLength > 0) Clipboard.SetText(report.Text); };
        var open = Button("OPEN EXE FOLDER", cyan, bg); open.Width = 165;
        open.Click += (_, _) =>
        {
            if (selected.Path.Length == 0 || !File.Exists(selected.Path))
                MessageBox.Show(dialog, "Executable file not accessible.");
            else
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{selected.Path}\"") { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(dialog, ex.Message); }
            }
        };
        var close = Button("CLOSE", Color.FromArgb(255, 190, 90), bg);
        close.Click += (_, _) => dialog.Close();
        controls.Controls.AddRange(new Control[] { copy, open, close });
        layout.Controls.Add(controls, 0, 3);
        dialog.Controls.Add(layout);
        openInspectorDialogs.Add(dialog);
        dialog.FormClosed += (_, _) => openInspectorDialogs.Remove(dialog);
        dialog.Show(this);

        try
        {
            string text = await Task.Run(() => CreateInspectionReport(selected, endpoints.Count));
            if (!dialog.IsDisposed && !report.IsDisposed) report.Text = text;
        }
        catch (Exception ex)
        {
            if (!dialog.IsDisposed && !report.IsDisposed) report.Text = "Could not inspect this process: " + ex.Message;
        }
    }
}
