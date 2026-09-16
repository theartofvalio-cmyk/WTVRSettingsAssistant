namespace WTVRSettingsAssistant;

internal enum AdvancedFlapMode { Raised, Combat, AutoTakeoffLanding }
internal enum DetectedFlapState { Unknown, Raised, Combat, Takeoff, Landing }
internal enum FlapCommand { Up, Down }

internal sealed class AdvancedFlapsSettings
{
    public bool Enabled { get; set; }
    public bool AutomaticAircraftLayout { get; set; } = true;
    public Dictionary<string, AircraftFlapLayout> AircraftLayouts { get; set; } = new();
    public bool HasCombatStage { get; set; } = true;
    public bool HasTakeoffStage { get; set; } = true;
    public int UpPosition { get; set; } = 0;
    public int CentrePosition { get; set; } = 1;
    public int DownPosition { get; set; } = 2;
    public SwitchStateDefinition FlapsUp { get; set; } = new() { OutputKind = SwitchActionKind.KeyboardKey, OutputKey = "U" };
    public SwitchStateDefinition FlapsDown { get; set; } = new() { OutputKind = SwitchActionKind.KeyboardKey, OutputKey = "D" };
    public int CommandCooldownMs { get; set; } = 200;
    public int StateChangeTimeoutMs { get; set; } = 6000;
    public int MissedCommandRetryMs { get; set; } = 1500;

    public bool IsValid => new[] { UpPosition, CentrePosition, DownPosition }.Order().SequenceEqual(new[] { 0, 1, 2 }) &&
        CommandCooldownMs is >= 150 and <= 2000 &&
        ValidOutput(FlapsUp) && ValidOutput(FlapsDown) &&
        (FlapsUp.OutputKind != FlapsDown.OutputKind || (FlapsUp.OutputKind == SwitchActionKind.VJoyButton
            ? FlapsUp.OutputButton != FlapsDown.OutputButton : !string.Equals(FlapsUp.OutputKey, FlapsDown.OutputKey, StringComparison.OrdinalIgnoreCase)));

    private static bool ValidOutput(SwitchStateDefinition? output) => output is not null && output.OutputKind switch
    {
        SwitchActionKind.KeyboardKey => Enum.TryParse(output.OutputKey, true, out Keys key) &&
            key != Keys.None && (key & Keys.Modifiers) == 0 && Enum.IsDefined(key),
        _ => false
    };
    public AdvancedFlapMode Mode(int index) => index == CentrePosition ? AdvancedFlapMode.Raised :
        index == UpPosition ? AdvancedFlapMode.Combat : AdvancedFlapMode.AutoTakeoffLanding;
}

internal readonly record struct FlapSample(DateTime Time, string Aircraft, double Percent, double Ias,
    double? GearPercent, double? VerticalSpeed, double? HeightAboveGround)
{
    public bool Valid => !string.IsNullOrWhiteSpace(Aircraft) && double.IsFinite(Percent) && Percent is >= 0 and <= 100 &&
        (GearPercent is null || double.IsFinite(GearPercent.Value) && GearPercent is >= 0 and <= 100) &&
        (VerticalSpeed is null || double.IsFinite(VerticalSpeed.Value)) &&
        (HeightAboveGround is null || double.IsFinite(HeightAboveGround.Value) && HeightAboveGround >= 0);
    public bool Fresh(DateTime now) => Valid && now >= Time && now - Time <= TimeSpan.FromMilliseconds(500);
}

// A percentage is an observed position, not a semantic stage. Names are confirmed
// during calibration; this detector only establishes that physical movement settled.
internal sealed class FlapStageDetector(int stableMilliseconds = 200)
{
    public const double Tolerance = .5;
    private DateTime _anchorTime, _lastTime;
    private string _aircraft = "";
    private double _anchor;
    public bool Stable { get; private set; }
    public void Reset() { _aircraft = ""; _lastTime = default; Stable = false; }
    public void Observe(FlapSample sample)
    {
        if (!sample.Valid) { Reset(); return; }
        if (_aircraft != sample.Aircraft || sample.Time < _lastTime || sample.Time - _lastTime > TimeSpan.FromMilliseconds(500))
        { _aircraft = sample.Aircraft; _anchor = sample.Percent; _anchorTime = sample.Time; Stable = false; }
        if (sample.Time <= _lastTime) return;
        _lastTime = sample.Time;
        if (Math.Abs(sample.Percent - _anchor) > .15)
        { _anchor = sample.Percent; _anchorTime = sample.Time; }
        Stable = sample.Time - _anchorTime >= TimeSpan.FromMilliseconds(stableMilliseconds);
    }
}

internal sealed class AdvancedFlapsController
{
    private readonly FlapStageDetector _detector = new(100);
    private AdvancedFlapMode? _mode;
    private string _aircraft = "";
    private DateTime _sentAt;
    private double _baseline;
    private FlapCommand? _pending;
    private bool _fault;
    private string _faultKey = "Flaps.Timeout";
    private int _commands;
    private double? _previousTarget;
    private double? _firstStep;
    public string StatusKey { get; private set; } = "Flaps.Waiting";
    public DetectedFlapState Target { get; private set; }
    public bool Pending => _pending.HasValue;

    public void Reset()
    {
        _mode = null; _aircraft = ""; _pending = null; _fault = false; _commands = 0;
        _previousTarget = null; _sentAt = default; _detector.Reset();
        _firstStep = null;
        Target = DetectedFlapState.Unknown; StatusKey = "Flaps.Waiting";
    }
    private void Fault(string key) { _pending = null; _fault = true; StatusKey = _faultKey = key; }
    public void FailOutput() => Fault("Flaps.OutputFailed");

    public FlapCommand? Step(DateTime now, AdvancedFlapMode mode, FlapSample? telemetry, AdvancedFlapsSettings settings, DateTime? earliestCommand = null)
    {
        if (telemetry is not { } sample || !sample.Fresh(now))
        {
            _pending = null; _detector.Reset(); Target = DetectedFlapState.Unknown;
            StatusKey = "Flaps.NoTelemetry"; return null;
        }
        if (!settings.IsValid) { Reset(); StatusKey = "Flaps.InvalidSettings"; return null; }
        if (_aircraft != sample.Aircraft) { Reset(); _aircraft = sample.Aircraft; }
        if (_mode != mode)
        {
            _mode = mode; _fault = false; _commands = 0; _previousTarget = null;
            // An in-flight command still needs acknowledgement even after the pilot
            // changes the target; never queue opposite inputs on top of a moving flap.
        }
        _detector.Observe(sample);
        Target = mode switch
        {
            AdvancedFlapMode.Raised => DetectedFlapState.Raised,
            AdvancedFlapMode.Combat => DetectedFlapState.Combat,
            _ => DetectedFlapState.Landing
        };
        // First-step mode establishes a baseline at Raised, then observes a single
        // deployment. No aircraft-specific semantic names or percentages are assumed.
        double desired = Target == DetectedFlapState.Landing ? 100 :
            Target == DetectedFlapState.Combat ? _firstStep ?? 0 : 0;
        if (_previousTarget != desired) { _commands = 0; _previousTarget = desired; }
        if (_fault) { StatusKey = _faultKey; return null; }
        if (Target == DetectedFlapState.Raised && sample.Percent <= FlapStageDetector.Tolerance &&
            (_pending is null || _pending == FlapCommand.Up && sample.Time > _sentAt))
        { _pending = null; _commands = 0; StatusKey = "Flaps.Reached"; return null; }
        if (_pending is { } pending)
        {
            double change = sample.Percent - _baseline;
            if (Math.Abs(change) > FlapStageDetector.Tolerance && Math.Sign(change) != (pending == FlapCommand.Down ? 1 : -1))
            { Fault("Flaps.WrongDirection"); return null; }
            if (sample.Time > _sentAt && Math.Abs(change) > FlapStageDetector.Tolerance && _detector.Stable &&
                now - _sentAt >= TimeSpan.FromMilliseconds(150))
            {
                if (pending == FlapCommand.Down && _baseline <= FlapStageDetector.Tolerance)
                    _firstStep ??= sample.Percent;
                if (Target == DetectedFlapState.Combat && _firstStep.HasValue) desired = _firstStep.Value;
                _pending = null;
            }
            else if (sample.Time > _sentAt && Math.Abs(change) <= FlapStageDetector.Tolerance && _detector.Stable &&
                now - _sentAt >= TimeSpan.FromMilliseconds(settings.MissedCommandRetryMs))
            {
                // Fresh observations confirm an ignored command, not missing data.
                // Keep reconciling the physical request after a menu/interruption.
                _pending = null;
                if (_commands >= 3) { Fault("Flaps.Timeout"); return null; }
            }
            else if (now - _sentAt >= TimeSpan.FromMilliseconds(settings.StateChangeTimeoutMs))
            { Fault("Flaps.Timeout"); return null; }
            else { StatusKey = "Flaps.Moving"; return null; }
        }
        if (!_detector.Stable) { StatusKey = "Flaps.Moving"; return null; }
        bool requestFirstStep = Target == DetectedFlapState.Combat && !_firstStep.HasValue && sample.Percent <= FlapStageDetector.Tolerance;
        if (!requestFirstStep && Math.Abs(sample.Percent - desired) <= FlapStageDetector.Tolerance)
        { _commands = 0; StatusKey = "Flaps.Reached"; return null; }
        if (now - _sentAt < TimeSpan.FromMilliseconds(settings.CommandCooldownMs)) return null;
        if (earliestCommand is { } earliest && now < earliest) return null;
        if (++_commands > 8) { Fault("Flaps.Timeout"); return null; }
        _pending = requestFirstStep ? FlapCommand.Down : sample.Percent > desired ? FlapCommand.Up : FlapCommand.Down;
        _sentAt = now; _baseline = sample.Percent; StatusKey = "Flaps.Moving";
        return _pending;
    }
}
