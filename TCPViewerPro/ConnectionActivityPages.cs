using System.Globalization;
using System.Net;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// Local-only presentation of newly observed TCP endpoints. Event rows are
/// observations, never malware assessments. The history is in-memory and
/// exported only after the user explicitly chooses a file.
/// </summary>
public sealed partial class MainForm
{
    private sealed record ConnectionActivity(
        DateTime SeenAt,
        string Process,
        int Pid,
        string RemoteAddress,
        int RemotePort,
        string State,
        string AddressScope,
        string EndpointKey);

    private static readonly Font ActivityStateFont = new("Segoe UI Semibold", 9F);
    private readonly List<ConnectionActivity> connectionActivity = new();
    private readonly DataGridView securityActivityGrid = new();
    private readonly DataGridView historyActivityGrid = new();
    private readonly TextBox securityActivitySearch = new();
    private readonly TextBox historyActivitySearch = new();
    private readonly ComboBox securityActivityScope = new();
    private readonly ComboBox historyActivityState = new();
    private readonly Label securityActivitySummary = new();
    private readonly Label historyActivitySummary = new();
    private readonly Label securityActivityCount = new();
    private readonly Label historyActivityCount = new();

    private TabPage BuildSecurityCenterPage()
    {
        var page = Page("SECURITY");
        var top = CreateActivityHeader(
            "SECURITY CENTER  /  NEW ENDPOINTS",
            "Recently seen remote TCP endpoints. Review unusual activity; a new connection is not a malware finding.",
            securityActivitySearch,
            "Find app, PID, address or state...",
            securityActivityScope,
            new[] { "ALL OBSERVATIONS", "PUBLIC ADDRESSES", "PRIVATE / LOCAL", "OTHER ADDRESSES" },
            securityActivityCount,
            securityActivitySummary,
            () => RenderActivityGrid(securityActivityGrid, true),
            () => ExportActivityCsv(securityActivityGrid, "Security_Observations"),
            () => CopyActivitySelection(securityActivityGrid));

        ConfigureActivityGrid(securityActivityGrid);
        var explanation = new Label
        {
            Text = "INFO  •  Public/private is an address classification, not a risk score. TIME-WAIT and PID 0 are often normal Windows networking behavior.",
            Dock = DockStyle.Bottom,
            Height = 43,
            ForeColor = muted,
            BackColor = panel,
            Padding = new Padding(15, 12, 8, 4),
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9F)
        };
        page.Controls.Add(securityActivityGrid);
        page.Controls.Add(explanation);
        page.Controls.Add(top);
        return page;
    }

    private TabPage BuildConnectionHistoryPage()
    {
        var page = Page("HISTORY");
        var top = CreateActivityHeader(
            "CONNECTION HISTORY  /  SESSION LOG",
            "A searchable record of newly observed TCP connections since this app started. Nothing is uploaded.",
            historyActivitySearch,
            "Search process, IP, PID, state...",
            historyActivityState,
            new[] { "ALL STATES", "ESTABLISHED", "TIME-WAIT", "SYN-SENT", "OTHER STATES" },
            historyActivityCount,
            historyActivitySummary,
            () => RenderActivityGrid(historyActivityGrid, false),
            () => ExportActivityCsv(historyActivityGrid, "Connection_History"),
            () => CopyActivitySelection(historyActivityGrid),
            showClear: true);

        ConfigureActivityGrid(historyActivityGrid);
        var tip = new Label
        {
            Text = "TIP  •  Select an event and choose Copy selected. Export CSV saves the currently visible filtered rows. Clear requires confirmation.",
            Dock = DockStyle.Bottom,
            Height = 43,
            BackColor = panel,
            ForeColor = muted,
            Padding = new Padding(15, 12, 8, 4),
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9F)
        };
        page.Controls.Add(historyActivityGrid);
        page.Controls.Add(tip);
        page.Controls.Add(top);
        return page;
    }

    private Panel CreateActivityHeader(
        string title,
        string description,
        TextBox searchBox,
        string searchPrompt,
        ComboBox filterBox,
        string[] filters,
        Label matchesLabel,
        Label summaryLabel,
        Action applyFilter,
        Action export,
        Action copy,
        bool showClear = false)
    {
        var top = new Panel
        {
            Dock = DockStyle.Top, Height = 164, BackColor = panel,
            AutoScroll = true, AutoScrollMinSize = new Size(890, 161)
        };

        var titleLabel = new Label
        {
            Text = title,
            Location = new Point(16, 9), Width = 835, Height = 37,
            Font = new Font("Segoe UI Semibold", 17F),
            ForeColor = cyan, AutoEllipsis = true
        };
        var subtitleLabel = new Label
        {
            Text = description,
            Location = new Point(17, 47), Width = 835, Height = 30,
            ForeColor = muted, Font = new Font("Segoe UI", 9.5F),
            AutoEllipsis = true
        };

        searchBox.Location = new Point(16, 83);
        searchBox.Size = new Size(254, 31);
        searchBox.PlaceholderText = searchPrompt;
        StyleText(searchBox);
        searchBox.TextChanged += (_, _) => applyFilter();

        filterBox.DropDownStyle = ComboBoxStyle.DropDownList;
        filterBox.Location = new Point(280, 83);
        filterBox.Size = new Size(180, 31);
        filterBox.BackColor = panel2;
        filterBox.ForeColor = Color.White;
        filterBox.Font = new Font("Segoe UI", 9F);
        filterBox.Items.AddRange(filters);
        filterBox.SelectedIndex = 0;
        filterBox.SelectedIndexChanged += (_, _) => applyFilter();

        var exportButton = Button("EXPORT CSV", green, bg);
        exportButton.Location = new Point(470, 81);
        exportButton.Size = new Size(122, 34);
        exportButton.Click += (_, _) => export();

        var copyButton = Button("COPY SELECTED", cyan, bg);
        copyButton.Location = new Point(602, 81);
        copyButton.Size = new Size(125, 34);
        copyButton.Click += (_, _) => copy();

        top.Controls.AddRange(new Control[]
        {
            titleLabel, subtitleLabel, searchBox, filterBox, exportButton, copyButton
        });

        if (showClear)
        {
            var clearButton = Button("CLEAR LOG", Color.FromArgb(239, 174, 98), bg);
            clearButton.Location = new Point(736, 81);
            clearButton.Size = new Size(113, 34);
            clearButton.Click += (_, _) => ClearActivityHistory();
            top.Controls.Add(clearButton);
        }

        summaryLabel.Location = new Point(17, 124);
        summaryLabel.Size = new Size(625, 25);
        summaryLabel.ForeColor = muted;
        summaryLabel.Font = new Font("Segoe UI", 9F);

        matchesLabel.Location = new Point(646, 124);
        matchesLabel.Size = new Size(200, 25);
        matchesLabel.ForeColor = green;
        matchesLabel.TextAlign = ContentAlignment.MiddleRight;
        matchesLabel.Font = new Font("Segoe UI Semibold", 9F);

        top.Controls.Add(summaryLabel);
        top.Controls.Add(matchesLabel);
        var hints = new ToolTip();
        hints.SetToolTip(searchBox, "Search the observations currently stored in this session.");
        hints.SetToolTip(filterBox, "Filters are local to this view and do not affect monitoring.");
        hints.SetToolTip(exportButton, "Saves the visible rows as CSV; nothing is uploaded.");
        hints.SetToolTip(copyButton, "Copies the selected event to the Windows clipboard.");
        return top;
    }

    private void ConfigureActivityGrid(DataGridView view)
    {
        ConfigureGrid(view);
        view.Margin = Padding.Empty;
        view.Font = new Font("Segoe UI", 9.2F);
        view.RowTemplate.Height = 34;
        view.ColumnHeadersHeight = 39;
        view.AllowUserToOrderColumns = false;
        view.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
        view.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        view.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        view.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        view.Columns.Add("Seen", "TIME");
        view.Columns.Add("Process", "APPLICATION");
        view.Columns.Add("Pid", "PID");
        view.Columns.Add("Remote", "REMOTE ENDPOINT");
        view.Columns.Add("State", "TCP STATE");
        view.Columns.Add("Scope", "ADDRESS TYPE");
        view.Columns["Seen"]!.Width = 110;
        view.Columns["Process"]!.Width = 190;
        view.Columns["Pid"]!.Width = 76;
        view.Columns["Remote"]!.Width = 245;
        view.Columns["State"]!.Width = 135;
        view.Columns["Scope"]!.Width = 160;
        foreach (DataGridViewColumn column in view.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;

        view.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != view.Columns["State"]!.Index)
                return;
            string state = e.Value?.ToString() ?? "";
            if (connectionColors.TryGetValue(state, out var stateColor))
            {
                e.CellStyle.ForeColor = stateColor;
                e.CellStyle.SelectionForeColor = stateColor;
                e.CellStyle.Font = ActivityStateFont;
            }
        };
        view.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0) CopyActivitySelection(view);
        };
        view.CellMouseDown += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.Button == MouseButtons.Right)
            {
                view.ClearSelection();
                view.Rows[e.RowIndex].Selected = true;
            }
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy selected event", null, (_, _) => CopyActivitySelection(view));
        menu.Items.Add("Export visible rows", null, (_, _) =>
            ExportActivityCsv(view, view == securityActivityGrid ? "Security_Observations" : "Connection_History"));
        view.ContextMenuStrip = menu;
    }

    private static string GetAddressScope(string address) =>
        SecurityGuards.ClassifyAddressScope(address);

    private void RecordConnectionActivity(ConnectionRow row)
    {
        var entry = new ConnectionActivity(
            DateTime.Now, row.Process, row.Pid, row.RemoteAddress,
            row.RemotePort, row.State, GetAddressScope(row.RemoteAddress), row.EndpointKey);
        connectionActivity.Insert(0, entry);
        if (connectionActivity.Count > 2000)
            connectionActivity.RemoveRange(2000, connectionActivity.Count - 2000);
    }

    private IEnumerable<ConnectionActivity> FilterActivity(bool security)
    {
        string query = (security ? securityActivitySearch.Text : historyActivitySearch.Text).Trim();
        string selection = security ? securityActivityScope.Text : historyActivityState.Text;

        foreach (var entry in connectionActivity)
        {
            if (security)
            {
                if (selection == "PUBLIC ADDRESSES" && entry.AddressScope != "PUBLIC") continue;
                if (selection == "PRIVATE / LOCAL" && entry.AddressScope != "PRIVATE / LOCAL") continue;
                if (selection == "OTHER ADDRESSES"
                    && entry.AddressScope != "UNKNOWN" && entry.AddressScope != "OTHER") continue;
            }
            else
            {
                if (selection == "ESTABLISHED" && entry.State != "ESTABLISHED") continue;
                if (selection == "TIME-WAIT" && entry.State != "TIME-WAIT") continue;
                if (selection == "SYN-SENT" && entry.State != "SYN-SENT") continue;
                if (selection == "OTHER STATES"
                    && (entry.State == "ESTABLISHED" || entry.State == "TIME-WAIT" || entry.State == "SYN-SENT"))
                    continue;
            }

            if (query.Length > 0)
            {
                string searchable = $"{entry.SeenAt:HH:mm:ss} {entry.Process} {entry.Pid} " +
                    $"{entry.RemoteAddress} {entry.RemotePort} {entry.State} {entry.AddressScope}";
                if (!searchable.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            }
            yield return entry;
        }
    }

    private void RenderActivityViews()
    {
        securityActivitySummary.Text = $"{connectionActivity.Count:N0} recorded  •  " +
            $"{connectionActivity.Count(e => e.AddressScope == "PUBLIC"):N0} public  •  " +
            $"{connectionActivity.Count(e => e.AddressScope == "PRIVATE / LOCAL"):N0} local";
        historyActivitySummary.Text = $"{connectionActivity.Count:N0} events stored locally  •  " +
            "Newest first  •  Last 2,000 retained";

        // Refresh only the active tab; a hidden 2,000-row grid does not need
        // rebuilding every two seconds when it is not visible.
        if (tabs.SelectedIndex == 2) RenderActivityGrid(securityActivityGrid, true);
        else if (tabs.SelectedIndex == 3) RenderActivityGrid(historyActivityGrid, false);
    }

    private void RenderActivityGrid(DataGridView view, bool security)
    {
        var entries = FilterActivity(security).Take(security ? 300 : 2000).ToArray();
        var counter = security ? securityActivityCount : historyActivityCount;
        counter.Text = $"{entries.Length:N0} shown";
        if (view.Columns.Count == 0) return;

        // Preserve the selected event across live refreshes. Default to the top
        // of the log for observers who have not selected an older event.
        var selected = view.SelectedRows.Count > 0
            ? view.SelectedRows[0].Tag as ConnectionActivity
            : null;
        view.SuspendLayout();
        try
        {
            view.Rows.Clear();
            foreach (var entry in entries)
            {
                int rowIndex = view.Rows.Add(
                    entry.SeenAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    entry.Process, entry.Pid,
                    $"{entry.RemoteAddress}:{entry.RemotePort}",
                    entry.State, entry.AddressScope);
                view.Rows[rowIndex].Tag = entry;
            }

            view.ClearSelection();
            if (selected is not null)
            {
                for (int i = 0; i < view.Rows.Count; i++)
                {
                    if (!ReferenceEquals(view.Rows[i].Tag, selected)) continue;
                    view.Rows[i].Selected = true;
                    break;
                }
            }
        }
        finally { view.ResumeLayout(true); }
    }

    private static string ActivityLine(ConnectionActivity e) =>
        $"{e.SeenAt:yyyy-MM-dd HH:mm:ss}  {e.Process} (PID {e.Pid})  " +
        $"{e.RemoteAddress}:{e.RemotePort}  {e.State}  [{e.AddressScope}]";

    private void CopyActivitySelection(DataGridView view)
    {
        if (view.SelectedRows.Count == 0
            || view.SelectedRows[0].Tag is not ConnectionActivity activity)
        {
            footer.Text = "Select an event first.";
            return;
        }
        try
        {
            Clipboard.SetText(ActivityLine(activity));
            footer.Text = "Selected event copied to clipboard.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not copy the event:\n" + ex.Message,
                "Copy event", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportActivityCsv(DataGridView view, string label)
    {
        var rows = view.Rows.Cast<DataGridViewRow>()
            .Select(r => r.Tag as ConnectionActivity)
            .Where(r => r is not null)
            .Cast<ConnectionActivity>().ToArray();
        if (rows.Length == 0)
        {
            MessageBox.Show(this, "There are no visible events to export.",
                "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"TCPViewerPro_{label}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var csv = new StringBuilder("TimeLocal,Process,PID,RemoteAddress,RemotePort,TcpState,AddressType\r\n");
            foreach (var e in rows)
                csv.AppendLine(string.Join(",", new[]
                {
                    e.SeenAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    e.Process, e.Pid.ToString(CultureInfo.InvariantCulture), e.RemoteAddress,
                    e.RemotePort.ToString(CultureInfo.InvariantCulture), e.State, e.AddressScope
                }.Select(Csv)));
            File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
            footer.Text = $"Exported {rows.Length:N0} visible events to CSV.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Unable to export history:\n" + ex.Message,
                "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ClearActivityHistory()
    {
        if (MessageBox.Show(this,
                "Clear all connection history and Security Center observations from this session?\n\n" +
                "This does not delete previously exported CSV files. Monitoring will continue.",
                "Clear session log", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        history.Clear();
        connectionActivity.Clear();
        RenderActivityViews();
        // Also clear the inactive grid so a tab change cannot show stale data.
        securityActivityGrid.Rows.Clear();
        historyActivityGrid.Rows.Clear();
        footer.Text = "Session connection history cleared. Monitoring continues.";
    }
}
