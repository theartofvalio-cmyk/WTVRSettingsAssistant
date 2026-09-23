namespace HOTASTrimUtility;

internal sealed class AxisCurvePoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public AxisCurvePoint() { }
    public AxisCurvePoint(double x, double y) { X = x; Y = y; }
}

internal sealed class AxisResponse
{
    public int Curve { get; set; }
    public double Deadzone { get; set; } = 1.8;
    public double InputRange { get; set; } = 100;
    public double OutputRange { get; set; } = 100;

    // Optional user-defined points for the positive half of the response curve.
    // The negative half is mirrored so roll/pitch/rudder stay centered and
    // predictable. X/Y are normalized 0..1 after deadzone/input-range scaling.
    public List<AxisCurvePoint> CustomPoints { get; set; } = new();

    public bool HasCustomCurve => CustomPoints is { Count: > 0 };

    public void Normalize()
    {
        Curve = Math.Clamp(Curve, -100, 100);
        Deadzone = Math.Clamp(double.IsFinite(Deadzone) ? Deadzone : 1.8, 0, 30);
        InputRange = Math.Clamp(double.IsFinite(InputRange) ? InputRange : 100, 40, 100);
        OutputRange = Math.Clamp(double.IsFinite(OutputRange) ? OutputRange : 100, 5, 100);
        if (InputRange <= Deadzone + 5) InputRange = Math.Min(100, Deadzone + 5);

        CustomPoints ??= new();
        var normalized = new List<AxisCurvePoint>(CustomPoints.Count);
        foreach (AxisCurvePoint? point in CustomPoints.OrderBy(p => p?.X ?? 0))
        {
            if (point is null || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
            double x = Math.Clamp(point.X, 0.015, 0.985);
            double y = Math.Clamp(point.Y, 0.0, 1.0);
            point.X = x;
            point.Y = y;
            if (normalized.Count > 0 && Math.Abs(normalized[^1].X - x) < 0.008)
            {
                normalized[^1].Y = y;
                continue;
            }
            // Keep the original point object. The editor holds this reference
            // while dragging; recreating every point on each mouse move made
            // handles feel slippery and easy to lose.
            normalized.Add(point);
        }

        // Axis response should remain monotonic; prevent custom points from
        // producing a reversed control direction between neighboring handles.
        double previousY = 0;
        foreach (AxisCurvePoint point in normalized)
        {
            point.Y = Math.Max(previousY, point.Y);
            previousY = point.Y;
        }
        for (int i = normalized.Count - 1; i >= 0; i--)
        {
            double nextY = i == normalized.Count - 1 ? 1.0 : normalized[i + 1].Y;
            normalized[i].Y = Math.Min(normalized[i].Y, nextY);
        }
        CustomPoints = normalized;
    }

    public void ClearCustomCurve() => CustomPoints.Clear();

    public int EstimateEquivalentCurve()
    {
        if (!HasCustomCurve) return Curve;
        int best = Curve;
        double bestError = double.PositiveInfinity;
        for (int candidate = -100; candidate <= 100; candidate++)
        {
            double error = 0;
            for (int i = 1; i < 40; i++)
            {
                double x = i / 40.0;
                double expected = EvaluateCustomShape(x);
                double actual = ParametricShape(x, candidate / 100.0);
                double delta = expected - actual;
                error += delta * delta;
            }
            if (error < bestError)
            {
                bestError = error;
                best = candidate;
            }
        }
        return best;
    }

    public double Apply(double input)
    {
        if (!double.IsFinite(input)) return 0;
        double deadzone = Math.Clamp(double.IsFinite(Deadzone) ? Deadzone / 100 : 0.018, 0, 0.30);
        double range = Math.Clamp(double.IsFinite(InputRange) ? InputRange / 100 : 1, deadzone + 0.05, 1);
        double output = Math.Clamp(double.IsFinite(OutputRange) ? OutputRange / 100 : 1, 0.05, 1);
        double x = Math.Clamp((Math.Abs(input) - deadzone) / (range - deadzone), 0, 1);
        double y = HasCustomCurve ? EvaluateCustomShape(x) : ParametricShape(x, Math.Clamp(Curve / 100.0, -1, 1));
        return Math.Sign(input) * y * output;
    }

    private static double ParametricShape(double x, double curvature)
    {
        x = Math.Clamp(x, 0, 1);
        double c = Math.Clamp(curvature, -1, 1);
        return c >= 0
            ? (1 - c) * x + c * x * x * x
            : 1 - ((1 + c) * (1 - x) - c * Math.Pow(1 - x, 3));
    }

    private double EvaluateCustomShape(double x)
    {
        x = Math.Clamp(x, 0, 1);
        if (!HasCustomCurve) return ParametricShape(x, Math.Clamp(Curve / 100.0, -1, 1));

        int n = CustomPoints.Count + 2;
        double[] xs = new double[n];
        double[] ys = new double[n];
        xs[0] = 0; ys[0] = 0;
        for (int i = 0; i < CustomPoints.Count; i++)
        {
            xs[i + 1] = CustomPoints[i].X;
            ys[i + 1] = CustomPoints[i].Y;
        }
        xs[^1] = 1; ys[^1] = 1;

        int segment = 0;
        while (segment < n - 2 && x > xs[segment + 1]) segment++;
        double h = Math.Max(0.000001, xs[segment + 1] - xs[segment]);
        double t = Math.Clamp((x - xs[segment]) / h, 0, 1);

        double[] slopes = new double[n - 1];
        for (int i = 0; i < slopes.Length; i++)
            slopes[i] = (ys[i + 1] - ys[i]) / Math.Max(0.000001, xs[i + 1] - xs[i]);

        double[] tangent = new double[n];
        tangent[0] = slopes[0];
        tangent[^1] = slopes[^1];
        for (int i = 1; i < n - 1; i++)
        {
            if (slopes[i - 1] <= 0 || slopes[i] <= 0)
            {
                tangent[i] = 0;
                continue;
            }
            double hPrev = xs[i] - xs[i - 1];
            double hNext = xs[i + 1] - xs[i];
            double w1 = 2 * hNext + hPrev;
            double w2 = hNext + 2 * hPrev;
            tangent[i] = (w1 + w2) / (w1 / slopes[i - 1] + w2 / slopes[i]);
        }

        double t2 = t * t, t3 = t2 * t;
        double h00 = 2 * t3 - 3 * t2 + 1;
        double h10 = t3 - 2 * t2 + t;
        double h01 = -2 * t3 + 3 * t2;
        double h11 = t3 - t2;
        double value = h00 * ys[segment] + h10 * h * tangent[segment] +
                       h01 * ys[segment + 1] + h11 * h * tangent[segment + 1];
        return Math.Clamp(value, 0, 1);
    }
}
