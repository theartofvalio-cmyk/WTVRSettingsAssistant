using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Text.Json;

namespace WTVRSettingsAssistant;

internal sealed class HiddenKeybindSettings
{
    public bool Enabled { get; set; }
    public string VrHeadPositionUpBinding { get; set; } = "Not assigned";
    public string VrHeadPositionDownBinding { get; set; } = "Not assigned";
    public string SwitchMapToBattlefieldBinding { get; set; } = "Not assigned";
    public string HoverUpBinding { get; set; } = "Not assigned";
    public string HoverDownBinding { get; set; } = "Not assigned";
    public string ScoreBoardMouseFixBinding { get; set; } = "Not assigned";
}

internal sealed class HiddenKeybindsForm : Form
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeybdInput Keyboard;
        // INPUT's native union is sized by MOUSEINPUT on 64-bit Windows. Without
        // this field Marshal.SizeOf<Input>() is too small and SendInput rejects it.
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventScanCode = 0x0008;
    private readonly string _settingsPath;
    private readonly HiddenKeybindSettings _settings;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 25 };
    private readonly Dictionary<string, bool> _wasPressed = new();
    private readonly Label _status;
    private readonly Button _upBinding;
    private readonly Button _downBinding;
    private readonly Button _mapBinding;
    private readonly Button _hoverUpBinding;
    private readonly Button _hoverDownBinding;
    private readonly Button _scoreBoardBinding;
    private readonly HashSet<string> _heldOutputs = new();
    private readonly System.Windows.Forms.Timer _scoreBoardCorrection = new() { Interval = 60 };
    private Point _scoreBoardTarget;
    private readonly AdvancedSwitchService _advancedSwitchService;
    private readonly ThemeCheckBox _advancedSwitchToggle;
    private readonly Button _manageSwitchesButton;

    public event EventHandler? CloseRequested;
    public event EventHandler? AssistantStateChanged;
    public bool AssistantEnabled => _settings.Enabled;
    private ThemeCheckBox? _enabledToggle;
    private string _languageCode = "en";
    private static readonly IReadOnlyDictionary<string, string> LocalizedTextKeys = new Dictionary<string, string>
    {
        ["KeyBind Assistant"] = "Nav.Keybind",
        ["KEYBIND ASSISTANT"] = "Nav.Keybind",
        ["These actions are built into War Thunder, but are not shown in the game Controls menu.\nUse this page to map a keyboard key, mouse button, HOTAS button, or combination to the command shown below."] = "Keybind.Description",
        ["Enable KeyBind Assistant"] = "Assistant.EnableKeybind",
        ["WAR THUNDER ACTION"] = "Keybind.Action",
        ["KEYBOARD COMMAND"] = "Keybind.Command",
        ["YOUR INPUT BINDING"] = "Keybind.Input",
        ["VR Head Position Up"] = "Keybind.HeadUp",
        ["VR Head Position Down"] = "Keybind.HeadDown",
        ["Switch map to battlefield"] = "Keybind.Map",
        ["Hover UP"] = "Keybind.HoverUp",
        ["Hover Down"] = "Keybind.HoverDown",
        ["Score Board Mouse Fix"] = "Keybind.ScoreBoardMouseFix",
        ["Left Shift"] = "Keybind.LeftShift",
        ["Left Ctrl"] = "Keybind.LeftCtrl",
        ["Mouse to top-left"] = "Keybind.MouseTopLeft",
        ["ADVANCED SWITCH BINDINGS"] = "Switch.AdvancedBindings",
        ["Advanced Switch Bindings"] = "Switch.AdvancedBindings",
        ["Create custom switch positions and map each position to a keyboard key or mouse button."] = "Keybind.AdvancedDescription",
        ["MANAGE CUSTOM SWITCHES"] = "Keybind.ManageSwitches",
        ["CLEAR"] = "Assistant.Clear",
        ["Important: keep War Thunder VR Assistant running while using these bindings. If War Thunder runs as administrator, run this app as administrator too so the keyboard commands can reach the game."] = "Keybind.Notice",
    };
    public void ToggleAssistance() { if (_enabledToggle != null) _enabledToggle.Checked = !_enabledToggle.Checked; }

    public HiddenKeybindsForm(string appFolder, Image homeImage, Image infoImage, Action showInfo)
    {
        _settingsPath = Path.Combine(appFolder, "Settings", "hidden_keybinds.json");
        _settings = LoadSettings();
        _advancedSwitchService = new AdvancedSwitchService(Path.Combine(appFolder, "Settings", "advanced_switches.json"));
        if (!_advancedSwitchService.Settings.Enabled) _advancedSwitchService.SetEnabled(true);

        Text = T("Nav.Keybind");
        BackColor = ThemePalette.FromArgb(5, 12, 14);
        ForeColor = ThemePalette.FromArgb(235, 238, 240);
        Font = new Font("Segoe UI", 11f);
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = new Size(900, 620);

        Label title = MakeLabel(T("Nav.Keybind"), 25, FontStyle.Bold, Color.White);
        // The large title needs a tall bounds rectangle; otherwise WinForms clips its lower glyphs.
        title.SetBounds(30, 20, 1180, 78);
        Controls.Add(title);

        Label description = MakeLabel(
            "These actions are built into War Thunder, but are not shown in the game Controls menu.\n" +
            "Use this page to map a keyboard key, mouse button, HOTAS button, or combination to the command shown below.",
            12.5f, FontStyle.Regular, ThemePalette.FromArgb(200, 230, 235));
        // Keep the explanation well below the large heading; this page has ample vertical space.
        description.SetBounds(32, 125, 1320, 62);
        Controls.Add(description);

        ThemeCheckBox enabled = new()
        {
            Text = T("Assistant.EnableKeybind"),
            Checked = _settings.Enabled,
            AutoSize = false,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = BackColor
        };
        enabled.SetBounds(32, 194, 360, 38);
        _enabledToggle = enabled;
        enabled.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = enabled.Checked;
            if (!enabled.Checked)
            {
                ReleaseHeldOutputs();
                _scoreBoardCorrection.Stop();
            }
            _wasPressed.Clear();
            _status!.Text = enabled.Checked ? T("Keybind.EnabledStatus") : T("Keybind.OffStatus");
            SaveSettings();
            AssistantStateChanged?.Invoke(this, EventArgs.Empty);
        };
        Controls.Add(enabled);

        PictureBox info = new() { Image = infoImage, SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
        info.SetBounds(Width - 170, 20, 62, 62);
        info.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        info.Click += (_, _) => showInfo();
        Controls.Add(info);

        PictureBox home = new() { Image = homeImage, SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
        home.SetBounds(Width - 95, 14, 76, 72);
        home.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        home.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(home);

        Panel table = new() { BackColor = ThemePalette.FromArgb(9, 22, 25), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        table.SetBounds(30, 245, Math.Max(600, ClientSize.Width - 60), 506);
        Controls.Add(table);
        table.Resize += (_, _) => LayoutRows(table);

        Label actionHeader = MakeLabel(T("Keybind.Action"), 11, FontStyle.Bold, ThemePalette.FromArgb(65, 220, 245));
        Label keyHeader = MakeLabel(T("Keybind.Command"), 11, FontStyle.Bold, ThemePalette.FromArgb(65, 220, 245));
        Label hotasHeader = MakeLabel(T("Keybind.Input"), 11, FontStyle.Bold, ThemePalette.FromArgb(65, 220, 245));
        actionHeader.Name = "ActionHeader"; keyHeader.Name = "KeyHeader"; hotasHeader.Name = "HotasHeader";
        table.Controls.AddRange(new Control[] { actionHeader, keyHeader, hotasHeader });

        (_upBinding, _) = AddBindingRow(table, "VR Head Position Up", "Page Up  (PGUP)", 0, "up", () => _settings.VrHeadPositionUpBinding, value => _settings.VrHeadPositionUpBinding = value);
        (_downBinding, _) = AddBindingRow(table, "VR Head Position Down", "Page Down  (PGDN)", 1, "down", () => _settings.VrHeadPositionDownBinding, value => _settings.VrHeadPositionDownBinding = value);
        (_mapBinding, _) = AddBindingRow(table, "Switch map to battlefield", "N", 2, "map", () => _settings.SwitchMapToBattlefieldBinding, value => _settings.SwitchMapToBattlefieldBinding = value);
        (_hoverUpBinding, _) = AddBindingRow(table, "Hover UP", "Left Shift", 3, "hoverUp", () => _settings.HoverUpBinding, value => _settings.HoverUpBinding = value);
        (_hoverDownBinding, _) = AddBindingRow(table, "Hover Down", "Left Ctrl", 4, "hoverDown", () => _settings.HoverDownBinding, value => _settings.HoverDownBinding = value);
        (_scoreBoardBinding, _) = AddBindingRow(table, "Score Board Mouse Fix", "Mouse to top-left", 5, "scoreBoard", () => _settings.ScoreBoardMouseFixBinding, value => _settings.ScoreBoardMouseFixBinding = value);

        Label scoreBoardHint = MakeLabel(T("Keybind.ScoreBoardHint"), 10.5f, FontStyle.Regular, IllustratedTheme.Gold);
        scoreBoardHint.SetBounds(32, 756, Math.Max(600, ClientSize.Width - 64), 52);
        scoreBoardHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        UiLanguage.Bind(scoreBoardHint, "Keybind.ScoreBoardHint", _languageCode);
        Controls.Add(scoreBoardHint);

        Panel advancedPanel = new()
        {
            BackColor = IllustratedTheme.Panel,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        advancedPanel.SetBounds(30, 820, Math.Max(600, ClientSize.Width - 60), 116);
        Controls.Add(advancedPanel);
        advancedPanel.Paint += (_, e) => IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(2, 2, advancedPanel.Width - 5, advancedPanel.Height - 5), true);

        Label advancedTitle = MakeLabel("ADVANCED SWITCH BINDINGS", 12.5f, FontStyle.Bold, IllustratedTheme.Gold);
        advancedTitle.SetBounds(20, 12, 330, 30);
        Label advancedDescription = MakeLabel("Create custom switch positions and map each position to a keyboard key or mouse button.", 10.5f, FontStyle.Regular, IllustratedTheme.Ivory);
        advancedDescription.SetBounds(20, 44, Math.Max(280, advancedPanel.Width - 500), 48);
        advancedDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _advancedSwitchToggle = new ThemeCheckBox { Visible = false, Checked = true, AutoSize = false };
        Label advancedToggleLabel = MakeLabel(T("Switch.SwitchesManagedInside"), 11f, FontStyle.Bold, IllustratedTheme.Ivory);
        UiLanguage.Bind(advancedToggleLabel, "Switch.SwitchesManagedInside", _languageCode);
        advancedToggleLabel.BackColor = Color.Transparent;
        advancedToggleLabel.SetBounds(Math.Max(360, advancedPanel.Width - 430), 17, 390, 36);
        advancedToggleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _manageSwitchesButton = MakeButton("MANAGE CUSTOM SWITCHES");
        _manageSwitchesButton.SetBounds(Math.Max(360, advancedPanel.Width - 430), 61, 390, 40);
        _manageSwitchesButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        advancedPanel.Controls.AddRange(new Control[] { advancedTitle, advancedDescription, advancedToggleLabel, _manageSwitchesButton });
        _manageSwitchesButton.Click += (_, _) =>
        {
            using AdvancedSwitchManagerForm manager = new(_advancedSwitchService, _languageCode) { Icon = Icon };
            manager.ShowDialog(this);
            _advancedSwitchService.SetEnabled(true);
        };
        _advancedSwitchService.StatusChanged += OnAdvancedStatusChanged;

        _status = MakeLabel(_settings.Enabled ? T("Keybind.EnabledStatus") : T("Keybind.OffStatus"), 11, FontStyle.Regular, ThemePalette.FromArgb(90, 238, 140));
        _status.SetBounds(32, 956, 1320, 34);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_status);

        Label notice = MakeLabel("Important: keep War Thunder VR Assistant running while using these bindings. If War Thunder runs as administrator, run this app as administrator too so the keyboard commands can reach the game.", 10.5f, FontStyle.Italic, ThemePalette.FromArgb(255, 190, 70));
        notice.SetBounds(32, 1000, 1320, 48);
        notice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(notice);
        AutoScroll = true;
        AutoScrollMinSize = new Size(0, 1060);

        _poll.Tick += (_, _) => PollBindings();
        _scoreBoardCorrection.Tick += (_, _) =>
        {
            _scoreBoardCorrection.Stop();
            if (_settings.Enabled) SetCursorPos(_scoreBoardTarget.X, _scoreBoardTarget.Y);
        };
        _poll.Start(); // Advanced switch mappings remain active even when this embedded page is hidden.
        FormClosed += (_, _) =>
        {
            _poll.Stop();
            _scoreBoardCorrection.Stop();
            _scoreBoardCorrection.Dispose();
            ReleaseHeldOutputs();
            _advancedSwitchService.StatusChanged -= OnAdvancedStatusChanged;
            SaveSettings();
            _advancedSwitchService.Dispose();
        };
        void LayoutForWindow()
        {
            if (IllustratedTheme.Enabled) return;
            int contentWidth = Math.Max(600, ClientSize.Width - 60);
            title.Width = Math.Max(580, contentWidth - 120);
            // Leave a clean right-side lane for the Info and Home icons.
            description.Width = Math.Min(1320, Math.Max(760, contentWidth - 180));
            _status.Width = contentWidth;
            notice.Width = contentWidth;
            scoreBoardHint.Width = contentWidth;
            table.Width = contentWidth;
            advancedPanel.Width = contentWidth;
            advancedDescription.Width = Math.Max(280, advancedPanel.Width - 500);
            LayoutRows(table);
        }

        Resize += (_, _) => LayoutForWindow();
        Shown += (_, _) => BeginInvoke((Action)LayoutForWindow);
        if (IllustratedTheme.Enabled)
        {
            table.Paint += (_,e) => IllustratedTheme.DrawFrame(e.Graphics,new Rectangle(2,2,table.Width-5,table.Height-5),false);
            info.Visible = home.Visible = false;
            foreach(Control child in Controls.Cast<Control>().Concat(table.Controls.Cast<Control>()))
                if(child is Label || child is ButtonBase) child.Font = new Font("Segoe UI",20,child.Font.Style,GraphicsUnit.Pixel);
            title.Font = new Font("Segoe UI",32,FontStyle.Bold,GraphicsUnit.Pixel);
            PictureBox headerIcon = new()
            {
                Image = AssetManager.LoadImage("IllustratedKeys.png"),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(30, 10, 88, 88)
            };
            title.SetBounds(136, 18, 760, 62);
            Controls.Add(headerIcon);
            headerIcon.BringToFront();

            Dictionary<Control, float> baseFonts = new();
            void CaptureFonts(Control parent)
            {
                foreach (Control child in parent.Controls)
                {
                    if (!baseFonts.ContainsKey(child)) baseFonts[child] = child.Font.Size;
                    if (child.HasChildren) CaptureFonts(child);
                }
            }
            CaptureFonts(this);
            void ScaleFonts()
            {
                float scale = Math.Max(.4f, Math.Min(ClientSize.Width / 1240f, ClientSize.Height / 880f));
                foreach ((Control control, float baseSize) in baseFonts.ToArray())
                {
                    if (control.IsDisposed) continue;
                    float target = baseSize * scale;
                    if (Math.Abs(control.Font.Size - target) < .05f) continue;
                    ResponsiveFonts.Set(control, target, control.Font.Style, control.Font.Unit);
                }
            }
            void LayoutTheme()
            {
                ScaleFonts();
                float scale = Math.Max(.4f, Math.Min(ClientSize.Width / 1240f, ClientSize.Height / 880f));
                int S(float v) => Math.Max(1, (int)Math.Round(v * scale));
                int margin = S(30), full = ClientSize.Width - margin * 2;
                headerIcon.SetBounds(margin, S(10), S(88), S(88));
                title.SetBounds(S(136), S(18), ClientSize.Width-S(166), S(70));
                description.SetBounds(margin,S(110),full,S(76));
                enabled.SetBounds(margin,S(192),full,S(40));
                table.SetBounds(margin,S(245),full,S(506));
                scoreBoardHint.SetBounds(margin,table.Bottom+S(8),full,S(54));
                int advancedY = scoreBoardHint.Bottom + S(14);
                advancedPanel.SetBounds(margin,advancedY,full,S(166));
                int split = (int)(full * .56f);
                int rightX = split + S(24);
                int rightW = Math.Max(1, full - rightX - S(20));
                advancedTitle.SetBounds(S(20), S(14), split-S(40), S(44));
                advancedDescription.SetBounds(S(20), S(62), split-S(40), S(90));
                int messageHeight = TextRenderer.MeasureText(advancedToggleLabel.Text, advancedToggleLabel.Font,
                    new Size(rightW, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + S(8);
                advancedToggleLabel.SetBounds(rightX, S(18), rightW, Math.Max(S(48), messageHeight));
                _manageSwitchesButton.SetBounds(rightX, Math.Max(S(92), advancedToggleLabel.Bottom + S(12)), rightW, S(48));
                advancedPanel.Height = Math.Max(S(166), _manageSwitchesButton.Bottom + S(16));
                _status.SetBounds(margin,advancedPanel.Bottom+S(10),full,S(44));
                notice.SetBounds(margin,_status.Bottom+S(8),full,S(70));
                AutoScrollMinSize = new Size(0, notice.Bottom + S(18));
                ScaleFonts();
                LayoutRows(table);
            }
            Resize+=(_,_)=>LayoutTheme(); LayoutTheme();
            advancedToggleLabel.TextChanged += (_, _) => LayoutTheme();
        }
        ApplyLanguage(_languageCode);
    }

    public void ApplyLanguage(string languageCode)
    {
        _languageCode = AppText.Normalize(languageCode);
        _advancedSwitchService.LanguageCode = _languageCode;
        UiLanguage.Apply(this, _languageCode, LocalizedTextKeys);
        _upBinding.Text = FormatBinding(_settings.VrHeadPositionUpBinding);
        _downBinding.Text = FormatBinding(_settings.VrHeadPositionDownBinding);
        _mapBinding.Text = FormatBinding(_settings.SwitchMapToBattlefieldBinding);
        _hoverUpBinding.Text = FormatBinding(_settings.HoverUpBinding);
        _hoverDownBinding.Text = FormatBinding(_settings.HoverDownBinding);
        _scoreBoardBinding.Text = FormatBinding(_settings.ScoreBoardMouseFixBinding);
        Text = T("Nav.Keybind");
        _status.Text = _settings.Enabled ? T("Keybind.EnabledStatus") : T("Keybind.OffStatus");
    }

    private void OnAdvancedStatusChanged(string text)
    {
        // Polling continues while this page is hidden and can report during the
        // manager's first handle creation. Never BeginInvoke onto a missing or
        // destroying handle; that race previously terminated the application.
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed && !Disposing) _status.Text = text;
            }));
        }
        catch (InvalidOperationException) { }
    }

    private (Button Bind, Button Clear) AddBindingRow(Panel table, string action, string key, int row, string id, Func<string> getBinding, Action<string> setBinding)
    {
        Label actionLabel = MakeLabel(action, 12.5f, FontStyle.Bold, Color.White);
        actionLabel.Name = "Action" + row;
        Label keyLabel = MakeLabel(key, 13, FontStyle.Bold, ThemePalette.FromArgb(255, 190, 70));
        keyLabel.Name = "Key" + row;
        Button bind = MakeButton(FormatBinding(getBinding()));
        bind.Name = "Bind" + row;
        bind.Click += (_, _) =>
        {
            string localizedAction = LocalizedTextKeys.TryGetValue(action, out string? actionKey) ? AppText.T(_languageCode, actionKey) : action;
            using HotasBindingDialog dialog = new(string.Format(System.Globalization.CultureInfo.CurrentCulture, T("Keybind.CapturePrompt"), localizedAction), _languageCode);
            if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Binding)) return;
            ReleaseBinding(id);
            setBinding(dialog.Binding);
            bind.Text = FormatBinding(dialog.Binding);
            SaveSettings();
        };
        Button clear = MakeButton("CLEAR");
        clear.Name = "Clear" + row;
        clear.Click += (_, _) =>
        {
            ReleaseBinding(id);
            setBinding("Not assigned");
            bind.Text = FormatBinding("Not assigned");
            SaveSettings();
        };
        table.Controls.AddRange(new Control[] { actionLabel, keyLabel, bind, clear });
        return (bind, clear);
    }

    private void LayoutRows(Panel table)
    {
        // Keep every cell inside the table at all supported window widths.
        // Older layouts used a hard 840 px design width, which could push the
        // CLEAR button outside the embedded viewport and make controls overlap.
        int width = Math.Max(1, table.ClientSize.Width);
        float uiScale = IllustratedTheme.Enabled
            ? Math.Max(.4f, Math.Min(ClientSize.Width / 1240f, ClientSize.Height / 880f))
            : 1f;
        int S(float value) => Math.Max(1, (int)Math.Round(value * uiScale));
        int margin = width < 720 ? S(14) : S(24);
        int gap = width < 720 ? S(7) : S(10);
        int clearW = width < 720 ? S(78) : S(94);
        int usable = Math.Max(360, width - margin * 2 - gap * 3 - clearW);
        int actionW = Math.Max(155, (int)Math.Round(usable * 0.34));
        int keyW = Math.Max(105, (int)Math.Round(usable * 0.25));
        int bindW = Math.Max(100, usable - actionW - keyW);

        // If minimums exceeded the real viewport, take space back proportionally
        // from the first two text columns rather than allowing horizontal overlap.
        int total = margin * 2 + gap * 3 + clearW + actionW + keyW + bindW;
        if (total > width)
        {
            int excess = total - width;
            int actionCut = Math.Min(excess / 2 + excess % 2, Math.Max(0, actionW - 120));
            actionW -= actionCut;
            excess -= actionCut;
            int keyCut = Math.Min(excess, Math.Max(0, keyW - 86));
            keyW -= keyCut;
            excess -= keyCut;
            bindW = Math.Max(80, bindW - excess);
        }

        int actionX = margin;
        int keyX = actionX + actionW + gap;
        int bindX = keyX + keyW + gap;
        int clearX = Math.Max(bindX + 80 + gap, width - margin - clearW);
        bindW = Math.Max(80, clearX - gap - bindX);

        table.Controls["ActionHeader"]!.SetBounds(actionX, S(8), actionW, S(46));
        table.Controls["KeyHeader"]!.SetBounds(keyX, S(8), keyW, S(46));
        table.Controls["HotasHeader"]!.SetBounds(bindX, S(8), bindW, S(46));
        foreach (string name in new[] { "ActionHeader", "KeyHeader", "HotasHeader" })
        {
            Control heading = table.Controls[name]!;
            float size = 11f;
            for (; size > 8f; size -= .5f)
            {
                using Font candidate = new("Segoe UI", size, FontStyle.Bold);
                Size measured = TextRenderer.MeasureText(heading.Text, candidate,
                    new Size(heading.Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                if (measured.Height <= heading.Height && measured.Width <= heading.Width) break;
            }
            ResponsiveFonts.Set(heading, size, FontStyle.Bold, GraphicsUnit.Point);
        }
        for (int row = 0; row < 6; row++)
        {
            int y = S(60 + row * 74);
            int h = S(44);
            table.Controls["Action" + row]!.SetBounds(actionX, y, actionW, S(66));
            table.Controls["Key" + row]!.SetBounds(keyX, y, keyW, h);
            table.Controls["Bind" + row]!.SetBounds(bindX, y, bindW, h);
            table.Controls["Clear" + row]!.SetBounds(clearX, y, clearW, h);
        }
    }

    private void PollBindings()
    {
        _advancedSwitchService.Poll();
        if (!_settings.Enabled)
        {
            if (_heldOutputs.Count > 0) ReleaseHeldOutputs();
            _wasPressed.Clear();
            return;
        }
        PollBinding("up", _settings.VrHeadPositionUpBinding, Keys.PageUp);
        PollBinding("down", _settings.VrHeadPositionDownBinding, Keys.PageDown);
        PollBinding("map", _settings.SwitchMapToBattlefieldBinding, Keys.N);
        PollHeldBinding("hoverUp", _settings.HoverUpBinding, Keys.LShiftKey);
        PollHeldBinding("hoverDown", _settings.HoverDownBinding, Keys.LControlKey);
        PollScoreBoardBinding();
    }

    private void PollBinding(string id, string binding, Keys output)
    {
        bool pressed = HotasBindingDialog.IsBindingPressed(binding);
        bool wasPressed = _wasPressed.TryGetValue(id, out bool previous) && previous;
        if (pressed && !wasPressed)
        {
            if (!SendKeyTap((int)output, out string? error))
                _status.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, T("Keybind.SendFailed"), FormatKeyboardCommand(output), error);
        }
        _wasPressed[id] = pressed;
    }

    private void PollHeldBinding(string id, string binding, Keys output)
    {
        bool pressed = HotasBindingDialog.IsBindingPressed(binding);
        if (pressed == _heldOutputs.Contains(id)) return;
        if (KeyboardTapSender.TrySetKeyState(output, pressed, _languageCode, out string? error))
        {
            if (pressed) _heldOutputs.Add(id);
            else _heldOutputs.Remove(id);
        }
        else
        {
            _status.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                T("Keybind.SendFailed"), FormatKeyboardCommand(output), error);
        }
    }

    private void PollScoreBoardBinding()
    {
        const string id = "scoreBoard";
        bool pressed = HotasBindingDialog.IsBindingPressed(_settings.ScoreBoardMouseFixBinding);
        bool wasPressed = _wasPressed.TryGetValue(id, out bool previous) && previous;
        if (pressed && !wasPressed)
        {
            // Use the monitor currently containing the pointer, which is normally
            // the monitor where War Thunder displays its scoreboard.
            _scoreBoardTarget = Screen.FromPoint(Cursor.Position).Bounds.Location;
            if (!SetCursorPos(_scoreBoardTarget.X, _scoreBoardTarget.Y))
            {
                int code = Marshal.GetLastWin32Error();
                _status.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    T("Keybind.SendFailed"), T("Keybind.ScoreBoardMouseFix"), new Win32Exception(code).Message);
            }
            else
            {
                // The game may center the cursor while opening the scoreboard.
                // Correct it once more just after the game's own move.
                _scoreBoardCorrection.Stop();
                _scoreBoardCorrection.Start();
            }
        }
        _wasPressed[id] = pressed;
    }

    private void ReleaseBinding(string id)
    {
        _wasPressed.Remove(id);
        Keys? output = id switch { "hoverUp" => Keys.LShiftKey, "hoverDown" => Keys.LControlKey, _ => null };
        if (output is Keys key && _heldOutputs.Contains(id))
        {
            if (KeyboardTapSender.TrySetKeyState(key, false, _languageCode, out _))
                _heldOutputs.Remove(id);
        }
        if (id == "scoreBoard") _scoreBoardCorrection.Stop();
    }

    private void ReleaseHeldOutputs()
    {
        ReleaseBinding("hoverUp");
        ReleaseBinding("hoverDown");
    }

    private bool SendKeyTap(int virtualKey, out string? error) =>
        KeyboardTapSender.TryTap((Keys)virtualKey, _languageCode, out error);

    private HiddenKeybindSettings LoadSettings()
    {
        foreach (string candidate in new[] { _settingsPath, _settingsPath + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                HiddenKeybindSettings? settings = JsonSerializer.Deserialize<HiddenKeybindSettings>(File.ReadAllText(candidate));
                if (settings is not null) return settings;
            }
            catch (JsonException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return new HiddenKeybindSettings();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            AtomicFile.WriteTextWithBackup(_settingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static Label MakeLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color,
        BackColor = Color.Transparent, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft
    };

    private static Button MakeButton(string text) => new ThemeButton()
    {
        Text = text, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.White,
        BackColor = ThemePalette.FromArgb(22, 43, 49), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, AutoEllipsis = false
    };

    private string FormatBinding(string binding) => string.IsNullOrWhiteSpace(binding) || binding == "Not assigned" ? AppText.T(_languageCode, "Assistant.BindInput") : HotasDirectInput.DisplayBinding(binding);
    private static string FormatKeyboardCommand(Keys key) => key switch
    {
        Keys.PageUp => "PGUP", Keys.PageDown => "PGDN",
        Keys.LShiftKey => "Left Shift", Keys.LControlKey => "Left Ctrl",
        _ => key.ToString().ToUpperInvariant()
    };
    private string T(string key) => AppText.T(_languageCode, key);
}
