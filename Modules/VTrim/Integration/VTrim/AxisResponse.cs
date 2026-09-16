namespace HOTASTrimUtility;

internal sealed class AxisResponse
{
    public int Curve { get; set; }
    public double Deadzone { get; set; } = 1.8;
    public double InputRange { get; set; } = 100;
    public double OutputRange { get; set; } = 100;

    public void Normalize()
    {
        Curve = Math.Clamp(Curve, -100, 100);
        Deadzone = Math.Clamp(double.IsFinite(Deadzone) ? Deadzone : 1.8, 0, 30);
        InputRange = Math.Clamp(double.IsFinite(InputRange) ? InputRange : 100, 40, 100);
        OutputRange = Math.Clamp(double.IsFinite(OutputRange) ? OutputRange : 100, 5, 100);
    }

    public double Apply(double input)
    {
        if (!double.IsFinite(input)) return 0;
        double deadzone = Math.Clamp(double.IsFinite(Deadzone) ? Deadzone / 100 : 0.018, 0, 0.30);
        double range = Math.Clamp(double.IsFinite(InputRange) ? InputRange / 100 : 1, deadzone + 0.05, 1);
        double output = Math.Clamp(double.IsFinite(OutputRange) ? OutputRange / 100 : 1, 0.05, 1);
        double x = Math.Clamp((Math.Abs(input) - deadzone) / (range - deadzone), 0, 1);
        double c = Math.Clamp(Curve / 100.0, -1, 1);
        // Blend linear and cubic response. Negative curvature expands the
        // center response using the mirrored function. Both are monotonic.
        double y = c >= 0 ? (1 - c) * x + c * x * x * x :
            1 - ((1 + c) * (1 - x) - c * Math.Pow(1 - x, 3));
        return Math.Sign(input) * y * output;
    }
}
