namespace HOTASTrimUtility;

internal sealed class InstructorTuning
{
    public bool PitchEnabled { get; set; } = true;
    public int AggressivenessPercent { get; set; } = 100;
    public AxisAssistTuning RollAssist { get; set; } = new();
    public AxisAssistTuning RudderAssist { get; set; } = new();
    public int AttitudeHoldStrength { get; set; } = 100;
    public int PitchDampingStrength { get; set; } = 100;
    public int MaximumCorrection { get; set; } = 100;
    public int PitchCentreDeadzone { get; set; } = 1;
    public int SmoothingMilliseconds { get; set; } = 10;
    // Retained for profile compatibility; the standard vJoy Y convention is fixed.
    public int PitchDirection { get; set; } = 1;
    public bool DiagnosticsEnabled { get; set; }

    public void Normalize()
    {
        RollAssist ??= new(); RudderAssist ??= new();
        AttitudeHoldStrength = Math.Clamp(AttitudeHoldStrength, 0, 400);
        PitchDampingStrength = Math.Clamp(PitchDampingStrength, 0, 400);
        AggressivenessPercent = Math.Clamp(AggressivenessPercent, 0, 400);
        MaximumCorrection = Math.Clamp(MaximumCorrection, 0, 100);
        PitchCentreDeadzone = Math.Clamp(PitchCentreDeadzone, 0, 20);
        SmoothingMilliseconds = Math.Clamp(SmoothingMilliseconds, 0, 500);
        PitchDirection = 1;
    }
}

internal readonly record struct AircraftSample(
    double Time, string Aircraft, double Roll, double Pitch, double Airspeed,
    double Heading = 0, double TrueAirspeed = 250,
    bool HasHeading = true, bool HasTrueAirspeed = true);

internal readonly record struct PitchHoldOutput(
    double Elevator, double Correction, double? TargetPitch, double PitchRate, string Status);

// Pitch-only PD feedback. Pitch and rate are nose-up-positive degrees and
// degrees/second; the rate is measured from attitude samples, never stick input.
internal sealed class InstructorController
{
    public const double FreshnessSeconds = .30;
    public const double MinimumAirspeed = 90;
    private readonly List<AircraftSample> _history = new();
    private AircraftSample? _sample;
    private double _lastControlTime = double.NaN;
    private double _correction;
    private double _smoothedInput;
    private bool _smoothingInput;
    private readonly Queue<double> _reversals = new();
    private int _lastCorrectionSign;
    private bool _unstable;
    public double PitchRate { get; private set; }
    public double? TargetPitch { get; private set; }
    public AircraftSample? Sample => _sample;
    public bool Ready(double now) => double.IsFinite(now) && _sample is { } s &&
        now >= s.Time && now - s.Time <= FreshnessSeconds && _history.Count >= 2;

    public void Accept(AircraftSample sample)
    {
        if (!double.IsFinite(sample.Time) || !double.IsFinite(sample.Pitch) ||
            !double.IsFinite(sample.Airspeed) || string.IsNullOrWhiteSpace(sample.Aircraft) ||
            Math.Abs(sample.Pitch) > 90 || sample.Airspeed < 0 || sample.Airspeed > 5000)
        { Invalidate(); return; }
        if (_sample is { } old)
        {
            if (sample.Time <= old.Time) return;
            double gap = sample.Time - old.Time;
            if (sample.Aircraft != old.Aircraft || gap > FreshnessSeconds ||
                Math.Abs(sample.Pitch - old.Pitch) > Math.Max(20, 250 * gap))
                Invalidate();
        }
        _sample = sample;
        _history.Add(sample);
        while (_history.Count > 2 && sample.Time - _history[0].Time > .15)
            _history.RemoveAt(0);
        // A short least-squares slope reduces spikes from rounded instruments.
        double origin = _history[0].Time, st = 0, sy = 0, stt = 0, sty = 0;
        foreach (var s in _history)
        {
            double t = s.Time - origin;
            st += t; sy += s.Pitch; stt += t * t; sty += t * s.Pitch;
        }
        double n = _history.Count, denominator = n * stt - st * st;
        PitchRate = denominator > 1e-8 ? Math.Clamp((n * sty - st * sy) / denominator, -500, 500) : 0;
    }

    public PitchHoldOutput Step(double now, double physicalPitch, double manualElevator,
        bool enabled, bool pitchAvailable, InstructorTuning tuning)
    {
        tuning.Normalize();
        double dt = double.IsFinite(_lastControlTime) ? now - _lastControlTime : .015;
        _lastControlTime = now;
        string? bypass = !enabled ? "OFF" : !pitchAvailable ? "CHECK PITCH AXIS" :
            Ready(now) && _sample!.Value.Airspeed < MinimumAirspeed ? "LOW AIRSPEED" :
            tuning.MaximumCorrection == 0 ||
            (tuning.AttitudeHoldStrength == 0 && tuning.PitchDampingStrength == 0) ? "ZERO STRENGTH" : null;
        if (bypass is not null || !double.IsFinite(physicalPitch) ||
            !double.IsFinite(manualElevator) || !double.IsFinite(dt) || dt < 0 || dt > .20)
        {
            ResetHold();
            return new(Safe(manualElevator), 0, null, PitchRate, bypass ?? "REACQUIRING");
        }
        if (!Ready(now))
        {
            TargetPitch = null;
            _correction = 0;
            // A short input filter, not aircraft feedback. Bound lag to 8% of travel.
            if (!_smoothingInput) _smoothedInput = Safe(manualElevator);
            _smoothingInput = true;
            double input = Safe(manualElevator);
            double blend = 1 - Math.Exp(-dt / .04);
            _smoothedInput += (input - _smoothedInput) * blend;
            double maximumLag = Math.Min(.08, tuning.MaximumCorrection / 100d);
            _smoothedInput = Math.Clamp(_smoothedInput, Math.Max(-1, input - maximumLag), Math.Min(1, input + maximumLag));
            return new(_smoothedInput, _smoothedInput - input, null, 0,
                "INPUT SMOOTHING - NO PITCH TELEMETRY");
        }
        _smoothingInput = false;
        if (Math.Abs(physicalPitch) > tuning.PitchCentreDeadzone / 100d)
        {
            // Manual takeover is immediate, not subject to the smoothing filter.
            ResetHold();
            return new(Safe(manualElevator), 0, null, PitchRate, "MANUAL PITCH");
        }
        if (_unstable)
            return new(Safe(manualElevator), 0, null, PitchRate, "FLIGHT ASSISTANT PAUSED - UNSTABLE RESPONSE");
        TargetPitch ??= _sample!.Value.Pitch;
        double error = Deadband(TargetPitch.Value - _sample!.Value.Pitch, .15);
        double rate = Deadband(PitchRate, .20);
        // Reduce authority as dynamic pressure rises; old 100% profiles must not
        // command full elevator from a delayed instrument derivative.
        double authority = Math.Clamp(Math.Pow(250 / _sample.Value.Airspeed, 2), .08, 1);
        double aggression = tuning.AggressivenessPercent / 100d;
        double requested = (error * tuning.AttitudeHoldStrength / 100d * .012 -
            rate * tuning.PitchDampingStrength / 100d * .012) * authority * aggression;
        double limit = Math.Min(.20 * authority * aggression, tuning.MaximumCorrection / 100d);
        requested = Math.Clamp(requested * tuning.PitchDirection, -limit, limit);
        double alpha = 1 - Math.Exp(-Math.Clamp(dt, 0, .05) / (Math.Max(80, tuning.SmoothingMilliseconds) / 1000d));
        double change = (requested - _correction) * alpha;
        double maximumChange = .35 * aggression * Math.Clamp(dt, 0, .05);
        _correction = Math.Clamp(_correction + Math.Clamp(change, -maximumChange, maximumChange), -limit, limit);
        int sign = Math.Abs(_correction) > .003 ? Math.Sign(_correction) : 0;
        if (sign != 0)
        {
            if (_lastCorrectionSign != 0 && sign != _lastCorrectionSign) _reversals.Enqueue(now);
            _lastCorrectionSign = sign;
        }
        while (_reversals.Count > 0 && now - _reversals.Peek() > 2) _reversals.Dequeue();
        if (_reversals.Count >= 4 || Math.Abs(error) > 12)
        {
            _unstable = true; TargetPitch = null; _correction = 0;
            return new(Safe(manualElevator), 0, null, PitchRate, "FLIGHT ASSISTANT PAUSED - UNSTABLE RESPONSE");
        }
        return new(Safe(manualElevator + _correction), _correction, TargetPitch, PitchRate, "PITCH HOLD");
    }

    public void ResetAxes(bool roll, bool pitch, bool rudder) { if (pitch) ResetHold(); }
    public void ResetHold()
    {
        TargetPitch = null; _correction = 0; _lastControlTime = double.NaN; _smoothingInput = false;
        _unstable = false; _reversals.Clear(); _lastCorrectionSign = 0;
    }
    public void Invalidate()
    {
        _sample = null; _history.Clear(); PitchRate = 0;
        TargetPitch = null; _correction = 0;
        // Missing telemetry can repeat every poll; do not restart the input filter.
    }
    private static double Deadband(double value, double width) => Math.CopySign(Math.Max(0, Math.Abs(value) - width), value);
    private static double Safe(double value) => double.IsFinite(value) ? Math.Clamp(value, -1, 1) : 0;
}
