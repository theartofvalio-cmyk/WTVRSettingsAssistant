using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using HOTASTrimUtility;

namespace WTVRSettingsAssistant;

internal enum SwitchTrigger { EnterState, LeaveState, WhileActive, EnterAndLeave }
internal enum SwitchOutputBehavior { Pulse, Hold, TogglePulseSameCommand, None }
internal enum SwitchStartupBehavior { IgnoreInitialPosition, WaitForFirstPhysicalMovement, ActivateCurrentPosition }
internal enum SwitchActionKind { VJoyButton, KeyboardKey, MouseButton }

internal sealed class AdvancedSwitchSettings
{
    public bool Enabled { get; set; }
    public int PulseDurationMs { get; set; } = 75;
    public int DebounceMs { get; set; } = 30;
    public SwitchStartupBehavior StartupBehavior { get; set; } = SwitchStartupBehavior.WaitForFirstPhysicalMovement;
    public List<SwitchDefinition> Switches { get; set; } = new();
}

internal sealed class SwitchDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Switch";
    public bool Enabled { get; set; } = true;
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = "Controller";
    public string SwitchType { get; set; } = "2-State";
    public bool ReversePositions { get; set; }
    public int PhysicalButton { get; set; }
    public int OnActionButton { get; set; }
    public int OffActionButton { get; set; }
    public SwitchActionKind OnActionKind { get; set; } = SwitchActionKind.VJoyButton;
    public SwitchActionKind OffActionKind { get; set; } = SwitchActionKind.VJoyButton;
    public string OnActionKey { get; set; } = string.Empty;
    public string OffActionKey { get; set; } = string.Empty;
    public List<SwitchStateDefinition> States { get; set; } = new();
    public AdvancedFlapsSettings AdvancedFlaps { get; set; } = new();
}

internal sealed class SwitchStateDefinition
{
    public List<SwitchMacroStep> Sequence { get; set; } = new();
    public List<SwitchGestureStep> Gesture { get; set; } = new();
    public string FlapRole { get; set; } = "None";
    public string Name { get; set; } = "STATE";
    public List<InputCondition> Conditions { get; set; } = new();
    public int OutputButton { get; set; } = 1;
    public SwitchActionKind OutputKind { get; set; } = SwitchActionKind.VJoyButton;
    public string OutputKey { get; set; } = string.Empty;
    public SwitchTrigger Trigger { get; set; } = SwitchTrigger.EnterState;
    public SwitchOutputBehavior Behavior { get; set; } = SwitchOutputBehavior.Pulse;
}

internal sealed class InputCondition
{
    public int ButtonId { get; set; }
    public bool RequiredState { get; set; }
}

internal sealed record PhysicalJoystick(int Id, string Name, uint ButtonMask, bool Connected)
{
    public bool Button(int oneBasedButton) => oneBasedButton is >= 1 and <= 32 && (ButtonMask & (1u << (oneBasedButton - 1))) != 0;
}

internal static class WinMmJoystickReader
{
    private const uint JoyReturnButtons = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort Mid, Pid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ProductName;
        public uint XMin, XMax, YMin, YMax, ZMin, ZMax, NumButtons, PeriodMin, PeriodMax, RMin, RMax, UMin, UMax, VMin, VMax, Caps, MaxAxes, NumAxes, MaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string OemVxd;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public int Size;
        public uint Flags;
        public int X, Y, Z, R, U, V;
        public uint Buttons;
        public uint ButtonNumber;
        public int Pov;
        public uint Reserved1, Reserved2;
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern int joyGetDevCapsW(int id, ref JoyCaps caps, int size);
    [DllImport("winmm.dll")] private static extern int joyGetPosEx(int id, ref JoyInfoEx info);

    public static List<PhysicalJoystick> Enumerate()
    {
        List<PhysicalJoystick> result = new();
        for (int id = 0; id < 16; id++)
        {
            JoyCaps caps = new();
            bool haveCaps = joyGetDevCapsW(id, ref caps, Marshal.SizeOf<JoyCaps>()) == 0;
            if (haveCaps && (IsVirtualOutputJoystick(caps) ||
                (XInputController.AnyConnected && LooksLikeXInputName(caps.ProductName))))
                continue;

            JoyInfoEx info = new() { Size = Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnButtons };
            if (joyGetPosEx(id, ref info) != 0) continue;
            string name = haveCaps && !string.IsNullOrWhiteSpace(caps.ProductName)
                ? caps.ProductName.Trim() : $"Joystick {id + 1}";
            result.Add(new PhysicalJoystick(id, name, info.Buttons, true));
        }
        for (int index = 0; index < 4; index++)
        {
            if (!XInputController.TryGetButtonMask(index, out uint mask)) continue;
            result.Add(new PhysicalJoystick(1000 + index, $"Xbox / XInput Controller {index + 1}", mask, true));
        }
        return result;
    }

    public static PhysicalJoystick? Read(int id, string fallbackName)
    {
        if (id is >= 1000 and <= 1003)
        {
            int index = id - 1000;
            return XInputController.TryGetButtonMask(index, out uint xmask)
                ? new PhysicalJoystick(id, $"Xbox / XInput Controller {index + 1}", xmask, true)
                : null;
        }

        JoyInfoEx info = new() { Size = Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnButtons };
        if (joyGetPosEx(id, ref info) != 0) return null;
        JoyCaps caps = new();
        bool haveCaps = joyGetDevCapsW(id, ref caps, Marshal.SizeOf<JoyCaps>()) == 0;
        if ((haveCaps && IsVirtualOutputJoystick(caps)) || IsVirtualOutputJoystickName(fallbackName))
            return null;

        string name = haveCaps && !string.IsNullOrWhiteSpace(caps.ProductName)
            ? caps.ProductName.Trim() : fallbackName;
        return new PhysicalJoystick(id, name, info.Buttons, true);
    }

    private static bool LooksLikeXInputName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name.Contains("xbox", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("xinput", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("gamepad for windows", StringComparison.OrdinalIgnoreCase));

    private static bool IsVirtualOutputJoystick(JoyCaps caps) =>
        IsVirtualOutputJoystickName(caps.ProductName) ||
        IsVirtualOutputJoystickName(caps.RegKey);

    private static bool IsVirtualOutputJoystickName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Contains("vJoy", StringComparison.OrdinalIgnoreCase);
}


internal interface IInputOutput : IDisposable
{
    string Name { get; }
    bool IsConnected { get; }
    void ButtonDown(int id);
    void ButtonUp(int id);
    void Pulse(int id, int durationMs);
    void ReleaseAll();
}


internal static class KeyboardSwitchOutput
{
    public static void Tap(Keys key, string languageCode) => KeyboardTapSender.Tap(key, languageCode);
}

internal sealed class VJoySwitchOutput : IInputOutput
{
    private bool _leased;
    private readonly HashSet<int> _held = new();
    private readonly ButtonPulseQueue _pulses;
    public VJoySwitchOutput()
    {
        _pulses = new((id, down) =>
        {
            if (down) SharedVJoyOutputCoordinator.ButtonDown(id);
            else if (!_held.Contains(id)) SharedVJoyOutputCoordinator.ButtonUp(id);
        });
    }
    public string Name => "vJoy Device 1";
    public bool IsConnected => _leased && SharedVJoyOutputCoordinator.IsConnected;

    private void EnsureConnected()
    {
        if (_leased) return;
        SharedVJoyOutputCoordinator.AcquireButtonClient();
        _leased = true;
    }

    public void ButtonDown(int id)
    {
        EnsureConnected();
        SharedVJoyOutputCoordinator.ButtonDown(id);
        _held.Add(id);
    }

    public void ButtonUp(int id)
    {
        if (!_leased) return;
        SharedVJoyOutputCoordinator.ButtonUp(id);
        _held.Remove(id);
    }

    public void Pulse(int id, int durationMs)
    {
        // Keep acquisition/button-down synchronous so vJoy connection errors are
        // caught by the switch engine instead of escaping from an async-void call.
        EnsureConnected();
        _pulses.Pulse(id, durationMs);
    }

    public void ReleaseAll()
    {
        if (!_leased) return;
        foreach (int id in _held.ToArray()) SharedVJoyOutputCoordinator.ButtonUp(id);
        _held.Clear();
        _pulses.Cancel();
        SharedVJoyOutputCoordinator.ReleaseAllButtons();
    }

    public void Dispose()
    {
        ReleaseAll();
        if (!_leased) return;
        _leased = false;
        SharedVJoyOutputCoordinator.ReleaseButtonClient();
    }
}

internal sealed partial class AdvancedSwitchService : IDisposable
{
    private sealed class Runtime
    {
        public int? ActiveIndex;
        public int? CandidateIndex;
        public DateTime CandidateSince;
        public uint? InitialMask;
        public uint? PreviousMask;
        public bool InitialHandled;
        public bool Armed;
        public FlapKeySequence? FlapKeys;
        public SwitchMacroRuntime? Macro;
    }

    private readonly string _path;
    private readonly Dictionary<string, Runtime> _runtime = new();
    private IInputOutput _output;
    private bool _disposed;
    private readonly Func<SwitchDefinition, PhysicalJoystick?>? _reader;
    private readonly Func<DateTime> _utcNow;
    private readonly Action<Keys, string> _keyboardTap;
    public AdvancedSwitchSettings Settings { get; }
    public event Action<string>? StatusChanged;
    public string LastStatus { get; private set; } = AppText.T("en", "Switch.Ready");
    public string LanguageCode { get; set; } = "en";

    public AdvancedSwitchService(string settingsPath) : this(settingsPath, new VJoySwitchOutput(), null, () => DateTime.UtcNow) { }

    internal AdvancedSwitchService(string settingsPath, IInputOutput output,
        Func<SwitchDefinition, PhysicalJoystick?>? reader, Func<DateTime> utcNow, Action<Keys, string>? keyboardTap = null)
    {
        _path = settingsPath;
        Settings = Load(settingsPath);
        Settings.Switches ??= new List<SwitchDefinition>();
        foreach (SwitchDefinition definition in Settings.Switches)
        {
            definition.States ??= new List<SwitchStateDefinition>();
            definition.AdvancedFlaps ??= new AdvancedFlapsSettings();
            foreach (SwitchStateDefinition state in definition.States)
            {
                state.Sequence ??= new List<SwitchMacroStep>();
                state.Gesture ??= new List<SwitchGestureStep>();
                state.Conditions ??= new List<InputCondition>();
            }
        }
        Settings.PulseDurationMs = Math.Clamp(Settings.PulseDurationMs, 50, 150);
        Settings.DebounceMs = Math.Clamp(Settings.DebounceMs, 20, 50);
        _output = output;
        _reader = reader;
        _utcNow = utcNow;
        _keyboardTap = keyboardTap ?? KeyboardSwitchOutput.Tap;
        foreach (var definition in Settings.Switches) definition.AdvancedFlaps ??= new();
        foreach (var definition in Settings.Switches.Where(d => d.AdvancedFlaps.Enabled))
        {
            definition.AdvancedFlaps.Enabled = false;
            definition.Enabled = false;
            LastStatus = AppText.T(LanguageCode, "Switch.LegacyFlapsRemoved");
        }
        MigrateLegacyOutputButtons();
    }

    private void MigrateLegacyOutputButtons()
    {
        // vJoy.Wrapper 1.0.0.5 exposes its normal button state through a 32-bit
        // mask. Older WT Assistant builds allowed VB33..VB128 in the editor,
        // which could never produce distinct bindable buttons through this backend.
        HashSet<int> used = Settings.Switches.SelectMany(x => x.States)
            .Select(x => x.OutputButton).Where(x => x is >= 1 and <= 32).ToHashSet();
        Dictionary<int, int> remap = new();
        bool changed = false;
        foreach (SwitchStateDefinition state in Settings.Switches.SelectMany(x => x.States))
        {
            if (state.OutputButton is >= 1 and <= 32) continue;
            int old = state.OutputButton;
            if (!remap.TryGetValue(old, out int replacement))
            {
                replacement = Enumerable.Range(1, 32).FirstOrDefault(x => !used.Contains(x));
                if (replacement == 0) replacement = 1;
                remap[old] = replacement;
                used.Add(replacement);
            }
            state.OutputButton = replacement;
            changed = true;
        }
        if (changed)
        {
            LastStatus = AppText.T(LanguageCode, "Switch.LegacyMigrated");
            try { Save(); } catch { }
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        AtomicFile.WriteTextWithBackup(_path, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void SetEnabled(bool enabled)
    {
        Settings.Enabled = enabled;
        if (!enabled) { ReleaseAll(); _runtime.Clear(); }
        Save();
    }

    public void ResetRuntime()
    {
        ReleaseAll();
        _runtime.Clear();
    }

    public void Poll()
    {
        if (_disposed || !Settings.Enabled) return;
        foreach (SwitchDefinition definition in Settings.Switches)
        {
            if (!definition.Enabled) { ReleaseHeldFor(definition); continue; }
            PhysicalJoystick? device = ResolveDevice(definition);
            if (device is null)
            {
                ReleaseHeldFor(definition);
                SetStatus(string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.DeviceStatus"), definition.Name, AppText.T(LanguageCode, "Switch.DeviceNotConnected")));
                continue;
            }

            Runtime runtime = GetRuntime(definition.Id);
            if (definition.States.Any(s => s.Sequence.Count > 0 || s.Gesture.Count > 0))
            {
                PollCustomActions(definition, device, runtime);
                continue;
            }
            if (runtime.FlapKeys is not null)
                _runtime[definition.Id] = runtime = new Runtime();
            uint mask = device.ButtonMask;
            runtime.InitialMask ??= mask;
            runtime.PreviousMask ??= mask;
            bool moved = runtime.PreviousMask.Value != mask;
            runtime.PreviousMask = mask;

            int? detected = DetectState(definition, device);
            if (!runtime.InitialHandled)
            {
                runtime.InitialHandled = true;
                runtime.ActiveIndex = detected;
                runtime.CandidateIndex = detected;
                runtime.CandidateSince = _utcNow();
                runtime.Armed = Settings.StartupBehavior != SwitchStartupBehavior.WaitForFirstPhysicalMovement;
                if (Settings.StartupBehavior == SwitchStartupBehavior.ActivateCurrentPosition && detected.HasValue)
                    Enter(definition, detected.Value, "startup");
                continue;
            }

            if (!runtime.Armed && moved)
                runtime.Armed = true;
            if (!runtime.Armed) continue;

            if (runtime.CandidateIndex != detected)
            {
                runtime.CandidateIndex = detected;
                runtime.CandidateSince = _utcNow();
                continue;
            }
            if ((_utcNow() - runtime.CandidateSince).TotalMilliseconds < Settings.DebounceMs) continue;
            if (runtime.ActiveIndex == detected)
            {
                if (detected.HasValue) WhileActive(definition, detected.Value);
                continue;
            }

            int? previous = runtime.ActiveIndex;
            if (previous.HasValue) Leave(definition, previous.Value, detected);
            runtime.ActiveIndex = detected;
            if (detected.HasValue) Enter(definition, detected.Value, previous?.ToString() ?? "none");
        }
    }

    public string GetCurrentState(SwitchDefinition definition, out uint mask, out bool connected)
    {
        PhysicalJoystick? device = ResolveDevice(definition);
        connected = device is not null;
        mask = device?.ButtonMask ?? 0;
        if (device is null) return AppText.T(LanguageCode, "Switch.DeviceNotConnected");
        bool custom = definition.States.Any(s => s.Sequence.Count > 0 || s.Gesture.Count > 0);
        int? index = custom
            ? (_runtime.TryGetValue(definition.Id, out Runtime? runtime) ? runtime.Macro?.ActiveRow : null)
            : DetectState(definition, device);
        return index.HasValue && index.Value < definition.States.Count ? definition.States[index.Value].Name : AppText.T(LanguageCode, "Switch.NoMatchingState");
    }


    public void TestPulse(int button)
    {
        if (button is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(button));
        _output.Pulse(button, Settings.PulseDurationMs);
        SetStatus(string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.TestOutputStatus"), button, Settings.PulseDurationMs));
    }

    private PhysicalJoystick? ResolveDevice(SwitchDefinition definition)
    {
        if (_reader is not null) return _reader(definition);
        PhysicalJoystick? direct = WinMmJoystickReader.Read(definition.DeviceId, definition.DeviceName);
        if (direct is not null && string.Equals(direct.Name, definition.DeviceName, StringComparison.OrdinalIgnoreCase)) return direct;
        PhysicalJoystick? byName = WinMmJoystickReader.Enumerate().FirstOrDefault(d => string.Equals(d.Name, definition.DeviceName, StringComparison.OrdinalIgnoreCase));
        if (byName is not null && byName.Id != definition.DeviceId)
        {
            definition.DeviceId = byName.Id;
            try { Save(); } catch { }
        }
        // A re-enumerated slot can belong to an entirely different controller.
        return byName;
    }
    private int? DetectState(SwitchDefinition definition, PhysicalJoystick device)
    {
        for (int i = 0; i < definition.States.Count; i++)
        {
            SwitchStateDefinition state = definition.States[i];
            if (state.Conditions.Count == 0) continue;
            if (state.Conditions.All(c => device.Button(c.ButtonId) == c.RequiredState))
            {
                if (definition.ReversePositions && definition.States.Count == 2) return 1 - i;
                return i;
            }
        }
        return null;
    }

    private Runtime GetRuntime(string id)
    {
        if (!_runtime.TryGetValue(id, out Runtime? runtime)) _runtime[id] = runtime = new Runtime();
        return runtime;
    }

    private void Enter(SwitchDefinition d, int index, string from)
    {
        if (index < 0 || index >= d.States.Count) return;
        SwitchStateDefinition state = d.States[index];
        if (state.Trigger is SwitchTrigger.EnterState or SwitchTrigger.EnterAndLeave || state.Behavior is SwitchOutputBehavior.Hold or SwitchOutputBehavior.TogglePulseSameCommand)
            Execute(d, state, true);
        SetStatus(string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.TransitionStatus"), d.Name, from, state.Name, DescribeAction(state, true)));
    }

    private void Leave(SwitchDefinition d, int index, int? next)
    {
        if (index < 0 || index >= d.States.Count) return;
        SwitchStateDefinition state = d.States[index];
        if (state.Behavior == SwitchOutputBehavior.Hold) _output.ButtonUp(state.OutputButton);
        bool nextPulsesSame = next is int n && n < d.States.Count &&
            d.States[n].OutputButton == state.OutputButton &&
            (d.States[n].Behavior == SwitchOutputBehavior.TogglePulseSameCommand ||
             (d.States[n].Behavior == SwitchOutputBehavior.Pulse &&
              d.States[n].Trigger is SwitchTrigger.EnterState or SwitchTrigger.EnterAndLeave));
        if (state.Behavior == SwitchOutputBehavior.TogglePulseSameCommand ? !nextPulsesSame :
            state.Trigger is SwitchTrigger.LeaveState or SwitchTrigger.EnterAndLeave)
            Execute(d, state, false);
        SetStatus(string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.LeaveStatus"), d.Name, state.Name, next.HasValue && next.Value < d.States.Count ? d.States[next.Value].Name : AppText.T(LanguageCode, "Switch.NoneState")));
    }

    private void WhileActive(SwitchDefinition d, int index)
    {
        SwitchStateDefinition state = d.States[index];
        if (state.Trigger != SwitchTrigger.WhileActive || state.Behavior != SwitchOutputBehavior.Hold) return;
        try { _output.ButtonDown(state.OutputButton); } catch (Exception ex) { SetStatus(string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.OutputError"), ex.Message)); }
    }

    private void Execute(SwitchDefinition d, SwitchStateDefinition state, bool entering)
    {
        if (state.Behavior == SwitchOutputBehavior.None) return;
        try
        {
            switch (state.Behavior)
            {
                case SwitchOutputBehavior.Pulse:
                case SwitchOutputBehavior.TogglePulseSameCommand:
                    ExecutePulse(state);
                    break;
                case SwitchOutputBehavior.Hold:
                    if (state.OutputKind == SwitchActionKind.KeyboardKey)
                    {
                        if (entering) ExecutePulse(state);
                    }
                    else if (entering) _output.ButtonDown(state.OutputButton); else _output.ButtonUp(state.OutputButton);
                    break;
            }
        }
        catch (Exception ex) { SetStatus(string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.OutputUnavailable"), d.Name, ex.Message)); }
    }

    private void ExecutePulse(SwitchStateDefinition state)
    {
        if (state.OutputKind == SwitchActionKind.KeyboardKey)
        {
            if (!Enum.TryParse(state.OutputKey, true, out Keys key)) throw new InvalidOperationException("Invalid keyboard key: " + state.OutputKey);
            _keyboardTap(key, LanguageCode);
        }
        else if (state.OutputKind == SwitchActionKind.MouseButton)
        {
            KeyboardTapSender.ClickMouse(state.OutputKey, LanguageCode);
        }
        else
        {
            _output.Pulse(state.OutputButton, Settings.PulseDurationMs);
        }
    }

    private string DescribeOutput(SwitchStateDefinition state) => state.OutputKind switch
    {
        SwitchActionKind.KeyboardKey => string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.TapKey"), FormatKeyName(state.OutputKey)),
        SwitchActionKind.MouseButton => string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.ClickMouse"), FormatKeyName(state.OutputKey)),
        _ => string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.PulseVB"), state.OutputButton)
    };

    private static string FormatKeyName(string key) => string.IsNullOrWhiteSpace(key) ? "-" : key.ToUpperInvariant();

    private string DescribeAction(SwitchStateDefinition state, bool entering) => state.Behavior switch
    {
        SwitchOutputBehavior.None => AppText.T(LanguageCode, "Switch.NoOutput"),
        SwitchOutputBehavior.Hold => entering
            ? string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.HoldVB"), state.OutputButton)
            : string.Format(CultureInfo.CurrentCulture, AppText.T(LanguageCode, "Switch.ReleaseVB"), state.OutputButton),
        _ => DescribeOutput(state)
    };

    private void ReleaseHeldFor(SwitchDefinition d)
    {
        foreach (SwitchStateDefinition state in d.States.Where(s => s.Behavior == SwitchOutputBehavior.Hold && s.OutputKind == SwitchActionKind.VJoyButton)) _output.ButtonUp(state.OutputButton);
        _runtime.Remove(d.Id);
    }

    public void ReleaseAll()
    {
        try { _output.ReleaseAll(); } catch { }
    }

    private void SetStatus(string text)
    {
        LastStatus = text;
        StatusChanged?.Invoke(text);
    }

    private static AdvancedSwitchSettings Load(string path)
    {
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                AdvancedSwitchSettings? settings = JsonSerializer.Deserialize<AdvancedSwitchSettings>(File.ReadAllText(candidate));
                if (settings is not null) return settings;
            }
            catch (JsonException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return new();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flapTelemetry?.Dispose();
        ReleaseAll();
        _output.Dispose();
    }
}

internal sealed class AdvancedSwitchManagerForm : Form
{
    private float _lastVisualScale;
    private Font? _cellFont;
    private Font? _headerFont;
    private readonly AdvancedSwitchService _service;
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();
    private readonly string _languageCode;

    public AdvancedSwitchManagerForm(AdvancedSwitchService service) : this(service, "en") { }

    public AdvancedSwitchManagerForm(AdvancedSwitchService service, string languageCode)
    {
        _service = service;
        _languageCode = AppText.Normalize(languageCode);
        Text = T("Switch.AdvancedBindings");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 760);
        Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(
            Math.Clamp((int)(workArea.Width * .72f), 1220, 1760),
            Math.Clamp((int)(workArea.Height * .78f), 800, 1160));
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = IllustratedTheme.Background;
        ForeColor = IllustratedTheme.Ivory;
        Font = new Font("Segoe UI", 17f, FontStyle.Regular, GraphicsUnit.Pixel);
        HandleCreated += (_, _) => IllustratedTheme.ApplyWindowChrome(this);

        Label title = L("ADVANCED SWITCH BINDINGS", 24, FontStyle.Bold); title.SetBounds(24, 16, 760, 48);
        Label hint = L("HOW IT WORKS:  1) Click ADD and add one row for each switch position.  2) Detect each row\'s physical condition.  3) Record the keyboard key or mouse button to tap when that position becomes active.", 11.5f, FontStyle.Regular);
        hint.SetBounds(26, 64, 1160, 74); hint.AutoEllipsis = false;
        Button add = B("ADD"); add.SetBounds(24, 148, 150, 46);
        Button edit = B("EDIT"); edit.SetBounds(184, 148, 120, 46);
        Button toggle = B("ENABLE / DISABLE"); toggle.SetBounds(314, 148, 190, 46);
        Button delete = B("DELETE"); delete.SetBounds(514, 148, 120, 46);
        Button test = B("TEST"); test.SetBounds(644, 148, 110, 46);
        Button options = B("OPTIONS"); options.SetBounds(764, 148, 130, 46);
        Button close = B("CLOSE"); close.SetBounds(904, 148, 100, 46);
        foreach (Button b in new[] { add, edit, toggle, delete, test, options, close }) b.Anchor = AnchorStyles.Top;

        _grid.SetBounds(24, 212, 1172, 488); _grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _grid.BackgroundColor = Color.FromArgb(28, 31, 32); _grid.BorderStyle = BorderStyle.None; _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.RowHeadersVisible = false; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.MultiSelect = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(33, 36, 37); _grid.DefaultCellStyle.ForeColor = IllustratedTheme.Ivory; _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(91, 74, 42); _grid.DefaultCellStyle.SelectionForeColor = Color.White; _grid.RowTemplate.Height = 44; _grid.ColumnHeadersHeight = 46;
        _cellFont = new Font("Segoe UI", 17f, FontStyle.Regular, GraphicsUnit.Pixel);
        _headerFont = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        _grid.DefaultCellStyle.Font = _cellFont; _grid.ColumnHeadersDefaultCellStyle.Font = _headerFont;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 27, 28); _grid.ColumnHeadersDefaultCellStyle.ForeColor = IllustratedTheme.Gold; _grid.EnableHeadersVisualStyles = false;
        _grid.Columns.Add("Name", "SWITCH"); _grid.Columns.Add("Device", "DEVICE"); _grid.Columns.Add("Physical", "PHYSICAL BUTTON"); _grid.Columns.Add("Actions", "ACTIONS"); _grid.Columns.Add("Enabled", "STATUS");
        ApplyLanguage();

        _status.SetBounds(24, 716, 1172, 42); _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; _status.ForeColor = Color.FromArgb(112, 225, 151); _status.AutoEllipsis = false;
        Controls.AddRange(new Control[] { title, hint, add, edit, toggle, delete, test, options, close, _grid, _status });
        Paint += (_, e) => IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(8, 8, ClientSize.Width - 17, ClientSize.Height - 17), false);
        _service.StatusChanged += OnStatus;
        FormClosed += (_, _) => _service.StatusChanged -= OnStatus;
        FormClosed += (_, _) => { _cellFont?.Dispose(); _headerFont?.Dispose(); };

        add.Click += (_, _) => AddDetectedSwitch();
        edit.Click += (_, _) => { if (Selected() is { } d) EditSwitch(d); };
        toggle.Click += (_, _) => { if (Selected() is { } d) { d.Enabled = !d.Enabled; _service.ResetRuntime(); _service.Save(); RefreshRows(); } };
        delete.Click += (_, _) => { if (Selected() is { } d && MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, T("Switch.DeletePrompt"), d.Name), T("Switch.AdvancedBindings"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { d.Enabled = false; _service.ResetRuntime(); _service.Settings.Switches.Remove(d); _service.Save(); RefreshRows(); } };
        test.Click += (_, _) => { if (Selected() is { } d) using (var f = new SwitchTestForm(_service, d, _languageCode) { Icon = Icon }) f.ShowDialog(this); };
        options.Click += (_, _) => ShowOptions();
        close.Click += (_, _) => Close();
        _grid.CellDoubleClick += (_, _) => { if (Selected() is { } d) EditSwitch(d); };

        void LayoutManager()
        {
            int w = Math.Max(1, ClientSize.Width);
            int h = Math.Max(1, ClientSize.Height);
            float scale = Math.Clamp(Math.Min(w / 1220F, h / 800F), 1F, 1.45F);
            int S(float value) => Math.Max(1, (int)Math.Round(value * scale));
            if (Math.Abs(scale - _lastVisualScale) > .02f)
            {
                _lastVisualScale = scale;
                ResponsiveFonts.Set(title, 32f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                ResponsiveFonts.Set(hint, 18f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
                ResponsiveFonts.Set(_status, 18f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
                foreach (Button button in new[] { add, edit, toggle, delete, test, options, close })
                    ResponsiveFonts.Set(button, 18f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                Font cell = new("Segoe UI", 17f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
                Font header = new("Segoe UI", 17f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                Font? oldCell = _cellFont, oldHeader = _headerFont;
                _grid.DefaultCellStyle.Font = cell; _grid.ColumnHeadersDefaultCellStyle.Font = header;
                _cellFont = cell; _headerFont = header;
                oldCell?.Dispose(); oldHeader?.Dispose();
            }
            int margin = S(24);
            int full = Math.Max(S(600), w - margin * 2);
            title.SetBounds(margin, S(16), Math.Min(full, S(760)), S(48));
            hint.SetBounds(margin, S(64), full, S(74));

            Button[] actionButtons = { add, edit, toggle, delete, test, options, close };
            int[] baseWidths = { 150, 120, 190, 120, 110, 130, 100 };
            int gap = S(10);
            int total = baseWidths.Sum(x => S(x)) + gap * (actionButtons.Length - 1);
            int x = margin;
            if (total > full)
            {
                int each = Math.Max(S(105), (full - gap * (actionButtons.Length - 1)) / actionButtons.Length);
                for (int i = 0; i < actionButtons.Length; i++)
                {
                    actionButtons[i].SetBounds(x, S(148), each, S(46));
                    x += each + gap;
                }
            }
            else
            {
                for (int i = 0; i < actionButtons.Length; i++)
                {
                    int bw = S(baseWidths[i]);
                    actionButtons[i].SetBounds(x, S(148), bw, S(46));
                    x += bw + gap;
                }
            }

            int statusHeight = S(42);
            _status.SetBounds(margin, h - S(84), full, statusHeight);
            int gridTop = S(212);
            _grid.SetBounds(margin, gridTop, full, Math.Max(S(260), _status.Top - S(16) - gridTop));
            _grid.RowTemplate.Height = S(44);
            foreach (DataGridViewRow row in _grid.Rows) row.Height = S(44);
            _grid.ColumnHeadersHeight = S(46);
        }
        Resize += (_, _) => LayoutManager();
        Shown += (_, _) => LayoutManager();
        LayoutManager();
        RefreshRows();
    }

    private void AddDetectedSwitch()
    {
        using var pause = _service.SuspendFlaps();
        if (!CaptureButtonChange(T("Switch.AddSwitch"), T("Switch.AddPrompt"), out PhysicalJoystick? detectedDevice, out int detectedButton, out _) || detectedDevice is null)
            return;
        int output = detectedButton;
        SwitchDefinition definition = CreateActionSwitch($"VBtn{output} Toggle", detectedDevice, detectedButton, output, output, true);
        using SwitchEditorForm editor = new(definition, _languageCode) { Icon = Icon };
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            if (definition.Enabled && definition.AdvancedFlaps.Enabled && _service.Settings.Switches.Any(d => d.Enabled && d.AdvancedFlaps.Enabled))
            { MessageBox.Show(this, T("Flaps.OneController"), T("Flaps.Title")); return; }
            _service.Settings.Switches.Add(definition);
            _service.Save();
            _service.ResetRuntime();
            RefreshRows();
        }
    }

    internal static bool CaptureButtonChange(IWin32Window owner, string title, string prompt, string languageCode, out PhysicalJoystick? detectedDevice, out int detectedButton, out bool pressed)
    {
        detectedDevice = null;
        detectedButton = 0;
        pressed = false;
        List<PhysicalJoystick> baseline = WinMmJoystickReader.Enumerate();
        if (baseline.Count == 0)
        {
            MessageBox.Show(owner, AppText.T(languageCode, "Switch.NoJoystick"), title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        using Form learn = new()
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(680, 250),
            BackColor = IllustratedTheme.Background,
            ForeColor = IllustratedTheme.Ivory,
            Font = new Font("Segoe UI", 13f),
            AutoScaleMode = AutoScaleMode.Dpi
        };
        Label message = L(prompt, 13.5f, FontStyle.Bold);
        message.SetBounds(30, 26, 620, 128);
        message.TextAlign = ContentAlignment.MiddleCenter;
        Label live = L(AppText.T(languageCode, "Switch.LiveWaiting"), 11.5f, FontStyle.Regular);
        live.SetBounds(30, 152, 620, 30);
        live.ForeColor = Color.FromArgb(112, 225, 151);
        Button cancel = B(AppText.T(languageCode, "Options.Cancel"));
        cancel.SetBounds(250, 196, 180, 42);
        cancel.Click += (_, _) => learn.Close();
        learn.Controls.AddRange(new Control[] { message, live, cancel });

        var timer = new System.Windows.Forms.Timer { Interval = 25 };
        PhysicalJoystick? foundDevice = null;
        int foundButton = 0;
        bool foundPressed = false;
        timer.Tick += (_, _) =>
        {
            foreach (PhysicalJoystick before in baseline)
            {
                PhysicalJoystick? now = WinMmJoystickReader.Read(before.Id, before.Name);
                if (now is null) continue;
                uint changed = before.ButtonMask ^ now.ButtonMask;
                if (changed == 0) continue;
                for (int bit = 0; bit < 32; bit++)
                {
                    if ((changed & (1u << bit)) == 0) continue;
                    foundDevice = now;
                    foundButton = bit + 1;
                    foundPressed = (now.ButtonMask & (1u << bit)) != 0;
                    live.Text = $"B{foundButton}={(foundPressed ? "ON" : "OFF")}";
                    timer.Stop();
                    learn.DialogResult = DialogResult.OK;
                    return;
                }
            }
        };
        learn.Shown += (_, _) => timer.Start();
        learn.FormClosed += (_, _) => { timer.Stop(); timer.Dispose(); };
        bool ok = learn.ShowDialog(owner) == DialogResult.OK && foundDevice is not null && foundButton > 0;
        detectedDevice = foundDevice;
        detectedButton = foundButton;
        pressed = foundPressed;
        return ok;
    }

    private bool CaptureButtonChange(string title, string prompt, out PhysicalJoystick? detectedDevice, out int detectedButton, out bool pressed) =>
        CaptureButtonChange(this, title, prompt, _languageCode, out detectedDevice, out detectedButton, out pressed);

    internal static SwitchDefinition CreateActionSwitch(string name, PhysicalJoystick device, int physicalButton, int onActionButton, int offActionButton, bool enabled)
    {
        physicalButton = Math.Clamp(physicalButton, 1, 32);
        onActionButton = Math.Clamp(onActionButton, 1, 32);
        offActionButton = Math.Clamp(offActionButton, 1, 32);
        return new SwitchDefinition
        {
            Name = name,
            DeviceId = device.Id,
            DeviceName = device.Name,
            SwitchType = "PhysicalAction",
            Enabled = enabled,
            PhysicalButton = physicalButton,
            OnActionButton = onActionButton,
            OffActionButton = offActionButton,
            OnActionKind = SwitchActionKind.KeyboardKey,
            OffActionKind = SwitchActionKind.KeyboardKey,
            ReversePositions = false,
            States = new()
            {
                new SwitchStateDefinition { Name = "ON", Conditions = new() { new InputCondition { ButtonId = physicalButton, RequiredState = true } }, OutputButton = 1, OutputKind = SwitchActionKind.KeyboardKey, OutputKey = string.Empty, Trigger = SwitchTrigger.EnterState, Behavior = SwitchOutputBehavior.Pulse },
                new SwitchStateDefinition { Name = "OFF", Conditions = new() { new InputCondition { ButtonId = physicalButton, RequiredState = false } }, OutputButton = 1, OutputKind = SwitchActionKind.KeyboardKey, OutputKey = string.Empty, Trigger = SwitchTrigger.EnterState, Behavior = SwitchOutputBehavior.Pulse }
            }
        };
    }

    private int NextFreeOutputButton()
    {
        HashSet<int> used = _service.Settings.Switches.SelectMany(x => x.States).Select(x => x.OutputButton).ToHashSet();
        for (int i=1;i<=32;i++) if (!used.Contains(i)) return i;
        return 1;
    }

    private void OnStatus(string text)
    {
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


    internal static int InferPhysicalButton(SwitchDefinition d)
    {
        if (d.PhysicalButton is >= 1 and <= 32) return d.PhysicalButton;
        return d.States.SelectMany(s => s.Conditions).FirstOrDefault(c => c.RequiredState)?.ButtonId
            ?? d.States.SelectMany(s => s.Conditions).FirstOrDefault()?.ButtonId
            ?? 0;
    }

    internal static int InferActionButton(SwitchDefinition d, bool on)
    {
        int stored = on ? d.OnActionButton : d.OffActionButton;
        if (stored is >= 1 and <= 32) return stored;
        SwitchStateDefinition? state = FindActionState(d, on);
        if (state?.OutputButton is >= 1 and <= 32) return state.OutputButton;
        return 1;
    }

    internal static SwitchActionKind InferActionKind(SwitchDefinition d, bool on)
    {
        SwitchActionKind stored = on ? d.OnActionKind : d.OffActionKind;
        SwitchStateDefinition? state = FindActionState(d, on);
        string key = on ? d.OnActionKey : d.OffActionKey;
        if (!string.IsNullOrWhiteSpace(key))
            return key.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase) ? SwitchActionKind.MouseButton : SwitchActionKind.KeyboardKey;
        return state?.OutputKind ?? stored;
    }

    internal static string InferActionKey(SwitchDefinition d, bool on)
    {
        string stored = on ? d.OnActionKey : d.OffActionKey;
        if (!string.IsNullOrWhiteSpace(stored)) return stored;
        return FindActionState(d, on)?.OutputKey ?? string.Empty;
    }

    internal static SwitchStateDefinition? FindActionState(SwitchDefinition d, bool on) =>
        d.States.FirstOrDefault(s => s.Name.Equals(on ? "ON" : "OFF", StringComparison.OrdinalIgnoreCase))
        ?? d.States.FirstOrDefault(s => s.Conditions.Any(c => c.RequiredState == on));

    internal static string FormatAction(SwitchDefinition d, bool on)
    {
        SwitchActionKind kind = InferActionKind(d, on);
        string value = InferActionKey(d, on);
        if (kind == SwitchActionKind.KeyboardKey) return "KEY " + value.ToUpperInvariant();
        if (kind == SwitchActionKind.MouseButton) return "MOUSE " + value.ToUpperInvariant();
        return "LEGACY VB" + InferActionButton(d, on).ToString(CultureInfo.InvariantCulture);
    }

    internal static string FormatAction(SwitchStateDefinition state)
    {
        if (state.OutputKind == SwitchActionKind.KeyboardKey) return "KEY " + state.OutputKey.ToUpperInvariant();
        if (state.OutputKind == SwitchActionKind.MouseButton) return "MOUSE " + state.OutputKey.Replace("Mouse", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return "LEGACY VB" + state.OutputButton.ToString(CultureInfo.InvariantCulture);
    }

    private SwitchDefinition? Selected() => _grid.SelectedRows.Count == 0 ? null : _grid.SelectedRows[0].Tag as SwitchDefinition;
    private void RefreshRows()
    {
        _grid.Rows.Clear();
        foreach (SwitchDefinition d in _service.Settings.Switches)
        {
            int physical = InferPhysicalButton(d);
            string physicalText = physical > 0 ? $"B{physical}" : "-";
            string actions = string.Join("  |  ", d.States.Select(s => $"{s.Name} -> {FormatAction(s)}"));
            if (d.AdvancedFlaps.Enabled) actions = T("Flaps.PositionGuide");
            int row = _grid.Rows.Add(d.Name, d.DeviceName, physicalText, actions, d.Enabled ? T("Switch.Enabled") : T("Switch.Disabled"));
            _grid.Rows[row].Tag = d;
        }
        _status.Text = _service.LastStatus;
    }

    private void EditSwitch(SwitchDefinition? existing)
    {
        using var pause = _service.SuspendFlaps();
        SwitchDefinition working = existing is null ? new SwitchDefinition() : Clone(existing);
        using var editor = new SwitchEditorForm(working, _languageCode);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        if (working.Enabled && working.AdvancedFlaps.Enabled && _service.Settings.Switches.Any(d =>
            d != existing && d.Enabled && d.AdvancedFlaps.Enabled))
        { MessageBox.Show(this, T("Flaps.OneController"), T("Flaps.Title")); return; }
        if (existing is null) _service.Settings.Switches.Add(working);
        else
        {
            int index = _service.Settings.Switches.IndexOf(existing);
            if (index >= 0) _service.Settings.Switches[index] = working;
        }
        _service.ResetRuntime(); _service.Save(); RefreshRows();
    }

    private void ShowOptions()
    {
        using Form f = new() { Text = T("Switch.OptionsTitle"), StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(520, 280), BackColor = IllustratedTheme.Background, ForeColor = IllustratedTheme.Ivory, Font = Font, AutoScaleMode = AutoScaleMode.Dpi };
        Label p = L(T("Switch.PulseDuration"), 11, FontStyle.Bold); p.SetBounds(24, 28, 280, 30);
        NumericUpDown pulse = new() { Minimum = 50, Maximum = 150, Value = _service.Settings.PulseDurationMs, Bounds = new Rectangle(320, 28, 150, 32) };
        Label d = L(T("Switch.Debounce"), 11, FontStyle.Bold); d.SetBounds(24, 82, 280, 30);
        NumericUpDown debounce = new() { Minimum = 20, Maximum = 50, Value = _service.Settings.DebounceMs, Bounds = new Rectangle(320, 82, 150, 32) };
        Label s = L(T("Switch.StartupHandling"), 11, FontStyle.Bold); s.SetBounds(24, 136, 280, 30);
        ComboBox startup = new() { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(260, 136, 210, 34) }; startup.Items.AddRange(Enum.GetNames<SwitchStartupBehavior>()); startup.SelectedItem = _service.Settings.StartupBehavior.ToString();
        Button ok = B(T("Switch.Save")); ok.SetBounds(330, 210, 140, 40); ok.Click += (_, _) => { _service.Settings.PulseDurationMs = (int)pulse.Value; _service.Settings.DebounceMs = (int)debounce.Value; _service.Settings.StartupBehavior = Enum.Parse<SwitchStartupBehavior>(startup.SelectedItem!.ToString()!); _service.ResetRuntime(); _service.Save(); f.DialogResult = DialogResult.OK; };
        f.Controls.AddRange(new Control[] { p, pulse, d, debounce, s, startup, ok }); f.ShowDialog(this);
    }

    private static SwitchDefinition Clone(SwitchDefinition source) => JsonSerializer.Deserialize<SwitchDefinition>(JsonSerializer.Serialize(source))!;
    private void ApplyLanguage()
    {
        Text = AppText.T(_languageCode, "Switch.AdvancedBindings");
        UiLanguage.Apply(this, _languageCode, new Dictionary<string, string>
        {
            ["Advanced Switch Bindings"] = "Switch.AdvancedBindings",
            ["ADVANCED SWITCH BINDINGS"] = "Switch.AdvancedBindings",
            ["HOW IT WORKS:  1) Click ADD and add one row for each switch position.  2) Detect each row\'s physical condition.  3) Record the keyboard key or mouse button to tap when that position becomes active."] = "Switch.ManagerHint",
            ["SWITCH"] = "Switch.SwitchColumn",
            ["DEVICE"] = "Switch.DeviceColumn",
            ["PHYSICAL BUTTON"] = "Switch.PhysicalButtonColumn",
            ["ACTIONS"] = "Switch.ActionsColumn",
            ["STATUS"] = "Switch.StatusColumn",
            ["ADD"] = "Switch.Add",
            ["EDIT"] = "Switch.Edit",
            ["ENABLE / DISABLE"] = "Switch.EnableDisable",
            ["DELETE"] = "Switch.Delete",
            ["TEST"] = "Switch.Test",
            ["OPTIONS"] = "Nav.Options",
            ["CLOSE"] = "Switch.Close",
        });
    }
    private string T(string key) => AppText.T(_languageCode, key);
    internal static Label L(string text, float size, FontStyle style) => new() { Text = text, Font = new Font("Segoe UI", size * 4f / 3f, style, GraphicsUnit.Pixel), ForeColor = IllustratedTheme.Ivory, BackColor = Color.Transparent, AutoSize = false };
    internal static Button B(string text) => new ThemeButton { Text = text, Font = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel), ForeColor = IllustratedTheme.Ivory, BackColor = Color.FromArgb(37, 40, 41), FlatStyle = FlatStyle.Flat, AutoEllipsis = false };
}

internal sealed partial class SwitchEditorForm : Form
{
    private float _lastVisualScale;
    private Font? _cellFont;
    private Font? _headerFont;
    private readonly SwitchDefinition _definition;
    private readonly string _languageCode;
    private readonly TextBox _name = new();
    private readonly ComboBox _device = new();
    private readonly CheckBox _enabled = new();
    private readonly DataGridView _states = new();
    private readonly Label _live = new();
    private readonly System.Windows.Forms.Timer _liveTimer = new() { Interval = 80 };
    private List<PhysicalJoystick> _devices = new();
    private uint? _lastLiveMask;

    public SwitchEditorForm(SwitchDefinition definition) : this(definition, "en") { }

    public SwitchEditorForm(SwitchDefinition definition, string languageCode)
    {
        _definition = definition;
        _languageCode = AppText.Normalize(languageCode);
        Text = T("Switch.Builder");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 780);
        Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(
            Math.Clamp((int)(workArea.Width * .72f), 1220, 1760),
            Math.Clamp((int)(workArea.Height * .78f), 820, 1180));
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = IllustratedTheme.Background;
        ForeColor = IllustratedTheme.Ivory;
        Font = new Font("Segoe UI", 17f, FontStyle.Regular, GraphicsUnit.Pixel);
        HandleCreated += (_, _) => IllustratedTheme.ApplyWindowChrome(this);

        Label title = AdvancedSwitchManagerForm.L(T("Switch.Builder").ToUpperInvariant(), 22, FontStyle.Bold);
        title.SetBounds(24, 18, 760, 44);
        Label guide = AdvancedSwitchManagerForm.L(T("Switch.MultiBuilderGuide"), 12.5f, FontStyle.Regular);
        guide.SetBounds(24, 66, 1160, 72);
        guide.AutoEllipsis = false;

        Label nameL = AdvancedSwitchManagerForm.L(T("Switch.Name"), 12, FontStyle.Bold);
        nameL.SetBounds(24, 148, 130, 32);
        _name.SetBounds(170, 146, 330, 36);
        _name.Text = definition.Name;
        _name.BackColor = IllustratedTheme.Panel;
        _name.ForeColor = IllustratedTheme.Ivory;

        Label devL = AdvancedSwitchManagerForm.L(T("Switch.DetectedDevice"), 12, FontStyle.Bold);
        devL.SetBounds(530, 148, 170, 32);
        _device.SetBounds(704, 146, 330, 36);
        _device.DropDownStyle = ComboBoxStyle.DropDownList;
        _device.BackColor = IllustratedTheme.Panel;
        _device.ForeColor = IllustratedTheme.Ivory;
        _device.Enabled = false;

        _enabled.Text = T("Switch.Enabled");
        _enabled.Checked = definition.Enabled;
        _enabled.SetBounds(1050, 146, 150, 36);
        _enabled.ForeColor = IllustratedTheme.Ivory;
        _enabled.BackColor = Color.Transparent;
        _enabled.Font = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Pixel);

        _states.SetBounds(24, 210, 1172, 360);
        _states.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _states.BackgroundColor = Color.FromArgb(28, 31, 32);
        _states.BorderStyle = BorderStyle.None;
        _states.RowHeadersVisible = false;
        _states.AllowUserToAddRows = false;
        _states.AllowUserToDeleteRows = false;
        _states.MultiSelect = false;
        _states.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _states.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _states.DefaultCellStyle.BackColor = Color.FromArgb(33, 36, 37);
        _states.DefaultCellStyle.ForeColor = IllustratedTheme.Ivory;
        _states.DefaultCellStyle.SelectionBackColor = Color.FromArgb(92, 79, 53);
        _states.DefaultCellStyle.SelectionForeColor = Color.White;
        _cellFont = new Font("Segoe UI", 17f, FontStyle.Regular, GraphicsUnit.Pixel);
        _headerFont = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        _states.DefaultCellStyle.Font = _cellFont;
        _states.ColumnHeadersDefaultCellStyle.Font = _headerFont;
        _states.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 27, 28);
        _states.ColumnHeadersDefaultCellStyle.ForeColor = IllustratedTheme.Gold;
        _states.EnableHeadersVisualStyles = false;
        _states.RowTemplate.Height = 50;
        _states.ColumnHeadersHeight = 48;
        _states.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = T("Switch.State"), FillWeight = 18 });
        _states.Columns.Add(new DataGridViewTextBoxColumn { Name = "Conditions", HeaderText = T("Switch.PhysicalCondition"), FillWeight = 34 });
        var kindColumn = new DataGridViewComboBoxColumn { Name = "Kind", HeaderText = T("Switch.ActionType"), FillWeight = 15, FlatStyle = FlatStyle.Flat };
        kindColumn.Items.AddRange("KEY", "MOUSE");
        _states.Columns.Add(kindColumn);
        _states.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = T("Switch.ActionOutput"), FillWeight = 18 });
        _states.Columns.Add(new DataGridViewTextBoxColumn { Name = "Summary", HeaderText = T("Switch.ActionSummary"), FillWeight = 24, ReadOnly = true });

        _live.SetBounds(24, 584, 1172, 44);
        _live.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _live.ForeColor = Color.FromArgb(112, 225, 151);
        _live.Font = new Font("Segoe UI", 16f, FontStyle.Regular, GraphicsUnit.Pixel);
        _live.BackColor = Color.Transparent;

        Button addState = AdvancedSwitchManagerForm.B(T("Switch.AddPosition"));
        Button removeState = AdvancedSwitchManagerForm.B(T("Switch.RemovePosition"));
        Button detectState = AdvancedSwitchManagerForm.B(T("Switch.DetectPosition"));
        Button recordKey = AdvancedSwitchManagerForm.B(T("Switch.RecordKey"));
        Button recordMouse = AdvancedSwitchManagerForm.B(T("Switch.RecordMouse"));
        Button save = AdvancedSwitchManagerForm.B(T("Switch.SaveClose"));
        Button cancel = AdvancedSwitchManagerForm.B(T("Options.Cancel"));
        Button[] buttons = { addState, removeState, detectState, recordKey, recordMouse, save, cancel };
        foreach (Button b in buttons) b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

        Controls.AddRange(new Control[] { title, guide, nameL, _name, devL, _device, _enabled, _states, _live, addState, removeState, detectState, recordKey, recordMouse, save, cancel });
        Paint += (_, e) => IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(8, 8, ClientSize.Width - 17, ClientSize.Height - 17), false);

        RefreshDevices();
        LoadRows();
        InitializeFlapsEditor(guide);
        UpdateRowSummaries();

        addState.Click += (_, _) => AddPositionRow(string.Format(CultureInfo.CurrentCulture, T("Switch.PositionName"), _states.Rows.Count + 1), string.Empty, SwitchActionKind.KeyboardKey, "");
        removeState.Click += (_, _) => { if (_states.CurrentRow is { } row && _states.Rows.Count > 1) _states.Rows.Remove(row); UpdateRowSummaries(); };
        detectState.Click += (_, _) => { if (_flapsToggle.Checked) LearnAllPositions(); else LearnSelectedPosition(); };
        _flapsToggle.CheckedChanged += (_, _) => detectState.Text = T(_flapsToggle.Checked ? "Switch.LearnPositions" : "Switch.DetectPosition");
        detectState.Text = T(_flapsToggle.Checked ? "Switch.LearnPositions" : "Switch.DetectPosition");
        _flapsToggle.CheckedChanged += (_, _) => recordKey.Enabled = recordMouse.Enabled = !_flapsToggle.Checked;
        recordKey.Enabled = recordMouse.Enabled = !_flapsToggle.Checked;
        recordKey.Click += (_, _) => { if (_states.CurrentRow?.Tag is SwitchStateDefinition s && s.Sequence.Count > 0) EditSequence(); else RecordSelectedKey(); };
        recordMouse.Click += (_, _) =>
        {
            if (_states.CurrentRow?.Tag is SwitchStateDefinition s && s.Sequence.Count > 0)
            { MessageBox.Show(this, T("Switch.SequenceKeyboardOnly"), T("Switch.SequenceTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            RecordSelectedMouse();
        };
        save.Click += (_, _) => { if (Commit()) DialogResult = DialogResult.OK; };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        _states.CellValueChanged += (_, _) => UpdateRowSummaries();
        _states.CurrentCellDirtyStateChanged += (_, _) => { if (_states.IsCurrentCellDirty) _states.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _states.DataError += (_, _) => { };

        Resize += (_, _) => LayoutEditor(title, guide, nameL, devL, buttons);
        Shown += (_, _) => { LayoutEditor(title, guide, nameL, devL, buttons); _liveTimer.Start(); };
        FormClosed += (_, _) => _liveTimer.Stop();
        _liveTimer.Tick += (_, _) => UpdateLiveInput();
        FormClosed += (_, _) => { _cellFont?.Dispose(); _headerFont?.Dispose(); };
    }

    private void LayoutEditor(Label title, Label guide, Label nameL, Label devL, Button[] buttons)
    {
        int w = Math.Max(1120, ClientSize.Width);
        int h = Math.Max(780, ClientSize.Height);
        float scale = Math.Clamp(Math.Min(w / 1220f, h / 820f), 1f, 1.45f);
        int S(float value) => (int)Math.Round(value * scale);
        if (Math.Abs(scale - _lastVisualScale) > .02f)
        {
            _lastVisualScale = scale;
            ResponsiveFonts.Set(title, 29f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(guide, 18f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(nameL, 17f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(devL, 17f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(_name, 17f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(_device, 17f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(_enabled, 18f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(_live, 18f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(_recordAdvanced, 18f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            ResponsiveFonts.Set(_flapsSetup, 18f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            foreach (Button button in buttons)
                ResponsiveFonts.Set(button, 18f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            Font cell = new("Segoe UI", 17f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            Font header = new("Segoe UI", 17f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            Font? oldCell = _cellFont, oldHeader = _headerFont;
            _states.DefaultCellStyle.Font = cell;
            _states.ColumnHeadersDefaultCellStyle.Font = header;
            _cellFont = cell; _headerFont = header;
            oldCell?.Dispose(); oldHeader?.Dispose();
        }
        int margin = S(24);
        int full = w - margin * 2;
        title.SetBounds(margin, S(18), Math.Min(S(760), full), S(44));
        guide.SetBounds(margin, S(66), full, S(72));
        nameL.SetBounds(margin, S(148), S(130), S(38));
        _name.SetBounds(S(170), S(146), Math.Min(S(330), full / 3), S(38));
        devL.SetBounds(Math.Max(S(520), _name.Right + S(28)), S(148), S(170), S(38));
        _device.SetBounds(devL.Right + S(12), S(146), Math.Max(S(280), w - devL.Right - S(210)), S(38));
        _enabled.SetBounds(w - S(158), S(146), S(132), S(38));
        int buttonY = h - S(86);
        _live.SetBounds(margin, buttonY - S(56), full, S(44));
        _recordAdvanced.SetBounds(margin, S(188), S(210), S(38));
        _flapsSetup.SetBounds(margin + S(225), S(188), S(220), S(38));
        int gridTop = S(238);
        _states.SetBounds(margin, gridTop, full, Math.Max(S(240), _live.Top - S(18) - gridTop));
        _states.RowTemplate.Height = S(50);
        foreach (DataGridViewRow row in _states.Rows) row.Height = S(50);
        _states.ColumnHeadersHeight = S(48);
        int x = margin;
        int gap = S(8);
        int buttonWidth = (full - (buttons.Length - 1) * gap) / buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].SetBounds(x, buttonY, buttonWidth, S(48));
            x += buttonWidth + gap;
        }
    }

    private void RefreshDevices()
    {
        _devices = WinMmJoystickReader.Enumerate();
        _device.Items.Clear();
        foreach (PhysicalJoystick d in _devices) _device.Items.Add($"{d.Id + 1}: {d.Name}");
        int selected = _devices.FindIndex(d => d.Id == _definition.DeviceId && (string.Equals(d.Name, _definition.DeviceName, StringComparison.OrdinalIgnoreCase) || _devices.Count == 1));
        if (selected < 0 && _devices.Count > 0) selected = 0;
        if (selected >= 0) _device.SelectedIndex = selected;
    }

    private void SetDevice(PhysicalJoystick? device)
    {
        if (device is null) return;
        int index = _devices.FindIndex(d => d.Id == device.Id && string.Equals(d.Name, device.Name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _devices.Add(device);
            _device.Items.Add($"{device.Id + 1}: {device.Name}");
            index = _devices.Count - 1;
        }
        _device.SelectedIndex = index;
    }

    private void LoadRows()
    {
        if (_definition.States.Count == 0)
        {
            int physical = Math.Max(1, _definition.PhysicalButton);
            AddPositionRow("ON", $"{physical}=ON", SwitchActionKind.KeyboardKey, string.Empty);
            AddPositionRow("OFF", $"{physical}=OFF", SwitchActionKind.KeyboardKey, string.Empty);
            return;
        }
        foreach (SwitchStateDefinition state in _definition.States)
        {
            SwitchActionKind kind = !string.IsNullOrWhiteSpace(state.OutputKey)
                ? (state.OutputKey.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase) ? SwitchActionKind.MouseButton : SwitchActionKind.KeyboardKey)
                : (state.OutputKind == SwitchActionKind.VJoyButton ? SwitchActionKind.KeyboardKey : state.OutputKind);
            string action = kind == SwitchActionKind.KeyboardKey || kind == SwitchActionKind.MouseButton ? state.OutputKey : string.Empty;
            AddPositionRow(state.Name, FormatConditions(state.Conditions), kind, action);
            _states.Rows[_states.Rows.Count - 1].Tag = JsonSerializer.Deserialize<SwitchStateDefinition>(JsonSerializer.Serialize(state));
        }
    }

    private void AddPositionRow(string name, string conditions, SwitchActionKind kind, string action)
    {
        int row = _states.Rows.Add(name, conditions, kind == SwitchActionKind.MouseButton ? "MOUSE" : "KEY", action, string.Empty);
        _states.Rows[row].Cells["Summary"].Value = SummarizeAction(kind, action);
    }

    private void LearnSelectedPosition()
    {
        if (_states.CurrentRow is not { } row || _device.SelectedIndex < 0) return;
        if (!AdvancedSwitchManagerForm.CaptureButtonChange(this, T("Switch.DetectPosition"), T("Switch.CapturePositionPrompt"), _languageCode, out PhysicalJoystick? device, out int button, out bool pressed)) return;
        SetDevice(device);
        row.Cells["Conditions"].Value = $"{button}={(pressed ? "ON" : "OFF")}";
        UpdateRowSummaries();
    }

    private void LearnAllPositions()
    {
        if (_device.SelectedIndex < 0 || _device.SelectedIndex >= _devices.Count)
        {
            MessageBox.Show(this, T("Switch.ConnectSelect"), T("Switch.Learn"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        List<DataGridViewRow> rows = _states.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
        if (rows.Count == 0) return;
        if (_flapsToggle.Checked)
        {
            LearnFlapPositions(rows);
            return;
        }
        List<uint> masks = new();
        foreach (DataGridViewRow row in rows)
        {
            string position = Convert.ToString(row.Cells["State"].Value) ?? T("Switch.PositionFallback");
            if (!CaptureMask(position, out uint mask)) return;
            masks.Add(mask);
        }
        uint varying = 0;
        foreach (uint mask in masks.Skip(1)) varying |= masks[0] ^ mask;
        if (varying == 0)
        {
            MessageBox.Show(this, T("Switch.NoDifferences"), T("Switch.Learn"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        for (int r = 0; r < rows.Count; r++)
        {
            List<InputCondition> conditions = new();
            for (int bit = 0; bit < 32; bit++)
                if ((varying & (1u << bit)) != 0)
                    conditions.Add(new InputCondition { ButtonId = bit + 1, RequiredState = (masks[r] & (1u << bit)) != 0 });
            rows[r].Cells["Conditions"].Value = FormatConditions(conditions);
        }
        UpdateRowSummaries();
    }

    private bool CaptureMask(string position, out uint mask)
    {
        mask = 0;
        PhysicalJoystick deviceInfo = _devices[_device.SelectedIndex];
        using Form f = new() { Text = T("Switch.Learn"), StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(620, 250), BackColor = IllustratedTheme.Background, ForeColor = IllustratedTheme.Ivory, Font = Font, AutoScaleMode = AutoScaleMode.Dpi };
        Label l = AdvancedSwitchManagerForm.L(string.Format(CultureInfo.CurrentCulture, T("Switch.CapturePrompt"), position), 12.5f, FontStyle.Bold);
        l.SetBounds(30, 24, 560, 140);
        l.TextAlign = ContentAlignment.MiddleCenter;
        Button capture = AdvancedSwitchManagerForm.B(T("Switch.Capture"));
        capture.SetBounds(215, 184, 190, 44);
        uint captured = 0;
        bool ok = false;
        capture.Click += (_, _) =>
        {
            PhysicalJoystick? d = WinMmJoystickReader.Read(deviceInfo.Id, deviceInfo.Name);
            if (d is null)
            {
                MessageBox.Show(f, T("Switch.DeviceNotConnected"), T("Switch.Learn"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            captured = d.ButtonMask;
            ok = true;
            f.DialogResult = DialogResult.OK;
        };
        f.Controls.AddRange(new Control[] { l, capture });
        f.ShowDialog(this);
        mask = captured;
        return ok;
    }

    private void RecordSelectedMouse()
    {
        if (_states.CurrentRow is not { } row) return;
        if (!CaptureMouseButton(T("Switch.RecordMouse"), out string mouseButton)) return;
        row.Cells["Kind"].Value = "MOUSE";
        row.Cells["Action"].Value = mouseButton;
        UpdateRowSummaries();
    }

    private bool CaptureMouseButton(string title, out string mouseButton)
    {
        mouseButton = string.Empty;
        string captured = string.Empty;
        using Form f = new()
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 220),
            BackColor = IllustratedTheme.Background,
            ForeColor = IllustratedTheme.Ivory,
            Font = new Font("Segoe UI", 13f),
            AutoScaleMode = AutoScaleMode.Dpi
        };
        Label prompt = AdvancedSwitchManagerForm.L(T("Switch.CaptureMousePrompt"), 13, FontStyle.Bold);
        prompt.SetBounds(28, 26, 504, 110);
        prompt.TextAlign = ContentAlignment.MiddleCenter;
        Button cancel = AdvancedSwitchManagerForm.B(T("Options.Cancel"));
        cancel.SetBounds(190, 154, 180, 42);
        cancel.Click += (_, _) => f.Close();
        f.MouseDown += (_, e) =>
        {
            captured = e.Button switch
            {
                MouseButtons.Left => "MouseLeft",
                MouseButtons.Right => "MouseRight",
                MouseButtons.Middle => "MouseMiddle",
                MouseButtons.XButton1 => "MouseX1",
                MouseButtons.XButton2 => "MouseX2",
                _ => string.Empty
            };
            if (!string.IsNullOrWhiteSpace(captured)) f.DialogResult = DialogResult.OK;
        };
        f.Controls.AddRange(new Control[] { prompt, cancel });
        bool ok = f.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(captured);
        mouseButton = captured;
        return ok;
    }

    private void RecordSelectedKey()
    {
        if (_states.CurrentRow is not { } row) return;
        if (!CaptureKey(this, T("Switch.RecordKey"), _languageCode, out Keys key)) return;
        row.Cells["Kind"].Value = "KEY";
        row.Cells["Action"].Value = key.ToString();
        UpdateRowSummaries();
    }

    internal static bool CaptureKey(IWin32Window owner, string title, string languageCode, out Keys key)
    {
        string T(string id) => AppText.T(languageCode, id);
        key = Keys.None;
        Keys capturedKey = Keys.None;
        using Form f = new()
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 220),
            BackColor = IllustratedTheme.Background,
            ForeColor = IllustratedTheme.Ivory,
            Font = new Font("Segoe UI", 13f),
            KeyPreview = true,
            AutoScaleMode = AutoScaleMode.Dpi
        };
        Label prompt = AdvancedSwitchManagerForm.L(T("Switch.CaptureKeyPrompt"), 13, FontStyle.Bold);
        prompt.SetBounds(28, 26, 504, 110);
        prompt.TextAlign = ContentAlignment.MiddleCenter;
        Button cancel = AdvancedSwitchManagerForm.B(T("Options.Cancel"));
        cancel.SetBounds(190, 154, 180, 42);
        cancel.Click += (_, _) => f.Close();
        f.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { f.Close(); return; }
            capturedKey = e.KeyCode;
            f.DialogResult = DialogResult.OK;
        };
        f.Controls.AddRange(new Control[] { prompt, cancel });
        bool ok = f.ShowDialog(owner) == DialogResult.OK && capturedKey != Keys.None;
        key = capturedKey;
        return ok;
    }

    private void UpdateLiveInput()
    {
        if (_device.SelectedIndex < 0 || _device.SelectedIndex >= _devices.Count)
        {
            _live.Text = T("Switch.LiveDisconnected");
            _lastLiveMask = null;
            return;
        }
        PhysicalJoystick selected = _devices[_device.SelectedIndex];
        PhysicalJoystick? current = WinMmJoystickReader.Read(selected.Id, selected.Name);
        if (current is null)
        {
            _live.Text = T("Switch.LiveDisconnected");
            _lastLiveMask = null;
            return;
        }
        if (!_lastLiveMask.HasValue)
        {
            _lastLiveMask = current.ButtonMask;
            _live.Text = T("Switch.LiveWaiting");
            return;
        }
        uint changed = _lastLiveMask.Value ^ current.ButtonMask;
        _lastLiveMask = current.ButtonMask;
        if (changed == 0)
        {
            _live.Text = T("Switch.LiveIdle");
            return;
        }
        string raw = string.Join("  ", Enumerable.Range(1, 32)
            .Where(i => (changed & (1u << (i - 1))) != 0)
            .Select(i => $"B{i}={(((current.ButtonMask & (1u << (i - 1))) != 0) ? "ON" : "OFF")}"));
        _live.Text = string.Format(CultureInfo.CurrentCulture, T("Switch.LiveChanged"), raw);
    }

    private void UpdateRowSummaries()
    {
        foreach (DataGridViewRow row in _states.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow))
        {
            SwitchActionKind kind = ParseKind(Convert.ToString(row.Cells["Kind"].Value));
            string action = Convert.ToString(row.Cells["Action"].Value)?.Trim() ?? string.Empty;
            row.Cells["Action"].ReadOnly = row.Tag is SwitchStateDefinition sequenceRow && sequenceRow.Sequence.Count > 0;
            row.Cells["Conditions"].ReadOnly = row.Tag is SwitchStateDefinition gestureRow && gestureRow.Gesture.Count > 0;
            if (row.Tag is SwitchStateDefinition displayedGesture && displayedGesture.Gesture.Count > 0)
            {
                row.Cells["Conditions"].Style.WrapMode = DataGridViewTriState.True;
                row.Cells["Conditions"].ToolTipText = FormatGesture(displayedGesture);
                row.Height = Math.Max(_states.RowTemplate.Height, displayedGesture.Gesture.Count * (_states.Font.Height + 8) + 12);
            }
            row.Cells["Summary"].Value = row.Tag is SwitchStateDefinition custom && custom.Sequence.Count > 0
                ? (custom.FlapRole == "None" ? "" : custom.FlapRole + ": ") +
                    string.Join("; ", custom.Sequence.Select(s => $"{s.Key} x{s.Repeat} / {s.IntervalMs}ms")) +
                    (custom.Gesture.Count > 0 ? $" | Gesture {custom.Gesture.Count} steps, {custom.Gesture[^1].ElapsedMs}ms" : "")
                : SummarizeAction(kind, action);
        }
    }

    private string SummarizeAction(SwitchActionKind kind, string action) => kind switch
    {
        SwitchActionKind.MouseButton => "MOUSE " + (string.IsNullOrWhiteSpace(action) ? T("Switch.NotSet") : action.Replace("Mouse", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant()),
        _ => "KEY " + (string.IsNullOrWhiteSpace(action) ? T("Switch.NotSet") : action.ToUpperInvariant())
    };

    private bool Commit()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, T("Switch.EnterName"), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_device.SelectedIndex < 0 || _device.SelectedIndex >= _devices.Count)
        {
            MessageBox.Show(this, T("Switch.SelectDevice"), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        List<SwitchStateDefinition> states = new();
        foreach (DataGridViewRow row in _states.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow))
        {
            string name = Convert.ToString(row.Cells["State"].Value)?.Trim() ?? string.Empty;
            string conditionsText = Convert.ToString(row.Cells["Conditions"].Value)?.Trim() ?? string.Empty;
            SwitchActionKind kind = ParseKind(Convert.ToString(row.Cells["Kind"].Value));
            string action = Convert.ToString(row.Cells["Action"].Value)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, T("Switch.StateNeedsCondition"), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            List<InputCondition> conditions;
            try { conditions = ParseConditions(conditionsText, true); }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, T("Switch.StateError"), name, ex.Message), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (conditions.Count == 0)
            {
                MessageBox.Show(this, T("Switch.StateNeedsCondition"), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (row.Tag is SwitchStateDefinition recorded && recorded.Gesture.Count > 0 && recorded.Sequence.Count == 0)
            { MessageBox.Show(this, T("Switch.GestureNeedsSequence"), T("Switch.SequenceTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            if (row.Tag is SwitchStateDefinition macro && macro.Sequence.Count > 0 || row.Tag is SwitchStateDefinition unchanged &&
                action == unchanged.OutputKey && (kind == unchanged.OutputKind || unchanged.OutputKind == SwitchActionKind.VJoyButton))
            {
                var preserved = row.Tag is SwitchStateDefinition original
                    ? JsonSerializer.Deserialize<SwitchStateDefinition>(JsonSerializer.Serialize(original))!
                    : new SwitchStateDefinition { Behavior = SwitchOutputBehavior.None };
                preserved.Name = name; preserved.Conditions = conditions;
                states.Add(preserved);
                continue;
            }
            int outputButton = 1;
            string outputKey = string.Empty;
            if (kind == SwitchActionKind.MouseButton)
            {
                if (!KeyboardTapSender.IsSupportedMouseButton(action))
                {
                    MessageBox.Show(this, T("Switch.ActionRequired"), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                outputKey = action;
            }
            else
            {
                kind = SwitchActionKind.KeyboardKey;
                if (string.IsNullOrWhiteSpace(action) || !Enum.TryParse(action, true, out Keys parsedKey) || parsedKey == Keys.None)
                {
                    MessageBox.Show(this, T("Switch.ActionRequired"), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                outputKey = (parsedKey & Keys.KeyCode).ToString();
            }
            states.Add(new SwitchStateDefinition { Name = name, Conditions = conditions, OutputKind = kind, OutputKey = outputKey, OutputButton = outputButton, Trigger = SwitchTrigger.EnterState, Behavior = SwitchOutputBehavior.Pulse });
        }
        if (states.Count < 1)
        {
            MessageBox.Show(this, T("Switch.NeedsTwoStates"), T("Switch.Builder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        PhysicalJoystick d = _devices[_device.SelectedIndex];
        if (!ValidateFlaps(states)) return false;
        _flapsDraft.Enabled = false;
        _definition.AdvancedFlaps = _flapsDraft;
        _definition.Name = _name.Text.Trim();
        _definition.DeviceId = d.Id;
        _definition.DeviceName = d.Name;
        _definition.SwitchType = states.Count == 2 ? "2-State" : states.Count == 3 ? "3-State" : "Custom / Multi-State";
        _definition.Enabled = _enabled.Checked;
        _definition.ReversePositions = false;
        _definition.PhysicalButton = states.SelectMany(s => s.Conditions).FirstOrDefault(c => c.RequiredState)?.ButtonId ?? states.SelectMany(s => s.Conditions).FirstOrDefault()?.ButtonId ?? 0;
        SwitchStateDefinition? on = states.FirstOrDefault(s => s.Name.Equals("ON", StringComparison.OrdinalIgnoreCase)) ?? states.FirstOrDefault();
        SwitchStateDefinition? off = states.FirstOrDefault(s => s.Name.Equals("OFF", StringComparison.OrdinalIgnoreCase)) ?? states.Skip(1).FirstOrDefault() ?? on;
        _definition.OnActionKind = on?.OutputKind ?? SwitchActionKind.VJoyButton;
        _definition.OffActionKind = off?.OutputKind ?? SwitchActionKind.VJoyButton;
        _definition.OnActionButton = on?.OutputButton ?? 1;
        _definition.OffActionButton = off?.OutputButton ?? 1;
        _definition.OnActionKey = on?.OutputKey ?? string.Empty;
        _definition.OffActionKey = off?.OutputKey ?? string.Empty;
        _definition.States = states;
        return true;
    }

    private List<InputCondition> ParseConditions(string text, bool strict)
    {
        List<InputCondition> result = new();
        foreach (string part in text.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !int.TryParse(pair[0].Replace("Button", "", StringComparison.OrdinalIgnoreCase).Trim(), out int id) || id is < 1 or > 32)
            { if (strict) throw new FormatException(string.Format(CultureInfo.CurrentCulture, T("Switch.InvalidCondition"), part)); else continue; }
            bool required = pair[1].Equals("ON", StringComparison.OrdinalIgnoreCase) || pair[1].Equals("TRUE", StringComparison.OrdinalIgnoreCase) || pair[1] == "1";
            if (!required && !pair[1].Equals("OFF", StringComparison.OrdinalIgnoreCase) && !pair[1].Equals("FALSE", StringComparison.OrdinalIgnoreCase) && pair[1] != "0")
            { if (strict) throw new FormatException(string.Format(CultureInfo.CurrentCulture, T("Switch.InvalidState"), pair[1])); else continue; }
            result.Add(new InputCondition { ButtonId = id, RequiredState = required });
        }
        return result;
    }

    private static string FormatConditions(IEnumerable<InputCondition> c) => string.Join("; ", c.Select(x => $"{x.ButtonId}={(x.RequiredState ? "ON" : "OFF")}"));
    private static SwitchActionKind ParseKind(string? value) => string.Equals(value, "MOUSE", StringComparison.OrdinalIgnoreCase) ? SwitchActionKind.MouseButton : SwitchActionKind.KeyboardKey;
    private string T(string key) => AppText.T(_languageCode, key);
}

internal sealed class SwitchTestForm : Form
{
    private readonly AdvancedSwitchService _service; private readonly SwitchDefinition _definition; private readonly Label _state; private readonly Label _raw; private readonly Label _transition; private readonly Label _action; private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 }; private string _previous = "";
    private readonly string _languageCode;
    public SwitchTestForm(AdvancedSwitchService service, SwitchDefinition definition) : this(service, definition, "en") { }
    public SwitchTestForm(AdvancedSwitchService service, SwitchDefinition definition, string languageCode)
    {
        _service = service; _definition = definition; _languageCode = AppText.Normalize(languageCode); Text = string.Format(CultureInfo.CurrentCulture, T("Switch.TestTitle"), definition.Name); StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(650, 410); BackColor = IllustratedTheme.Background; ForeColor = IllustratedTheme.Ivory; Font = new Font("Segoe UI", 11f); AutoScaleMode = AutoScaleMode.Dpi;
        HandleCreated += (_, _) => IllustratedTheme.ApplyWindowChrome(this);
        Label title = AdvancedSwitchManagerForm.L(definition.Name.ToUpperInvariant(), 18, FontStyle.Bold); title.SetBounds(24, 20, 580, 40);
        _state = AdvancedSwitchManagerForm.L(string.Format(CultureInfo.CurrentCulture, T("Switch.PhysicalState"), ""), 13, FontStyle.Bold); _state.SetBounds(30, 85, 580, 44);
        _transition = AdvancedSwitchManagerForm.L(string.Format(CultureInfo.CurrentCulture, T("Switch.Transition"), "", ""), 11, FontStyle.Regular); _transition.SetBounds(30, 145, 580, 38);
        _action = AdvancedSwitchManagerForm.L(T("Switch.ActionLabel"), 11, FontStyle.Regular); _action.SetBounds(30, 195, 580, 38);
        _raw = AdvancedSwitchManagerForm.L(string.Format(CultureInfo.CurrentCulture, T("Switch.RawButtons"), ""), 10, FontStyle.Regular); _raw.SetBounds(30, 245, 580, 80);
        Button testOutput = AdvancedSwitchManagerForm.B(T("Switch.TestOutput")); testOutput.SetBounds(290, 340, 155, 42);
        testOutput.Click += (_, _) =>
        {
            SwitchStateDefinition? currentState = _definition.States.FirstOrDefault(s => s.Name.Equals(_previous, StringComparison.OrdinalIgnoreCase)) ?? _definition.States.FirstOrDefault();
            if (currentState is null) return;
            try { _service.TestPulse(currentState.OutputButton); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Switch.VJoyOutputTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        Button close = AdvancedSwitchManagerForm.B(T("Switch.Close")); close.SetBounds(465, 340, 140, 42); close.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { title, _state, _transition, _action, _raw, testOutput, close });
        _timer.Tick += (_, _) => UpdateLive(); Shown += (_, _) => _timer.Start(); FormClosed += (_, _) => _timer.Stop();
    }
    private void UpdateLive()
    {
        string current = _service.GetCurrentState(_definition, out uint mask, out bool connected);
        if (current != _previous) { _transition.Text = string.Format(CultureInfo.CurrentCulture, T("Switch.Transition"), _previous.Length == 0 ? "-" : _previous, current); _previous = current; }
        _state.Text = string.Format(CultureInfo.CurrentCulture, T("Switch.PhysicalState"), current.ToUpperInvariant());
        List<string> raw = new(); for (int i = 1; i <= 32; i++) if ((mask & (1u << (i - 1))) != 0 || _definition.States.SelectMany(s => s.Conditions).Any(c => c.ButtonId == i)) raw.Add($"Button {i}: {(((mask & (1u << (i - 1))) != 0) ? "ON" : "OFF")}");
        _raw.Text = connected ? string.Format(CultureInfo.CurrentCulture, T("Switch.RawButtons"), string.Join("   ", raw)) : T("Switch.RawDisconnected");
        SwitchStateDefinition? state = _definition.States.FirstOrDefault(s => s.Name.Equals(current, StringComparison.OrdinalIgnoreCase)); _action.Text = state is null ? T("Switch.ActionNone") : string.Format(CultureInfo.CurrentCulture, T("Switch.ActionDetail"), state.Behavior, state.OutputKind == SwitchActionKind.KeyboardKey ? ("KEY " + state.OutputKey.ToUpperInvariant()) : ("VB" + state.OutputButton.ToString(CultureInfo.InvariantCulture)), state.Trigger);
    }
    private string T(string key) => AppText.T(_languageCode, key);
}
