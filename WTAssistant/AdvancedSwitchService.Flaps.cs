using HOTASTrimUtility;
using System.Runtime.InteropServices;

namespace WTVRSettingsAssistant;

internal sealed partial class AdvancedSwitchService
{
    private IFlapTelemetry? _flapTelemetry;
    private Func<bool> _flapGameFocused = GameWindow.GameFocused;
    private int _flapSuspensions;
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    private static bool IsEditorForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return true;
        GetWindowThreadProcessId(window, out uint processId);
        return processId == Environment.ProcessId;
    }
    private readonly Dictionary<string, string> _flapTraceState = new();
    internal IDisposable SuspendFlaps()
    {
        _flapSuspensions++;
        foreach (var runtime in _runtime.Values) { runtime.FlapKeys?.Reset(); runtime.Macro?.Reset(); }
        return new FlapSuspension(this);
    }
    private sealed class FlapSuspension(AdvancedSwitchService service) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; service._flapSuspensions--;
        }
    }
    internal void SetFlapTestEnvironment(IFlapTelemetry telemetry, Func<bool> focused)
    { _flapTelemetry?.Dispose(); _flapTelemetry = telemetry; _flapGameFocused = focused; }
    internal FlapSample? ReadFlapTelemetry()
    {
        _flapTelemetry ??= new AdvancedFlapsTelemetry();
        _flapTelemetry.Refresh(_utcNow());
        return _flapTelemetry.Sample;
    }
    private void PollFlaps(SwitchDefinition definition, PhysicalJoystick device, Runtime runtime)
    {
        runtime.FlapKeys ??= new FlapKeySequence();
        var controller = runtime.FlapKeys;
        if (_flapSuspensions > 0 && IsEditorForeground())
        { controller.Reset(); FlapStatus(definition, "Flaps.Configuring"); return; }
        var settings = definition.AdvancedFlaps;
        var matches = definition.States.Select((s, n) => (s, n)).Where(x => x.s.Conditions.Count > 0 &&
            x.s.Conditions.All(c => device.Button(c.ButtonId) == c.RequiredState)).Select(x => x.n).ToArray();
        if (!settings.IsValid || definition.States.Count != 3 || matches.Length != 1 ||
            Settings.Switches.Count(d => d.Enabled && d.AdvancedFlaps.Enabled) != 1)
        { controller.Reset(); FlapStatus(definition, "Flaps.InvalidSettings"); return; }
        int detected = matches[0];
        int position = detected == settings.UpPosition ? 0 : detected == settings.CentrePosition ? 1 : 2;
        runtime.ActiveIndex = detected;
        var command = controller.Step(position, _utcNow(), Settings.DebounceMs, settings.CommandCooldownMs,
            settings.HasCombatStage, settings.HasTakeoffStage);
        TraceFlaps(definition, "state", $"position={position} mask=0x{device.ButtonMask:X8}");
        if (command is { } action)
        {
            var binding = action == FlapCommand.Up ? settings.FlapsUp : settings.FlapsDown;
            try
            {
                ExecutePulse(binding);
                TraceFlaps(definition, "command", $"{action} keyboard={binding.OutputKey}", true);
            }
            catch (Exception ex)
            {
                controller.Reset();
                TraceFlaps(definition, "error", ex.Message, true);
                FlapStatus(definition, "Flaps.OutputFailed");
                return;
            }
        }
        FlapStatus(definition, controller.Pending > 0 ? "Flaps.KeysSending" :
            controller.CombatUnavailable ? "Flaps.CombatUnavailable" : "Flaps.KeysReady");
    }

    private void FlapStatus(SwitchDefinition definition, string key, DetectedFlapState target = DetectedFlapState.Unknown)
    {
        string text = definition.Name + ": " + AppText.T(LanguageCode, key);
        if (target != DetectedFlapState.Unknown) text += " | " + AppText.T(LanguageCode, "Flaps.Stage." + target);
        if (LastStatus != text) SetStatus(text);
        TraceFlaps(definition, "status", text);
    }

    private void TraceFlaps(SwitchDefinition definition, string category, string message, bool always = false)
    {
        string key = definition.Id + ":" + category;
        if (!always && _flapTraceState.TryGetValue(key, out var previous) && previous == message) return;
        _flapTraceState[key] = message;
        try
        {
            string path = Path.ChangeExtension(_path, ".flaps.log");
            if (File.Exists(path) && new FileInfo(path).Length > 262144)
                File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{_utcNow():O} [{definition.Name}] {category}: {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { /* Diagnostics must never interrupt physical input processing. */ }
    }
}
