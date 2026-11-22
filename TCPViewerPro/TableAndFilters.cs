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
        RestoreColumns(grid, saved.Connections);
    }

    private void RestoreProcessGridLayout() => RestoreColumns(processGrid, ReadTableLayoutSettings().Processes);

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
            var data = new TableLayoutSettings
            {
                Connections = CaptureColumns(grid),
                Processes = CaptureColumns(processGrid),
                GroupByProcess = groupByProcess
            };
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
        menu.Items.Add("Reset table columns", null, (_, _) =>
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
