using WTVRSettingsAssistant;

var curve = new BezierNeckCurve();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
foreach (var (pivot, maximum, limit) in new[] { (45, 180, 110), (30, 100, 80), (79, 140, 80) })
{
    curve.Validate();
    Check(Math.Abs(curve.Evaluate(0, pivot, maximum, limit, 100)) < .001, "Centre must remain zero");
    Check(Math.Abs(curve.Evaluate(pivot, pivot, maximum, limit, 100) - pivot) < .001, "Pivot must be continuous");
    Check(curve.Evaluate(limit, pivot, maximum, limit, 100) == maximum, "Endpoint must match slider");
    float previous = -1;
    for (float angle = 0; angle <= limit; angle += .1f)
    {
        float value = curve.Evaluate(angle, pivot, maximum, limit, 100);
        Check(value >= previous - .001f, "Curve must remain monotonic");
        Check(Math.Abs(value + curve.Evaluate(-angle, pivot, maximum, limit, 100)) < .001, "Directions must mirror");
        previous = value;
    }
}
curve.Knots.Add(new() { Input = .5f, Output = .8f });
Check(Math.Abs(curve.Evaluate(77.5f, 45, 180, 110, 100) - 112.5f) < .001, "Legacy points must not affect slider-only mapping");
Check(curve.Evaluate(20, 45, 180, 110, 100) == 20, "No amplification before activation");
Check(Math.Abs(curve.Evaluate(20, 45, 180, 110, 0) - 20) < .001, "Zero strength must be linear");
string json = System.Text.Json.JsonSerializer.Serialize(curve);
var restored = System.Text.Json.JsonSerializer.Deserialize<BezierNeckCurve>(json)!;
Check(Math.Abs(restored.Evaluate(20,45,180,110,100) - curve.Evaluate(20,45,180,110,100)) < .001, "Handles must persist");
Console.WriteLine("PASS: centre, activation, endpoints, monotonicity, mirroring, legacy points ignored, linear blend, persistence.");

namespace WTVRSettingsAssistant
{
    internal sealed class NeckCurvePoint { public float Input { get; set; } public float Output { get; set; } }
}
