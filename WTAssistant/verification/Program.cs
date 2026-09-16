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

string sample = "video{\n  driver:t=\"dx12\"\n}\nyunetwork{ curCircuit:t=\"production\" }\nsound{}\n";
string testServer = WarThunderServerConfig.Apply(sample, GameServerChannel.Test);
Check(testServer.Contains("curCircuit:t=\"dev\""), "Test circuit must be dev");
Check(testServer.Contains("isExpertMode:b=yes"), "Test circuit must enable expert mode");
Check(testServer.Contains("webStatusPort:i=23456"), "Test circuit must preserve the requested port");
Check(testServer.StartsWith("video{"), "Content before yunetwork must be preserved");
Check(testServer.EndsWith("sound{}\n"), "Content after yunetwork must be preserved");
Check(WarThunderServerConfig.Detect(testServer) == GameServerChannel.Test, "Test circuit must be detected");
string liveServer = WarThunderServerConfig.Apply(testServer, GameServerChannel.Live);
Check(liveServer.Contains("curCircuit:t=\"production\""), "Live circuit must be production");
Check(!liveServer.Contains("isExpertMode", StringComparison.OrdinalIgnoreCase), "Test-only fields must be removed in Live mode");
Check(WarThunderServerConfig.Detect(liveServer) == GameServerChannel.Live, "Live circuit must be detected");
Check(WarThunderServerConfig.Apply(liveServer, GameServerChannel.Live) == liveServer, "Applying the selected circuit twice must be stable");
Console.WriteLine("PASS: Live/Test yunetwork replacement, preservation, detection and idempotence.");

namespace WTVRSettingsAssistant
{
    internal sealed class NeckCurvePoint { public float Input { get; set; } public float Output { get; set; } }
}
