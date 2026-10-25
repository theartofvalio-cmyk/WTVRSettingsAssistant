using System.Drawing.Imaging;
using System.Text.Json;
using WTVRSettingsAssistant;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }

    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        SettingsChecks();
        MotionChecks();
        UiChecks();
        Console.WriteLine($"PASS: {_checks} Neck Assistant speed, persistence, motion and localized layout checks.");
    }

    private static void SettingsChecks()
    {
        var legacy = JsonSerializer.Deserialize<NeckAssistSettings>("{\"TransitionSpeed\":100}")!;
        Check(!legacy.UnlockTransitionSpeed && legacy.EffectiveTransitionSpeed == 100, "Existing settings retain the 100% limit");
        Check(new NeckAssistSettings().EffectiveTransitionSpeed == 55, "Default speed is unchanged");
        foreach (int speed in new[] { -20, 0, 1, 55, 100, 101, 200, 300, 999 })
        foreach (bool unlocked in new[] { false, true })
        {
            var settings = new NeckAssistSettings { TransitionSpeed = speed, UnlockTransitionSpeed = unlocked };
            var restored = JsonSerializer.Deserialize<NeckAssistSettings>(JsonSerializer.Serialize(settings))!;
            Check(restored.EffectiveTransitionSpeed == Math.Clamp(speed, 1, unlocked ? 300 : 100), "Persisted speed respects the unlock limit");
        }
    }

    private static void MotionChecks()
    {
        for (int speed = 1; speed <= 100; speed++)
        {
            var p = OpenXrNeckBackend.TransitionParameters(new() { TransitionSpeed = speed });
            Check(Math.Abs(p.Tau - Math.Max(.06f, .52f - speed / 100f * .46f)) < .000001f &&
                Math.Abs(p.YawRate - (80 + speed / 100f * 160)) < .0001f &&
                Math.Abs(p.PitchRate - (60 + speed / 100f * 120)) < .0001f, "Original 1–100% response is preserved");
        }
        var normal = OpenXrNeckBackend.TransitionParameters(new() { TransitionSpeed = 100 });
        var fast = OpenXrNeckBackend.TransitionParameters(new() { TransitionSpeed = 300, UnlockTransitionSpeed = true });
        Check(Math.Abs(normal.Tau / fast.Tau - 3) < .0001f && fast.YawRate == 720 && fast.PitchRate == 540,
            "300% scales response and both rate limits by three");
        Check(OpenXrNeckBackend.TransitionParameters(new() { TransitionSpeed = 300 }) == normal, "Locked backend enforces 100% independently of UI");
        foreach (int hz in new[] { 72, 90, 120 })
        foreach (float target in new[] { -180f, 180f })
        foreach (bool pitch in new[] { false, true })
        {
            float Settle(int speed)
            {
                var p = OpenXrNeckBackend.TransitionParameters(new() { TransitionSpeed = speed, UnlockTransitionSpeed = true });
                float dt = 1f / hz, current = 0, velocity = 0;
                float limit = pitch ? p.PitchRate : p.YawRate;
                int frames = 0;
                while (Math.Abs(current - target) > .01f && frames < hz * 5)
                {
                    float next = OpenXrNeckBackend.Stabilize(current, target, ref velocity, dt, p.Tau, limit);
                    Check(float.IsFinite(next) && Math.Abs(next - current) <= limit * dt + .0001f,
                        "Motion is finite and stays within the selected angular-rate limit");
                    Check(next >= Math.Min(current, target) - .0001f && next <= Math.Max(current, target) + .0001f,
                        "Activation does not overshoot");
                    current = next;
                    frames++;
                }
                Check(frames < hz * 5, "Motion settles on activation");
                return frames * dt;
            }
            Check(Settle(300) < Settle(100) * .5f, "300% completes large rotations substantially faster than 100%");
            // Test the release separately, starting with the active offset.
            float value = target, velocity = 0;
            for (int frame = 0; frame < hz; frame++)
            {
                float next = OpenXrNeckBackend.Stabilize(value, 0, ref velocity, 1f / hz, fast.Tau, pitch ? fast.PitchRate : fast.YawRate);
                Check(float.IsFinite(next) && Math.Abs(next) <= Math.Abs(value), "Boosted release returns smoothly without overshoot");
                value = next;
            }
            Check(Math.Abs(value) < .01f, "Boosted release settles to natural view");
        }
    }

    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>()
        .SelectMany(c => new[] { c }.Concat(Descendants(c)));

    private static void UiChecks()
    {
        string output = Path.GetFullPath("artifacts/neck-speed-preview");
        string fixture = Path.Combine(output, "fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        using var form = new NeckAssistForm(fixture, true);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000);
        form.ShowInTaskbar = false;
        form.Show();
        Application.DoEvents();
        T Find<T>(string name) where T : Control => Descendants(form).OfType<T>().Single(c => c.Name == name);
        var slider = Find<TrackBar>("TransitionSpeedSlider");
        var unlock = Find<CheckBox>("UnlockTransitionSpeed");
        var input = Find<NumericUpDown>("TransitionSpeedInput");
        var simple = Find<RadioButton>("SimpleMovementMode");
        var advanced = Find<RadioButton>("AdvancedMovementMode");
        Check(unlock.Visible && !input.Visible && slider.Maximum == 100, "Simple starts with a checkbox and the normal limit");
        unlock.Checked = true;
        slider.Value = 300;
        string path = Path.Combine(fixture, "Settings", "neck_assist.json");
        var saved = JsonSerializer.Deserialize<NeckAssistSettings>(File.ReadAllText(path))!;
        Check(saved.TransitionSpeed == 300 && saved.UnlockTransitionSpeed, "Simple boost is saved");
        unlock.Checked = false;
        Check(slider.Value == 100 && slider.Maximum == 100 && input.Value == 100, "Unchecking clamps the actual speed and both controls");
        advanced.Checked = true;
        Application.DoEvents();
        Check(!unlock.Visible && input.Visible && input.Maximum == 300, "Advanced shows manual speed entry without the unlock checkbox");
        input.Value = 275;
        Check(slider.Value == 275 && unlock.Checked, "Manual entry updates the slider and unlock state");
        simple.Checked = true;
        Application.DoEvents();
        Check(unlock.Visible && unlock.Checked && slider.Maximum == 300 && slider.Value == 275, "Mode switches preserve the chosen speed");
        foreach (var language in AppText.Languages)
        foreach (int width in new[] { 850, 1500, 3100 })
        foreach (bool simpleMode in new[] { true, false })
        {
            (simpleMode ? simple : advanced).Checked = true;
            form.ClientSize = new Size(width, 900);
            form.AutoScrollPosition = Point.Empty;
            form.ApplyLanguage(language.Code);
            form.PerformLayout();
            Application.DoEvents();
            Control control = simpleMode ? unlock : input;
            Check(control.Visible && control.Right <= control.Parent!.ClientSize.Width && control.Left >= 0,
                $"{language.Code}: speed control fits at width {width}");
            var overlaps = control.Parent!.Controls.Cast<Control>().Where(c => c != control && c.Visible && c.Bounds.IntersectsWith(control.Bounds)).ToArray();
            Check(overlaps.Length == 0, $"{language.Code}: {(simpleMode ? "Simple" : "Advanced")} speed control {control.Bounds} overlaps at width {width}: " +
                string.Join("; ", overlaps.Select(c => $"{c.GetType().Name} {c.Text} {c.Bounds}")));
            if (simpleMode) Check(unlock.Height >= unlock.GetPreferredSize(new Size(unlock.Width, 0)).Height,
                $"{language.Code}: complete checkbox caption fits");
            Check(AppText.T(language.Code, "Neck.UnlockTransitionSpeed") != "Neck.UnlockTransitionSpeed", "Speed label is localized");
            if (language.Code == "en" && width is 850 or 1500)
            {
                form.ScrollControlIntoView(control);
                Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save(Path.Combine(output, $"neck-{(simpleMode ? "simple" : "advanced")}-{width}.png"), ImageFormat.Png);
                using var section = new Bitmap(control.Parent!.Width, control.Parent.Height);
                control.Parent.DrawToBitmap(section, control.Parent.ClientRectangle);
                section.Save(Path.Combine(output, $"neck-{(simpleMode ? "simple" : "advanced")}-{width}-speed.png"), ImageFormat.Png);
            }
        }
        form.Close();
        using var reopened = new NeckAssistForm(fixture, true);
        var restored = JsonSerializer.Deserialize<NeckAssistSettings>(File.ReadAllText(path))!;
        Check(restored.TransitionSpeed == 275 && restored.UnlockTransitionSpeed, "Boost survives reopening the settings");
    }
}
