using System.Text.Json;

namespace TCPViewerPro;

public sealed partial class MainForm
{
    static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TCPViewerPro", "colors.json");

    static Dictionary<string, Color> DefaultConnectionColors() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ESTABLISHED"] = Color.FromArgb(83, 220, 150),
        ["LISTENING"] = Color.FromArgb(0, 205, 235),
        ["SYN-SENT"] = Color.FromArgb(100, 160, 255),
        ["SYN-RECEIVED"] = Color.FromArgb(180, 135, 255),
        ["TIME-WAIT"] = Color.FromArgb(175, 135, 255),
        ["CLOSE-WAIT"] = Color.FromArgb(255, 165, 90),
        ["FIN-WAIT-1"] = Color.FromArgb(255, 195, 95),
        ["FIN-WAIT-2"] = Color.FromArgb(255, 195, 95),
        ["LAST-ACK"] = Color.FromArgb(255, 195, 95),
        ["CLOSING"] = Color.FromArgb(255, 195, 95),
        ["CLOSED"] = Color.FromArgb(255, 90, 105),
        ["DELETE-TCB"] = Color.FromArgb(255, 90, 105),
        ["UDP"] = Color.FromArgb(145, 190, 225),
        ["EXE"] = Color.FromArgb(255, 75, 90)
    };

    sealed class ColorSettingsData
    {
        public Dictionary<string, int>? Colors { get; set; }
        public bool HighlightExe { get; set; } = true;
    }

    void LoadColorSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var data = JsonSerializer.Deserialize<ColorSettingsData>(File.ReadAllText(SettingsPath));
            if (data is null) return;
            highlightExe = data.HighlightExe;
            if (data.Colors != null)
                foreach (var item in data.Colors)
                    if (connectionColors.ContainsKey(item.Key))
                        connectionColors[item.Key] = Color.FromArgb(item.Value);
        }
        catch { /* Corrupt settings fall back to safe defaults. */ }
    }

    void SaveColorSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var data = new ColorSettingsData
            {
                HighlightExe = highlightExe,
                Colors = connectionColors.ToDictionary(x => x.Key, x => x.Value.ToArgb())
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { MessageBox.Show("Could not save color settings:\n" + ex.Message); }
    }

    TabPage BuildColorSettingsPage()
    {
        var page = Page("SETTINGS");
        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(15), BackColor = bg
        };
        page.Controls.Add(scroll);
        BuildAppModeSettingsSection(scroll);
        scroll.Controls.Add(new Label
        {
            Text = "APPLICATION THEME", Font = new Font("Segoe UI Semibold", 17F),
            ForeColor = cyan, AutoSize = true, Margin = new Padding(4, 4, 4, 9)
        });
        scroll.Controls.Add(new Label
        {
            Text = "Choose Matrix Green, Cyber Blue, or Midnight Dark. Changes apply immediately and are saved.",
            ForeColor = muted, AutoSize = true, Margin = new Padding(4, 0, 4, 8)
        });
        var themeSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Height = 36,
            BackColor = panel2, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10F), Margin = new Padding(5, 2, 4, 16)
        };
        themeSelector.Items.AddRange(new object[] { "Matrix Green", "Cyber Blue", "Midnight Dark" });
        themeSelector.SelectedItem = activeTheme;
        themeSelector.SelectedIndexChanged += (_, _) =>
        {
            if (themeSelector.SelectedItem is string choice) ApplyTheme(choice);
        };
        scroll.Controls.Add(themeSelector);
        BuildUpdateSettingsSection(scroll);
        scroll.Controls.Add(new Label
        {
            Text = "CONNECTION COLORS", Font = new Font("Segoe UI Semibold", 17F),
            ForeColor = cyan, AutoSize = true, Margin = new Padding(4, 4, 4, 12)
        });
        scroll.Controls.Add(new Label
        {
            Text = "Choose a color for each TCP state. Changes apply immediately and persist after restart.",
            ForeColor = muted, AutoSize = true, Margin = new Padding(4, 0, 4, 14)
        });

        foreach (var state in DefaultConnectionColors().Keys)
        {
            string key = state;
            var line = new Panel { Width = 480, Height = 39, BackColor = panel, Margin = new Padding(4, 2, 4, 2) };
            var name = new Label { Text = key, Width = 185, Location = new Point(10, 11), ForeColor = Color.White };
            var swatch = new Button
            {
                Text = "CHANGE COLOR", BackColor = connectionColors[key], ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat, Size = new Size(155, 28), Location = new Point(215, 5),
                Cursor = Cursors.Hand
            };
            swatch.FlatAppearance.BorderSize = 0;
            swatch.Click += (_, _) =>
            {
                using var picker = new ColorDialog { Color = connectionColors[key], FullOpen = true };
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                connectionColors[key] = picker.Color;
                swatch.BackColor = picker.Color;
                SaveColorSettings();
                grid.Invalidate();
                processGrid.Invalidate();
            };
            line.Controls.Add(name);
            line.Controls.Add(swatch);
            scroll.Controls.Add(line);
        }
        var check = new CheckBox
        {
            Text = "Highlight .exe process names and executable paths",
            Checked = highlightExe, AutoSize = true, ForeColor = Color.White,
            Margin = new Padding(8, 14, 4, 12)
        };
        check.CheckedChanged += (_, _) =>
        {
            highlightExe = check.Checked;
            SaveColorSettings();
            grid.Invalidate();
            processGrid.Invalidate();
        };
        scroll.Controls.Add(check);

        var reset = new Button
        {
            Text = "RESET TO DEFAULT COLORS", BackColor = panel2, ForeColor = cyan,
            FlatStyle = FlatStyle.Flat, Width = 230, Height = 36, Margin = new Padding(5, 4, 4, 12)
        };
        reset.Click += (_, _) =>
        {
            foreach (var kv in DefaultConnectionColors()) connectionColors[kv.Key] = kv.Value;
            highlightExe = true;
            SaveColorSettings();
            // Rebuild settings page so swatches reflect the reset palette.
            int index = tabs.TabPages.IndexOf(page);
            tabs.TabPages.Remove(page);
            var fresh = BuildColorSettingsPage();
            tabs.TabPages.Insert(index, fresh);
            tabs.SelectedIndex = index;
            grid.Invalidate();
            processGrid.Invalidate();
        };
        scroll.Controls.Add(reset);
        return page;
    }

    void FormatConnectionCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
            grid.Rows[e.RowIndex].DataBoundItem is not ConnectionRow row) return;

        if (e.CellStyle is not DataGridViewCellStyle style) return;
        string col = grid.Columns[e.ColumnIndex].DataPropertyName;
        bool exe = row.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                   row.Process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        if (highlightExe && exe && (col == "Process" || col == "Path"))
        {
            style.ForeColor = connectionColors["EXE"];
            style.SelectionForeColor = connectionColors["EXE"];
            // Only the colors change; leave value conversion to DataGridView.
            return;
        }

        if (col == "PublisherCategory")
        {
            style.ForeColor = row.PublisherCategory switch
            {
                "Verified Microsoft" => green,
                "Verified third party" => cyan,
                "Unsigned" => Color.FromArgb(255, 105, 115),
                "Unverified" => Color.FromArgb(255, 190, 90),
                _ => muted
            };
            style.SelectionForeColor = style.ForeColor;
            return;
        }
        if (col == "Group" && row.Group.Length > 0)
        {
            style.ForeColor = cyan;
            style.SelectionForeColor = cyan;
            return;
        }
        string key = row.Protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase) ? "UDP" : row.State;
        if (connectionColors.TryGetValue(key, out var color))
        {
            style.ForeColor = color;
            style.SelectionForeColor = color;
            // Only the colors change; leave value conversion to DataGridView.
        }
    }

    void FormatProcessCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (!highlightExe || e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (e.ColumnIndex != 1 && e.ColumnIndex != 5) return;
        if (e.CellStyle is not DataGridViewCellStyle style) return;
        var row = processGrid.Rows[e.RowIndex];
        var path = Convert.ToString(row.Cells[5].Value) ?? "";
        var name = Convert.ToString(row.Cells[1].Value) ?? "";
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            style.ForeColor = connectionColors["EXE"];
            style.SelectionForeColor = connectionColors["EXE"];
            // Only the colors change; leave value conversion to DataGridView.
        }
    }
}
