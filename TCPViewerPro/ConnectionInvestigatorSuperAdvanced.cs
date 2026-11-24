namespace TCPViewerPro;

/// <summary>
/// The third, technician-focused mode for Connection Investigator.
/// Displays existing local evidence without polling, probing or modifying traffic.
/// </summary>
public sealed partial class MainForm
{
    private (Control View, Action<InvestigationProcessDetails?> Update) CreateSuperAdvancedView(
        ConnectionInvestigation snapshot,
        IReadOnlyList<InvestigationSocket> sockets,
        IReadOnlyList<InvestigationEvent> events)
    {
        var aggregate = ConnectionInvestigatorForensics.Summarize(sockets, events);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            BackColor = bg, Padding = new Padding(4, 5, 4, 0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 87));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = new Label
        {
            Dock = DockStyle.Fill, BackColor = panel, ForeColor = muted,
            Padding = new Padding(12, 8, 10, 5), AutoEllipsis = true,
            Text = "SUPER ADVANCED  /  DEEP LOCAL INVESTIGATION\n" +
                   "Captured socket table + session observations by PID. " +
                   "No packet capture, external lookups, speed measurement or threat score."
        };
        root.Controls.Add(intro, 0, 0);

        var counters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = bg, ColumnCount = 5,
            RowCount = 1, Padding = new Padding(0, 6, 0, 7)
        };
        for (int i = 0; i < 5; i++)
            counters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        void Counter(int column, string title, string value)
        {
            var box = new Panel
            {
                Dock = DockStyle.Fill, BackColor = panel,
                Margin = new Padding(0, 0, 7, 0)
            };
            var heading = new Label
            {
                Text = title, Dock = DockStyle.Top, Height = 28,
                ForeColor = muted, Padding = new Padding(9, 9, 2, 0),
                Font = new Font("Segoe UI", 8.5F)
            };
            var count = new Label
            {
                Text = value, Dock = DockStyle.Fill, ForeColor = cyan,
                Font = new Font("Segoe UI Semibold", 17F),
                Padding = new Padding(8, 1, 2, 0)
            };
            box.Controls.Add(count);
            box.Controls.Add(heading);
            counters.Controls.Add(box, column, 0);
        }
        Counter(0, "SOCKETS / PID", aggregate.EndpointCount.ToString());
        Counter(1, "TCP / UDP", $"{aggregate.TcpCount} / {aggregate.UdpCount}");
        Counter(2, "ESTABLISHED", aggregate.EstablishedCount.ToString());
        Counter(3, "REMOTE IPs", aggregate.UniqueRemoteAddresses.ToString());
        Counter(4, "SESSION EVENTS", aggregate.ObservationCount.ToString());
        root.Controls.Add(counters, 0, 1);

        var browser = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = bg
        };
        browser.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var switches = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, AutoScroll = true, BackColor = panel,
            Padding = new Padding(8, 6, 0, 0)
        };
        var endpointsButton = Button("RELATED SOCKETS", cyan, PaletteFor(activeTheme).DarkButton);
        endpointsButton.Width = 168;
        var eventsButton = Button("SESSION EVENTS", panel2, Color.White);
        eventsButton.Width = 158;
        var evidenceButton = Button("RAW EVIDENCE", panel2, Color.White);
        evidenceButton.Width = 150;
        switches.Controls.AddRange(new Control[] { endpointsButton, eventsButton, evidenceButton });
        browser.Controls.Add(switches, 0, 0);

        var pages = new Panel { Dock = DockStyle.Fill, BackColor = panel };

        var endpointsView = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        var endpointInfo = new Label
        {
            Dock = DockStyle.Bottom, Height = 36, BackColor = panel,
            ForeColor = muted, Padding = new Padding(9, 8, 5, 3),
            Text = $"Captured once at {snapshot.CapturedAt:HH:mm:ss}. " +
                   $"Showing up to 500 of {sockets.Count} socket rows for PID {snapshot.Pid}. " +
                   "Remote peer may be absent for listening and bound sockets."
        };
        var endpointGrid = new DataGridView();
        ConfigureGrid(endpointGrid);
        endpointGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        endpointGrid.AllowUserToOrderColumns = true;
        endpointGrid.Columns.Add("Protocol", "PROTOCOL");
        endpointGrid.Columns.Add("State", "STATE");
        endpointGrid.Columns.Add("Local", "LOCAL ENDPOINT");
        endpointGrid.Columns.Add("Remote", "REMOTE ENDPOINT");
        endpointGrid.Columns[0].Width = 105;
        endpointGrid.Columns[1].Width = 145;
        endpointGrid.Columns[2].Width = 280;
        endpointGrid.Columns[3].Width = 280;
        foreach (var item in sockets.Take(500))
            endpointGrid.Rows.Add(
                item.Protocol, item.State,
                ConnectionInvestigatorService.Endpoint(item.LocalAddress, item.LocalPort),
                item.RemotePort == 0 ? "No remote peer" :
                    ConnectionInvestigatorService.Endpoint(item.RemoteAddress, item.RemotePort));
        endpointGrid.ClearSelection();
        endpointsView.Controls.Add(endpointGrid);
        endpointsView.Controls.Add(endpointInfo);

        var eventsView = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        var eventsInfo = new Label
        {
            Dock = DockStyle.Bottom, Height = 52, BackColor = panel,
            ForeColor = muted, Padding = new Padding(9, 8, 5, 3),
            Text = $"Showing {events.Count} PID-matched observations retained in this app session. " +
                   "Observations are NOT packet events or actual start times; PID reuse can mix different processes."
        };
        var eventGrid = new DataGridView();
        ConfigureGrid(eventGrid);
        eventGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        eventGrid.Columns.Add("SeenAt", "OBSERVED");
        eventGrid.Columns.Add("State", "TCP STATE WHEN SEEN");
        eventGrid.Columns.Add("Remote", "REMOTE ENDPOINT");
        eventGrid.Columns[0].Width = 195;
        eventGrid.Columns[1].Width = 185;
        eventGrid.Columns[2].Width = 320;
        foreach (var item in events.OrderByDescending(x => x.SeenAt).Take(500))
            eventGrid.Rows.Add(item.SeenAt.ToString("yyyy-MM-dd HH:mm:ss"),
                item.State, ConnectionInvestigatorService.Endpoint(item.RemoteAddress, item.RemotePort));
        eventGrid.ClearSelection();
        eventsView.Controls.Add(eventGrid);
        eventsView.Controls.Add(eventsInfo);

        var rawView = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        var rawDisclaimer = new Label
        {
            Dock = DockStyle.Top, Height = 47, BackColor = panel,
            ForeColor = muted, Padding = new Padding(10, 8, 5, 5),
            Text = "LOCAL VIEW / Raw evidence below includes actual IPs and process details. " +
                   "COPY/SAVE REPORT uses the separate redaction checkbox."
        };
        var raw = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, WordWrap = false,
            ScrollBars = ScrollBars.Both, BackColor = panel, ForeColor = Color.Gainsboro,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.3F)
        };
        rawView.Controls.Add(raw);
        rawView.Controls.Add(rawDisclaimer);

        pages.Controls.AddRange(new Control[] { endpointsView, eventsView, rawView });
        browser.Controls.Add(pages, 0, 1);
        root.Controls.Add(browser, 0, 2);

        void SetPane(int which)
        {
            endpointsView.Visible = which == 0;
            eventsView.Visible = which == 1;
            rawView.Visible = which == 2;
            (which == 0 ? endpointsView : which == 1 ? eventsView : rawView).BringToFront();

            var allButtons = new[] { endpointsButton, eventsButton, evidenceButton };
            for (int i = 0; i < allButtons.Length; i++)
            {
                allButtons[i].BackColor = i == which ? cyan : panel2;
                allButtons[i].ForeColor = i == which
                    ? PaletteFor(activeTheme).DarkButton : Color.White;
            }
        }
        endpointsButton.Click += (_, _) => SetPane(0);
        eventsButton.Click += (_, _) => SetPane(1);
        evidenceButton.Click += (_, _) => SetPane(2);

        Action<InvestigationProcessDetails?> update = details =>
        {
            if (raw.IsDisposed) return;
            raw.Text = ConnectionInvestigatorForensics.FormatDeepReport(
                snapshot, details, sockets, events, redactSensitive: false);
        };
        update(null);
        SetPane(0);
        return (root, update);
    }
}
