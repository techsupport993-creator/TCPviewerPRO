using System.ComponentModel;

namespace TCPViewerPro;

/// <summary>
/// Simple mode hides diagnostic jargon until the user explicitly chooses
/// Advanced. Switching modes does not discard test results or rerun tests.
/// </summary>
public sealed partial class MainForm
{
    private readonly Panel diagnosticSimpleView = new() { Dock = DockStyle.Fill };
    private readonly Panel diagnosticAdvancedView = new() { Dock = DockStyle.Fill };
    private readonly Label simpleHeadline = new();
    private readonly Label simpleExplanation = new();
    private readonly Label simpleNetworkInfo = new();
    private readonly List<(Label Status, Label Explanation)> simpleChecks = new();
    private Button? simpleRunButton;
    private Button? simpleCancelButton;
    private Button? simpleModeButton;
    private Button? advancedModeButton;
    private bool diagnosticsAdvanced;

    private TabPage BuildNetworkDiagnosticsPage()
    {
        var page = Page("NETWORK DIAGNOSTICS");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            BackColor = bg, Padding = new Padding(6, 4, 6, 6)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        header.Controls.Add(new Label
        {
            Text = "NETWORK DIAGNOSTICS", ForeColor = cyan,
            Font = new Font("Segoe UI Semibold", 16.5F),
            AutoSize = true, Location = new Point(12, 6)
        });
        header.Controls.Add(new Label
        {
            Text = "Simple guidance for everyone  •  Advanced details for IT technicians",
            ForeColor = muted, AutoSize = true, Location = new Point(14, 39)
        });
        simpleModeButton = Button("SIMPLE", cyan, bg);
        advancedModeButton = Button("ADVANCED", panel2, Color.White);
        simpleModeButton.Width = 104;
        advancedModeButton.Width = 112;
        simpleModeButton.Height = advancedModeButton.Height = 32;
        simpleModeButton.Anchor = advancedModeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        simpleModeButton.Click += (_, _) => SetDiagnosticMode(false);
        advancedModeButton.Click += (_, _) => SetDiagnosticMode(true);
        header.Controls.Add(simpleModeButton);
        header.Controls.Add(advancedModeButton);
        header.Resize += (_, _) =>
        {
            advancedModeButton.Left = header.ClientSize.Width - advancedModeButton.Width - 10;
            simpleModeButton.Left = advancedModeButton.Left - simpleModeButton.Width - 8;
            advancedModeButton.Top = simpleModeButton.Top = 15;
        };
        layout.Controls.Add(header, 0, 0);

        var container = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        diagnosticSimpleView.Controls.Add(BuildSimpleNetworkView());
        diagnosticAdvancedView.Controls.Add(BuildAdvancedNetworkDiagnosticsView());
        container.Controls.Add(diagnosticAdvancedView);
        container.Controls.Add(diagnosticSimpleView);
        layout.Controls.Add(container, 0, 1);
        SetDiagnosticMode(false);
        return page;
    }

    private void SetDiagnosticMode(bool advanced)
    {
        diagnosticsAdvanced = advanced;
        diagnosticAdvancedView.Visible = advanced;
        diagnosticSimpleView.Visible = !advanced;
        if (advanced) diagnosticAdvancedView.BringToFront();
        else diagnosticSimpleView.BringToFront();

        if (simpleModeButton is not null && advancedModeButton is not null)
        {
            simpleModeButton.BackColor = advanced ? panel2 : cyan;
            simpleModeButton.ForeColor = advanced ? Color.White : bg;
            advancedModeButton.BackColor = advanced ? cyan : panel2;
            advancedModeButton.ForeColor = advanced ? bg : Color.White;
        }
    }

    private Control BuildSimpleNetworkView()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, BackColor = bg, AutoScroll = true };
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 430,
            ColumnCount = 1, RowCount = 5,
            BackColor = bg, Padding = new Padding(3, 8, 3, 2)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 144));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        scroll.Controls.Add(body);

        var actionPanel = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        actionPanel.Controls.Add(new Label
        {
            Text = "Check your connection", ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 14F), AutoSize = true,
            Location = new Point(14, 8)
        });
        var actionHint = new Label
        {
            Text = "One click checks your router, a test server and website name lookup.",
            ForeColor = muted, AutoSize = false, AutoEllipsis = true,
            Location = new Point(15, 42), Size = new Size(400, 26)
        };
        actionPanel.Controls.Add(actionHint);
        simpleRunButton = Button("CHECK CONNECTION", green, bg);
        simpleRunButton.Width = 175;
        simpleRunButton.Height = 39;
        simpleRunButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        simpleRunButton.Click += async (_, _) => await RunSimpleHealthCheckAsync();
        simpleCancelButton = Button("CANCEL", Color.FromArgb(255, 190, 90), bg);
        simpleCancelButton.Width = 90;
        simpleCancelButton.Height = 39;
        simpleCancelButton.Enabled = false;
        simpleCancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        simpleCancelButton.Click += (_, _) => diagnosticCancellation?.Cancel();
        actionPanel.Controls.Add(simpleRunButton);
        actionPanel.Controls.Add(simpleCancelButton);
        actionPanel.Resize += (_, _) =>
        {
            simpleCancelButton.Left = actionPanel.ClientSize.Width - simpleCancelButton.Width - 11;
            simpleRunButton.Left = simpleCancelButton.Left - simpleRunButton.Width - 7;
            simpleRunButton.Top = simpleCancelButton.Top = 18;
            actionHint.Width = Math.Max(120, simpleRunButton.Left - 26);
        };
        body.Controls.Add(actionPanel, 0, 0);

        var overall = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        overall.Controls.Add(new Label
        {
            Text = "CONNECTION SUMMARY", ForeColor = muted,
            Font = new Font("Consolas", 9F, FontStyle.Bold),
            Location = new Point(14, 9), Size = new Size(340, 23)
        });
        simpleHeadline.Text = "Not checked yet";
        simpleHeadline.ForeColor = cyan;
        simpleHeadline.Font = new Font("Segoe UI Semibold", 16F);
        simpleHeadline.Location = new Point(14, 32);
        simpleHeadline.Size = new Size(750, 36);
        simpleHeadline.AutoEllipsis = true;
        simpleExplanation.Text = "Click CHECK CONNECTION to see whether these basic tests respond.";
        simpleExplanation.ForeColor = Color.Gainsboro;
        simpleExplanation.Font = new Font("Segoe UI", 10F);
        simpleExplanation.Location = new Point(16, 72);
        simpleExplanation.Size = new Size(750, 33);
        simpleExplanation.AutoEllipsis = true;
        overall.Controls.Add(simpleHeadline);
        overall.Controls.Add(simpleExplanation);
        overall.Resize += (_, _) =>
        {
            simpleHeadline.Width = Math.Max(160, overall.ClientSize.Width - 30);
            simpleExplanation.Width = Math.Max(160, overall.ClientSize.Width - 32);
        };
        body.Controls.Add(overall, 0, 1);

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = bg, ColumnCount = 3, RowCount = 1
        };
        for (int i = 0; i < 3; i++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var titles = new[]
        {
            ("LOCAL ROUTER", "Can this PC reach its reported gateway?"),
            ("INTERNET TEST", "Does a public test server answer?"),
            ("WEBSITE NAMES", "Can Windows resolve a website name?")
        };
        foreach (var (title, hint) in titles)
        {
            // Fixed, separate rows prevent dock-order bugs: the v6.6.0 cards
            // showed the status ABOVE the title and clipped the description.
            var item = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = panel,
                ColumnCount = 1, RowCount = 3,
                Padding = new Padding(12, 9, 10, 7),
                Margin = new Padding(0, 5, 7, 0)
            };
            item.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            item.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            item.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            item.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var heading = new Label
            {
                Text = title, ForeColor = muted, Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                Margin = Padding.Empty
            };
            var signal = new Label
            {
                Text = "○ Not checked", ForeColor = muted, Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 13F),
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                Margin = Padding.Empty
            };
            var detail = new Label
            {
                Text = hint, ForeColor = Color.Gainsboro, Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 4, 0, 0),
                AutoEllipsis = true, Margin = Padding.Empty
            };
            item.Controls.Add(heading, 0, 0);
            item.Controls.Add(signal, 0, 1);
            item.Controls.Add(detail, 0, 2);
            simpleChecks.Add((signal, detail));
            cards.Controls.Add(item, simpleChecks.Count - 1, 0);
        }
        body.Controls.Add(cards, 0, 2);

        var networkPanel = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        simpleNetworkInfo.Text = "Network adapter: Not checked";
        simpleNetworkInfo.ForeColor = muted;
        simpleNetworkInfo.Dock = DockStyle.Fill;
        simpleNetworkInfo.Padding = new Padding(12, 15, 8, 0);
        simpleNetworkInfo.AutoEllipsis = true;
        networkPanel.Controls.Add(simpleNetworkInfo);
        body.Controls.Add(networkPanel, 0, 3);

        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var tip = new Label
        {
            Text = "Results stay on this PC. Ping failures can be caused by firewalls; this is not a speed test.",
            ForeColor = muted, AutoSize = true, Location = new Point(2, 9)
        };
        var copy = Button("COPY REPORT", cyan, bg);
        copy.Width = 108;
        copy.Height = 29;
        copy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(ComposeDiagnosticsReport()); }
            catch (Exception ex) { MessageBox.Show(this, "Copy failed:\n" + ex.Message); }
        };
        var save = Button("SAVE REPORT", green, bg);
        save.Width = 108;
        save.Height = 29;
        save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        save.Click += (_, _) => SaveNetworkDiagnosticsReport();
        bottom.Controls.Add(tip);
        bottom.Controls.Add(copy);
        bottom.Controls.Add(save);
        bottom.Resize += (_, _) =>
        {
            save.Left = bottom.ClientSize.Width - save.Width - 5;
            copy.Left = save.Left - copy.Width - 7;
            save.Top = copy.Top = 4;
            tip.MaximumSize = new Size(Math.Max(150, copy.Left - 20), 34);
        };
        body.Controls.Add(bottom, 0, 4);
        return scroll;
    }

    private async Task RunSimpleHealthCheckAsync()
    {
        await RunNetworkDiagnosticAsync("Connection check", async token =>
        {
            NetworkHealthService.HealthResult health = await NetworkHealthService.CheckAsync(token);
            if (!IsDisposed)
            {
                diagnosticAdapters = health.Adapters;
                diagnosticAdaptersGrid.DataSource =
                    new BindingList<NetworkDiagnosticsService.AdapterSnapshot>(health.Adapters.ToList());
                UpdateSimpleHealth(health);
            }
            return health.ToReport();
        });
    }

    private void UpdateSimpleHealth(NetworkHealthService.HealthResult health)
    {
        simpleHeadline.Text = health.Headline;
        bool allGood = health.Items.All(x => x.Level == NetworkHealthService.HealthLevel.Good);
        simpleHeadline.ForeColor = allGood ? green : Color.FromArgb(255, 190, 90);
        simpleExplanation.Text = health.Explanation;

        for (int i = 0; i < simpleChecks.Count; i++)
        {
            var (indicator, description) = simpleChecks[i];
            if (i >= health.Items.Count) continue;
            var item = health.Items[i];
            indicator.Text = item.Level switch
            {
                NetworkHealthService.HealthLevel.Good => "✓ Responded",
                NetworkHealthService.HealthLevel.Attention => "⚠ Check result",
                _ => "○ Not available"
            };
            indicator.ForeColor = item.Level switch
            {
                NetworkHealthService.HealthLevel.Good => green,
                NetworkHealthService.HealthLevel.Attention => Color.FromArgb(255, 190, 90),
                _ => muted
            };
            description.Text = item.Explanation;
        }

        var primary = health.Adapters
            .Where(x => x.Status.Equals("Up", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(x => x.Gateways.Count > 0)
            ?? health.Adapters.FirstOrDefault(x =>
                x.Status.Equals("Up", StringComparison.OrdinalIgnoreCase) && x.IPv4 != "None");
        simpleNetworkInfo.Text = primary is null
            ? "Network adapter: No active adapter reported by Windows."
            : $"Windows reports: {primary.Name}  •  IPv4: {primary.IPv4}  •  Gateway: {primary.Gateway}";
    }
}
