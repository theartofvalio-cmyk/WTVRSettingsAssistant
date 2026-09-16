using HOTASTrimUtility;

internal static class CoreTests
{
    private static int _passed, _failed;
    private const string Instruments = """{"valid":true,"type":"test_plane","aviahorizon_pitch":-12}""";
    private const string State = """{"valid":true,"IAS, km/h":250,"pitch 1, deg":45,"Wx, deg/s":123}""";
    private static void Main()
    {
        Test("Auto store captures held command once on release", () =>
        {
            var c = new AutoStoreTrimAxis();
            Yes(c.Step(1, .4, .55, .02, true) is null);
            Yes(c.Step(1.26, .4, .55, .02, true) is null);
            Yes(c.Step(1.27, .4, .55, .02, true) is null);
            Yes(c.Step(1.48, .4, .55, .02, true) is null);
            Yes(c.Step(1.54, .4, .55, .02, true) is null);
            Yes(c.Step(1.55, .38, .53, .02, true) is null);
            Near(c.Step(1.56, .34, .49, .02, true)!.Value, .55);
            Yes(c.Step(1.57, .2, .4, .02, true) is null);
            Yes(c.Step(1.58, 0, .55, .02, true) is null);
            Yes(c.Step(1.59, 0, .55, .02, true) is null);
        });
        Test("Auto store rejects noise and disabled captures", () =>
        {
            var c = new AutoStoreTrimAxis();
            c.Step(1, .01, .01, .02, true);
            Yes(c.Step(1.1, 0, 0, .02, true) is null);
            c.Step(2, .5, .5, .02, true); c.Step(2.15, .5, .5, .02, true);
            c.Step(2.28, .5, .5, .02, true);
            Yes(c.Step(2.29, .1, .1, .02, false) is null);
            Yes(c.Step(2.3, 0, 0, .02, true) is null);
        });
        Test("Axis store leaves unrelated return states alone", () =>
        {
            var c = new StoreTrimController(); c.BeginAxis(0, .3, .02);
            c.BeginAxis(2, -.4, .02);
            Yes(c.RollWaiting && !c.PitchWaiting && c.RudderWaiting);
            var o = c.Apply(new(.2, .1, -.2), new(.2, .1, -.2), new(.3, 0, -.4), true, true, true);
            Near(o.Roll, .3); Near(o.Pitch, .1); Near(o.Rudder, -.4);
        });
        Test("Axis settings persist independently", () =>
        {
            var t = new InstructorTuning { PitchEnabled = false,
                RollAssist = new AxisAssistTuning { Enabled = true, Strength = 42 },
                RudderAssist = new AxisAssistTuning { Enabled = false, MaximumCorrection = 3 } };
            var copy = System.Text.Json.JsonSerializer.Deserialize<InstructorTuning>(System.Text.Json.JsonSerializer.Serialize(t))!;
            Yes(!copy.PitchEnabled && copy.RollAssist.Enabled && !copy.RudderAssist.Enabled);
            Yes(copy.RollAssist.Strength == 42 && copy.RudderAssist.MaximumCorrection == 3);
        });
        Test("Missing lateral signals never produce corrections", () =>
        {
            var t = new AxisAssistTuning { Enabled = true };
            var c = new LateralAssistController();
            Near(c.Step(1, 0, .2, true, true, new(1, "test", 250, null, 2), true, t).Correction, 0);
            Near(c.Step(1, 0, .2, true, true, new(1, "test", 250, 10, null), false, t).Correction, 0);
        });
        Test("Lateral telemetry independent of pitch availability", () =>
        {
            var s = WarThunderTelemetry.ParseLateral("""{"valid":true,"type":"test","aviahorizon_roll":-10}""",
                """{"valid":true,"IAS, km/h":250,"AoS, deg":2}""", 1);
            Yes(s.HasValue); Near(s!.Value.Bank!.Value, 10); Near(s.Value.Slip!.Value, 2);
            Yes(WarThunderTelemetry.ParseLateral("{}", "{}", 1) is null);
        });
        Test("Lateral assist respects defaults, manual takeover and telemetry gates", () =>
        {
            foreach (bool roll in new[] { true, false })
            {
                var c = new LateralAssistController(); var t = new AxisAssistTuning();
                var s = new LateralSample(1, "test", 250, 10, 2);
                Near(c.Step(1, 0, 0, true, true, s, roll, t).Correction, 0);
                t.Enabled = true;
                var o = c.Step(1.01, 0, 0, true, true, s, roll, t);
                Yes(roll ? o.Correction < 0 : o.Correction > 0);
                Yes(Math.Abs(o.Correction) <= .003);
                Near(c.Step(1.02, .5, .5, true, true, s, roll, t).Output, .5);
                Near(c.Step(1.03, 0, .2, false, true, s, roll, t).Output, .2);
                Near(c.Step(1.04, 0, .2, true, false, s, roll, t).Correction, 0);
                Near(c.Step(2, 0, .2, true, true, s, roll, t).Correction, 0);
                Near(c.Step(2.01, 0, .2, true, true, null, roll, t).Correction, 0);
            }
        });
        Test("Rapid correction reversals trip the oscillation guard", () =>
        {
            var c = Ready(); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
            bool paused = false;
            for (int i = 1; i <= 200; i++)
            {
                double now = .1 + i * .01;
                if (i % 5 == 0) c.Accept(new(now, "test", 0, 12 + 3 * Math.Sin(i * .01 * 16), 250));
                var o = c.Step(now, 0, 0, true, true, t);
                if (o.Status.Contains("UNSTABLE")) { paused = true; Near(o.Correction, 0); break; }
            }
            Yes(paused);
        });
        Test("High-speed authority and correction slew remain bounded at maximum settings", () =>
        {
            var c = new InstructorController(); var t = Tuning();
            c.Accept(new(0, "jet", 0, 0, 826)); c.Accept(new(.05, "jet", 0, 1, 826));
            double last = 0;
            for (int i = 1; i < 100; i++)
            {
                double now = .05 + i * .01;
                if (i % 5 == 0) c.Accept(new(now, "jet", 0, 1 + Math.Sin(now * 10), 826));
                var o = c.Step(now, 0, 0, true, true, t);
                Yes(Math.Abs(o.Correction) <= .20 * Math.Pow(250d / 826, 2) + 1e-8);
                if (i > 1 && o.Correction != 0) Yes(Math.Abs(o.Correction - last) <= .0035 + 1e-8);
                last = o.Correction;
            }
        });
        Test("Divergent hold pauses until manual takeover", () =>
        {
            var c = Ready(); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
            c.Accept(new(.15, "test", 0, 26, 250));
            var o = c.Step(.15, 0, 0, true, true, t);
            Yes(o.Status.Contains("UNSTABLE")); Near(o.Correction, 0);
            Yes(c.Step(.16, 0, 0, true, true, t).Status.Contains("UNSTABLE"));
            Near(c.Step(.17, .4, .4, true, true, t).Elevator, .4);
            Yes(c.Step(.18, 0, 0, true, true, t).Status == "PITCH HOLD");
        });
        Test("No telemetry starts smoothing without a jump", () =>
        {
            var c = new InstructorController();
            var o = c.Step(1, .4, .4, true, true, Tuning());
            Near(o.Elevator, .4); Near(o.Correction, 0);
            Yes(o.Status == "INPUT SMOOTHING - NO PITCH TELEMETRY" && o.TargetPitch is null);
            o = c.Step(1.015, .5, .5, true, true, Tuning());
            Yes(o.Elevator < .5 && o.Elevator >= .42 && o.Correction < 0);
        });
        Test("Repeated missing telemetry preserves smoothing and settles", () =>
        {
            var c = new InstructorController(); var t = Tuning();
            c.Step(1, 0, 0, true, true, t);
            var o = c.Step(1.015, .1, .1, true, true, t);
            c.Invalidate();
            var next = c.Step(1.03, .1, .1, true, true, t);
            Yes(next.Elevator > o.Elevator && next.Elevator < .1);
            for (int i = 3; i < 100; i++) next = c.Step(1 + i * .015, .1, .1, true, true, t);
            Near(next.Elevator, .1);
        });
        Test("Fallback yields immediately when disabled or disconnected", () =>
        {
            foreach (bool disconnected in new[] { false, true })
            {
                var c = new InstructorController(); var t = Tuning();
                c.Step(1, 0, 0, true, true, t); c.Step(1.015, 1, 1, true, true, t);
                var o = c.Step(1.03, -.5, -.5, disconnected, !disconnected, t);
                Near(o.Elevator, -.5); Near(o.Correction, 0);
            }
        });
        Test("Telemetry recovery captures a new target after fallback", () =>
        {
            var c = Ready(); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
            c.Invalidate(); c.Step(.115, 0, 0, true, true, t);
            Yes(c.TargetPitch is null);
            c.Accept(new(.12, "test", 0, 20, 250)); c.Accept(new(.15, "test", 0, 20, 250));
            var o = c.Step(.15, 0, 0, true, true, t);
            Near(o.TargetPitch!.Value, 20); Near(o.Correction, 0); Yes(o.Status == "PITCH HOLD");
        });
        Test("Pitch uses horizon, not engine pitch or Wx", () =>
        {
            Yes(WarThunderTelemetry.TryParse(Instruments, State, 1, out var s));
            Near(s.Pitch, 12);
        });
        Test("Missing pitch fails closed", () => Yes(!WarThunderTelemetry.TryParse(
            Instruments.Replace("aviahorizon_pitch", "unknown"), State, 1, out _)));
        Test("Roll and rudder instruments not required", () => Yes(WarThunderTelemetry.TryParse(Instruments, State, 1, out _)));
        Test("Invalid state clears flight validity", () => Yes(!WarThunderTelemetry.TryParse(Instruments, State.Replace("true", "false"), 1, out _)));
        Test("Invalid indicators rejected", () => Yes(!WarThunderTelemetry.TryParse(Instruments.Replace("true", "false"), State, 1, out _)));
        Test("Malformed telemetry rejected", () => Yes(!WarThunderTelemetry.TryParse("{bad", State, 1, out _)));
        Test("Array root rejected", () => Yes(!WarThunderTelemetry.TryParse("[]", State, 1, out _)));
        Test("Nonfinite time rejected", () => Yes(!WarThunderTelemetry.TryParse(Instruments, State, double.NaN, out _)));
        Test("Out of range pitch rejected", () => Yes(!WarThunderTelemetry.TryParse(Instruments.Replace("-12", "120"), State, 1, out _)));
        Test("Missing IAS rejected", () => Yes(!WarThunderTelemetry.TryParse(Instruments, State.Replace("IAS, km/h", "unknown"), 1, out _)));
        Test("Manual stick is never opposed", () =>
        {
            var c = Ready(); var o = c.Step(.11, .45, .45, true, true, Tuning());
            Near(o.Elevator, .45); Near(o.Correction, 0); Yes(o.TargetPitch is null);
        });
        Test("Release captures current climb angle without delay", () =>
        {
            var c = Ready(); var t = Tuning();
            c.Step(.1, .45, .45, true, true, t);
            var o = c.Step(.115, 0, 0, true, true, t);
            Near(o.TargetPitch!.Value, 12); Near(o.Correction, 0);
        });
        Test("Nose above target produces forward correction", () =>
        {
            var c = Ready(); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
            c.Accept(new(.15, "test", 0, 13, 250));
            var o = c.Step(.15, 0, 0, true, true, t);
            Yes(o.Correction < 0); Near(o.TargetPitch!.Value, 12);
        });
        Test("Nose below target produces back correction", () =>
        {
            var c = Ready(); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
            c.Accept(new(.15, "test", 0, 11, 250));
            Yes(c.Step(.15, 0, 0, true, true, t).Correction > 0);
        });
        Test("Rate damping starts on release even at zero attitude error", () =>
        {
            var c = Ready(10, 12); var o = c.Step(.1, 0, 0, true, true, Tuning());
            Near(o.TargetPitch!.Value, 12); Yes(o.Correction < 0); Near(o.PitchRate, 20);
        });
        Test("Immediate manual takeover clears filtered correction and target", () =>
        {
            var c = Ready(10, 12); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
            var o = c.Step(.115, .041, .041, true, true, t);
            Near(o.Correction, 0); Near(o.Elevator, .041); Yes(o.TargetPitch is null);
            c.Accept(new(.15, "test", 0, 14, 250));
            Near(c.Step(.15, 0, 0, true, true, t).TargetPitch!.Value, 14);
        });
        Test("Tiny attitude and rate noise produces no correction", () =>
        {
            var c = Ready(12, 12.005); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
            c.Accept(new(.15, "test", 0, 12.008, 250));
            Near(c.Step(.15, 0, 0, true, true, t).Correction, 0);
        });
        Test("Independent automatic and final output limits", () =>
        {
            var c = Ready(-10, 12); var t = Tuning(); t.SmoothingMilliseconds = 0; t.MaximumCorrection = 30;
            var o = c.Step(.1, 0, -.9, true, true, t);
            Yes(o.Correction < 0 && Math.Abs(o.Correction) <= .35 * .015);
            Near(o.Elevator, -.9 + o.Correction);
        });
        Test("Smoothing starts immediately and limits first response", () =>
        {
            var c = Ready(10, 12); var t = Tuning();
            t.SmoothingMilliseconds = 200;
            var o = c.Step(.1, 0, 0, true, true, t);
            Yes(o.Correction < 0 && o.Correction > -.05);
        });
        Test("Legacy direction uses standard vJoy convention", () =>
        {
            var c = Ready(10, 12); var t = Tuning(); t.PitchDirection = -1;
            Yes(c.Step(.1, 0, 0, true, true, t).Correction < 0);
        });
        Test("Fresh and previously unverified profiles apply correction", () =>
        {
            var c = Ready(10, 12); var o = c.Step(.1, 0, .12, true, true, new());
            Yes(o.Correction < 0); Near(o.Elevator, .12 + o.Correction); Yes(o.TargetPitch is not null);
            var legacy = new InstructorTuning { PitchDirection = 0 };
            Yes(Ready(10, 12).Step(.1, 0, 0, true, true, legacy).Correction < 0);
        });
        foreach (string failure in new[] { "disabled", "disconnected", "stale", "invalid", "low speed", "zero max", "zero strength", "gap" })
            Test("Fail-safe clears target: " + failure, () =>
            {
                var c = Ready(10, 12); var t = Tuning(); c.Step(.1, 0, 0, true, true, t);
                if (failure == "invalid") c.Invalidate();
                if (failure == "low speed") c.Accept(new(.15, "test", 0, 12, 20));
                if (failure == "zero max") t.MaximumCorrection = 0;
                if (failure == "zero strength") t.AttitudeHoldStrength = t.PitchDampingStrength = 0;
                if (failure == "gap") c.Accept(new(.35, "test", 0, 12, 250));
                var o = c.Step(failure == "stale" ? 1 : failure == "gap" ? .35 : .15,
                    0, .2, failure != "disabled", failure != "disconnected", t);
                Near(o.Correction, 0); Near(o.Elevator, .2); Yes(o.TargetPitch is null);
            });
        Test("Aircraft change drops old target and rate history", () =>
        {
            var c = Ready(); c.Step(.1, 0, 0, true, true, Tuning());
            c.Accept(new(.15, "another", 0, 20, 250));
            Yes(!c.Ready(.15)); Yes(c.TargetPitch is null);
        });
        Test("Duplicate timestamps do not change rate", () =>
        {
            var c = Ready(10, 12); c.Accept(new(.1, "test", 0, 60, 250));
            Near(c.PitchRate, 20);
        });
        Test("Nonfinite command fails safe", () =>
        {
            var o = Ready().Step(.1, double.NaN, double.NaN, true, true, Tuning());
            Near(o.Elevator, 0); Yes(o.TargetPitch is null);
        });
        Test("Synthetic pitch dynamics settle near captured climb angle", () =>
        {
            var c = Ready(); var t = Tuning();
            double pitch = 12, rate = 0, elevator = 0;
            c.Step(.1, 0, 0, true, true, t);
            for (int n = 1; n <= 1000; n++)
            {
                double now = .1 + n * .01;
                if (n == 10) rate = 4;
                rate += (40 * elevator - .8 * rate) * .01;
                pitch += rate * .01;
                if (n % 5 == 0) c.Accept(new(now, "test", 0, pitch, 250));
                elevator = c.Step(now, 0, 0, true, true, t).Elevator;
                Yes(Math.Abs(elevator) <= .35);
            }
            Yes(Math.Abs(pitch - 12) < .5); Yes(Math.Abs(rate) < .2);
        });
        Test("Store capture independent axes", () =>
        {
            var c = StoreTrimController.SolveCapture(new(.2, -.3, .1), 0, 0);
            Near(c.ManualTrimPercent.Roll, 20); Near(c.ManualTrimPercent.Pitch, -30);
            Near(c.ManualTrimPercent.Rudder, 10); Yes(!c.Limited);
        });
        Test("Store solves existing horizontal roll/pitch assist", () =>
        {
            var c = StoreTrimController.SolveCapture(new(.2, -.3, .5), -.4, .2);
            Near(c.ManualTrimPercent.Roll, 40); Near(c.ManualTrimPercent.Pitch, -40);
            Near(c.NeutralCommand.Roll, .2); Near(c.NeutralCommand.Pitch, -.3);
        });
        Test("Store holds command during spring return", () =>
        {
            var c = new StoreTrimController(); c.Begin(new(.2, -.3, .1), new(.018, .018, .018));
            var o = c.Apply(new(.2, -.3, .1), new(.4, -.6, .2), new(.2, -.3, .1), true, true, true);
            Near(o.Roll, .2); Near(o.Pitch, -.3); Near(o.Rudder, .1); Yes(c.AnyWaiting);
        });
        Test("Store reactivates each axis separately", () =>
        {
            var c = new StoreTrimController(); c.Begin(new(.2, -.3, .1), new(.018, .018, .018));
            c.Apply(new(0, -.2, .1), new(.2, -.5, .2), new(.2, -.3, .1), true, true, true);
            Yes(!c.RollWaiting && c.PitchWaiting && c.RudderWaiting);
            var o = c.Apply(new(.1, -.2, .1), new(.3, -.5, .2), new(.2, -.3, .1), true, true, true);
            Near(o.Roll, .3); Near(o.Pitch, -.3);
        });
        Test("Store no doubling when all axes centered", () =>
        {
            var c = new StoreTrimController(); c.Begin(new(.2, -.3, .1), new(.018, .018, .018));
            var o = c.Apply(default, new(.2, -.3, .1), new(.2, -.3, .1), true, true, true);
            Near(o.Roll, .2); Yes(!c.AnyWaiting);
        });
        Test("Disconnected axis cancels stored hold", () =>
        {
            var c = new StoreTrimController(); c.Begin(new(.4, 0, 0), new(.02, .02, .02));
            var o = c.Apply(new(.4, 0, 0), new(.8, 0, 0), new(.4, 0, 0), false, true, true);
            Near(o.Roll, 0); Yes(!c.AnyWaiting);
        });
        Test("Reset cancels all stored holds", () =>
        {
            var c = new StoreTrimController(); c.Begin(new(.2, .3, .4), new(.02, .02, .02)); c.Cancel(); Yes(!c.AnyWaiting);
        });
        Test("Trim limit reported instead of claiming exact capture", () =>
        {
            var c = StoreTrimController.SolveCapture(new(1, -1, 1), -1, 1); Yes(c.Limited);
            Yes(Math.Abs(c.ManualTrimPercent.Roll) <= 95);
        });
        Test("Neutral capture does not require recenter", () =>
        { var c = new StoreTrimController(); c.Begin(default, new(.018, .018, .018)); Yes(!c.AnyWaiting); });
        Test("Nonfinite capture rejected", () =>
        {
            bool rejected = false;
            try { StoreTrimController.SolveCapture(new(double.NaN, 0, 0), 0, 0); }
            catch (ArgumentException) { rejected = true; } Yes(rejected);
        });
        Test("Default curve center and extremes", () =>
        { var c = new AxisResponse(); Near(c.Apply(0), 0); Near(c.Apply(1), 1); Near(c.Apply(-1), -1); });
        Test("Curve deadzone", () => Near(new AxisResponse().Apply(.01), 0));
        Test("Curve monotonicity for all curve values", () =>
        {
            for (int curve = -100; curve <= 100; curve += 5)
            {
                var c = new AxisResponse { Curve = curve }; double previous = -1;
                for (int n = -100; n <= 100; n++)
                { double v = c.Apply(n / 100.0); Yes(v + 1e-12 >= previous); previous = v; }
            }
        });
        Test("Output range precedes trim", () => Near(new AxisResponse { OutputRange = 50 }.Apply(1), .5));
        Test("Store capture uses curved rather than raw deflection", () =>
        {
            var response = new AxisResponse { Curve = 80 };
            double shaped = response.Apply(.5);
            var c = StoreTrimController.SolveCapture(new(shaped, 0, 0), 0, 0);
            Near(c.NeutralCommand.Roll, shaped); Yes(c.ManualTrimPercent.Roll < 50);
        });

        Console.WriteLine($"\n{_passed} passed, {_failed} failed.");
        Environment.ExitCode = _failed == 0 ? 0 : 1;
    }
    private static InstructorTuning Tuning() => new() { PitchDirection = 1 };
    private static InstructorController Ready(double first = 12, double second = 12)
    {
        var c = new InstructorController();
        c.Accept(new(0, "test", 0, first, 250)); c.Accept(new(.1, "test", 0, second, 250));
        return c;
    }
    private static void Test(string name, Action action)
    {
        try { action(); _passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Yes(bool condition) { if (!condition) throw new Exception("Condition failed"); }
    private static void Near(double actual, double expected)
    { if (!double.IsFinite(actual) || Math.Abs(actual - expected) > 1e-8) throw new Exception($"Expected {expected}; got {actual}"); }
}
