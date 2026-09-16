namespace HOTASTrimUtility;

internal sealed class AxisAssistTuning
{
    public bool Enabled { get; set; }
    public int Strength { get; set; } = 30;
    public int MaximumCorrection { get; set; } = 5;
    public int CentreDeadzone { get; set; } = 3;
}

internal readonly record struct LateralSample(double Time, string Aircraft, double Airspeed, double? Bank, double? Slip);
internal readonly record struct LateralOutput(double Output, double Correction, string Status);

internal sealed class LateralAssistController
{
    private double _time = double.NaN, _correction;
    private string? _aircraft;
    private int _sign;
    private bool _paused;
    private readonly Queue<double> _reversals = new();

    public void Reset()
    {
        _time = double.NaN; _correction = 0; _sign = 0; _paused = false;
        _aircraft = null; _reversals.Clear();
    }

    public LateralOutput Step(double now, double physical, double manual, bool enabled,
        bool available, LateralSample? sample, bool roll, AxisAssistTuning tuning)
    {
        double safe = double.IsFinite(manual) ? Math.Clamp(manual, -1, 1) : 0;
        double? angle = roll ? sample?.Bank : sample?.Slip;
        string? reason = !enabled || !tuning.Enabled ? "OFF" : !available ? "CHECK AXIS" :
            !double.IsFinite(now) || !double.IsFinite(physical) || !double.IsFinite(manual) ? "INVALID INPUT" :
            sample is null || now < sample.Value.Time || now - sample.Value.Time > .30 ||
            angle is null || !double.IsFinite(angle.Value) ? "NO TELEMETRY" :
            sample.Value.Airspeed < 90 ? "LOW AIRSPEED" :
            Math.Abs(physical) > Math.Clamp(tuning.CentreDeadzone, 0, 20) / 100d ? "MANUAL" :
            Math.Abs(angle.Value) > (roll ? 60 : 15) ? "OUTSIDE ASSIST RANGE" : null;
        if (reason is not null) { Reset(); return new(safe, 0, reason); }
        if (_aircraft != sample!.Value.Aircraft) Reset();
        _aircraft = sample.Value.Aircraft;
        if (_paused) return new(safe, 0, "PAUSED - UNSTABLE");
        double dt = double.IsFinite(_time) ? now - _time : .015;
        _time = now;
        if (dt < 0 || dt > .20) { Reset(); return new(safe, 0, "REACQUIRING"); }
        double authority = Math.Clamp(Math.Pow(250 / sample.Value.Airspeed, 2), .08, 1);
        double limit = Math.Clamp(tuning.MaximumCorrection, 0, 50) / 100d * authority;
        double measured = angle.GetValueOrDefault();
        double error = Math.CopySign(Math.Max(0, Math.Abs(measured) - (roll ? 1 : .3)), measured);
        // Bank is right-wing-down positive. Positive sideslip needs right rudder.
        double wanted = (roll ? -1 : 1) * error * (roll ? .008 : .015) *
            Math.Clamp(tuning.Strength, 0, 400) / 100d * authority;
        wanted = Math.Clamp(wanted, -limit, limit);
        _correction = Math.Clamp(_correction + Math.Clamp((wanted - _correction) *
            (1 - Math.Exp(-dt / .15)), -.20 * dt, .20 * dt), -limit, limit);
        int sign = Math.Abs(_correction) > .002 ? Math.Sign(_correction) : 0;
        if (sign != 0)
        {
            if (_sign != 0 && sign != _sign) _reversals.Enqueue(now);
            _sign = sign;
        }
        while (_reversals.Count > 0 && now - _reversals.Peek() > 2) _reversals.Dequeue();
        if (_reversals.Count >= 4)
        {
            _paused = true; _correction = 0;
            return new(safe, 0, "PAUSED - UNSTABLE");
        }
        double output = Math.Clamp(safe + _correction, -1, 1);
        return new(output, output - safe, roll ? "WINGS LEVEL" : "SLIP ASSIST");
    }
}
