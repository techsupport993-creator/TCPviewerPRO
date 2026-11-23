using System.Text.Json;

namespace TCPViewerPro;

/// <summary>
/// Global application palettes. The per-connection state colors in ColorSettings
/// are intentionally independent so established/listening/warning cues remain
/// meaningful even when the surrounding interface theme changes.
/// </summary>
public sealed partial class MainForm
{
    private const string GreenTheme = "Matrix Green";
    private const string BlueTheme = "Cyber Blue";
    private const string DarkTheme = "Midnight Dark";
    private string activeTheme = GreenTheme;

    private static readonly string ThemePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TCPViewerPro", "theme.json");

    private readonly record struct ThemePalette(
        Color Background, Color Panel, Color Raised, Color Accent,
        Color SecondaryAccent, Color Muted, Color GridLine,
        Color AlternateRow, Color SelectedRow, Color DarkButton);

    private static ThemePalette PaletteFor(string name) => name switch
    {
        BlueTheme => new ThemePalette(
            Color.FromArgb(12, 17, 29),
            Color.FromArgb(20, 29, 46),
            Color.FromArgb(25, 38, 59),
            Color.FromArgb(0, 210, 255),
            Color.FromArgb(66, 220, 160),
            Color.FromArgb(145, 164, 190),
            Color.FromArgb(38, 53, 75),
            Color.FromArgb(17, 25, 40),
            Color.FromArgb(31, 72, 94),
            Color.FromArgb(7, 20, 32)),

        // Accessible, neutral charcoal palette with cool silver-blue accents.
        // TCP-state colors remain separate from the surrounding UI.
        DarkTheme => new ThemePalette(
            Color.FromArgb(12, 14, 19),      // window background
            Color.FromArgb(22, 25, 32),      // main panels
            Color.FromArgb(32, 36, 46),      // raised controls
            Color.FromArgb(141, 184, 250),   // headings and selection accent
            Color.FromArgb(109, 222, 178),   // secondary accent
            Color.FromArgb(169, 181, 200),   // descriptions / secondary labels
            Color.FromArgb(48, 54, 66),      // grid dividers
            Color.FromArgb(26, 30, 38),      // alternating rows
            Color.FromArgb(45, 68, 99),      // selection background
            Color.FromArgb(14, 20, 30)),     // contrasting button text

        _ => new ThemePalette(
            Color.FromArgb(7, 20, 13),       // black-green background
            Color.FromArgb(13, 35, 24),      // panel surface
            Color.FromArgb(22, 54, 37),      // raised surface
            Color.FromArgb(67, 235, 135),    // bright green accent
            Color.FromArgb(154, 244, 105),   // vivid lime accent
            Color.FromArgb(146, 181, 160),   // muted sage
            Color.FromArgb(32, 75, 49),      // grid separators
            Color.FromArgb(10, 27, 18),      // alternating rows
            Color.FromArgb(24, 78, 48),      // selection highlight
            Color.FromArgb(5, 35, 20))
    };

    private ThemePalette CurrentPalette() => new(
        bg, panel, panel2, cyan, green, muted,
        PaletteFor(activeTheme).GridLine,
        PaletteFor(activeTheme).AlternateRow,
        PaletteFor(activeTheme).SelectedRow,
        PaletteFor(activeTheme).DarkButton);

    private void SetPalette(string name)
    {
        var p = PaletteFor(name);
        bg = p.Background;
        panel = p.Panel;
        panel2 = p.Raised;
        cyan = p.Accent;
        green = p.SecondaryAccent;
        muted = p.Muted;
        activeTheme = name;
    }

    private sealed class ThemeSettingsData
    {
        public string Theme { get; set; } = GreenTheme;
    }

    private void LoadThemeSettings()
    {
        string theme = GreenTheme; // Green theme is default even for v6 upgrades.
        try
        {
            if (File.Exists(ThemePath))
                theme = JsonSerializer.Deserialize<ThemeSettingsData>(File.ReadAllText(ThemePath))?.Theme
                    ?? GreenTheme;
        }
        catch { /* Invalid or inaccessible theme file: use Matrix Green. */ }
        SetPalette(theme == BlueTheme || theme == DarkTheme ? theme : GreenTheme);
    }

    private void SaveThemeSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath)!);
            File.WriteAllText(ThemePath, JsonSerializer.Serialize(
                new ThemeSettingsData { Theme = activeTheme },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Unable to save theme preference:\n" + ex.Message,
                "Theme Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyTheme(string name)
    {
        if (name != GreenTheme && name != BlueTheme && name != DarkTheme) return;
        if (activeTheme == name) return;
        ThemePalette old = CurrentPalette();
        SetPalette(name);
        ThemePalette next = CurrentPalette();
        SaveThemeSettings();

        SuspendLayout();
        try
        {
            RecolorTree(this, old, next);
            ApplyGridPalette(grid, next);
            ApplyGridPalette(processGrid, next);

            // Process Inspector windows are owned forms, not child controls.
            // Recolor them as well, including their endpoint DataGridView headers.
            foreach (var inspector in openInspectorDialogs.ToArray())
                if (!inspector.IsDisposed)
                {
                    inspector.SuspendLayout();
                    try
                    {
                        RecolorTree(inspector, old, next);
                        inspector.Invalidate(true);
                    }
                    finally { inspector.ResumeLayout(true); }
                }

            // The status strip's items are not children in Control.Controls.
            foreach (Control control in Descendants(this))
            {
                if (control is ToolStrip strip)
                    foreach (ToolStripItem item in strip.Items)
                        if (item.ForeColor == old.Muted) item.ForeColor = next.Muted;
            }
            UpdateNavSelection();
            Invalidate(true);
        }
        finally { ResumeLayout(true); }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (Control sub in Descendants(control)) yield return sub;
        }
    }

    private void RecolorTree(Control control, ThemePalette before, ThemePalette after)
    {
        // A change-color swatch represents a TCP state, not the app theme.
        bool isColorSwatch = control is Button b && b.Text == "CHANGE COLOR";
        if (!isColorSwatch)
        {
            control.BackColor = RemapColor(control.BackColor, before, after);
            control.ForeColor = RemapColor(control.ForeColor, before, after);
            if (control is Button button)
                button.FlatAppearance.MouseOverBackColor = RemapColor(
                    button.FlatAppearance.MouseOverBackColor, before, after);
        }

        if (control is DataGridView view) ApplyGridPalette(view, after);
        foreach (Control child in control.Controls) RecolorTree(child, before, after);
    }

    private static Color RemapColor(Color value, ThemePalette from, ThemePalette to)
    {
        if (value == from.Background) return to.Background;
        if (value == from.Panel) return to.Panel;
        if (value == from.Raised) return to.Raised;
        if (value == from.Accent) return to.Accent;
        if (value == from.SecondaryAccent) return to.SecondaryAccent;
        if (value == from.Muted) return to.Muted;
        if (value == from.DarkButton) return to.DarkButton;
        if (value == from.GridLine) return to.GridLine;
        if (value == from.AlternateRow) return to.AlternateRow;
        if (value == from.SelectedRow) return to.SelectedRow;
        return value;
    }

    private static void ApplyGridPalette(DataGridView view, ThemePalette theme)
    {
        view.BackgroundColor = theme.Background;
        view.GridColor = theme.GridLine;
        view.ColumnHeadersDefaultCellStyle.BackColor = theme.Raised;
        view.ColumnHeadersDefaultCellStyle.ForeColor = theme.Accent;
        view.DefaultCellStyle.BackColor = theme.Panel;
        view.DefaultCellStyle.ForeColor = Color.Gainsboro;
        view.DefaultCellStyle.SelectionBackColor = theme.SelectedRow;
        view.DefaultCellStyle.SelectionForeColor = Color.White;
        view.AlternatingRowsDefaultCellStyle.BackColor = theme.AlternateRow;
        view.RowHeadersDefaultCellStyle.BackColor = theme.Raised;
        view.Invalidate();
    }
}
