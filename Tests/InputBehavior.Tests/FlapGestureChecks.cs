using WTVRSettingsAssistant;

internal static class FlapGestureChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
        var detector = new FlapGestureDetector(); var now = DateTime.UtcNow;
        AdvancedFlapMode Observe(AdvancedFlapMode p, int ms = 40)
        { now = now.AddMilliseconds(ms); return detector.Observe(p, now, 30); }
        var up = AdvancedFlapMode.Combat; var down = AdvancedFlapMode.AutoTakeoffLanding; var centre = AdvancedFlapMode.Raised;
        Check(Observe(down) == centre, "Starting at an endpoint must not deploy");
        Observe(centre, 10); Observe(up, 10);
        Check(Observe(up) == up, "DOWN CENTER UP flick activates Combat request");
        Check(Observe(up) == up, "Endpoint hold preserves gesture mode");
        Check(Observe(centre) == up, "Transit center preserves mode during grace period");
        Check(Observe(centre, 360) == centre, "Held CENTER retracts");
        Observe(up); Observe(centre, 10); Observe(down, 10);
        Check(Observe(down) == down, "UP CENTER DOWN flick selects landing");
        Observe(centre); Check(Observe(centre, 360) == centre, "CENTER clears landing");
        Observe(down); Observe(centre); Observe(centre, 400); Observe(up); Check(Observe(up) == centre, "Slow switch move does not deploy");
        detector.Reset(); Observe(down); Observe(centre, 10); Observe(down, 10); Check(Observe(down) == centre, "Same-end bounce is not a gesture");
        detector.Reset(); Observe(down); Observe(up, 25); Check(Observe(up) == up, "Fast flick detects neutral crossed between polls");
        detector.Reset(); Observe(up); Observe(down, 25); Check(Observe(down) == down, "Reverse fast flick detects neutral crossed between polls");
        detector.Reset(); Observe(down); Observe(up, 200); Check(Observe(up) == centre, "Long polling gap cannot invent a flick");
        detector.Reset(); Observe(down); Observe(up, 10); Observe(down, 10);
        Check(Observe(down, 10) == centre, "Unsettled endpoint cannot commit a gesture");
        detector.Reset(); Observe(up); Observe(centre); Observe(down); Observe(down);
        detector.Reset(); Check(Observe(down) == centre, "Reset cannot replay gesture");

        var settings = AdvancedFlapsChecks.Settings();
        var controller = new AdvancedFlapsController();
        List<FlapCommand> Stay(double percent, AdvancedFlapMode mode, double speed = 180, int samples = 10)
        {
            var result = new List<FlapCommand>();
            for (int i = 0; i < samples; i++)
            {
                now = now.AddMilliseconds(100);
                var sample = new FlapSample(now, "plane", percent, speed, null, null, null);
                if (controller.Step(now, mode, sample, settings) is { } command) result.Add(command);
            }
            return result;
        }
        Check(Stay(0, up).SequenceEqual([FlapCommand.Down]), "First step requires no profile");
        Check(Stay(18, up).Count == 0, "First observed step stops deployment");
        Check(Stay(18, up, 1340).Count == 0, "Speed cannot retract Combat request");
        Check(Stay(18, up, double.NaN).Count == 0, "Missing IAS cannot invalidate flap feedback");
        Stay(18, up); Check(Stay(18, centre).SequenceEqual([FlapCommand.Up]), "CENTER retracts regardless of aircraft mapping");
        Stay(0, centre);
        Check(Stay(0, down, 1340).SequenceEqual([FlapCommand.Down]), "Landing gesture deploys independently of IAS");
        Check(Stay(18, down, 240).SequenceEqual([FlapCommand.Down]), "Landing checks telemetry then continues");
        Check(Stay(43, down, 240).SequenceEqual([FlapCommand.Down]), "Landing traverses next stage singly");
        Check(Stay(100, down, 240, 20).Count == 0, "Landing stops at full flap");
        Check(Stay(100, down, 1340).Count == 0, "Speed cannot retract Landing request");
        Check(Stay(100, centre, 1340).SequenceEqual([FlapCommand.Up]), "Only raised request retracts landing flaps");
        controller.Reset(); Stay(0, up);
        Check(Stay(0, up, samples: 30).Count == 2, "Missed commands use spaced retries");
        Check(controller.Step(now.AddSeconds(3), up, null, settings) is null && !controller.Pending, "Missing telemetry stops output");
        controller.Reset(); Stay(0, up); controller.FailOutput();
        Check(Stay(0, up, samples: 50).Count == 0, "Output fault does not flood");
        Check(FlapPositionCaptureForm.BuildConditions([1u << 22, 0, 1u << 23]) is { Length: 3 }, "Two-button switch capture valid");
        Check(FlapPositionCaptureForm.BuildConditions([1, 1, 2]) is null, "Duplicate capture rejected");
        Check(AdvancedFlapsTelemetry.Parse("{\"valid\":true,\"flaps, %\":18}",
            "{\"valid\":true,\"army\":\"air\",\"type\":\"test\"}", now) is { Percent: 18 }, "Flap feedback does not require IAS");
        Console.WriteLine($"PASS: {count} flick gesture and calibration-free flap checks.");
        return count;
    }
}
