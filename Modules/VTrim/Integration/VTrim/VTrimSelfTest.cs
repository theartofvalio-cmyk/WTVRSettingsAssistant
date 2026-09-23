namespace HOTASTrimUtility;

/// <summary>
/// Hardware-independent regression checks for the VTrim control algorithms.
/// These tests intentionally do not touch DirectInput, vJoy or WinForms so they
/// can run on a clean Windows CI machine without HOTAS hardware attached.
/// </summary>
public static class VTrimSelfTest
{
    private sealed class OfflineAircraftProvider : IAircraftDataProvider
    {
        public Task<IReadOnlyList<AircraftInfo>> FetchAsync(CancellationToken token) =>
            throw new HttpRequestException("Offline test");
    }

    public static IReadOnlyList<string> Run()
    {
        List<string> failures = new();
        void Check(bool condition, string name)
        {
            if (!condition) failures.Add(name);
        }
        static bool Finite(double value) => double.IsFinite(value) && value is >= -1.000001 and <= 1.000001;

        try
        {
            foreach (string language in new[] { "en", "bg", "es", "de", "fr", "pt", "pl", "ru", "uk", "tr", "el", "ro", "he", "zh-Hans" })
            foreach (string key in new[] { "Profiles.Section.TrimKeybinds", "Profiles.Section.AxisCurves", "Profiles.Section.FlightLocked" })
                Check(VTrimTranslations.TryGet(language, key, out string? label) &&
                      !string.IsNullOrWhiteSpace(label) && label != key,
                    $"Profile section heading {language}/{key}");

            failures.AddRange(Form1.RunProfileStorageSelfTests());

            // Telemetry profiles created while the index loads must later find
            // the correct artwork and category for any helicopter family.
            string aircraftRoot = Path.Combine(Path.GetTempPath(), "WTA-aircraft-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(aircraftRoot, "Aircraft", "Icons"));
                AircraftInfo[] examples =
                [
                    new("ah_1w", "AH-1W", "usa", "Helicopter", new(), null, FlightCategory: "Helicopter"),
                    new("mi_24v", "Mi-24V", "ussr", "Helicopter", new(), null, FlightCategory: "Helicopter"),
                    new("f_16a", "F-16A", "usa", "Fighter", new(), null, FlightCategory: "Jet Plane")
                ];
                File.WriteAllText(Path.Combine(aircraftRoot, "Aircraft", "aircraft-index.json"),
                    System.Text.Json.JsonSerializer.Serialize(examples, AircraftDatabaseService.JsonOptions));
                using (var icon = new System.Drawing.Bitmap(2, 2))
                    icon.Save(Path.Combine(aircraftRoot, "Aircraft", "Icons", "mi_24v.png"));
                var database = new AircraftDatabaseService(aircraftRoot, new OfflineAircraftProvider());
                database.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                Check(database.ResolveProfile(null, "ah_1w")?.FlightCategory == "Helicopter" &&
                      database.ResolveProfile(null, "mi_24v")?.FlightCategory == "Helicopter" &&
                      database.ResolveProfile(null, "f_16a")?.FlightCategory == "Jet Plane",
                    "Telemetry profiles resolve aircraft across helicopter families");
                using var http = new HttpClient(new OfflineIconHandler());
                var cache = new AircraftAssetCache(aircraftRoot, http);
                string? iconPath = cache.GetIconAsync(database.ResolveProfile(null, "mi_24v")!, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(iconPath is not null && File.Exists(iconPath), "Resolved helicopter uses cached artwork offline");
            }
            finally { Directory.Delete(aircraftRoot, recursive: true); }

            AxisResponse linear = new() { Curve = 0, Deadzone = 0, InputRange = 100, OutputRange = 100 };
            Check(Math.Abs(linear.Apply(.5) - .5) < .0001, "AxisResponse linear mapping");

            AxisResponse normalized = new() { Curve = 999, Deadzone = double.NaN, InputRange = 1, OutputRange = 500 };
            normalized.Normalize();
            Check(normalized.Curve == 100 && normalized.Deadzone is >= 0 and <= 30 &&
                  normalized.InputRange is >= 40 and <= 100 && normalized.OutputRange is >= 5 and <= 100,
                "AxisResponse normalization");

            TrimCapture capture = StoreTrimController.SolveCapture(new AxisVector(.20, -.10, .30), .20, -.10);
            Check(Finite(capture.NeutralCommand.Roll) && Finite(capture.NeutralCommand.Pitch) &&
                  Finite(capture.NeutralCommand.Rudder), "Store Trim finite capture");

            StoreTrimController store = new();
            store.Begin(new AxisVector(.4, -.3, .2), new AxisVector(.02, .02, .02));
            AxisVector held = store.Apply(new AxisVector(.3, -.2, .1), new AxisVector(0, 0, 0),
                new AxisVector(.4, -.3, .2), true, true, true);
            Check(Math.Abs(held.Roll - .4) < .0001 && Math.Abs(held.Pitch + .3) < .0001,
                "Store Trim spring-return hold");
            AxisVector released = store.Apply(new AxisVector(0, 0, 0), new AxisVector(.05, -.04, .03),
                new AxisVector(.4, -.3, .2), true, true, true);
            Check(Math.Abs(released.Roll - .05) < .0001 && Math.Abs(released.Pitch + .04) < .0001,
                "Store Trim release to normal command");

            InstructorController instructor = new();
            instructor.Accept(new AircraftSample(1.00, "TEST", 0, 5, 250));
            instructor.Accept(new AircraftSample(1.05, "TEST", 0, 5, 250));
            PitchHoldOutput pitch = instructor.Step(1.05, 0, 0, true, true, new InstructorTuning());
            Check(Finite(pitch.Elevator) && pitch.Status == "PITCH HOLD", "Instructor pitch-hold logic");

            LateralAssistController lateral = new();
            AxisAssistTuning lateralTuning = new() { Enabled = true, Strength = 30, MaximumCorrection = 5, CentreDeadzone = 3 };
            LateralOutput roll = lateral.Step(2.00, 0, 0, true, true,
                new LateralSample(2.00, "TEST", 250, 5, 0), true, lateralTuning);
            Check(Finite(roll.Output) && roll.Status == "WINGS LEVEL", "Lateral roll-assist logic");

            AutoStoreTrimAxis auto = new();
            _ = auto.Step(0.00, .50, .40, .02, true);
            _ = auto.Step(0.30, .50, .40, .02, true);
            double? captured = auto.Step(0.31, .40, .35, .02, true);
            Check(captured.HasValue && Math.Abs(captured.Value - .40) < .0001, "Auto Store Trim release capture");

            var xbox = XInputGamepad.CreateStateForTest(12000, -8000, 5000, -4000, 128, 255, 0x1000 | 0x0001);
            Check(xbox.X == 12000 && xbox.Y == -8000 && xbox.RotationX == 5000 && xbox.RotationY == -4000 &&
                  xbox.Z is > 32000 and < 34000 && xbox.RotationZ == 65535 && xbox.Buttons[0] &&
                  xbox.PointOfViewControllers[0] == 0, "Xbox / XInput controller mapping");
        }
        catch (Exception ex)
        {
            failures.Add("VTrim self-test threw: " + ex.GetType().Name + ": " + ex.Message);
        }

        return failures;
    }

    private sealed class OfflineIconHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            throw new HttpRequestException("Cached icon should avoid the network");
    }
}
