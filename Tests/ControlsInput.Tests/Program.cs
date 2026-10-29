using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using WTVRSettingsAssistant;

internal static class Program
{
    private static int _checks;
    private static readonly DateTime Epoch = new(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); _checks++; }
    private static UInt128 Bit(int button) => (UInt128)1 << (button - 1);
    private static SwitchDefinition Definition(int button = 35, int interval = 100, bool repeat = true, int count = 1) => new()
    {
        Name = "Controls regression", DeviceId = 1, DeviceName = "128-button HOTAS",
        States = [new() { Conditions = [new() { ButtonId = button, RequiredState = true }],
            RepeatWhileHeld = repeat, OutputKind = SwitchActionKind.KeyboardKey,
            Sequence = [new() { Key = "Z", IntervalMs = interval, Repeat = count }] }]
    };

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        ButtonChecks(); TimingChecks(); ServiceChecks(); SharedDeviceReadChecks(); UiChecks(args.Contains("--preview")); TimerCadence();
        if (args.Contains("--native-timing")) NativeTiming();
        Console.WriteLine($"PASS: {_checks} controls input, timing, persistence, cancellation and UI checks.");
    }

    private static void ButtonChecks()
    {
        Check(Marshal.SizeOf<Vortice.DirectInput.RawJoystickState>() == 272 &&
            new Vortice.DirectInput.JoystickState().Buttons.Length == 128, "DirectInput uses the extended 128-button state format");
        for (int button = 1; button <= 128; button++)
        {
            var raw = new bool[128]; raw[button - 1] = true;
            var joystick = new PhysicalJoystick(1, "HOTAS", ControlsJoystickReader.ToMask(raw), true);
            Check(Enumerable.Range(1, 128).Where(joystick.Button).SequenceEqual([button]), $"B{button} has no 32/64-bit alias");
        }
        var all = new PhysicalJoystick(1, "HOTAS", UInt128.MaxValue, true);
        Check(!all.Button(0) && !all.Button(129) && Enumerable.Range(1, 128).All(all.Button), "All 128 buttons and range boundaries");
        var legacy = JsonSerializer.Deserialize<SwitchGestureStep>("{\"Mask\":2147483648,\"ElapsedMs\":30}")!;
        Check(legacy.Buttons == Bit(32), "Original 32-bit gesture JSON remains compatible");
        var gesture = new SwitchGestureStep { Buttons = Bit(1) | Bit(35) | Bit(64) | Bit(96) | Bit(128) };
        Check(JsonSerializer.Deserialize<SwitchGestureStep>(JsonSerializer.Serialize(gesture))!.Buttons == gesture.Buttons,
            "Extended gesture survives JSON round-trip without numeric precision loss");
        Guid guid = Guid.NewGuid();
        var binding = AdvancedSwitchManagerForm.CreateActionSwitch("PTO2", new(2000, "PTO2", Bit(37), true, guid), 37, 1, 1, true);
        Check(binding.PhysicalButton == 37 && binding.DeviceGuid == guid && binding.States[0].Conditions[0].ButtonId == 37,
            "Binding creation retains B37 and stable device identity");
        var positions = FlapPositionCaptureForm.BuildConditions([Bit(35), 0, Bit(37)])!;
        Check(positions.Length == 3 && positions.SelectMany(c => c).All(c => c.ButtonId is 35 or 37), "Capture conditions preserve high buttons");
        var d = Definition(128); d.States[0].Gesture = [new() { Buttons = 0 }, new() { Buttons = Bit(128), ElapsedMs = 0 }];
        var runtime = new SwitchMacroRuntime(); runtime.Step(d, 0, Epoch, 30);
        Check(runtime.Step(d, Bit(128), Epoch.AddMilliseconds(10), 30) == "Z", "Recorded high-button gesture triggers its action");
    }

    private static void TimingChecks()
    {
        var d = Definition(); var engine = new SwitchMacroRuntime();
        engine.Step(d, 0, Epoch, 30); engine.Step(d, Bit(35), Epoch.AddMilliseconds(10), 30);
        var presses = new List<int>();
        for (int ms = 40; ms < 3040; ms += 10)
            if (engine.Step(d, Bit(35), Epoch.AddMilliseconds(ms), 30) is not null) presses.Add(ms);
        Check(presses.Count == 30 && presses.Zip(presses.Skip(1), (a, b) => b - a).All(ms => ms == 100),
            "100 ms produces ten press starts per second beyond the old 20-press limit");
        Check(engine.Step(d, 0, Epoch.AddMilliseconds(3040), 30) is null &&
            engine.Step(d, 0, Epoch.AddMilliseconds(4040), 30) is null, "Physical release cancels repeat before another press");

        foreach (int delay in new[] { 0, 1, 20, 40, 100, 333, 5000 })
        {
            d = Definition(interval: delay, repeat: false, count: 20); engine = new();
            engine.Step(d, 0, Epoch, 30); engine.Step(d, Bit(35), Epoch.AddMilliseconds(10), 30);
            presses.Clear();
            int end = 40 + Math.Max(40, delay) * 21;
            for (int ms = 40; ms <= end; ms += 10)
                if (engine.Step(d, Bit(35), Epoch.AddMilliseconds(ms), 30) is not null) presses.Add(ms);
            Check(presses.Count == 20, $"Fixed count remains exactly 20 for {delay} ms");
            Check(Math.Abs((presses[^1] - presses[0]) / 19.0 - Math.Max(40, delay)) < 1, $"Cadence is start-to-start for {delay} ms");
        }
        d = Definition(); engine = new(); engine.Step(d, 0, Epoch, 30); engine.Step(d, Bit(35), Epoch.AddMilliseconds(10), 30);
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(40), 30) == "Z", "First press after debounce");
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(140), 30, false) is null, "Wait for existing key-up without overlapping it");
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(150), 30) == "Z", "Readiness does not add a second full interval");
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(850), 30) == "Z" &&
            engine.Step(d, Bit(35), Epoch.AddMilliseconds(851), 30) is null, "Late poll skips backlog rather than bursting");
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(2000), 30) is null, "Long polling interruption disarms held repeat");
        engine = new(); engine.Step(d, Bit(35), Epoch, 30);
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(500), 30) is null, "Already held startup never starts spamming");
        engine.Step(d, 0, Epoch.AddMilliseconds(510), 30); engine.Step(d, 0, Epoch.AddMilliseconds(550), 30);
        engine.Step(d, Bit(35), Epoch.AddMilliseconds(560), 30);
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(600), 30) == "Z", "New physical press rearms repeat");
        Check(engine.Step(d, Bit(35) | Bit(128), Epoch.AddMilliseconds(700), 30) == "Z", "Unrelated high button does not interrupt repeat");
        d.States[0].RepeatWhileHeld = false; engine = new();
        engine.Step(d, 0, Epoch, 30); engine.Step(d, Bit(35), Epoch.AddMilliseconds(10), 30);
        Check(engine.Step(d, Bit(35), Epoch.AddMilliseconds(40), 30) == "Z" &&
            engine.Step(d, Bit(35), Epoch.AddMilliseconds(140), 30) is null, "Repeat is opt-in for existing sequences");
    }

    private static void ServiceChecks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "WTA-controls-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "switches.json");
        try
        {
            var time = Epoch; bool focused = true; PhysicalJoystick? device = new(1, "128-button HOTAS", 0, true);
            var keys = new List<Keys>();
            using var service = new AdvancedSwitchService(path, new FakeOutput(), _ => device, () => time, (key, _) => keys.Add(key));
            service.SetFlapTestEnvironment(new FakeTelemetry(), () => focused);
            service.Settings.Enabled = true; var definition = Definition(); service.Settings.Switches.Add(definition);
            void Poll(UInt128 mask, int ms = 40)
            { time = time.AddMilliseconds(ms); device = new(1, "128-button HOTAS", mask, true); service.Poll(); }
            Poll(0); Poll(0); Poll(Bit(35)); Poll(Bit(35));
            Check(keys.Count == 1, "B35 reaches production switch service");
            focused = false; Poll(Bit(35), 200); focused = true; Poll(Bit(35), 200); Poll(Bit(35), 200);
            Check(keys.Count == 1, "Focus loss cancels repeat and refocus while held cannot restart it");
            Poll(0); Poll(0); Poll(Bit(35)); Poll(Bit(35)); Check(keys.Count == 2, "Release and press resumes after refocus");
            device = null; service.Poll(); Poll(Bit(35)); Poll(Bit(35), 200);
            Check(keys.Count == 2, "Disconnect cancels repeat and reconnect while held cannot restart it");
            Poll(0); Poll(0); Poll(Bit(35)); Poll(Bit(35)); service.SetEnabled(false); Poll(Bit(35), 200);
            Check(keys.Count == 3, "Disable cancels held repeat");
            service.SetEnabled(true); Poll(Bit(35)); Poll(Bit(35), 200); Check(keys.Count == 3, "Re-enable requires a fresh physical press");
            service.ResetRuntime(); Poll(0); Poll(0); Poll(Bit(35)); Poll(Bit(35));
            using (service.SuspendFlaps()) { Poll(Bit(35), 200); }
            // Editor foreground is tested by UI checks; ResetRuntime always disarms the macro.
            service.ResetRuntime(); int count = keys.Count; Poll(Bit(35), 200);
            Check(keys.Count == count, "Settings reset drops pending repeated output");
            definition.DeviceGuid = Guid.NewGuid(); definition.States[0].Gesture = [new() { Buttons = Bit(128) | Bit(35) }];
            service.Save();
            using var loaded = new AdvancedSwitchService(path, new FakeOutput(), _ => null, () => time, (_, _) => { });
            var restored = loaded.Settings.Switches[0];
            Check(restored.DeviceGuid == definition.DeviceGuid && restored.States[0].RepeatWhileHeld &&
                restored.States[0].Gesture[0].Buttons == definition.States[0].Gesture[0].Buttons, "Settings retain repeat mode, high gesture bits and device GUID");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static void UiChecks(bool preview)
    {
        foreach (var language in AppText.Languages)
        {
            var row = Definition().States[0];
            using var form = new SwitchSequenceForm(row, language.Code);
            form.CreateControl(); form.PerformLayout();
            Check(form.Controls.Find("RepeatWhileHeld", true).OfType<CheckBox>().Single().Checked, $"Repeat checkbox loads in {language.Code}");
            Check(AppText.T(language.Code, "Switch.SequenceDelay").Contains("5000") &&
                !AppText.T(language.Code, "Switch.SequenceTimingHelp").StartsWith("Switch."), $"Timing help translated in {language.Code}");
            foreach (Size size in new[] { new Size(760, 620), new Size(700, 600) })
            {
                form.ClientSize = size; form.PerformLayout();
                var help = form.Controls.Find("TimingHelp", true).OfType<Label>().Single();
                var measured = TextRenderer.MeasureText(help.Text, help.Font, new Size(help.ClientSize.Width, int.MaxValue),
                    TextFormatFlags.WordBreak);
                Check(measured.Height <= help.ClientSize.Height - help.Padding.Vertical, $"Timing help fits at {size.Width}px in {language.Code}");
            }
            form.ClientSize = new(760, 620);
            if (preview && language.Code == "en")
            {
                form.Show(); Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(Environment.CurrentDirectory, "artifacts", "controls-sequence-preview.png")); form.Hide();
            }
        }
    }

    private static void SharedDeviceReadChecks()
    {
        string path = Path.Combine(Path.GetTempPath(), "WTA-shared-read-" + Guid.NewGuid() + ".json");
        int reads = 0;
        var state = new PhysicalJoystick(1, "128-button HOTAS", 0, true);
        using var service = new AdvancedSwitchService(path, new FakeOutput(), _ =>
        {
            reads++;
            return state;
        }, () => Epoch);
        service.Settings.Enabled = true;
        service.Settings.Switches.Add(Definition(35));
        service.Settings.Switches.Add(Definition(36));
        service.Poll();
        Check(reads == 1, "Two switch mappings on one HOTAS share one input sample");
        state = new PhysicalJoystick(1, "128-button HOTAS", Bit(35) | Bit(36), true);
        service.Poll();
        Check(reads == 2, "A fresh HOTAS sample is read on the next cycle");
    }

    private static void TimerCadence()
    {
        var clock = Stopwatch.StartNew(); var presses = new List<double>();
        using var service = new AdvancedSwitchService(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), new FakeOutput(),
            _ => new(1, "HOTAS", clock.ElapsedMilliseconds >= 100 ? Bit(35) : 0, true), () => Epoch + clock.Elapsed,
            (_, _) => presses.Add(clock.Elapsed.TotalMilliseconds));
        service.SetFlapTestEnvironment(new FakeTelemetry(), () => true);
        service.Settings.Enabled = true; service.Settings.Switches.Add(Definition());
        using var timer = new System.Windows.Forms.Timer { Interval = 25 };
        timer.Tick += (_, _) => service.Poll(); timer.Start();
        while (clock.ElapsedMilliseconds < 3200) { Application.DoEvents(); Thread.Sleep(1); }
        timer.Stop();
        double average = presses.Count > 1 ? (presses[^1] - presses[0]) / (presses.Count - 1) : 0;
        Check(presses.Count is >= 28 and <= 32 && average is > 90 and < 110,
            $"Production UI timer cadence: count={presses.Count}, mean={average:F1} ms");
        Console.WriteLine($"25 ms Windows UI polling: {presses.Count} scheduled presses; mean interval {average:F1} ms (requested 100 ms; output mocked).");
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    private static void NativeTiming()
    {
        using var form = new Form { Text = "WT Assistant isolated key timing test", Width = 600, Height = 200, TopMost = true };
        var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, Text = "Measuring 100 ms key press intervals..." };
        form.Controls.Add(box); var clock = Stopwatch.StartNew(); var downs = new List<double>(); int ups = 0;
        box.KeyDown += (_, e) => { if (e.KeyCode == Keys.Z) downs.Add(clock.Elapsed.TotalMilliseconds); };
        box.KeyUp += (_, e) => { if (e.KeyCode == Keys.Z) ups++; };
        form.Show(); form.Activate(); SetForegroundWindow(form.Handle); box.Focus(); Application.DoEvents();
        var activation = Stopwatch.StartNew();
        while (GetForegroundWindow() != form.Handle && activation.ElapsedMilliseconds < 500)
        { Application.DoEvents(); Thread.Sleep(5); }
        clock.Restart();
        using var service = new AdvancedSwitchService(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"),
            new FakeOutput(), _ => new(1, "HOTAS", clock.ElapsedMilliseconds > 100 ? Bit(35) : 0, true),
            () => Epoch + clock.Elapsed);
        service.SetFlapTestEnvironment(new FakeTelemetry(), () => GetForegroundWindow() == form.Handle);
        service.Settings.Enabled = true; service.Settings.Switches.Add(Definition());
        while (clock.ElapsedMilliseconds < 3200)
        {
            if (GetForegroundWindow() != form.Handle) throw new Exception("Timing window lost focus; stopped without sending further input.");
            service.Poll(); Application.DoEvents(); Thread.Sleep(5);
        }
        service.ResetRuntime(); Thread.Sleep(80); Application.DoEvents();
        double average = downs.Count > 1 ? (downs[^1] - downs[0]) / (downs.Count - 1) : 0;
        Check(downs.Count >= 28 && downs.Count <= 32 && ups == downs.Count && average is > 90 and < 110,
            $"Native key cadence: down={downs.Count}, up={ups}, mean={average:F1} ms");
        Console.WriteLine($"Windows native input: {downs.Count} distinct press/release pairs; mean interval {average:F1} ms (requested 100 ms).");
    }

    private sealed class FakeOutput : IInputOutput
    {
        public string Name => "Test"; public bool IsConnected => true;
        public void ButtonDown(int id) { } public void ButtonUp(int id) { }
        public void Pulse(int id, int durationMs) { } public void ReleaseAll() { } public void Dispose() { }
    }
    private sealed class FakeTelemetry : IFlapTelemetry
    {
        public FlapSample? Sample => null;
        public void Refresh(DateTime now) { } public void Dispose() { }
    }
}
