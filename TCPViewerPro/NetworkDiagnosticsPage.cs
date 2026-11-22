using System.ComponentModel;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// User-initiated diagnostics in the existing TCP Viewer Pro workspace.
/// All UI updates stay on the WinForms thread; slow network work is async.
/// </summary>
public sealed partial class MainForm
{
    private readonly TextBox diagnosticPingHost = new();
    private readonly TextBox diagnosticDnsHost = new();
    private readonly RichTextBox diagnosticOutput = new();
    private readonly DataGridView diagnosticAdaptersGrid = new();
    private readonly Label diagnosticStatus = new();
    private readonly List<Button> diagnosticRunButtons = new();
    private Button? diagnosticCancelButton;
    private CancellationTokenSource? diagnosticCancellation;
    private IReadOnlyList<NetworkDiagnosticsService.AdapterSnapshot> diagnosticAdapters =
        Array.Empty<NetworkDiagnosticsService.AdapterSnapshot>();

    private TabPage BuildNetworkDiagnosticsPage()
    {
        var page = Page("NETWORK DIAGNOSTICS");
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = bg,
            ColumnCount = 1, RowCount = 5, Padding = new Padding(10, 5, 10, 8)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 53));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 47));
        page.Controls.Add(content);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = panel };
        header.Controls.Add(new Label
        {
            Text = "NETWORK DIAGNOSTICS", Font = new Font("Segoe UI Semibold", 18F),
            ForeColor = cyan, AutoSize = true, Location = new Point(13, 6)
        });
        header.Controls.Add(new Label
        {
            Text = "Ping • DNS resolution • Gateway reachability • Local adapters  |  Tests run only when requested",
            ForeColor = muted, AutoSize = true, Location = new Point(16, 45),
            Font = new Font("Segoe UI", 9F)
        });
        content.Controls.Add(header, 0, 0);

        var targets = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = panel,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            AutoScroll = true, Padding = new Padding(9, 9, 2, 2)
        };
        targets.Controls.Add(new Label
        {
            Text = "PING HOST / IP", ForeColor = muted, AutoSize = false,
            Width = 120, Height = 26, TextAlign = ContentAlignment.MiddleLeft
        });
        diagnosticPingHost.Text = "1.1.1.1";
        diagnosticPingHost.Width = 220;
        diagnosticPingHost.MaxLength = 253;
        diagnosticPingHost.Margin = new Padding(0, 1, 22, 0);
        StyleText(diagnosticPingHost);
        targets.Controls.Add(diagnosticPingHost);
        targets.Controls.Add(new Label
        {
            Text = "DNS HOST", ForeColor = muted, AutoSize = false,
            Width = 78, Height = 26, TextAlign = ContentAlignment.MiddleLeft
        });
        diagnosticDnsHost.Text = "example.com";
        diagnosticDnsHost.Width = 220;
        diagnosticDnsHost.MaxLength = 253;
        diagnosticDnsHost.Margin = new Padding(0, 1, 5, 0);
        StyleText(diagnosticDnsHost);
        targets.Controls.Add(diagnosticDnsHost);
        content.Controls.Add(targets, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = panel,
            WrapContents = false, AutoScroll = true, Padding = new Padding(9, 7, 0, 0)
        };

        Button AddRunButton(string title, Color accent, int width, Action action)
        {
            var button = Button(title, accent, bg);
            button.Width = width;
            button.Height = 33;
            button.Margin = new Padding(0, 1, 7, 0);
            button.Click += (_, _) => action();
            diagnosticRunButtons.Add(button);
            actions.Controls.Add(button);
            return button;
        }

        AddRunButton("PING TEST", cyan, 105, () =>
            _ = RunNetworkDiagnosticAsync("Ping test",
                token => NetworkDiagnosticsService.PingAsync(diagnosticPingHost.Text, 3, token)));
        AddRunButton("DNS LOOKUP", green, 112, () =>
            _ = RunNetworkDiagnosticAsync("DNS lookup",
                token => NetworkDiagnosticsService.LookupDnsAsync(diagnosticDnsHost.Text, token)));
        AddRunButton("GATEWAY TEST", cyan, 125, () =>
            _ = RunNetworkDiagnosticAsync("Gateway test", async token =>
            {
                var adapters = await LoadNetworkAdaptersAsync(token);
                return await NetworkDiagnosticsService.PingGatewayAsync(adapters, token);
            }));
        AddRunButton("REFRESH ADAPTERS", green, 151, () =>
            _ = RunNetworkDiagnosticAsync("Adapter inventory", async token =>
                NetworkDiagnosticsService.FormatAdapterReport(
                    await LoadNetworkAdaptersAsync(token))));
        AddRunButton("RUN ALL", cyan, 90, () =>
            _ = RunNetworkDiagnosticAsync("Full diagnostics", RunAllNetworkDiagnosticsAsync));

        diagnosticCancelButton = Button("CANCEL", Color.FromArgb(255, 190, 90), bg);
        diagnosticCancelButton.Width = 88;
        diagnosticCancelButton.Height = 33;
        diagnosticCancelButton.Margin = new Padding(0, 1, 6, 0);
        diagnosticCancelButton.Enabled = false;
        diagnosticCancelButton.Click += (_, _) => diagnosticCancellation?.Cancel();
        actions.Controls.Add(diagnosticCancelButton);
        content.Controls.Add(actions, 0, 2);

        var outputPanel = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(4) };
        var outputHeader = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = panel };
        var outputTitle = new Label
        {
            Text = "TEST RESULTS", AutoSize = true,
            Font = new Font("Consolas", 9F, FontStyle.Bold),
            ForeColor = cyan, Location = new Point(9, 10)
        };
        outputHeader.Controls.Add(outputTitle);
        diagnosticStatus.Text = "Ready • No diagnostics have been run";
        diagnosticStatus.AutoSize = false;
        diagnosticStatus.ForeColor = muted;
        diagnosticStatus.Location = new Point(125, 10);
        diagnosticStatus.Width = 320;
        outputHeader.Controls.Add(diagnosticStatus);

        var copy = Button("COPY REPORT", cyan, bg);
        copy.Width = 118;
        copy.Height = 28;
        copy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(ComposeDiagnosticsReport());
                diagnosticStatus.Text = "Report copied";
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not copy report:\n" + ex.Message); }
        };
        var save = Button("SAVE REPORT", green, bg);
        save.Width = 114;
        save.Height = 28;
        save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        save.Click += (_, _) => SaveNetworkDiagnosticsReport();
        outputHeader.Controls.Add(copy);
        outputHeader.Controls.Add(save);
        outputHeader.Resize += (_, _) =>
        {
            save.Left = outputHeader.ClientSize.Width - save.Width - 8;
            copy.Left = save.Left - copy.Width - 8;
            copy.Top = save.Top = 3;
            diagnosticStatus.Width = Math.Max(100, copy.Left - 140);
        };
        diagnosticOutput.ReadOnly = true;
        diagnosticOutput.Dock = DockStyle.Fill;
        diagnosticOutput.BorderStyle = BorderStyle.None;
        diagnosticOutput.BackColor = panel2;
        diagnosticOutput.ForeColor = Color.Gainsboro;
        diagnosticOutput.Font = new Font("Consolas", 9F);
        diagnosticOutput.WordWrap = false;
        diagnosticOutput.DetectUrls = false;
        diagnosticOutput.ScrollBars = RichTextBoxScrollBars.Both;
        diagnosticOutput.Text = "Run a test above to see results.\n" +
            "ICMP ping timeouts can occur even when a host is online.\n" +
            "Results stay on this PC unless you choose COPY or SAVE REPORT.";
        outputPanel.Controls.Add(diagnosticOutput);
        outputPanel.Controls.Add(outputHeader);
        content.Controls.Add(outputPanel, 0, 3);

        var adapterPanel = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(4) };
        var adapterLabel = new Label
        {
            Text = "NETWORK ADAPTERS  •  WINDOWS-REPORTED INFORMATION",
            Dock = DockStyle.Top, Height = 32, Padding = new Padding(9, 7, 0, 0),
            Font = new Font("Consolas", 9F, FontStyle.Bold),
            ForeColor = cyan
        };
        ConfigureGrid(diagnosticAdaptersGrid);
        void AddAdapterColumn(string property, string title, int width)
        {
            diagnosticAdaptersGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = property, Name = property, HeaderText = title, Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
        AddAdapterColumn("Name", "ADAPTER", 195);
        AddAdapterColumn("Status", "STATUS", 82);
        AddAdapterColumn("Kind", "TYPE", 126);
        AddAdapterColumn("IPv4", "IPv4", 172);
        AddAdapterColumn("Gateway", "GATEWAY", 177);
        AddAdapterColumn("Dns", "DNS SERVERS", 190);
        AddAdapterColumn("IPv6", "IPv6", 225);
        AddAdapterColumn("Mac", "MAC ADDRESS", 155);
        AddAdapterColumn("Speed", "LINK SPEED", 105);
        adapterPanel.Controls.Add(diagnosticAdaptersGrid);
        adapterPanel.Controls.Add(adapterLabel);
        content.Controls.Add(adapterPanel, 0, 4);
        return page;
    }

    private async Task<IReadOnlyList<NetworkDiagnosticsService.AdapterSnapshot>>
        LoadNetworkAdaptersAsync(CancellationToken cancellation)
    {
        var adapters = await Task.Run(NetworkDiagnosticsService.GetAdapters, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (!IsDisposed)
        {
            diagnosticAdapters = adapters;
            diagnosticAdaptersGrid.DataSource =
                new BindingList<NetworkDiagnosticsService.AdapterSnapshot>(adapters.ToList());
        }
        return adapters;
    }

    private async Task<string> RunAllNetworkDiagnosticsAsync(CancellationToken cancellation)
    {
        var output = new StringBuilder();
        var adapters = await LoadNetworkAdaptersAsync(cancellation);
        output.AppendLine(NetworkDiagnosticsService.FormatAdapterReport(adapters));
        output.AppendLine();
        output.AppendLine(await TryDiagnosticSectionAsync("GATEWAY CHECK",
            () => NetworkDiagnosticsService.PingGatewayAsync(adapters, cancellation), cancellation));
        output.AppendLine();
        output.AppendLine(await TryDiagnosticSectionAsync("PING TEST",
            () => NetworkDiagnosticsService.PingAsync(diagnosticPingHost.Text, 3, cancellation),
            cancellation));
        output.AppendLine();
        output.AppendLine(await TryDiagnosticSectionAsync("DNS LOOKUP",
            () => NetworkDiagnosticsService.LookupDnsAsync(diagnosticDnsHost.Text, cancellation),
            cancellation));
        return output.ToString().TrimEnd();
    }

    private static async Task<string> TryDiagnosticSectionAsync(
        string heading, Func<Task<string>> operation, CancellationToken cancellation)
    {
        try { return await operation(); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception ex) { return $"{heading}\nError: {ex.Message}"; }
    }

    private async Task RunNetworkDiagnosticAsync(
        string title, Func<CancellationToken, Task<string>> operation)
    {
        if (diagnosticCancellation is not null || IsDisposed) return;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        diagnosticCancellation = cancellation;
        foreach (var button in diagnosticRunButtons) button.Enabled = false;
        if (diagnosticCancelButton is not null) diagnosticCancelButton.Enabled = true;
        diagnosticStatus.Text = $"Running {title}...";
        try
        {
            string result = await operation(cancellation.Token);
            if (IsDisposed) return;
            AppendDiagnosticResult($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n" +
                                   result + "\n");
            diagnosticStatus.Text = $"{title} complete";
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                AppendDiagnosticResult($"[{DateTime.Now:HH:mm:ss}] {title} cancelled or timed out.\n");
                diagnosticStatus.Text = "Cancelled / timeout";
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                AppendDiagnosticResult($"[{DateTime.Now:HH:mm:ss}] {title} FAILED: {ex.Message}\n");
                diagnosticStatus.Text = $"{title} failed";
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                foreach (var button in diagnosticRunButtons) button.Enabled = true;
                if (diagnosticCancelButton is not null) diagnosticCancelButton.Enabled = false;
            }
            diagnosticCancellation = null;
        }
    }

    private void AppendDiagnosticResult(string result)
    {
        // Bound UI log memory even after many repeated diagnostic runs.
        if (diagnosticOutput.TextLength > 65_000) diagnosticOutput.Clear();
        diagnosticOutput.AppendText("\n" + new string('─', 56) + "\n" + result + "\n");
        diagnosticOutput.SelectionStart = diagnosticOutput.TextLength;
        diagnosticOutput.ScrollToCaret();
    }

    private string ComposeDiagnosticsReport()
    {
        return "TCP VIEWER PRO • NETWORK DIAGNOSTICS\n" +
               $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
               $"Application version: {typeof(MainForm).Assembly.GetName().Version}\n" +
               "Local report. May contain private IP addresses, MAC addresses and DNS servers.\n\n" +
               diagnosticOutput.Text + "\n\n" +
               NetworkDiagnosticsService.FormatAdapterReport(diagnosticAdapters) + "\n";
    }

    private void SaveNetworkDiagnosticsReport()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text report (*.txt)|*.txt",
            FileName = $"TCPViewerPro_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, ComposeDiagnosticsReport(), Encoding.UTF8);
            diagnosticStatus.Text = "Report saved";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save diagnostic report:\n" + ex.Message,
                "Save report", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
