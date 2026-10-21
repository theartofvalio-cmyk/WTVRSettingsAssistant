using System.Text.Json.Serialization;

namespace WTVRSettingsAssistant;

internal sealed class SwitchMacroStep
{
    public string Key { get; set; } = "U";
    public int Repeat { get; set; } = 1;
    public int IntervalMs { get; set; } = 150;
}
internal sealed class SwitchGestureStep
{
    // Preserve the original low word so existing gesture files still load exactly.
    public uint Mask { get; set; }
    public uint Mask32 { get; set; }
    public uint Mask64 { get; set; }
    public uint Mask96 { get; set; }
    [JsonIgnore]
    public UInt128 Buttons
    {
        get => Mask | ((UInt128)Mask32 << 32) | ((UInt128)Mask64 << 64) | ((UInt128)Mask96 << 96);
        set { Mask = (uint)value; Mask32 = (uint)(value >> 32); Mask64 = (uint)(value >> 64); Mask96 = (uint)(value >> 96); }
    }
    public int ElapsedMs { get; set; }
}

internal sealed class SwitchMacroRuntime
{
    private UInt128? _mask;
    private UInt128? _settledMask;
    private DateTime _changed, _last, _next;
    private readonly List<(UInt128 Mask, DateTime Time)> _history = new();
    private readonly Queue<(string Key, int Interval)> _output = new();
    private int? _pendingRow;
    private bool _initial = true;
    public int? ActiveRow { get; private set; }
    // Intervals are measured between press starts, rather than after key release.
    internal const int PressDurationMs = 20;
    internal const int MinimumIntervalMs = 40;
    private SwitchStateDefinition? _repeatRow;
    public void Reset()
    { _mask = null; _settledMask = null; _history.Clear(); _output.Clear(); _pendingRow = null; _initial = true; _next = default; ActiveRow = null; _repeatRow = null; _last = default; }
    public string? Step(SwitchDefinition definition, UInt128 mask, DateTime now, int debounce, bool outputReady = true)
    {
        if (_last != default && (now < _last || now - _last > TimeSpan.FromSeconds(1))) Reset();
        _last = now;
        UInt128 relevant = 0;
        foreach (var c in definition.States.SelectMany(s => s.Conditions))
            if (c.ButtonId is >= 1 and <= 128) relevant |= (UInt128)1 << (c.ButtonId - 1);
        foreach (var s in definition.States.Where(s => s.Gesture.Count > 1))
            foreach (var g in s.Gesture) relevant |= g.Buttons ^ s.Gesture[0].Buttons;
        mask &= relevant;
        var gestures = definition.States.Where(s => s.Gesture.Count > 1).ToList();
        int grace = gestures.Count == 0 ? debounce : Math.Min(10000, gestures.Max(s => s.Gesture[^1].ElapsedMs) * 3 / 2 + 100);
        if (_mask != mask)
        {
            UInt128? previousMask = _settledMask;
            _mask = mask; _changed = now;
            _output.Clear(); _repeatRow = null; _next = default;
            _history.Add((mask, now));
            if (_history.Count > 64) _history.RemoveAt(0);
            // An unchanged OFF condition must not steal another button's transition.
            // Prefer the active position when ON and OFF edges arrive in one poll.
            _pendingRow = definition.States.Select((state, index) => (state, index))
                .Where(item => item.state.Gesture.Count == 0 && item.state.Conditions.Count > 0 &&
                    Matches(item.state, mask) && previousMask.HasValue && !Matches(item.state, previousMask.Value))
                .OrderByDescending(item => item.state.Conditions.Count(c => c.RequiredState))
                .ThenByDescending(item => item.state.Conditions.Count)
                .Select(item => (int?)item.index).FirstOrDefault();
            if (_initial) { _initial = false; _settledMask = mask; _pendingRow = null; }
            else
            {
                foreach (var row in gestures.OrderByDescending(s => s.Gesture.Count))
                {
                    int start = _history.Count - row.Gesture.Count;
                    if (start < 0) continue;
                    bool match = true;
                    UInt128 gestureMask = 0;
                    foreach (var step in row.Gesture) gestureMask |= step.Buttons ^ row.Gesture[0].Buttons;
                    for (int i = 0; i < row.Gesture.Count; i++)
                    {
                        var actual = _history[start + i];
                        match &= (actual.Mask & gestureMask) == (row.Gesture[i].Buttons & gestureMask);
                        // Initial position can be held indefinitely before starting.
                        if (i > 1)
                        {
                            int expected = row.Gesture[i].ElapsedMs - row.Gesture[i - 1].ElapsedMs;
                            double elapsed = (actual.Time - _history[start + i - 1].Time).TotalMilliseconds;
                            match &= elapsed <= Math.Max(100, expected * 1.5 + 50);
                        }
                    }
                    if (!match) continue;
                    Queue(row); ActiveRow = definition.States.IndexOf(row); _pendingRow = null; _settledMask = mask;
                    _history.Clear(); _history.Add((mask, now)); break;
                }
            }
        }
        if (now - _changed >= TimeSpan.FromMilliseconds(grace))
        {
            if (_pendingRow is >= 0) { ActiveRow = _pendingRow; Queue(definition.States[_pendingRow.Value]); }
            _pendingRow = null;
            // Commit only a settled position: transient contacts must not become
            // the origin used to choose between different return-to-neutral rows.
            _settledMask = mask;
        }
        if (_output.Count == 0 && _repeatRow is not null && Matches(_repeatRow, mask))
            Enqueue(_repeatRow);
        if (!outputReady || _output.Count == 0 || now < _next) return null;
        var item = _output.Dequeue();
        // Keep the average cadence without accumulating poll latency. If the app
        // stalls, skip missed deadlines instead of delivering a burst of presses.
        int interval = Math.Clamp(item.Interval, MinimumIntervalMs, 5000);
        _next = _next == default ? now.AddMilliseconds(interval) : _next.AddMilliseconds(interval);
        if (_next <= now) _next = now.AddMilliseconds(interval);
        return item.Key;
    }

    private static bool Matches(SwitchStateDefinition state, UInt128 mask) =>
        state.Conditions.All(c => c.ButtonId is >= 1 and <= 128 &&
            ((mask & ((UInt128)1 << (c.ButtonId - 1))) != 0) == c.RequiredState);

    private void Queue(SwitchStateDefinition row)
    {
        if (row.Behavior == SwitchOutputBehavior.None && row.Sequence.Count == 0) return;
        _output.Clear(); _next = default;
        _repeatRow = row.RepeatWhileHeld && row.Conditions.Any(c => c.RequiredState) ? row : null;
        Enqueue(row);
    }

    private void Enqueue(SwitchStateDefinition row)
    {
        string legacyKey = row.OutputKind == SwitchActionKind.MouseButton ? "MOUSE:" + row.OutputKey :
            row.OutputKind == SwitchActionKind.VJoyButton ? "VJOY:" + row.OutputButton : row.OutputKey;
        var steps = row.Sequence.Count > 0 ? row.Sequence : !string.IsNullOrWhiteSpace(legacyKey)
                ? new List<SwitchMacroStep> { new() { Key = legacyKey } } : new();
        foreach (var step in steps.Take(32))
            for (int i = 0; i < Math.Clamp(step.Repeat, 1, 20); i++)
                _output.Enqueue((step.Key, Math.Clamp(step.IntervalMs, 0, 5000)));
    }
}

internal sealed partial class AdvancedSwitchService
{
    private void PollCustomActions(SwitchDefinition definition, PhysicalJoystick device, Runtime runtime)
    {
        runtime.Macro ??= new();
        if (!_flapGameFocused() || (_flapSuspensions > 0 && IsEditorForeground()))
        { runtime.Macro.Reset(); return; }
        string? key = runtime.Macro.Step(definition, device.ButtonMask, _utcNow(), Settings.DebounceMs,
            KeyboardTapSender.SequenceTapReady);
        if (key is null) return;
        try
        {
            if (key.StartsWith("MOUSE:"))
            { KeyboardTapSender.ClickMouse(key[6..], LanguageCode); return; }
            if (key.StartsWith("VJOY:") && int.TryParse(key[5..], out int button))
            { _output.Pulse(button, Settings.PulseDurationMs); return; }
            if (!Enum.TryParse(key, true, out Keys parsed) || parsed == Keys.None) throw new InvalidOperationException("Invalid sequence key: " + key);
            _sequenceKeyboardTap(parsed, LanguageCode);
            SetStatus(definition.Name + ": KEY " + key);
        }
        catch (Exception ex) { runtime.Macro.Reset(); SetStatus(definition.Name + ": " + ex.Message); }
    }
}
