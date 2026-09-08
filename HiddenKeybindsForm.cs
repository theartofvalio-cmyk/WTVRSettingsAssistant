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

    public event EventHandler? CloseRequested;
    public event EventHandler? AssistantStateChanged;
    public bool AssistantEnabled => _settings.Enabled;

    public HiddenKeybindsForm(string appFolder, Image homeImage, Image infoImage, Action showInfo)
    {
        _settingsPath = Path.Combine(appFolder, "Settings", "hidden_keybinds.json");
        _settings = LoadSettings();

        Text = "KeyBind Assistant";
        BackColor = Color.FromArgb(5, 12, 14);
        ForeColor = Color.FromArgb(235, 238, 240);
        Font = new Font("Segoe UI", 11f);
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = new Size(900, 620);

        Label title = MakeLabel("KEYBIND ASSISTANT", 25, FontStyle.Bold, Color.White);
        // The large title needs a tall bounds rectangle; otherwise WinForms clips its lower glyphs.
        title.SetBounds(30, 20, 1180, 78);
        Controls.Add(title);

        Label description = MakeLabel(
            "These actions are built into War Thunder, but are not shown in the game Controls menu.\n" +
            "Use this page to map a keyboard key, mouse button, HOTAS button, or combination to the command shown below.",
            12.5f, FontStyle.Regular, Color.FromArgb(200, 230, 235));
        // Keep the explanation well below the large heading; this page has ample vertical space.
        description.SetBounds(32, 125, 1320, 62);
        Controls.Add(description);

        CheckBox enabled = new()
        {
            Text = "Enable KeyBind Assistant",
            Checked = _settings.Enabled,
            AutoSize = false,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = BackColor
        };
        enabled.SetBounds(32, 194, 360, 38);
        enabled.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = enabled.Checked;
            _wasPressed.Clear();
            _status.Text = enabled.Checked
                ? "Enabled. Keyboard, mouse, and HOTAS bindings are active."
                : "KeyBind Assistant is off. Enable it above to activate assigned inputs.";
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

        Panel table = new() { BackColor = Color.FromArgb(9, 22, 25), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        table.SetBounds(30, 245, Math.Max(840, ClientSize.Width - 60), 282);
        Controls.Add(table);
        table.Resize += (_, _) => LayoutRows(table);

        Label actionHeader = MakeLabel("WAR THUNDER ACTION", 11, FontStyle.Bold, Color.FromArgb(65, 220, 245));
        Label keyHeader = MakeLabel("KEYBOARD COMMAND", 11, FontStyle.Bold, Color.FromArgb(65, 220, 245));
        Label hotasHeader = MakeLabel("YOUR INPUT BINDING", 11, FontStyle.Bold, Color.FromArgb(65, 220, 245));
        actionHeader.Name = "ActionHeader"; keyHeader.Name = "KeyHeader"; hotasHeader.Name = "HotasHeader";
        table.Controls.AddRange(new Control[] { actionHeader, keyHeader, hotasHeader });

        (_upBinding, Button upClear) = AddBindingRow(table, "VR Head Position Up", "Page Up  (PGUP)", 0, () => _settings.VrHeadPositionUpBinding, value => _settings.VrHeadPositionUpBinding = value);
        (_downBinding, Button downClear) = AddBindingRow(table, "VR Head Position Down", "Page Down  (PGDN)", 1, () => _settings.VrHeadPositionDownBinding, value => _settings.VrHeadPositionDownBinding = value);
        (_mapBinding, Button mapClear) = AddBindingRow(table, "Switch map to battlefield", "N", 2, () => _settings.SwitchMapToBattlefieldBinding, value => _settings.SwitchMapToBattlefieldBinding = value);

        _status = MakeLabel(_settings.Enabled
            ? "Enabled. Keyboard, mouse, and HOTAS bindings are active."
            : "KeyBind Assistant is off. Enable it above to activate assigned inputs.", 11, FontStyle.Regular, Color.FromArgb(90, 238, 140));
        _status.SetBounds(32, 550, 1320, 34);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_status);

        Label notice = MakeLabel("Important: keep WT VR Settings Assistant running while using these bindings. If War Thunder runs as administrator, run this app as administrator too so the keyboard commands can reach the game.", 10.5f, FontStyle.Italic, Color.FromArgb(255, 190, 70));
        notice.SetBounds(32, 596, 1320, 48);
        notice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(notice);

        _poll.Tick += (_, _) => PollBindings();
        Shown += (_, _) => _poll.Start();
        FormClosed += (_, _) => { _poll.Stop(); SaveSettings(); };
        void LayoutForWindow()
        {
            int contentWidth = Math.Max(840, ClientSize.Width - 60);
            title.Width = Math.Max(580, contentWidth - 120);
            // Leave a clean right-side lane for the Info and Home icons.
            description.Width = Math.Min(1320, Math.Max(760, contentWidth - 180));
            _status.Width = contentWidth;
            notice.Width = contentWidth;
            table.Width = contentWidth;
            LayoutRows(table);
        }

        Resize += (_, _) => LayoutForWindow();
        Shown += (_, _) => BeginInvoke((Action)LayoutForWindow);
    }

    private (Button Bind, Button Clear) AddBindingRow(Panel table, string action, string key, int row, Func<string> getBinding, Action<string> setBinding)
    {
        Label actionLabel = MakeLabel(action, 12.5f, FontStyle.Bold, Color.White);
        actionLabel.Name = "Action" + row;
        Label keyLabel = MakeLabel(key, 13, FontStyle.Bold, Color.FromArgb(255, 190, 70));
        keyLabel.Name = "Key" + row;
        Button bind = MakeButton(FormatBinding(getBinding()));
        bind.Name = "Bind" + row;
        bind.Click += (_, _) =>
        {
            using HotasBindingDialog dialog = new($"Press a keyboard key, mouse button, HOTAS button, or combination for {action}");
            if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Binding)) return;
            setBinding(dialog.Binding);
            bind.Text = FormatBinding(dialog.Binding);
            SaveSettings();
        };
        Button clear = MakeButton("CLEAR");
        clear.Name = "Clear" + row;
        clear.Click += (_, _) => { setBinding("Not assigned"); bind.Text = "BIND INPUT"; SaveSettings(); };
        table.Controls.AddRange(new Control[] { actionLabel, keyLabel, bind, clear });
        return (bind, clear);
    }

    private void LayoutRows(Panel table)
    {
        int width = table.ClientSize.Width;
        int actionX = 24, actionW = (int)(width * .35);
        int keyX = actionX + actionW + 12, keyW = (int)(width * .20);
        int bindX = keyX + keyW + 12, bindW = Math.Max(200, width - bindX - 118);
        int clearX = bindX + bindW + 10;
        table.Controls["ActionHeader"].SetBounds(actionX, 14, actionW, 28);
        table.Controls["KeyHeader"].SetBounds(keyX, 14, keyW, 28);
        table.Controls["HotasHeader"].SetBounds(bindX, 14, bindW, 28);
        for (int row = 0; row < 3; row++)
        {
            int y = 58 + row * 70;
            table.Controls["Action" + row].SetBounds(actionX, y, actionW, 40);
            table.Controls["Key" + row].SetBounds(keyX, y, keyW, 40);
            table.Controls["Bind" + row].SetBounds(bindX, y, bindW, 40);
            table.Controls["Clear" + row].SetBounds(clearX, y, 94, 40);
        }
    }

    private void PollBindings()
    {
        if (!_settings.Enabled)
        {
            _wasPressed.Clear();
            return;
        }
        PollBinding("up", _settings.VrHeadPositionUpBinding, Keys.PageUp);
        PollBinding("down", _settings.VrHeadPositionDownBinding, Keys.PageDown);
        PollBinding("map", _settings.SwitchMapToBattlefieldBinding, Keys.N);
    }

    private void PollBinding(string id, string binding, Keys output)
    {
        bool pressed = HotasBindingDialog.IsBindingPressed(binding);
        bool wasPressed = _wasPressed.TryGetValue(id, out bool previous) && previous;
        if (pressed && !wasPressed)
        {
            if (SendKeyTap((int)output, out string? error))
                _status.Text = $"Sent {FormatKeyboardCommand(output)} from {FormatBinding(binding)}.";
            else
                _status.Text = $"Could not send {FormatKeyboardCommand(output)}: {error}";
        }
        _wasPressed[id] = pressed;
    }

    private static bool SendKeyTap(int virtualKey, out string? error)
    {
        SyntheticKeyGuard.Suppress(virtualKey);
        ushort scanCode = (ushort)MapVirtualKey((uint)virtualKey, 0);
        uint flags = KeyEventScanCode;
        if (virtualKey is (int)Keys.PageUp or (int)Keys.PageDown) flags |= KeyEventExtendedKey;
        Input[] inputs =
        [
            new Input { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeybdInput { ScanCode = scanCode, Flags = flags } } },
            new Input { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeybdInput { ScanCode = scanCode, Flags = flags | KeyEventKeyUp } } }
        ];
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == inputs.Length)
        {
            error = null;
            return true;
        }

        int code = Marshal.GetLastWin32Error();
        error = code == 5
            ? "Windows blocked input injection. Run this app at the same administrator level as War Thunder."
            : new Win32Exception(code).Message;
        return false;
    }

    private HiddenKeybindSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath)) return JsonSerializer.Deserialize<HiddenKeybindSettings>(File.ReadAllText(_settingsPath)) ?? new HiddenKeybindSettings();
        }
        catch { }
        return new HiddenKeybindSettings();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static Label MakeLabel(string text, float size, FontStyle style, Color color) => new()
    {
        Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color,
        BackColor = Color.Transparent, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft
    };

    private static Button MakeButton(string text) => new()
    {
        Text = text, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.White,
        BackColor = Color.FromArgb(22, 43, 49), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
    };

    private static string FormatBinding(string binding) => string.IsNullOrWhiteSpace(binding) || binding == "Not assigned" ? "BIND INPUT" : binding;
    private static string FormatKeyboardCommand(Keys key) => key switch { Keys.PageUp => "PGUP", Keys.PageDown => "PGDN", _ => key.ToString().ToUpperInvariant() };
}
