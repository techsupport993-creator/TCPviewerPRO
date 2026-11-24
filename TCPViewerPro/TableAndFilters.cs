using System.Text.Json;

namespace TCPViewerPro;

public sealed partial class MainForm
{
    private static object[] PublisherFilterOptions() => new object[]
    {
        "ALL PUBLISHERS", "HIDE VERIFIED MS", "HIDE MS + METADATA", "VERIFIED MS ONLY", "VERIFIED THIRD PARTY",
        "UNSIGNED ONLY", "UNVERIFIED / UNKNOWN"
    };

    private static bool MatchesPublisherFilter(string category, string company, string filter) => filter switch
    {
        "HIDE VERIFIED MS" => category != "Verified Microsoft",
        // Optional convenience mode. CompanyName can be spoofed, so verified
        // and metadata-only entries remain distinguishable in the grid.
        "HIDE MS + METADATA" => category != "Verified Microsoft" &&
            !company.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase),
        "VERIFIED MS ONLY" => category == "Verified Microsoft",
        "VERIFIED THIRD PARTY" => category == "Verified third party",
        "UNSIGNED ONLY" => category == "Unsigned",
        "UNVERIFIED / UNKNOWN" => category is "Unverified" or "Trusted signer unknown" or "Unavailable",
        _ => true
    };

    private sealed class ColumnLayout
    {
        public int Order { get; set; }
        public int Width { get; set; }
        public bool Visible { get; set; } = true;
    }

    private sealed class TableLayoutSettings
    {
        public Dictionary<string, ColumnLayout> Connections { get; set; } = new();
        public Dictionary<string, ColumnLayout> Processes { get; set; } = new();
        // Independent per-mode layouts prevent the app-wide mode switch from
        // destroying a user's preferred columns, widths, or sort order.
        public Dictionary<string, ColumnLayout> StandardConnections { get; set; } = new();
        public Dictionary<string, ColumnLayout> AdvancedConnections { get; set; } = new();
        public Dictionary<string, ColumnLayout> StandardProcesses { get; set; } = new();
        public Dictionary<string, ColumnLayout> AdvancedProcesses { get; set; } = new();
        public bool GroupByProcess { get; set; }
    }

    private static readonly string TableLayoutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TCPViewerPro", "table-layout.json");

    private readonly System.Windows.Forms.Timer layoutSaveTimer = new() { Interval = 700 };
    private bool layoutReady;

    private TableLayoutSettings ReadTableLayoutSettings()
    {
        try
        {
            return File.Exists(TableLayoutPath)
                ? JsonSerializer.Deserialize<TableLayoutSettings>(File.ReadAllText(TableLayoutPath)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    private void RestoreGridLayout()
    {
        var saved = ReadTableLayoutSettings();
        groupByProcess = saved.GroupByProcess;
        RestoreConnectionLayoutForMode(saved);
    }

    private void RestoreConnectionLayoutForMode(TableLayoutSettings saved)
    {
        if (advancedAppMode)
        {
            if (saved.AdvancedConnections is { Count: > 0 })
                RestoreColumns(grid, saved.AdvancedConnections);
            else ApplyAdvancedConnectionLayout();
        }
        else if (saved.StandardConnections is { Count: > 0 })
            RestoreColumns(grid, saved.StandardConnections);
        else if (saved.Connections is { Count: > 0 })
            // One-time compatibility for v6.6/v6.7 legacy layouts.
            RestoreColumns(grid, saved.Connections);
        else
            ApplyRecommendedConnectionLayout();
    }

    private void RestoreAppModeGridLayouts()
    {
        var saved = ReadTableLayoutSettings();
        RestoreConnectionLayoutForMode(saved);
        RestoreProcessLayoutForMode(saved);
        grid.Columns["Group"].Visible = groupByProcess;
    }

    private void ApplyAdvancedConnectionLayout()
    {
        ApplyRecommendedConnectionLayout();
        foreach (DataGridViewColumn column in grid.Columns)
            column.Visible = column.Name != "Group" || groupByProcess;
        grid.Columns["Signer"].Width = 190;
        grid.Columns["Company"].Width = 180;
        grid.Columns["LocalAddress"].Width = 165;
        grid.Columns["LocalPort"].Width = 80;
        grid.Columns["Path"].Width = 300;
    }

    private void ApplyRecommendedConnectionLayout()
    {
        var order = new[]
        {
            "Group", "Process", "Pid", "Protocol", "RemoteAddress",
            "RemotePort", "State", "PublisherCategory",
            "LocalAddress", "LocalPort", "Signer", "Company", "Path"
        };
        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Process", "Pid", "Protocol", "RemoteAddress", "RemotePort",
            "State", "PublisherCategory"
        };
        if (groupByProcess) visible.Add("Group");

        var widths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Group"] = 146, ["Process"] = 150, ["Pid"] = 65,
            ["Protocol"] = 85, ["RemoteAddress"] = 155, ["RemotePort"] = 90,
            ["State"] = 122, ["PublisherCategory"] = 160
        };
        grid.SuspendLayout();
        try
        {
            // Set a stable order before hiding optional technical columns.
            for (int i = 0; i < order.Length; i++)
                grid.Columns[order[i]].DisplayIndex = i;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                if (widths.TryGetValue(column.Name, out int width)) column.Width = width;
                column.Visible = visible.Contains(column.Name);
            }
        }
        finally { grid.ResumeLayout(true); }
    }

    private void RestoreProcessGridLayout() => RestoreProcessLayoutForMode(ReadTableLayoutSettings());

    private void RestoreProcessLayoutForMode(TableLayoutSettings saved)
    {
        if (advancedAppMode)
        {
            if (saved.AdvancedProcesses is { Count: > 0 })
                RestoreColumns(processGrid, saved.AdvancedProcesses);
            else ApplyAdvancedProcessLayout();
        }
        else if (saved.StandardProcesses is { Count: > 0 })
            RestoreColumns(processGrid, saved.StandardProcesses);
        else if (saved.Processes is { Count: > 0 })
            RestoreColumns(processGrid, saved.Processes);
        else ApplyStandardProcessLayout();
    }

    private void ApplyStandardProcessLayout()
    {
        foreach (DataGridViewColumn column in processGrid.Columns)
            column.Visible = column.Name is not ("company" or "path");
    }

    private void ApplyAdvancedProcessLayout()
    {
        foreach (DataGridViewColumn column in processGrid.Columns)
            column.Visible = true;
    }

    private static void RestoreColumns(DataGridView view, Dictionary<string, ColumnLayout>? saved)
    {
        if (saved == null || saved.Count == 0) return;
        foreach (DataGridViewColumn col in view.Columns)
        {
            if (!saved.TryGetValue(col.Name, out var c)) continue;
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            col.Width = Math.Clamp(c.Width, 50, 900);
            if (col.Name != "Group") col.Visible = c.Visible;
        }
        var order = view.Columns.Cast<DataGridViewColumn>()
            .OrderBy(c => saved.TryGetValue(c.Name, out var data) ? data.Order : c.DisplayIndex)
            .ToList();
        for (int i = 0; i < order.Count; i++) order[i].DisplayIndex = i;
        if (view.Columns.Cast<DataGridViewColumn>().All(c => !c.Visible))
            view.Columns.Cast<DataGridViewColumn>().First(c => c.Name != "Group").Visible = true;
    }

    private void ActivateGridLayoutPersistence()
    {
        if (layoutReady) return;
        layoutSaveTimer.Tick += (_, _) => { layoutSaveTimer.Stop(); SaveTableLayoutSettings(); };
        layoutReady = true;
    }

    private void ScheduleGridLayoutSave()
    {
        if (!layoutReady || IsDisposed || Disposing) return;
        layoutSaveTimer.Stop();
        layoutSaveTimer.Start();
    }

    private static Dictionary<string, ColumnLayout> CaptureColumns(DataGridView view)
    {
        return view.Columns.Cast<DataGridViewColumn>().ToDictionary(c => c.Name, c => new ColumnLayout
        {
            Order = c.DisplayIndex,
            Width = Math.Clamp(c.Width, 50, 900),
            Visible = c.Visible
        });
    }

    private void SaveTableLayoutSettings()
    {
        if (!layoutReady) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TableLayoutPath)!);
            var data = ReadTableLayoutSettings();
            // Preserve settings from earlier releases as the default Standard
            // layout before a user creates their first Advanced layout.
            if (data.StandardConnections.Count == 0 && data.Connections.Count > 0)
                data.StandardConnections = data.Connections;
            if (data.StandardProcesses.Count == 0 && data.Processes.Count > 0)
                data.StandardProcesses = data.Processes;

            var connectionColumns = CaptureColumns(grid);
            var processColumns = CaptureColumns(processGrid);
            if (advancedAppMode)
            {
                data.AdvancedConnections = connectionColumns;
                data.AdvancedProcesses = processColumns;
            }
            else
            {
                data.StandardConnections = connectionColumns;
                data.StandardProcesses = processColumns;
            }
            data.GroupByProcess = groupByProcess;
            File.WriteAllText(TableLayoutPath, JsonSerializer.Serialize(data,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { footer.Text = "Could not save table layout: " + ex.Message; }
    }

    private void ShowColumnMenu(Control anchor)
    {
        var menu = new ContextMenuStrip { BackColor = panel2, ForeColor = Color.White };
        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.Name == "Group") continue;
            var target = col;
            var item = new ToolStripMenuItem(col.HeaderText) { Checked = col.Visible, CheckOnClick = true };
            item.CheckedChanged += (_, _) =>
            {
                if (!item.Checked && grid.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible && c != target) == 0)
                {
                    item.Checked = true;
                    return;
                }
                target.Visible = item.Checked;
                ScheduleGridLayoutSave();
            };
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Recommended compact layout", null, (_, _) =>
        {
            ApplyRecommendedConnectionLayout();
            ScheduleGridLayoutSave();
        });
        menu.Items.Add("Advanced technician columns", null, (_, _) =>
        {
            ApplyAdvancedConnectionLayout();
            ScheduleGridLayoutSave();
        });
        menu.Items.Add("Show all columns (wide table)", null, (_, _) =>
        {
            int width = 138;
            foreach (DataGridViewColumn c in grid.Columns)
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                c.Width = c.Name == "Path" ? 270 : width;
                c.Visible = c.Name != "Group" || groupByProcess;
                c.DisplayIndex = c.Index;
            }
            ScheduleGridLayoutSave();
        });
        menu.Show(anchor, new Point(0, anchor.Height));
    }
}
