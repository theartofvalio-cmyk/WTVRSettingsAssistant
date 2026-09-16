namespace WTVRSettingsAssistant;

internal sealed class FlapPositionCaptureForm : Form
{
    public List<InputCondition>[] Result { get; private set; } = [];
    internal static List<InputCondition>[]? BuildConditions(uint[] masks)
    {
        if (masks.Length != 3 || masks.Distinct().Count() != 3) return null;
        uint varying = (masks[0] ^ masks[1]) | (masks[0] ^ masks[2]);
        // A two-button three-way switch has a released neutral and separate ends.
        if ((masks[1] & varying) != 0 || (masks[0] & masks[2] & varying) != 0) return null;
        return masks.Select(mask => Enumerable.Range(0, 32).Where(bit => (varying & (1u << bit)) != 0)
            .Select(bit => new InputCondition { ButtonId = bit + 1, RequiredState = (mask & (1u << bit)) != 0 }).ToList()).ToArray();
    }

    internal FlapPositionCaptureForm(string[] names, PhysicalJoystick device, string language)
    {
        string T(string key) => AppText.T(language, "Flaps." + key);
        Text = T("LearnTitle"); ClientSize = new Size(700, 470); MinimumSize = new Size(650, 490);
        StartPosition = FormStartPosition.CenterParent; BackColor = IllustratedTheme.Background;
        ForeColor = IllustratedTheme.Ivory; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        var prompt = new Label { Dock = DockStyle.Fill };
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
            BackColor = IllustratedTheme.Panel, ForeColor = IllustratedTheme.Ivory, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        list.Columns.Add(T("PositionColumn"), 150); list.Columns.Add(T("CaptureStatus"), 180); list.Columns.Add(T("CapturedButtons"), 285);
        var live = new Label { Dock = DockStyle.Fill };
        var status = new Label { Dock = DockStyle.Fill, ForeColor = IllustratedTheme.Gold };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var capture = new Button { AutoSize = true, Text = T("Capture") };
        var save = new Button { AutoSize = true, Text = T("UsePositions"), Enabled = false };
        var cancel = new Button { AutoSize = true, Text = AppText.T(language, "Common.Cancel"), DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([save, capture, cancel]);
        root.Controls.Add(prompt); root.Controls.Add(list); root.Controls.Add(live); root.Controls.Add(status); root.Controls.Add(buttons); Controls.Add(root);
        uint?[] masks = new uint?[3];
        string Mask(uint mask) => mask == 0 ? T("Released") : string.Join(", ", Enumerable.Range(1, 32).Where(b => (mask & (1u << (b - 1))) != 0).Select(b => "BTN" + b));
        for (int i = 0; i < 3; i++) list.Items.Add(new ListViewItem([names[i], T("NotCaptured"), "-"]));
        list.Items[0].Selected = true;
        int Selected() => list.SelectedIndices.Count == 1 ? list.SelectedIndices[0] : 0;
        void RefreshPrompt()
        {
            int i = Selected();
            prompt.Text = string.Format(T("CaptureStep"), i + 1, names[i]) + Environment.NewLine +
                T(i == 1 ? "CentreCaptureHelp" : "EndCaptureHelp");
            capture.Text = T(masks[i].HasValue ? "Recapture" : "Capture");
        }
        list.SelectedIndexChanged += (_, _) => RefreshPrompt();
        capture.Click += (_, _) =>
        {
            var current = WinMmJoystickReader.Read(device.Id, device.Name);
            if (current is null || current.Name != device.Name) { status.Text = T("CaptureDisconnected"); return; }
            int i = Selected(); masks[i] = current.ButtonMask;
            list.Items[i].SubItems[1].Text = T("Captured"); list.Items[i].SubItems[2].Text = Mask(current.ButtonMask);
            int next = Array.FindIndex(masks, m => !m.HasValue);
            if (next >= 0) { list.Items[i].Selected = false; list.Items[next].Selected = true; status.Text = T("MorePositions"); }
            else
            {
                var result = BuildConditions(masks.Select(m => m!.Value).ToArray());
                save.Enabled = result is not null; Result = result ?? [];
                status.Text = T(result is null ? "CaptureInvalid" : "CaptureReview");
            }
            RefreshPrompt();
        };
        save.Click += (_, _) => { if (Result.Length == 3) DialogResult = DialogResult.OK; };
        var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            var current = WinMmJoystickReader.Read(device.Id, device.Name);
            capture.Enabled = current is not null && current.Name == device.Name;
            live.Text = current is null ? T("CaptureDisconnected") : T("LiveButtons") + " " + Mask(current.ButtonMask);
        };
        Shown += (_, _) => { RefreshPrompt(); timer.Start(); };
        Disposed += (_, _) => timer.Dispose();
        CancelButton = cancel; RefreshPrompt();
    }
}
