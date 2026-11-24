using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace TCPViewerPro;

public sealed partial class MainForm : Form
{
    Color bg = Color.FromArgb(12, 17, 29);
    Color panel = Color.FromArgb(20, 29, 46);
    Color panel2 = Color.FromArgb(25, 38, 59);
    Color cyan = Color.FromArgb(0, 210, 255);
    Color green = Color.FromArgb(66, 220, 160);
    Color muted = Color.FromArgb(145, 164, 190);
    readonly DataGridView grid = new();
    readonly TextBox search = new();
    readonly ComboBox publisherFilterBox = new();
    readonly Label tcpValue = new(), udpValue = new(), listenValue = new(), procValue = new(), status = new();
    readonly Label detail = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    readonly BindingSource binding = new();
    readonly List<ConnectionRow> current = new();
    readonly ConcurrentDictionary<string, string> dnsCache = new();
    readonly HashSet<string> previous = new();
    readonly List<string> history = new();
    readonly SidebarPageControl tabs = new();
    readonly DataGridView processGrid = new();
    readonly ToolStripStatusLabel footer = new("Ready");
    readonly FlowLayoutPanel navPanel = new();
    readonly List<Button> navButtons = new();
    readonly List<Button> plannedNavButtons = new();
    bool plannedNavExpanded;
    Button? pauseButton;
    bool monitoringPaused;
    readonly Dictionary<string, Color> connectionColors = DefaultConnectionColors();
    bool highlightExe = true;
    bool loading;
    string activeFilter = "ALL";
    string activePublisherFilter = "ALL PUBLISHERS";
    string processPublisherFilter = "ALL PUBLISHERS";
    bool groupByProcess;
    string processSearch = "";
    string sortProperty = "Process";
    bool sortAscending = true;
    readonly Label visibleCount = new();
    readonly Label refreshIndicator = new();
    readonly Dictionary<string, ProcessMetadata> fileMetadata = new(StringComparer.OrdinalIgnoreCase);
    readonly object fileMetadataLock = new();

    public MainForm()
    {
        Text = $"TCP Viewer Pro v{typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "?"}";
        Width = 1450; Height = 850; MinimumSize = new Size(1050, 650);
        BackColor = bg; ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        KeyDown += HandleShortcut;
        LoadUpdatePreferences();
        LoadThemeSettings();
        LoadColorSettings();
        groupByProcess = ReadTableLayoutSettings().GroupByProcess;
        BuildUi();
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon; }
        catch { /* No executable icon is available during some development launches. */ }
        timer.Tick += (_, _) => RefreshData();
        Shown += (_, _) => { RefreshData(); timer.Start(); };
        Shown += async (_, _) => await AutoCheckForUpdatesAsync();
        FormClosing += (_, _) => { timer.Stop(); layoutSaveTimer.Stop(); SaveTableLayoutSettings(); };
    }

    void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = bg, ColumnCount = 1, RowCount = 4, Padding = new Padding(14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var brand = new Label { Text = "TCP VIEWER  /  PRO", Font = new Font("Segoe UI Semibold", 19F), ForeColor = cyan, AutoSize = true, Location = new Point(4, 4) };
        var subtitle = new Label { Text = "LIVE NETWORK CONNECTION MONITOR", ForeColor = muted, AutoSize = true, Location = new Point(7, 39), Font = new Font("Consolas", 8.5F) };
        header.Controls.Add(brand); header.Controls.Add(subtitle);
        var refresh = Button("REFRESH", cyan, PaletteFor(activeTheme).DarkButton); refresh.Size = new Size(104, 34); refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right; refresh.Click += (_, _) => RefreshData(manual: true);
        pauseButton = Button("PAUSE", Color.FromArgb(255, 190, 90), Color.FromArgb(20, 25, 35)); pauseButton.Size = new Size(104, 34); pauseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; pauseButton.Click += (_, _) => ToggleMonitoring();
        refreshIndicator.Text = "AUTO • 2 SEC";
        refreshIndicator.ForeColor = green;
        refreshIndicator.AutoSize = true;
        refreshIndicator.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        header.Controls.Add(refresh); header.Controls.Add(pauseButton); header.Controls.Add(refreshIndicator);
        header.Resize += (_, _) =>
        {
            refresh.Left = header.ClientSize.Width - refresh.Width - 5;
            pauseButton.Left = refresh.Left - pauseButton.Width - 8;
            refreshIndicator.Left = pauseButton.Left - refreshIndicator.Width - 18;
            refresh.Top = pauseButton.Top = 15; refreshIndicator.Top = 24;
        };
        root.Controls.Add(header, 0, 0);

        var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = bg, Padding = new Padding(0, 6, 0, 8) };
        for (int i=0;i<4;i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        stats.Controls.Add(StatCard("TCP CONNECTIONS", tcpValue, cyan),0,0);
        stats.Controls.Add(StatCard("UDP ENDPOINTS", udpValue, green),1,0);
        stats.Controls.Add(StatCard("LISTENING PORTS", listenValue, Color.FromArgb(255,190,90)),2,0);
        stats.Controls.Add(StatCard("ACTIVE PROCESSES", procValue, Color.FromArgb(180,145,255)),3,0);
        root.Controls.Add(stats,0,1);

        tabs.Dock = DockStyle.Fill;
        tabs.Appearance = TabAppearance.FlatButtons;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(1, 1);
        tabs.Multiline = true;
        tabs.Margin = Padding.Empty;
        tabs.TabPages.Add(BuildConnectionsTab());
        tabs.TabPages.Add(BuildProcessesTab());
        tabs.TabPages.Add(BuildSecurityTab());
        tabs.TabPages.Add(BuildHistoryTab());
        tabs.TabPages.Add(BuildPlannedFeaturePage("ETW MONITOR", "Event-based network and process activity", "Capture connection lifecycle events and correlate them with process activity using Windows Event Tracing (ETW)."));
        tabs.TabPages.Add(BuildPlannedFeaturePage("BANDWIDTH", "Per-process network usage", "Measure bytes sent and received per process, show live throughput graphs, and compare usage over time. Requires a validated ETW or performance-counter collector."));
        tabs.TabPages.Add(BuildPlannedFeaturePage("FIREWALL MANAGER", "Windows Defender Firewall rules", "Inspect inbound and outbound rules, search rule properties, enable or disable rules, and create or remove rules with explicit confirmation."));
        tabs.TabPages.Add(BuildPlannedFeaturePage("PORT SCANNER", "Authorized network diagnostics", "Scan selected hosts and port ranges that you own or are authorized to assess. Include rate limits, cancellation, and clear scope confirmation."));
        tabs.TabPages.Add(BuildPlannedFeaturePage("IP INTELLIGENCE", "Remote address investigation", "Optional online lookups for ASN, provider, country, and reputation signals. Local monitoring remains functional offline; external checks will be opt-in."));
        tabs.TabPages.Add(BuildPlannedFeaturePage("DNS MONITOR", "DNS activity and resolution", "Track DNS query metadata, cache lookups, compare answers, and flag unusual query patterns. Requires an event-based DNS collector."));
        tabs.TabPages.Add(BuildNetworkDiagnosticsPage());
        tabs.TabPages.Add(BuildColorSettingsPage());
        tabs.TabPages.Add(BuildAboutPage());

        var workspace = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        navPanel.Dock = DockStyle.Left;
        navPanel.Width = 208;
        navPanel.BackColor = panel;
        navPanel.Padding = new Padding(7, 9, 3, 8);
        navPanel.AutoScroll = true;
        navPanel.FlowDirection = FlowDirection.TopDown;
        navPanel.WrapContents = false;
        var navTitle = new Label
        {
            Text = "WORKSPACE", ForeColor = cyan,
            Font = new Font("Consolas", 9F, FontStyle.Bold),
            Width = 171, Height = 28, TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 0, 5)
        };
        navPanel.Controls.Add(navTitle);
        AddNavButton("LIVE CONNECTIONS", 0);
        AddNavButton("PROCESSES", 1);
        AddNavButton("SECURITY CENTER", 2);
        AddNavButton("CONNECTION HISTORY", 3);
        AddNavSection("DIAGNOSTIC TOOLS");
        AddNavButton("NETWORK DIAGNOSTICS", 10);
        AddNavSection("APPLICATION");
        AddNavButton("SETTINGS", 11);
        AddNavButton("ABOUT", 12);
        AddPlannedNavToggle();
        AddNavButton("ETW MONITOR", 4, planned: true);
        AddNavButton("BANDWIDTH", 5, planned: true);
        AddNavButton("FIREWALL MANAGER", 6, planned: true);
        AddNavButton("PORT SCANNER", 7, planned: true);
        AddNavButton("IP INTELLIGENCE", 8, planned: true);
        AddNavButton("DNS MONITOR", 9, planned: true);
        tabs.SelectedIndexChanged += (_, _) => { UpdateNavSelection(); RenderActivityViews(); };
        workspace.Controls.Add(tabs);
        workspace.Controls.Add(navPanel);
        root.Controls.Add(workspace,0,2);
        UpdateNavSelection();

        var strip = new StatusStrip { BackColor = panel, SizingGrip = false };
        footer.ForeColor = muted; strip.Items.Add(footer);
        var right = new ToolStripStatusLabel($"TCP Viewer Pro v{typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "?"}  •  Local machine") { ForeColor = muted, Spring = true, TextAlign = ContentAlignment.MiddleRight };
        strip.Items.Add(right); root.Controls.Add(strip,0,3);
    }


    void AddNavSection(string title)
    {
        var label = new Label
        {
            Text = title, ForeColor = cyan, Font = new Font("Consolas", 8.2F, FontStyle.Bold),
            Width = 169, Height = 29, TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(4, 7, 0, 4)
        };
        navPanel.Controls.Add(label);
    }

    void AddNavButton(string title, int tabIndex, bool planned = false)
    {
        var b = new Button
        {
            Text = "  " + title, Tag = tabIndex, Width = 171, Height = 33,
            TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat,
            BackColor = panel, ForeColor = muted, Font = new Font("Segoe UI Semibold", 8.2F),
            Cursor = Cursors.Hand, Margin = new Padding(2, 1, 0, 1),
            Visible = !planned || plannedNavExpanded
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = panel2;
        b.Click += (_, _) => tabs.SelectedIndex = tabIndex;
        navButtons.Add(b);
        navPanel.Controls.Add(b);
        if (planned) plannedNavButtons.Add(b);
    }

    void AddPlannedNavToggle()
    {
        var toggle = new Button
        {
            Text = "  ▸ PLANNED TOOLS", Width = 171, Height = 34,
            TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat,
            BackColor = panel, ForeColor = cyan,
            Font = new Font("Segoe UI Semibold", 8.4F),
            Cursor = Cursors.Hand, Margin = new Padding(2, 9, 0, 1)
        };
        toggle.FlatAppearance.BorderSize = 0;
        toggle.FlatAppearance.MouseOverBackColor = panel2;
        toggle.Click += (_, _) =>
        {
            plannedNavExpanded = !plannedNavExpanded;
            navPanel.SuspendLayout();
            try
            {
                foreach (var button in plannedNavButtons) button.Visible = plannedNavExpanded;
                toggle.Text = plannedNavExpanded ? "  ▾ PLANNED TOOLS" : "  ▸ PLANNED TOOLS";
            }
            finally { navPanel.ResumeLayout(true); }
        };
        navPanel.Controls.Add(toggle);
    }

    void UpdateNavSelection()
    {
        foreach (var b in navButtons)
        {
            bool selected = b.Tag is int i && i == tabs.SelectedIndex;
            b.BackColor = selected ? panel2 : panel;
            b.ForeColor = selected ? cyan : muted;
        }
    }

    TabPage BuildPlannedFeaturePage(string title, string subtitle, string description)
    {
        var page = Page(title);
        var card = new Panel
        {
            BackColor = panel, Dock = DockStyle.Top, Height = 230,
            Padding = new Padding(22), Margin = new Padding(14)
        };
        var statusLabel = new Label
        {
            Text = "PLANNED MODULE", ForeColor = Color.FromArgb(255, 190, 90),
            Font = new Font("Consolas", 9F, FontStyle.Bold), AutoSize = true,
            Location = new Point(22, 22)
        };
        var heading = new Label
        {
            Text = title, ForeColor = cyan, Font = new Font("Segoe UI Semibold", 20F),
            AutoSize = true, Location = new Point(22, 52)
        };
        var sub = new Label
        {
            Text = subtitle, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 11F),
            AutoSize = true, Location = new Point(24, 94)
        };
        var desc = new Label
        {
            Text = description, ForeColor = muted, Font = new Font("Segoe UI", 10F),
            Location = new Point(24, 126), Size = new Size(760, 62),
            AutoEllipsis = true
        };
        card.Controls.AddRange(new Control[] { statusLabel, heading, sub, desc });
        var note = new Label
        {
            Text = "This module is reserved in the navigation. It is not active yet; no scan, monitoring, or external lookup is running from this page.",
            ForeColor = muted, Dock = DockStyle.Top, Height = 48, Padding = new Padding(4, 12, 4, 4)
        };
        page.Controls.Add(note);
        page.Controls.Add(card);
        return page;
    }

    void ToggleMonitoring()
    {
        monitoringPaused = !monitoringPaused;
        if (monitoringPaused)
        {
            timer.Stop();
            pauseButton!.Text = "RESUME";
            pauseButton.BackColor = green;
            refreshIndicator.Text = "PAUSED";
            refreshIndicator.ForeColor = Color.FromArgb(255, 190, 90);
            footer.Text = "Monitoring paused • Current results are frozen • Manual refresh remains available";
        }
        else
        {
            pauseButton!.Text = "PAUSE";
            pauseButton.BackColor = Color.FromArgb(255, 190, 90);
            refreshIndicator.Text = $"AUTO • {timer.Interval / 1000} SEC";
            refreshIndicator.ForeColor = green;
            RefreshData();
            timer.Start();
            footer.Text = "Monitoring resumed • Automatic refresh every 2 seconds";
        }
    }

    Control StatCard(string caption, Label value, Color accent)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = panel, Margin = new Padding(0,0,9,0), Padding = new Padding(13,10,8,8) };
        var c = new Label { Text = caption, ForeColor = muted, Font = new Font("Consolas",8.5F), AutoSize = true, Location = new Point(12,10) };
        value.Text = "—"; value.ForeColor = accent; value.Font = new Font("Segoe UI Semibold",23F); value.AutoSize = true; value.Location = new Point(10,31);
        p.Controls.Add(c); p.Controls.Add(value); return p;
    }

    TabPage BuildConnectionsTab()
    {
        var page = Page("CONNECTIONS");

        // Explicit rows prevent a docked toolbar from obscuring connection headers
        // or data when the app is resized or Windows display scaling changes.
        var pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = bg, ColumnCount = 1, RowCount = 3,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        page.Controls.Add(pageLayout);

        var bar = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = panel
        };
        search.Width = 232;
        search.Height = 30;
        search.Location = new Point(14, 12);
        search.PlaceholderText = "Search connections, apps or IPs...";
        StyleText(search);
        search.TextChanged += (_, _) => ApplyFilter();

        var filter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Height = 30,
            BackColor = panel2, ForeColor = Color.White
        };
        filter.Items.AddRange(new object[] { "ALL", "TCP", "UDP", "LISTENING", "ESTABLISHED" });
        filter.SelectedIndex = 0;
        filter.SelectedIndexChanged += (_, _) => { activeFilter = filter.Text; ApplyFilter(); };
        new ToolTip().SetToolTip(filter, "Show connections by protocol or state.");

        publisherFilterBox.DropDownStyle = ComboBoxStyle.DropDownList;
        publisherFilterBox.BackColor = panel2;
        publisherFilterBox.ForeColor = Color.White;
        publisherFilterBox.Width = 180;
        publisherFilterBox.Height = 30;
        publisherFilterBox.Items.AddRange(PublisherFilterOptions());
        publisherFilterBox.SelectedIndex = 0;
        publisherFilterBox.SelectedIndexChanged += (_, _) =>
        {
            activePublisherFilter = publisherFilterBox.Text;
            ApplyFilter();
        };
        new ToolTip().SetToolTip(publisherFilterBox,
            "Publisher trust uses offline Authenticode checks. Company metadata can be spoofed.");

        var rate = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 88, Height = 30,
            BackColor = panel2, ForeColor = Color.White
        };
        rate.Items.AddRange(new object[] { "1 sec", "2 sec", "5 sec", "10 sec" });
        rate.SelectedIndex = 1;
        rate.SelectedIndexChanged += (_, _) =>
        {
            timer.Interval = rate.SelectedIndex switch
            {
                0 => 1000, 1 => 2000, 2 => 5000, _ => 10000
            };
            if (!monitoringPaused)
                refreshIndicator.Text = $"AUTO • {timer.Interval / 1000} SEC";
        };

        visibleCount.ForeColor = muted;
        visibleCount.AutoSize = false;
        visibleCount.AutoEllipsis = true;
        visibleCount.Height = 25;
        visibleCount.TextAlign = ContentAlignment.MiddleLeft;
        visibleCount.Text = "Loading endpoints…";

        // The most useful investigation actions stay visible. Less-frequent and
        // destructive actions move into a clearly named dropdown.
        var explain = Button("EXPLAIN", green, PaletteFor(activeTheme).DarkButton);
        explain.Width = 145;
        explain.Click += (_, _) => InvestigateSelectedConnection();
        new ToolTip().SetToolTip(explain,
            "Explain the selected connection in Simple or Advanced mode.");

        var inspect = Button("INSPECT", Color.FromArgb(180, 145, 255), bg);
        inspect.Width = 110;
        inspect.Click += (_, _) => InspectSelectedProcess();

        var export = Button("EXPORT CSV", cyan, PaletteFor(activeTheme).DarkButton);
        export.Width = 112;
        export.Click += (_, _) => ExportCsv();

        var more = Button("MORE ACTIONS ▾", panel2, Color.White);
        more.Width = 126;
        var moreMenu = new ContextMenuStrip { BackColor = panel2, ForeColor = Color.White };
        moreMenu.Items.Add("DNS lookup for selected remote IP", null,
            async (_, _) => await LookupSelectedDns());
        moreMenu.Items.Add(new ToolStripSeparator());
        moreMenu.Items.Add("Close selected TCP connection (confirm)", null,
            (_, _) => CloseSelectedTcp());
        moreMenu.Items.Add("Block remote IP for all apps (confirm)", null,
            (_, _) => BlockSelectedIp());
        moreMenu.Items.Add("End owning process (confirm)", null,
            (_, _) => KillSelectedProcess());
        more.Click += (_, _) => moreMenu.Show(more, new Point(0, more.Height));

        var columns = Button("COLUMNS", panel2, Color.White);
        columns.Width = 104;
        columns.Click += (_, _) => ShowColumnMenu(columns);
        new ToolTip().SetToolTip(columns,
            "Select columns, or switch to a recommended compact connection layout.");

        var group = new CheckBox
        {
            Text = "GROUP PROCESSES", Checked = groupByProcess,
            AutoSize = false, Width = 144, Height = 34,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White
        };
        var groupMenu = new ToolStripMenuItem("Group connections by process")
        {
            Checked = groupByProcess, CheckOnClick = true
        };
        group.CheckedChanged += (_, _) =>
        {
            groupByProcess = group.Checked;
            groupMenu.Checked = group.Checked;
            ApplyFilter();
            ScheduleGridLayoutSave();
        };
        groupMenu.CheckedChanged += (_, _) => group.Checked = groupMenu.Checked;
        moreMenu.Items.Add(new ToolStripSeparator());
        moreMenu.Items.Add(groupMenu);

        var toolbarButtons = new Control[] { explain, inspect, export, more, columns, group };
        void ArrangeToolbar()
        {
            filter.Location = new Point(search.Right + 10, 12);
            publisherFilterBox.Location = new Point(filter.Right + 10, 12);
            rate.Location = new Point(publisherFilterBox.Right + 10, 12);
            visibleCount.Left = rate.Right + 14;
            visibleCount.Top = 12;
            visibleCount.Width = Math.Max(70, bar.ClientSize.Width - visibleCount.Left - 12);
            visibleCount.Visible = bar.ClientSize.Width >= visibleCount.Left + 115;

            int nextX = 14;
            foreach (var control in toolbarButtons)
            {
                control.Left = nextX;
                control.Top = 60;
                nextX = control.Right + 8;
            }
            // The same toggle remains available under MORE ACTIONS at narrow widths.
            group.Visible = group.Right + 8 <= bar.ClientSize.Width;
        }

        bar.Controls.AddRange(new Control[]
        {
            search, filter, publisherFilterBox, rate, visibleCount,
            explain, inspect, export, more, columns, group
        });
        bar.Resize += (_, _) => ArrangeToolbar();
        ArrangeToolbar();
        pageLayout.Controls.Add(bar, 0, 0);

        ConfigureGrid(grid);
        AddCol("Group", "PROCESS GROUP", 178);
        AddCol("Protocol", "PROTOCOL", 76);
        AddCol("Process", "PROCESS", 137);
        AddCol("Pid", "PID", 62);
        AddCol("PublisherCategory", "SIGNATURE", 175);
        AddCol("Signer", "SIGNER", 175);
        AddCol("Company", "COMPANY (METADATA)", 160);
        AddCol("LocalAddress", "LOCAL ADDRESS", 155);
        AddCol("LocalPort", "L.PORT", 69);
        AddCol("RemoteAddress", "REMOTE ADDRESS", 155);
        AddCol("RemotePort", "R.PORT", 69);
        AddCol("State", "STATE", 128);
        AddCol("Path", "EXE PATH", 260);
        grid.Columns["Path"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        grid.Columns["Path"].Width = 295;
        grid.Columns["Group"].Visible = false;
        RestoreGridLayout();
        grid.CellFormatting += FormatConnectionCell;
        grid.ColumnHeaderMouseClick += SortConnectionHeader;
        grid.ColumnWidthChanged += (_, _) => ScheduleGridLayoutSave();
        grid.ColumnDisplayIndexChanged += (_, _) => ScheduleGridLayoutSave();
        grid.ColumnStateChanged += (_, _) => ScheduleGridLayoutSave();
        grid.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                grid.ClearSelection();
                grid.Rows[e.RowIndex].Selected = true;
                var colIndex = e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Visible
                    ? e.ColumnIndex
                    : grid.Columns.Cast<DataGridViewColumn>()
                        .Where(c => c.Visible).OrderBy(c => c.DisplayIndex).First().Index;
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[colIndex];
            }
        };
        grid.ContextMenuStrip = BuildConnectionContextMenu();
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0) InspectSelectedProcess();
        };
        grid.Dock = DockStyle.Fill;
        grid.Margin = Padding.Empty;
        grid.DataSource = binding;
        ActivateGridLayoutPersistence();
        grid.SelectionChanged += (_, _) => UpdateDetails();
        pageLayout.Controls.Add(grid, 0, 1);

        var bottom = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = panel,
            Padding = new Padding(12, 8, 10, 6)
        };
        detail.Dock = DockStyle.Fill;
        detail.ForeColor = muted;
        detail.AutoEllipsis = true;
        detail.Text = "Select a connection, then EXPLAIN for Simple/Advanced details.";
        bottom.Controls.Add(detail);
        pageLayout.Controls.Add(bottom, 0, 2);
        return page;
    }

    TabPage BuildProcessesTab()
    {
        var page = Page("PROCESSES");
        var top = new Panel { Dock = DockStyle.Top, Height = 85, BackColor = panel };
        var title = new Label
        {
            Location = new Point(12, 7), AutoSize = true, ForeColor = muted,
            Text = "Owning processes • Authenticode trust checked offline; revocation is not checked."
        };
        var filter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Left = 12, Top = 37, Width = 205
        };
        filter.Items.AddRange(PublisherFilterOptions());
        filter.SelectedIndex = 0;
        filter.SelectedIndexChanged += (_, _) =>
        {
            processPublisherFilter = filter.Text;
            RefreshProcesses(current);
        };
        var processSearchBox = new TextBox
        {
            Left = 230, Top = 38, Width = 305, PlaceholderText = "Find process, PID, company or path…"
        };
        StyleText(processSearchBox);
        processSearchBox.TextChanged += (_, _) =>
        {
            processSearch = processSearchBox.Text.Trim();
            RefreshProcesses(current);
        };
        top.Controls.AddRange(new Control[] { title, filter, processSearchBox });

        ConfigureGrid(processGrid);
        processGrid.CellFormatting += FormatProcessCell;
        processGrid.Columns.Add("pid", "PID");
        processGrid.Columns.Add("name", "PROCESS");
        processGrid.Columns.Add("count", "ENDPOINTS");
        processGrid.Columns.Add("signature", "SIGNATURE");
        processGrid.Columns.Add("company", "COMPANY (METADATA)");
        processGrid.Columns.Add("path", "EXECUTABLE PATH");
        processGrid.Columns[0].Width = 80;
        processGrid.Columns[1].Width = 195;
        processGrid.Columns[2].Width = 95;
        processGrid.Columns[3].Width = 180;
        processGrid.Columns[4].Width = 160;
        processGrid.Columns[5].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        processGrid.Columns[5].Width = 285;
        RestoreProcessGridLayout();
        processGrid.ColumnWidthChanged += (_, _) => ScheduleGridLayoutSave();
        processGrid.ColumnDisplayIndexChanged += (_, _) => ScheduleGridLayoutSave();
        processGrid.ColumnStateChanged += (_, _) => ScheduleGridLayoutSave();
        foreach (DataGridViewColumn c in processGrid.Columns) c.SortMode = DataGridViewColumnSortMode.Programmatic;
        processGrid.ColumnHeaderMouseClick += SortProcessHeader;
        processGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            int pid = Convert.ToInt32(processGrid.Rows[e.RowIndex].Cells[0].Value);
            var match = current.FirstOrDefault(x => x.Pid == pid);
            if (match is not null) ShowProcessProperties(match);
        };
        page.Controls.Add(processGrid);
        page.Controls.Add(top);
        return page;
    }

    TabPage BuildSecurityTab() => BuildSecurityCenterPage();

    TabPage BuildHistoryTab() => BuildConnectionHistoryPage();

    TabPage Page(string name)=>new(name){BackColor=bg,ForeColor=Color.White,Padding=new Padding(0,8,0,0)};
    Button Button(string text,Color color,Color fore){var b=new Button{Text=text,BackColor=color,ForeColor=fore,FlatStyle=FlatStyle.Flat,Height=34,Width=120,Font=new Font("Segoe UI Semibold",8.2F),Cursor=Cursors.Hand};b.FlatAppearance.BorderSize=0;return b;}
    void StyleText(TextBox t){t.BackColor=panel2;t.ForeColor=Color.White;t.BorderStyle=BorderStyle.FixedSingle;t.Font=new Font("Segoe UI",9);}

    void ConfigureGrid(DataGridView g)
    {
        g.BackgroundColor=bg;g.BorderStyle=BorderStyle.None;g.GridColor=PaletteFor(activeTheme).GridLine;
        g.EnableHeadersVisualStyles=false;g.ColumnHeadersDefaultCellStyle.BackColor=panel2;g.ColumnHeadersDefaultCellStyle.ForeColor=cyan;g.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI Semibold",9F);g.ColumnHeadersDefaultCellStyle.Padding=new Padding(7,0,3,0);
        g.DefaultCellStyle.BackColor=panel;g.DefaultCellStyle.ForeColor=Color.Gainsboro;g.DefaultCellStyle.SelectionBackColor=PaletteFor(activeTheme).SelectedRow;g.DefaultCellStyle.SelectionForeColor=Color.White;
        g.DefaultCellStyle.Padding = new Padding(6,0,3,0);
        g.AlternatingRowsDefaultCellStyle.BackColor=PaletteFor(activeTheme).AlternateRow;
        g.AutoGenerateColumns=false;g.RowHeadersVisible=false;g.AllowUserToAddRows=false;g.AllowUserToDeleteRows=false;g.ReadOnly=true;g.SelectionMode=DataGridViewSelectionMode.FullRowSelect;g.MultiSelect=false;g.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;g.Dock=DockStyle.Fill;
        g.ColumnHeadersHeight = 37;
        g.RowTemplate.Height = 30;
        g.AllowUserToResizeRows = false;
        // Prevent modal error dialogs if a Windows data binding conversion fails;
        // the actual underlying type conversion is handled by the framework.
        g.DataError += (_, e) =>
        {
            e.ThrowException = false;
            footer.Text = "Connection table display issue: " + (e.Exception?.Message ?? "unknown");
        };
    }

    async void RefreshData(bool manual = false)
    {
        if (loading) return;
        if (monitoringPaused && !manual) return;
        loading = true;
        try
        {
            var rows = await Task.Run(ReadConnections);
            if (monitoringPaused && !manual) return;
            current.Clear();current.AddRange(rows);
            tcpValue.Text=rows.Count(x=>x.Protocol=="TCP").ToString();
            udpValue.Text=rows.Count(x=>x.Protocol=="UDP").ToString();
            listenValue.Text=rows.Count(x=>x.State=="LISTENING").ToString();
            procValue.Text=rows.Select(x=>x.Pid).Where(x=>x>0).Distinct().Count().ToString();
            // TCP state changes should not create duplicate "new connection" events.
            var nowKeys=rows.Where(x=>x.Protocol=="TCP" && x.State!="LISTENING").Select(x=>x.EndpointKey).ToHashSet();
            bool hasNewActivity = false;
            foreach(var row in rows.Where(x=>x.Protocol=="TCP"&&x.State!="LISTENING"&&!previous.Contains(x.EndpointKey)))
            {
                var msg=$"[{DateTime.Now:HH:mm:ss}] NEW  {row.Process} (PID {row.Pid})  {row.RemoteAddress}:{row.RemotePort}  {row.State}";
                history.Insert(0,msg);
                RecordConnectionActivity(row);
                hasNewActivity = true;
            }
            previous.Clear();foreach(var k in nowKeys)previous.Add(k);
            if(history.Count>2000)history.RemoveRange(2000,history.Count-2000);
            ApplyFilter();
            if (hasNewActivity) RenderHistory();
            RefreshProcesses(rows);
            footer.Text=$"Updated {DateTime.Now:HH:mm:ss}  •  {rows.Count} endpoints  •  Refresh interval: {timer.Interval / 1000} seconds";
        }
        catch(Exception ex){footer.Text="Refresh error: "+ex.Message;}
        finally{loading=false;}
    }


    List<ConnectionRow> ReadConnections()
    {
        // IP Helper owner-PID tables provide a direct PID association rather than
        // trying to infer ownership from a separate netstat text snapshot.
        var result = new List<ConnectionRow>();
        var processSnapshot = new Dictionary<int, ProcessMetadata>();
        foreach (var c in IpHelperTables.GetTcp4())
        {
            string state = IpHelperTables.TcpStateName(c.State);
            result.Add(MakeRow("TCP", new IPEndPoint(c.LocalAddress, c.LocalPort),
                new IPEndPoint(c.RemoteAddress, c.RemotePort), state, c.Pid, processSnapshot));
        }
        foreach (var u in IpHelperTables.GetUdp4())
        {
            result.Add(MakeRow("UDP", new IPEndPoint(u.LocalAddress, u.LocalPort),
                new IPEndPoint(IPAddress.Any, 0), "BOUND", u.Pid, processSnapshot));
        }

        foreach (var c in IpHelperTables.GetTcp6())
        {
            string state = IpHelperTables.TcpStateName(c.State);
            result.Add(MakeRow("TCP", new IPEndPoint(c.LocalAddress, c.LocalPort),
                new IPEndPoint(c.RemoteAddress, c.RemotePort), state, c.Pid, processSnapshot));
        }
        foreach (var u in IpHelperTables.GetUdp6())
        {
            result.Add(MakeRow("UDP", new IPEndPoint(u.LocalAddress, u.LocalPort),
                new IPEndPoint(IPAddress.IPv6Any, 0), "BOUND", u.Pid, processSnapshot));
        }
        return result.OrderBy(x => x.Protocol).ThenBy(x => x.State).ThenBy(x => x.LocalPort).ToList();
    }

    ConnectionRow MakeRow(string proto, IPEndPoint local, IPEndPoint remote, string state, int pid,
        Dictionary<int, ProcessMetadata> processSnapshot)
    {
        if (!processSnapshot.TryGetValue(pid, out var meta))
        {
            meta = ReadProcessMetadata(pid);
            processSnapshot[pid] = meta;
        }
        return new ConnectionRow
        {
            Protocol = proto, LocalAddress = local.Address.ToString(), LocalPort = local.Port,
            RemoteAddress = remote.Address.ToString(), RemotePort = remote.Port, State = state,
            Pid = pid, Process = meta.Name, Path = meta.Path, Company = meta.Company,
            PublisherCategory = meta.Category, Signer = meta.Signer,
            SignatureStatus = meta.SignatureStatus, SignatureDetail = meta.SignatureDetail,
            ProcessStartedUtc = meta.StartTimeUtc
        };
    }

    void ApplyFilter()
    {
        IEnumerable<ConnectionRow> rows = current;
        if (activeFilter != "ALL")
            rows = rows.Where(x => activeFilter switch
            {
                "TCP" => x.Protocol == "TCP", "UDP" => x.Protocol == "UDP",
                "LISTENING" => x.State == "LISTENING", "ESTABLISHED" => x.State == "ESTABLISHED",
                _ => true
            });
        rows = rows.Where(x => MatchesPublisherFilter(x.PublisherCategory, x.Company, activePublisherFilter));
        string term = search.Text.Trim();
        if (term.Length > 0)
            rows = rows.Where(x => $"{x.Protocol} {x.Process} {x.PublisherCategory} {x.Signer} {x.Company} {x.Pid} {x.LocalAddress} {x.LocalPort} {x.RemoteAddress} {x.RemotePort} {x.State} {x.Path}".Contains(term, StringComparison.OrdinalIgnoreCase));

        rows = SortConnections(rows);
        string? previousSelection = Selected()?.Key;
        var output = rows.ToList();
        foreach (var r in output) r.Group = "";
        if (groupByProcess)
        {
            output = output.GroupBy(x => (x.Pid, x.Process))
                .OrderBy(g => g.Key.Process, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Pid)
                .SelectMany(g => SortConnections(g)).ToList();
            foreach (var g in output.GroupBy(x => (x.Pid, x.Process)))
                g.First().Group = $"{g.Key.Process}  ·  {g.Count()}";
        }
        grid.Columns["Group"].Visible = groupByProcess;
        binding.DataSource = output;
        visibleCount.Text = $"Showing {output.Count}/{current.Count}  •  Verified MS: {current.Count(x => x.PublisherCategory == "Verified Microsoft")}";
        if (!string.IsNullOrEmpty(previousSelection))
        {
            foreach (DataGridViewRow gRow in grid.Rows)
                if (gRow.DataBoundItem is ConnectionRow c && c.Key == previousSelection)
                {
                    var firstVisible = grid.Columns.Cast<DataGridViewColumn>()
                        .Where(c => c.Visible).OrderBy(c => c.DisplayIndex).FirstOrDefault();
                    if (firstVisible != null) grid.CurrentCell = gRow.Cells[firstVisible.Index];
                    break;
                }
        }
        foreach (DataGridViewColumn col in grid.Columns)
            col.HeaderCell.SortGlyphDirection = col.DataPropertyName == sortProperty
                ? (sortAscending ? SortOrder.Ascending : SortOrder.Descending) : SortOrder.None;
    }

    void AddCol(string prop,string header,int width){grid.Columns.Add(new DataGridViewTextBoxColumn{Name=prop,DataPropertyName=prop,HeaderText=header,Width=width,SortMode=DataGridViewColumnSortMode.Programmatic});}
    void UpdateDetails()
    {
        if(grid.CurrentRow?.DataBoundItem is ConnectionRow r)
            detail.Text=$"{r.Protocol} | {r.Process} (PID {r.Pid}) | {r.PublisherCategory} | {r.LocalAddress}:{r.LocalPort} → {r.RemoteAddress}:{r.RemotePort} | {r.State} | DNS: {dnsCache.GetValueOrDefault(r.RemoteAddress,"not resolved")}";
    }
    ConnectionRow? Selected()=>grid.CurrentRow?.DataBoundItem as ConnectionRow;

    async Task LookupSelectedDns()
    {
        var r=Selected();if(r==null){MessageBox.Show("Select a connection first.");return;}
        if(IPAddress.TryParse(r.RemoteAddress,out var a)&&IPAddress.IsLoopback(a)){MessageBox.Show("Loopback address; DNS lookup skipped.");return;}
        try{string host=await Task.Run(()=>Dns.GetHostEntry(r.RemoteAddress).HostName);dnsCache[r.RemoteAddress]=host;UpdateDetails();MessageBox.Show($"DNS hostname:\n{host}","DNS lookup");}
        catch(Exception ex){MessageBox.Show("Reverse DNS lookup failed:\n"+ex.Message,"DNS lookup");}
    }

    void CloseSelectedTcp()
    {
        var r = Selected();
        if (r == null || r.Protocol != "TCP" || r.State != "ESTABLISHED")
        {
            MessageBox.Show("Select an ESTABLISHED TCP connection first.");
            return;
        }
        if (!IPAddress.TryParse(r.LocalAddress, out var local) || local.AddressFamily != AddressFamily.InterNetwork ||
            !IPAddress.TryParse(r.RemoteAddress, out var remote) || remote.AddressFamily != AddressFamily.InterNetwork)
        {
            MessageBox.Show("Closing connections is currently supported for IPv4 TCP endpoints only.");
            return;
        }
        if (MessageBox.Show($"Close this TCP connection?\n\n{r.Process} (PID {r.Pid})\n{r.LocalAddress}:{r.LocalPort} → {r.RemoteAddress}:{r.RemotePort}\n\nThe application may reconnect automatically.",
            "Confirm TCP disconnect", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            // A selected row is only a snapshot. Refuse to disconnect if the
            // endpoint is gone or now belongs to a different process.
            bool stillOwned = IpHelperTables.GetTcp4().Any(c =>
                c.Pid == r.Pid && c.LocalAddress.Equals(local) && c.LocalPort == r.LocalPort &&
                c.RemoteAddress.Equals(remote) && c.RemotePort == r.RemotePort &&
                IpHelperTables.TcpStateName(c.State) == "ESTABLISHED");
            if (!stillOwned)
            {
                MessageBox.Show(this, "This connection changed since the last refresh. Refresh and select it again.",
                    "Connection changed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            uint status = IpHelperTables.CloseTcp4(local, r.LocalPort, remote, r.RemotePort);
            if (status == 0) footer.Text = "TCP disconnect requested.";
            else MessageBox.Show($"Windows could not close this connection. IP Helper error: {status}\nTry running TCP Viewer Pro as Administrator.");
        }
        catch (Exception ex) { MessageBox.Show("Could not close TCP connection:\n" + ex.Message); }
    }

    private bool firewallRulePending;

    async void BlockSelectedIp()
    {
        if (firewallRulePending) return;
        var r = Selected();
        if (r is null) { MessageBox.Show(this, "Select a connection first."); return; }
        if (!IPAddress.TryParse(r.RemoteAddress, out var ip) ||
            !SecurityGuards.IsBlockableRemoteAddress(ip))
        {
            MessageBox.Show(this, "The selected connection has no blockable remote IP address.");
            return;
        }

        string localWarning = SecurityGuards.IsPrivateOrSharedAddress(ip)
            ? "\n\nWARNING: This is a local/private or shared-network address. " +
              "Blocking it may disrupt printers, routers, servers or other devices on your network."
            : "";
        var confirmation = MessageBox.Show(this,
            $"Create an OUTBOUND Windows Firewall block rule for {ip}?\n" +
            $"Process shown: {r.Process} (PID {r.Pid})\n" +
            "This blocks the IP for ALL applications, not only this process. " +
            "It may interrupt other network connections." + localWarning +
            "\n\nAdministrator approval will be required. Continue?",
            "Confirm system-wide firewall change", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes) return;

        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string netsh = Path.Combine(system, "netsh.exe");
        if (!File.Exists(netsh))
        {
            MessageBox.Show(this, "The Windows system copy of netsh.exe could not be found.");
            return;
        }

        firewallRulePending = true;
        try
        {
            string name = "TCPViewerPro_Block_" + ip.ToString().Replace(':', '_').Replace('.', '_');
            var psi = new ProcessStartInfo(netsh)
            {
                UseShellExecute = true, Verb = "runas", WorkingDirectory = system
            };
            foreach (string argument in new[]
            {
                "advfirewall", "firewall", "add", "rule", "name=" + name,
                "dir=out", "action=block", "remoteip=" + ip
            }) psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi) ??
                throw new InvalidOperationException("Windows did not launch the firewall command.");
            footer.Text = "Waiting for elevated Windows Firewall command...";
            await process.WaitForExitAsync();
            if (!IsDisposed)
            {
                if (process.ExitCode == 0)
                    footer.Text = $"Windows Firewall reported rule created: {name}";
                else
                    MessageBox.Show(this, $"Windows Firewall reported error code {process.ExitCode}. " +
                        "The block rule was not confirmed. Check Windows Defender Firewall.",
                        "Firewall rule error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                MessageBox.Show(this, "Could not create firewall rule: " + ex.Message,
                    "Firewall rule error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { firewallRulePending = false; }
    }

    void KillSelectedProcess()
    {
        var r = Selected();
        if (r is null || r.Pid <= 4 || r.Pid == Environment.ProcessId)
        {
            MessageBox.Show(this, "This process cannot be terminated safely.");
            return;
        }
        try
        {
            using var process = Process.GetProcessById(r.Pid);
            string currentPath = TryReadImagePath(r.Pid);
            string currentName = process.ProcessName;
            DateTime currentStartUtc = process.StartTime.ToUniversalTime();
            if (!SecurityGuards.IsSameProcessSnapshot(r.Process, r.Path, r.ProcessStartedUtc,
                    currentName, currentPath, currentStartUtc) ||
                SecurityGuards.IsCriticalWindowsProcess(currentName, currentPath))
            {
                MessageBox.Show(this,
                    "The selected process is protected, inaccessible or has changed since the snapshot. " +
                    "Refresh and select a normal user application instead.",
                    "Process termination refused", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(this,
                $"Terminate {currentName} (PID {r.Pid})?\n\nExecutable: {currentPath}" +
                "\nUnsaved work in the application may be lost.",
                "Confirm process termination", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            process.Refresh();
            string finalPath = TryReadImagePath(r.Pid);
            if (process.HasExited ||
                !SecurityGuards.IsSameProcessSnapshot(r.Process, r.Path, r.ProcessStartedUtc,
                    process.ProcessName, finalPath, process.StartTime.ToUniversalTime()) ||
                SecurityGuards.IsCriticalWindowsProcess(process.ProcessName, finalPath))
            {
                MessageBox.Show(this, "The process changed or exited before confirmation. No action taken.",
                    "Process changed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            process.Kill();
            footer.Text = $"Termination requested for {currentName} (PID {r.Pid}).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not safely terminate this process: " + ex.Message,
                "Process termination", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void ExportCsv()
    {
        using var d=new SaveFileDialog{Filter="CSV files|*.csv",FileName=$"TCPViewerPro_{DateTime.Now:yyyyMMdd_HHmmss}.csv"};
        if(d.ShowDialog()!=DialogResult.OK)return;
        var rows=binding.List?.Cast<ConnectionRow>().ToList()??current;
        var sb=new StringBuilder("Protocol,Process,PID,SignatureCategory,Signer,CompanyMetadata,LocalAddress,LocalPort,RemoteAddress,RemotePort,State,ExecutablePath\r\n");
        foreach(var r in rows)sb.AppendLine(string.Join(",",new[]{r.Protocol,r.Process,r.Pid.ToString(),r.PublisherCategory,r.Signer,r.Company,r.LocalAddress,r.LocalPort.ToString(),r.RemoteAddress,r.RemotePort.ToString(),r.State,r.Path}.Select(Csv)));
        try
        {
            File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
            footer.Text = "CSV exported: " + d.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not export connections:\n" + ex.Message,
                "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    static string Csv(string s) => "\"" + SecurityGuards.ProtectCsvFormula(s).Replace("\"", "\"\"") + "\"";
    void RenderHistory() => RenderActivityViews();
    void ExportHistory()
    {
        using var d=new SaveFileDialog{Filter="Text files|*.txt",FileName=$"TCPViewerPro_History_{DateTime.Now:yyyyMMdd_HHmmss}.txt"};
        if(d.ShowDialog()!=DialogResult.OK)return;File.WriteAllLines(d.FileName,history);footer.Text="History exported.";
    }
    void RefreshProcesses(List<ConnectionRow> rows)
    {
        if(processGrid.Columns.Count==0)return;
        var groups=rows.Where(x=>x.Pid>0).GroupBy(x=>x.Pid).OrderByDescending(g=>g.Count()).ToList();
        int selectedPid = processGrid.CurrentRow?.Cells[0].Value is int pid ? pid : -1;
        int previousScrollRow = -1;
        try { previousScrollRow = processGrid.FirstDisplayedScrollingRowIndex; }
        catch (InvalidOperationException) { /* No visible rows yet. */ }
        processGrid.SuspendLayout();
        try
        {
            processGrid.Rows.Clear();
            foreach(var group in groups)
            {
                var r=group.First();
                bool matchesCategory = MatchesPublisherFilter(r.PublisherCategory, r.Company, processPublisherFilter);
                if (!matchesCategory) continue;
                if (processSearch.Length > 0 &&
                    !$"{r.Pid} {r.Process} {r.PublisherCategory} {r.Company} {r.Signer} {r.Path}".Contains(processSearch, StringComparison.OrdinalIgnoreCase)) continue;
                processGrid.Rows.Add(group.Key,r.Process,group.Count(),r.PublisherCategory,r.Company,r.Path);
            }
            ApplyProcessSort();
            if (selectedPid >= 0)
            {
                foreach (DataGridViewRow row in processGrid.Rows)
                {
                    if (row.Cells[0].Value is not int rowPid || rowPid != selectedPid) continue;
                    var firstVisibleColumn = processGrid.Columns.Cast<DataGridViewColumn>()
                        .Where(column => column.Visible).OrderBy(column => column.DisplayIndex).FirstOrDefault();
                    if (firstVisibleColumn is not null)
                        processGrid.CurrentCell = row.Cells[firstVisibleColumn.Index];
                    row.Selected = true;
                    break;
                }
            }
            if (previousScrollRow >= 0 && previousScrollRow < processGrid.Rows.Count)
            {
                try { processGrid.FirstDisplayedScrollingRowIndex = previousScrollRow; }
                catch (InvalidOperationException) { /* Row may not be displayable at this size. */ }
            }
        }
        finally { processGrid.ResumeLayout(); }
    }

    sealed class ConnectionRow
    {
        public string Protocol{get;set;}="";public string Process{get;set;}="";public int Pid{get;set;}
        public string LocalAddress{get;set;}="";public int LocalPort{get;set;}public string RemoteAddress{get;set;}="";public int RemotePort{get;set;}
        public string State{get;set;}="";public string Path{get;set;}="";
        public string Company{get;set;}="Unknown";
        public string PublisherCategory{get;set;}="Unavailable";
        public string Signer{get;set;}="Unknown";
        public string SignatureStatus{get;set;}="Not checked";
        public string SignatureDetail{get;set;}="";
        public DateTime ProcessStartedUtc { get; set; } = DateTime.MinValue;
        public string Group{get;set;}="";
        // Use this stable identity for "new connection" detection; state is an attribute, not identity.
        public string EndpointKey=>SecurityGuards.EndpointIdentity(Protocol, Pid, LocalAddress,
            LocalPort, RemoteAddress, RemotePort);
        public string Key=>$"{EndpointKey}|{State}";
    }
}
