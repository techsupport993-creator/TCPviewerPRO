using System.Diagnostics;

namespace TCPViewerPro;

public sealed partial class MainForm
{
    private TabPage BuildAboutPage()
    {
        var page = Page("ABOUT");
        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, BackColor = bg,
            Padding = new Padding(18)
        };

        string version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "Unknown";
        scroll.Controls.Add(new Label
        {
            Text = "ABOUT TCP VIEWER PRO", ForeColor = cyan,
            Font = new Font("Segoe UI Semibold", 20F), AutoSize = true,
            Margin = new Padding(4, 2, 0, 12)
        });

        var card = new Panel
        {
            Width = 720, Height = 240, BackColor = panel,
            Padding = new Padding(20), Margin = new Padding(4, 0, 0, 14)
        };
        card.Controls.Add(new Label
        {
            Text = "TCP VIEWER PRO", ForeColor = cyan,
            Font = new Font("Segoe UI Semibold", 18F),
            AutoSize = true, Location = new Point(18, 15)
        });
        card.Controls.Add(new Label
        {
            Text = $"Version {version}  •  Windows 10/11 x64",
            ForeColor = green, Font = new Font("Consolas", 10F),
            AutoSize = true, Location = new Point(21, 58)
        });
        card.Controls.Add(new Label
        {
            Text = "Local TCP/UDP connection monitoring and process investigation.",
            ForeColor = Color.White, Font = new Font("Segoe UI", 11F),
            AutoSize = true, MaximumSize = new Size(670, 0),
            Location = new Point(21, 95)
        });
        card.Controls.Add(new Label
        {
            Text = "Includes publisher verification, connection history, CSV export, " +
                   "saved color themes and optional GitHub Releases update checks. " +
                   "Some navigation modules are planned and are not yet implemented.",
            ForeColor = muted, Font = new Font("Segoe UI", 9.5F),
            AutoSize = true, MaximumSize = new Size(665, 0),
            Location = new Point(21, 137)
        });
        scroll.Controls.Add(card);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = true, Width = 720,
            BackColor = bg, Margin = new Padding(3, 0, 0, 10)
        };
        var releases = Button("DOWNLOADS / RELEASES", cyan, PaletteFor(activeTheme).DarkButton);
        releases.Width = 195;
        releases.Click += (_, _) => OpenProjectLink(
            "https://github.com/techsupport993-creator/TCPviewerPRO/releases/latest");
        var source = Button("VIEW SOURCE CODE", green, PaletteFor(activeTheme).DarkButton);
        source.Width = 150;
        source.Click += (_, _) => OpenProjectLink(
            "https://github.com/techsupport993-creator/TCPviewerPRO");
        var update = Button("UPDATE SETTINGS", panel2, cyan);
        update.Width = 145;
        update.Click += (_, _) => tabs.SelectedIndex = 11;
        var copyInfo = Button("COPY APP INFO", panel2, cyan);
        copyInfo.Width = 155;
        copyInfo.Click += (_, _) =>
        {
            // Copy only basic product diagnostics — never process lists,
            // network addresses, machine name, usernames or credentials.
            string info = $"TCP Viewer Pro v{version}\\n" +
                          $"Theme: {activeTheme}\\n" +
                          $"Windows: {Environment.OSVersion.Version}\\n" +
                          $"App architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}\\n" +
                          $"Update repository: {updatePrefs.Repository}";
            try
            {
                Clipboard.SetText(info.Replace("\\n", Environment.NewLine));
                footer.Text = "Copied application information (no network data).";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not copy application information: " + ex.Message,
                    "Copy app info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        actions.Controls.AddRange(new Control[] { releases, source, update, copyInfo });
        scroll.Controls.Add(actions);

        scroll.Controls.Add(new Label
        {
            Text = "PRIVACY & SECURITY",
            Font = new Font("Segoe UI Semibold", 13F), ForeColor = cyan,
            AutoSize = true, Margin = new Padding(4, 12, 0, 8)
        });
        scroll.Controls.Add(new Label
        {
            Text = "Connection monitoring runs locally. CSV and process reports are saved " +
                   "only when you choose to export them. The optional update checker " +
                   "contacts GitHub, asks before installing, and verifies the download checksum. " +
                   "A verified signature does not guarantee a process is safe.",
            AutoSize = true, MaximumSize = new Size(690, 0),
            ForeColor = muted, Font = new Font("Segoe UI", 10F),
            Margin = new Padding(4, 0, 0, 12)
        });

        page.Controls.Add(scroll);
        return page;
    }

    private void OpenProjectLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open the browser: " + ex.Message,
                "TCP Viewer Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
