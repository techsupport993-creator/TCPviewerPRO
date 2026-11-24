using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace TCPViewerPro;

/// <summary>
/// A local-only, read-only investigator launched from a selected live endpoint.
/// No outbound lookups, traffic modifications or automatic threat scoring occur.
/// </summary>
public sealed partial class MainForm
{
    private void InvestigateSelectedConnection()
    {
        if (Selected() is not { } row)
        {
            MessageBox.Show(this, "Select a connection in Live Connections first.",
                "Connection Investigator", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        OpenConnectionInvestigator(row);
    }

    private static InvestigationProcessDetails ReadInvestigationDetails(ConnectionInvestigation snapshot)
    {
        const string unavailable = "Unavailable";
        if (snapshot.Pid <= 4 || string.IsNullOrWhiteSpace(snapshot.ExecutablePath) ||
            snapshot.ProcessStartedUtc == DateTime.MinValue)
            return new(unavailable, unavailable, unavailable, unavailable,
                "Live process identity was not available in the captured snapshot; no additional process data was read.");

        try
        {
            using var process = Process.GetProcessById(snapshot.Pid);
            string currentPath = TryReadImagePath(snapshot.Pid);
            DateTime startedUtc = process.StartTime.ToUniversalTime();
            if (!SecurityGuards.IsSameProcessSnapshot(snapshot.Process, snapshot.ExecutablePath,
                    snapshot.ProcessStartedUtc, process.ProcessName, currentPath, startedUtc))
                return new(unavailable, unavailable, unavailable, unavailable,
                    "The process ended or changed identity since capture. Live inspection was skipped.");

            var native = ReadNativeProcessDetails(snapshot.Pid);
            string hash = unavailable;
            try
            {
                using var file = new FileStream(currentPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                hash = Convert.ToHexString(SHA256.HashData(file));
            }
            catch (Exception) { /* An inaccessible or deleted executable is not a malware verdict. */ }

            process.Refresh();
            string finalPath = TryReadImagePath(snapshot.Pid);
            if (process.HasExited ||
                !SecurityGuards.IsSameProcessSnapshot(snapshot.Process, snapshot.ExecutablePath,
                    snapshot.ProcessStartedUtc, process.ProcessName, finalPath,
                    process.StartTime.ToUniversalTime()))
                return new(unavailable, unavailable, unavailable, unavailable,
                    "The process ended or changed during inspection; live details were discarded.");

            return new(native.ParentPid, native.CommandLine,
                startedUtc.ToString("yyyy-MM-dd HH:mm:ss"), hash,
                "Live PID/name/path/start time matched the captured process identity. " +
                "The file hash describes the executable on disk, not necessarily the loaded image.");
        }
        catch (Exception)
        {
            return new(unavailable, unavailable, unavailable, unavailable,
                "Live process details could not be collected (process ended or access was denied).");
        }
    }

    private void OpenConnectionInvestigator(ConnectionRow selected)
    {
        // Copy fields from the selected row now; later 2-second refreshes must not
        // alter the evidence displayed in an already-open report.
        var firstSeen = connectionActivity
            .Where(x => x.EndpointKey == selected.EndpointKey)
            .Select(x => (DateTime?)x.SeenAt)
            .OrderBy(x => x)
            .FirstOrDefault();
        var snapshot = new ConnectionInvestigation(
            selected.Protocol, selected.Process, selected.Pid,
            selected.LocalAddress, selected.LocalPort, selected.RemoteAddress, selected.RemotePort,
            selected.State, selected.PublisherCategory, selected.Signer, selected.Company,
            selected.SignatureStatus, selected.SignatureDetail, selected.Path,
            selected.ProcessStartedUtc, DateTime.Now, firstSeen);

        var dialog = new Form
        {
            Text = $"Connection Investigator | {snapshot.Process} | v6.7.0",
            Width = 1050, Height = 790, MinimumSize = new Size(820, 600),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 9F),
            KeyPreview = true
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(15), BackColor = bg
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        dialog.Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = bg, ColumnCount = 2, RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var title = new Label
        {
            Dock = DockStyle.Fill, Text = "CONNECTION INVESTIGATOR\n" +
                "Selected endpoint snapshot  •  Offline and read-only",
            Font = new Font("Segoe UI Semibold", 14F), ForeColor = cyan,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var modeButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(0, 20, 0, 0), BackColor = bg
        };
        var simpleMode = Button("SIMPLE", cyan, PaletteFor(activeTheme).DarkButton);
        var advancedMode = Button("ADVANCED", panel2, Color.White);
        simpleMode.Width = 125;
        advancedMode.Width = 125;
        modeButtons.Controls.Add(advancedMode);
        modeButtons.Controls.Add(simpleMode);
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(modeButtons, 1, 0);
        root.Controls.Add(header, 0, 0);

        var views = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var simple = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, BackColor = bg,
            Padding = new Padding(4, 8, 4, 8)
        };
        var advanced = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var advancedNote = new Label
        {
            Dock = DockStyle.Top, Height = 50, Padding = new Padding(8, 8, 5, 5),
            Text = "ADVANCED / Captured values are shown locally. " +
                   "Copy and Export use the redaction option below. " +
                   "Process details are checked against the original PID identity.",
            ForeColor = muted, BackColor = panel
        };
        var advancedText = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
            ScrollBars = ScrollBars.Both, WordWrap = false,
            BackColor = panel, ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5F)
        };
        advanced.Controls.Add(advancedText);
        advanced.Controls.Add(advancedNote);
        views.Controls.Add(advanced);
        views.Controls.Add(simple);
        root.Controls.Add(views, 0, 1);

        Panel Card(string heading, string body, int height)
        {
            var card = new Panel
            {
                Width = 910, Height = height, BackColor = panel, Margin = new Padding(0, 0, 0, 10)
            };
            var caption = new Label
            {
                Text = heading, ForeColor = cyan, Font = new Font("Segoe UI Semibold", 11F),
                Left = 17, Top = 13, Height = 29,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = 865
            };
            var value = new Label
            {
                Text = body, ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 10F),
                Left = 18, Top = 48, Height = height - 55, Width = 860,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AutoEllipsis = true
            };
            card.Controls.Add(caption);
            card.Controls.Add(value);
            return card;
        }

        simple.Controls.Add(Card("WHAT IS THIS CONNECTION?",
            ConnectionInvestigatorService.Explain(snapshot), 138));
        simple.Controls.Add(Card("WHAT DO WE KNOW ABOUT THE ADDRESS?",
            ConnectionInvestigatorService.AddressType(snapshot) +
            (ConnectionInvestigatorService.HasRemotePeer(snapshot)
                ? $"  •  Remote port {snapshot.RemotePort}" : ""), 93));
        simple.Controls.Add(Card("WHAT DO WE KNOW ABOUT THE APPLICATION?",
            ConnectionInvestigatorService.ExplainSignature(snapshot), 118));
        simple.Controls.Add(Card("WHAT SHOULD I CHECK NEXT?",
            ConnectionInvestigatorService.NextSteps(snapshot), 135));
        simple.Controls.Add(Card("IMPORTANT LIMITATIONS",
            "These explanations do not detect malware. There is no online reputation lookup, " +
            "packet capture, traffic-volume estimate, or automatic firewall action.", 96));
        void ResizeSimpleCards()
        {
            int width = Math.Max(500, simple.ClientSize.Width - simple.Padding.Horizontal - 30);
            foreach (Control card in simple.Controls)
                card.Width = width;
        }
        simple.Resize += (_, _) => ResizeSimpleCards();
        ResizeSimpleCards();

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = bg
        };
        footerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        footerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var redact = new CheckBox
        {
            Checked = true, AutoSize = true,
            Text = "Redact IP endpoints, executable paths, command lines and signature details in shared reports (recommended)",
            ForeColor = muted, Dock = DockStyle.Fill
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, AutoScroll = true, BackColor = bg
        };
        var copy = Button("COPY REPORT", green, PaletteFor(activeTheme).DarkButton);
        var save = Button("SAVE REPORT", cyan, PaletteFor(activeTheme).DarkButton);
        var openProcess = Button("PROCESS DETAILS", panel2, Color.White);
        var close = Button("CLOSE", Color.FromArgb(255, 190, 90), Color.FromArgb(20, 25, 35));
        copy.Width = 145; save.Width = 145; openProcess.Width = 155; close.Width = 112;
        actions.Controls.AddRange(new Control[] { copy, save, openProcess, close });
        footerPanel.Controls.Add(redact, 0, 0);
        footerPanel.Controls.Add(actions, 0, 1);
        root.Controls.Add(footerPanel, 0, 2);

        InvestigationProcessDetails? details = null;
        string ExportText() => ConnectionInvestigatorService.FormatReport(snapshot, details, redact.Checked);

        bool ConfirmUnredacted()
        {
            if (redact.Checked) return true;
            return MessageBox.Show(dialog,
                "Redaction is OFF. This report may include local/remote IP addresses, user paths, " +
                "command lines or other private information. Continue?",
                "Share private connection data?", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        copy.Click += (_, _) =>
        {
            if (!ConfirmUnredacted()) return;
            try
            {
                Clipboard.SetText(ExportText());
                footer.Text = "Investigation report copied to clipboard.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, "Could not copy the report: " + ex.Message,
                    "Copy failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        save.Click += (_, _) =>
        {
            if (!ConfirmUnredacted()) return;
            using var dialogSave = new SaveFileDialog
            {
                Filter = "Text reports (*.txt)|*.txt",
                FileName = $"TCPViewerPro_Investigation_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            if (dialogSave.ShowDialog(dialog) != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dialogSave.FileName, ExportText(), Encoding.UTF8);
                footer.Text = "Investigation report saved: " + dialogSave.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, "Could not save the report: " + ex.Message,
                    "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        openProcess.Click += (_, _) => OpenProcessInspector(selected);
        close.Click += (_, _) => dialog.Close();

        void SelectMode(bool showAdvanced)
        {
            simple.Visible = !showAdvanced;
            advanced.Visible = showAdvanced;
            (showAdvanced ? advanced : simple).BringToFront();
            simpleMode.BackColor = showAdvanced ? panel2 : cyan;
            simpleMode.ForeColor = showAdvanced ? Color.White : PaletteFor(activeTheme).DarkButton;
            advancedMode.BackColor = showAdvanced ? cyan : panel2;
            advancedMode.ForeColor = showAdvanced ? PaletteFor(activeTheme).DarkButton : Color.White;
        }
        simpleMode.Click += (_, _) => SelectMode(false);
        advancedMode.Click += (_, _) => SelectMode(true);
        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            dialog.Close();
            e.Handled = true;
        };
        advancedText.Text = ConnectionInvestigatorService.FormatReport(snapshot, details, false);
        SelectMode(false);

        openInspectorDialogs.Add(dialog);
        dialog.FormClosed += (_, _) => openInspectorDialogs.Remove(dialog);
        dialog.Show(this);

        _ = LoadDetailsAsync();
        async Task LoadDetailsAsync()
        {
            try
            {
                InvestigationProcessDetails result = await Task.Run(() => ReadInvestigationDetails(snapshot));
                if (dialog.IsDisposed || advancedText.IsDisposed) return;
                details = result;
                advancedText.Text = ConnectionInvestigatorService.FormatReport(snapshot, details, false);
            }
            catch (Exception)
            {
                if (!dialog.IsDisposed)
                {
                    details = new("Unavailable", "Unavailable", "Unavailable", "Unavailable",
                        "Unexpected error while reading live process details.");
                    advancedText.Text = ConnectionInvestigatorService.FormatReport(snapshot, details, false);
                }
            }
        }
    }
}
