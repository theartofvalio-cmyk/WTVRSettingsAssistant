namespace HOTASTrimUtility;

// Controller coordinates, not aircraft angles. Each component is -1 .. +1.
internal readonly record struct AxisVector(double Roll, double Pitch, double Rudder);
internal readonly record struct TrimCapture(AxisVector ManualTrimPercent,
    AxisVector NeutralCommand, bool Limited);

// No WinForms, telemetry or driver dependencies. Can be tested independently.
internal sealed class StoreTrimController
{
    public bool RollWaiting { get; private set; }
    public bool PitchWaiting { get; private set; }
    public bool RudderWaiting { get; private set; }
    public bool AnyWaiting => RollWaiting || PitchWaiting || RudderWaiting;
    private AxisVector _threshold;

    public static TrimCapture SolveCapture(AxisVector command,
        double rollPerRudder, double pitchPerRudder)
    {
        if (!Finite(command) || !double.IsFinite(rollPerRudder) ||
            !double.IsFinite(pitchPerRudder))
            throw new ArgumentException("Store Trim needs finite controller values.");

        double rudder = Math.Clamp(command.Rudder * 100, -95, 95);
        double autoRoll = Math.Clamp(rudder * rollPerRudder, -95, 95);
        double autoPitch = Math.Clamp(rudder * pitchPerRudder, -95, 95);
        double roll = Math.Clamp(command.Roll * 100 - autoRoll, -95, 95);
        double pitch = Math.Clamp(command.Pitch * 100 - autoPitch, -95, 95);
        var neutral = new AxisVector(Math.Clamp(roll + autoRoll, -95, 95) / 100,
            Math.Clamp(pitch + autoPitch, -95, 95) / 100, rudder / 100);
        bool limited = Math.Abs(neutral.Roll - command.Roll) > 0.000001 ||
            Math.Abs(neutral.Pitch - command.Pitch) > 0.000001 ||
            Math.Abs(neutral.Rudder - command.Rudder) > 0.000001;
        return new(new(roll, pitch, rudder), neutral, limited);
    }

    public void Begin(AxisVector rawPhysical, AxisVector releaseThreshold)
    {
        if (!Finite(rawPhysical) || !Finite(releaseThreshold))
            throw new ArgumentException("Store Trim needs finite controller values.");
        _threshold = new(Math.Clamp(releaseThreshold.Roll, 0.002, 0.30),
            Math.Clamp(releaseThreshold.Pitch, 0.002, 0.30),
            Math.Clamp(releaseThreshold.Rudder, 0.002, 0.30));
        RollWaiting = Math.Abs(rawPhysical.Roll) > _threshold.Roll;
        PitchWaiting = Math.Abs(rawPhysical.Pitch) > _threshold.Pitch;
        RudderWaiting = Math.Abs(rawPhysical.Rudder) > _threshold.Rudder;
    }

    // On each axis, ignore the spring-return motion until the calibrated
    // deadzone is reached. Manual trim may still adjust the held neutral.
    public AxisVector Apply(AxisVector rawPhysical, AxisVector normalCommand,
        AxisVector neutralCommand, bool rollAvailable, bool pitchAvailable, bool rudderAvailable)
    {
        RollWaiting = RollWaiting && rollAvailable && Math.Abs(rawPhysical.Roll) > _threshold.Roll;
        PitchWaiting = PitchWaiting && pitchAvailable && Math.Abs(rawPhysical.Pitch) > _threshold.Pitch;
        RudderWaiting = RudderWaiting && rudderAvailable && Math.Abs(rawPhysical.Rudder) > _threshold.Rudder;
        return new(rollAvailable ? Safe(RollWaiting ? neutralCommand.Roll : normalCommand.Roll) : 0,
            pitchAvailable ? Safe(PitchWaiting ? neutralCommand.Pitch : normalCommand.Pitch) : 0,
            rudderAvailable ? Safe(RudderWaiting ? neutralCommand.Rudder : normalCommand.Rudder) : 0);
    }

    public void Cancel() => RollWaiting = PitchWaiting = RudderWaiting = false;
    public void BeginAxis(int axis, double raw, double threshold)
    {
        threshold = Math.Clamp(threshold, .002, .30);
        if (axis == 0) { _threshold = _threshold with { Roll = threshold }; RollWaiting = Math.Abs(raw) > threshold; }
        if (axis == 1) { _threshold = _threshold with { Pitch = threshold }; PitchWaiting = Math.Abs(raw) > threshold; }
        if (axis == 2) { _threshold = _threshold with { Rudder = threshold }; RudderWaiting = Math.Abs(raw) > threshold; }
    }
    private static double Safe(double x) => double.IsFinite(x) ? Math.Clamp(x, -1, 1) : 0;
    private static bool Finite(AxisVector v) => double.IsFinite(v.Roll) &&
        double.IsFinite(v.Pitch) && double.IsFinite(v.Rudder);
}

// Release is inferred from a stable hold followed by motion toward center.
internal sealed class AutoStoreTrimAxis
{
    private double _held, _command, _since = double.NaN, _last = double.NaN;
    private bool _armed, _waitForCenter;
    public void Reset() { _since = _last = double.NaN; _armed = _waitForCenter = false; }
    public double? Step(double now, double raw, double command, double deadzone, bool enabled)
    {
        if (!enabled || !double.IsFinite(now) || !double.IsFinite(raw) || !double.IsFinite(command))
        { Reset(); return null; }
        // A normal polling hiccup must not erase a hold immediately before the
        // spring-return sample. The previous .25 s gap limit was the same length
        // as the hold-to-arm interval, so a perfectly valid 250-300 ms sample
        // cadence could never arm Auto Store Trim.
        if (double.IsFinite(_last) && (now < _last || now - _last > .75)) Reset();
        _last = now;
        if (Math.Abs(raw) <= Math.Max(.02, deadzone))
        {
            double? captured = _armed && !_waitForCenter ? _command : null;
            Reset(); return captured;
        }
        if (_waitForCenter) return null;
        if (_armed && Math.Abs(raw) < Math.Abs(_held) - .04 && raw * _held >= 0)
        { _armed = false; _waitForCenter = true; return _command; }
        if (_armed && raw * _held > 0 && Math.Abs(raw) <= Math.Abs(_held)) return null;
        if (double.IsNaN(_since) || Math.Abs(raw - _held) > .015)
        { _held = raw; _command = command; _since = now; _armed = false; }
        if (Math.Abs(raw) >= Math.Max(.06, deadzone + .04) && now - _since >= .25)
        { _armed = true; _command = command; }
        return null;
    }
}
