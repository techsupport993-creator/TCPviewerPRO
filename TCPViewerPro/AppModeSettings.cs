using System.Text.Json;

namespace TCPViewerPro;

/// <summary>
/// Application-wide presentation preference. Changing modes only changes
/// visible controls and default detail level; it never starts diagnostics,
/// permits unsafe actions, or changes the network monitoring collector.
/// </summary>
public sealed partial class MainForm
{
    private static readonly string AppModeSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TCPViewerPro", "app-mode.json");

    private bool advancedAppMode;
    private readonly Label appModeSubtitle = new();
    private Action? updateConnectionToolbarForMode;

    private void LoadAppModeSettings()
    {
        try
        {
            if (!File.Exists(AppModeSettingsPath)) return;
            var preferences = AppModeProfile.FromJson(File.ReadAllText(AppModeSettingsPath));
            advancedAppMode = AppModeProfile.IsAdvanced(preferences.Mode);
        }
        catch
        {
            // A missing, corrupt or unreadable preference must never block startup.
            advancedAppMode = false;
        }
    }

    private void SaveAppModeSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppModeSettingsPath)!);
            var preferences = new AppModePreferences
            {
                Mode = advancedAppMode ? AppModeProfile.Advanced : AppModeProfile.Standard
            };
            File.WriteAllText(AppModeSettingsPath,
                JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "App mode was changed for this session, but the preference could not be saved:\n" +
                ex.Message + "\n\nIt may reset after you restart TCP Viewer Pro.",
                "Application mode settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyAppMode(bool useAdvanced)
    {
        if (advancedAppMode == useAdvanced) return;

        // Capture the current mode's user-customized columns BEFORE switching
        // the mode flag. Layouts for both modes are preserved separately.
        layoutSaveTimer.Stop();
        SaveTableLayoutSettings();
        advancedAppMode = useAdvanced;
        SaveAppModeSettings();

        RestoreAppModeGridLayouts();
        SetDiagnosticMode(advancedAppMode); // Changes the view, runs no tests.
        updateConnectionToolbarForMode?.Invoke();
        UpdateAppModeSubtitle();
        ApplyFilter();
        RefreshProcesses(current);
        ScheduleGridLayoutSave();

        footer.Text = advancedAppMode
            ? "Advanced App Mode enabled • Detailed tables, technician controls and Advanced Diagnostics."
            : "Standard App Mode enabled • Simplified tables, streamlined tools and Simple Diagnostics.";
    }

    private void UpdateAppModeSubtitle()
    {
        if (!appModeSubtitle.IsDisposed)
            appModeSubtitle.Text = advancedAppMode
                ? "LIVE NETWORK MONITOR  •  ADVANCED APP MODE"
                : "LIVE NETWORK MONITOR  •  STANDARD APP MODE";
    }

    private void BuildAppModeSettingsSection(FlowLayoutPanel scroll)
    {
        scroll.Controls.Add(new Label
        {
            Text = "APPLICATION MODE", Font = new Font("Segoe UI Semibold", 17F),
            ForeColor = cyan, AutoSize = true, Margin = new Padding(4, 4, 4, 8)
        });
        scroll.Controls.Add(new Label
        {
            Text = "Choose the detail level used across TCP Viewer Pro. " +
                   "Changes take effect immediately and persist after restart.",
            Width = 670, Height = 43, ForeColor = muted,
            Font = new Font("Segoe UI", 9.5F), Margin = new Padding(4, 0, 4, 5)
        });
        var modeSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 258, Height = 36,
            BackColor = panel2, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10F),
            Margin = new Padding(5, 2, 4, 8)
        };
        modeSelector.Items.AddRange(new object[]
        {
            "STANDARD — Everyday use",
            "ADVANCED — IT Technician"
        });
        modeSelector.SelectedIndex = advancedAppMode ? 1 : 0;
        modeSelector.SelectedIndexChanged += (_, _) =>
            ApplyAppMode(modeSelector.SelectedIndex == 1);
        scroll.Controls.Add(modeSelector);
        scroll.Controls.Add(new Label
        {
            Width = 680, Height = 90, ForeColor = muted,
            Text = "STANDARD: Compact connection table, focused process details, " +
                   "Simple Network Diagnostics, and Simple Connection Investigator by default.\n" +
                   "ADVANCED: Full TCP/UDP table columns, full process metadata, " +
                   "a quick DNS lookup action, Advanced Network Diagnostics, and " +
                   "Advanced Connection Investigator by default.\n" +
                   "All essential features remain available in either mode. " +
                   "Network checks and system-changing actions still require user action.",
            Font = new Font("Segoe UI", 9F),
            Margin = new Padding(5, 0, 5, 17)
        });
    }
}
