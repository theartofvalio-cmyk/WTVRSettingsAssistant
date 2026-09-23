namespace WTVRSettingsAssistant;

internal sealed class SwitchSequenceForm : Form
{
    public SwitchSequenceForm(SwitchStateDefinition row, string language)
    {
        string T(string key) => AppText.T(language, key);

        Text = T("Switch.SequenceTitle");
        ClientSize = new Size(700, 460);
        MinimumSize = new Size(650, 420);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = IllustratedTheme.Background;
        ForeColor = IllustratedTheme.Ivory;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 2,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = IllustratedTheme.Panel
        };
        grid.EnableHeadersVisualStyles = false;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(33, 36, 37);
        grid.DefaultCellStyle.ForeColor = IllustratedTheme.Ivory;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(92, 79, 53);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 27, 28);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = IllustratedTheme.Gold;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 27, 28);
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = IllustratedTheme.Gold;
        grid.RowTemplate.Height = 36;
        grid.ColumnHeadersHeight = 44;
        grid.MultiSelect = false;
        grid.EditingControlShowing += (_, e) =>
        {
            e.Control.BackColor = Color.FromArgb(33, 36, 37);
            e.Control.ForeColor = IllustratedTheme.Ivory;
        };
        grid.Columns.Add("Key", T("Switch.SequenceKey"));
        grid.Columns.Add("Repeat", T("Switch.SequencePressCount"));
        grid.Columns.Add("Interval", T("Switch.SequenceDelay"));
        foreach (DataGridViewColumn column in grid.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        foreach (SwitchMacroStep step in row.Sequence)
            grid.Rows.Add(step.Key, step.Repeat, step.IntervalMs);
        if (grid.Rows.Count == 0)
            grid.Rows.Add(row.OutputKey, 1, 150);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
        for (int i = 0; i < 4; i++) footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        int buttonIndex = 0;
        Button Button(string text, Action action)
        {
            Button button = AdvancedSwitchManagerForm.B(text);
            button.Dock = DockStyle.Fill;
            button.Click += (_, _) => action();
            footer.Controls.Add(button, buttonIndex % 4, buttonIndex / 4);
            buttonIndex++;
            return button;
        }

        void MoveRow(int offset)
        {
            grid.EndEdit();
            if (grid.CurrentRow is not { } selected) return;
            int destination = selected.Index + offset;
            if (destination < 0 || destination >= grid.Rows.Count) return;
            grid.Rows.Remove(selected);
            grid.Rows.Insert(destination, selected);
            grid.CurrentCell = selected.Cells[0];
        }

        Button(T("Switch.SequenceAddKey"), () =>
        {
            if (grid.Rows.Count >= 32) return;
            grid.EndEdit();
            int index = grid.Rows.Add("", 1, 150);
            grid.CurrentCell = grid.Rows[index].Cells[0];
        });
        Button(T("Switch.SequenceRemove"), () => { if (grid.CurrentRow is { } current) grid.Rows.Remove(current); });
        Button(T("Switch.SequenceMoveUp"), () => MoveRow(-1));
        Button(T("Switch.SequenceMoveDown"), () => MoveRow(1));
        Button(T("Switch.SequenceCaptureKey"), () =>
        {
            if (grid.CurrentRow is { } current &&
                SwitchEditorForm.CaptureKey(this, T("Switch.SequenceCaptureKey"), language, out Keys key))
                current.Cells[0].Value = key.ToString();
        });
        Button(T("Switch.Save"), () =>
        {
            grid.EndEdit();
            var steps = new List<SwitchMacroStep>();
            foreach (DataGridViewRow current in grid.Rows)
            {
                if (!Enum.TryParse(Convert.ToString(current.Cells[0].Value), true, out Keys key) || key == Keys.None ||
                    (key & Keys.Modifiers) != 0 || !Enum.IsDefined(key) ||
                    !int.TryParse(Convert.ToString(current.Cells[1].Value), out int repeat) || repeat is < 1 or > 20 ||
                    !int.TryParse(Convert.ToString(current.Cells[2].Value), out int interval) || interval is < 0 or > 5000)
                {
                    MessageBox.Show(this, T("Switch.SequenceValidation"), T("Switch.SequenceTitle"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                steps.Add(new SwitchMacroStep { Key = key.ToString(), Repeat = repeat, IntervalMs = interval });
            }
            if (steps.Count == 0) return;
            row.Sequence = steps;
            // Automated/telemetry-driven flap roles were intentionally removed from 2.1.
            row.FlapRole = "None";
            DialogResult = DialogResult.OK;
        });
        Button(T("Common.Cancel"), () => DialogResult = DialogResult.Cancel);

        root.Controls.Add(grid, 0, 0);
        root.Controls.Add(footer, 0, 1);
        Controls.Add(root);
    }
}

internal sealed class SwitchGestureRecorder : Form
{
    public List<SwitchGestureStep> Result { get; } = new();

    public SwitchGestureRecorder(PhysicalJoystick device, string language)
    {
        string T(string key) => AppText.T(language, key);

        Text = T("Switch.GestureTitle");
        ClientSize = new Size(620, 380);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = IllustratedTheme.Background;
        ForeColor = IllustratedTheme.Ivory;
        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = IllustratedTheme.Panel,
            ForeColor = IllustratedTheme.Ivory,
            BorderStyle = BorderStyle.FixedSingle
        };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50 };
        var start = AdvancedSwitchManagerForm.B(T("Switch.GestureRecord"));
        var stop = AdvancedSwitchManagerForm.B(T("Switch.GestureStop")); stop.Enabled = false;
        var save = AdvancedSwitchManagerForm.B(T("Switch.GestureUse")); save.Enabled = false;
        var cancel = AdvancedSwitchManagerForm.B(T("Common.Cancel"));
        foreach (Button button in new[] { start, stop, save, cancel }) button.Size = new Size(145, 42);
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        var clock = new System.Diagnostics.Stopwatch();
        start.Click += (_, _) =>
        {
            Result.Clear(); list.Items.Clear(); clock.Restart(); timer.Start();
            start.Enabled = false; stop.Enabled = true; save.Enabled = false;
        };
        stop.Click += (_, _) =>
        {
            timer.Stop(); start.Enabled = true; stop.Enabled = false; save.Enabled = Result.Count >= 2;
        };
        timer.Tick += (_, _) =>
        {
            PhysicalJoystick? current = WinMmJoystickReader.Read(device.Id, device.Name);
            if (current is null)
            {
                timer.Stop();
                list.Items.Add(T("Switch.GestureDisconnected"));
                start.Enabled = true; save.Enabled = false;
                return;
            }
            if (clock.ElapsedMilliseconds > 10000 || Result.Count >= 32) { stop.PerformClick(); return; }
            if (Result.Count > 0 && Result[^1].Mask == current.ButtonMask) return;
            int elapsed = (int)clock.ElapsedMilliseconds;
            Result.Add(new SwitchGestureStep { Mask = current.ButtonMask, ElapsedMs = elapsed });
            list.Items.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                T("Switch.GestureLine"), elapsed, current.ButtonMask));
        };
        save.Click += (_, _) => { timer.Stop(); DialogResult = DialogResult.OK; };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Disposed += (_, _) => timer.Dispose();
        footer.Controls.AddRange([start, stop, save, cancel]);
        Controls.Add(list);
        Controls.Add(footer);
    }
}
