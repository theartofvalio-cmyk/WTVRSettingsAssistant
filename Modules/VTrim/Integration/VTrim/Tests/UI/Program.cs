using System.Collections;
using System.Reflection;
using System.Text.Json;
using HOTASTrimUtility;

internal static class UiTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type FormType = typeof(Form1);
    private static readonly Type ActionType = FormType.GetNestedType("TrimAction", BindingFlags.NonPublic)!;
    private static int _passed;

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        try
        {
            using var form = (Form1)Activator.CreateInstance(FormType, Private, null, new object?[] { true, null, true }, null)!;
            form.MinimumSize = Size.Empty;
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.ShowInTaskbar = false;
            form.Show(); Application.DoEvents();
            form.SetOutputEnabled(true);
            Yes(form.OutputEnabled, "Enabled preference is independent of temporary connection failure");
            Yes(!(bool)Get(form, "_manualVJoyDisconnect")!, "Enable permits automatic reconnect");
            Set(form, "_vJoyConnected", true);
            form.ApplyLanguage("en");
            Yes(((Label)Get(form, "_vJoyStatusLabel")!).Text == "vJoy Device 1 connected: X, Y and Rz output is active.", "Language refresh preserves connected status");
            Yes(((Button)Get(form, "_vJoyConnectButton")!).Text == "Disconnect", "Language refresh preserves disconnect command");
            form.SetOutputEnabled(false);
            Yes(!form.OutputEnabled && !form.OutputConnected, "Disable clears preference and disconnects output");
            Yes((bool)Get(form, "_manualVJoyDisconnect")!, "Disable blocks reconnect");
            Yes(!((System.Windows.Forms.Timer)Get(form, "_vJoyAutoConnectTimer")!).Enabled, "Disable stops reconnect timer");
            foreach (Size size in new[] { new Size(1080, 1120), new Size(1000, 820), new Size(1400, 1000) })
            {
                form.Size = size; form.PerformLayout(); Application.DoEvents();
                Button store = (Button)Get(form, "_storeTrimButton")!;
                Button instructor = (Button)Get(form, "_instructorModeButton")!;
                Yes(store.Visible && instructor.Visible, "Both top actions visible");
                Yes(store.Text == "Store Trim", "Store label restored");
                Yes(instructor.Text.Contains("ON") || instructor.Text.Contains("OFF"), "Instructor label includes state");
                Yes(store.Width >= 110 && instructor.Width >= 110, "Action buttons have readable widths");
                var bindingButtons = (IDictionary)Get(form, "_bindingButtons")!;
                var storeBinding = (Button)bindingButtons[Enum.Parse(ActionType, "StoreCurrentTrim")]!;
                var centerBinding = (Button)bindingButtons[Enum.Parse(ActionType, "ResetAll")]!;
                Yes(storeBinding.Parent == centerBinding.Parent && storeBinding.Left == centerBinding.Left && storeBinding.Width == centerBinding.Width && storeBinding.Top > centerBinding.Bottom,
                    "Store binding is centered directly beneath Center All");
                var rudderPreview = (Control)Get(form, "_rudderPreview")!;
                Yes(rudderPreview.Height >= 24 && rudderPreview.Top >= 0, "Rudder preview retains usable height");
                var assist = (CheckBox)Get(form, "_horizontalRudderAssistBox")!;
                assist.Checked = true;
                Yes(assist.Text.Contains("ON") && assist.Height >= 24, "Horizontal assist can be enabled without clipping");
                assist.Checked = false;
                Yes(assist.Text.Contains("OFF"), "Horizontal assist can be disabled");
            }
            Set(form, "_instructorModeEnabled", false);
            Set(form, "_lastPhysicalRoll", .2); Set(form, "_lastPhysicalPitch", -.3); Set(form, "_lastPhysicalRudder", .1);
            Set(form, "_rollInputAvailable", true); Set(form, "_pitchInputAvailable", true); Set(form, "_rudderInputAvailable", true);
            Set(form, "_lastAxisSnapshotTime", Now());
            Type vector = FormType.Assembly.GetType("HOTASTrimUtility.AxisVector")!;
            Set(form, "_currentManualCommand", Activator.CreateInstance(vector, new object[] { .2, -.3, .1 })!);
            Action(form, "StoreCurrentTrim");
            Near((double)Get(form, "_rollTrim")!, 20, "Store captures Roll");
            Near((double)Get(form, "_pitchTrim")!, -30, "Store captures Pitch");
            Yes(!(bool)Get(form, "_instructorModeEnabled")!, "Store does not enable Instructor");
            Action(form, "ToggleInstructor");
            Yes((bool)Get(form, "_instructorModeEnabled")!, "Instructor toggles separately");
            Near((double)Get(form, "_rollTrim")!, 20, "Instructor toggle does not recapture trim");

            Set(form, "_horizontalRudderCalibrationArmed", true);
            Set(form, "_lastPhysicalRudder", .5); Set(form, "_lastPhysicalRoll", .2); Set(form, "_lastPhysicalPitch", .1);
            Action(form, "StoreCurrentTrim");
            Near((double)Get(form, "_rollTrim")!, 20, "Calibration does not store new trim");
            Yes((bool)Get(form, "_instructorModeEnabled")!, "Calibration does not toggle Instructor");
            Yes(!(bool)Get(form, "_horizontalRudderCalibrationArmed")!, "Calibration capture disarms once");
            Action(form, "ResetAll");
            Near((double)Get(form, "_rollTrim")!, 0, "Center All clears offsets");
            Yes((bool)Get(form, "_instructorModeEnabled")!, "Center All preserves independent Instructor setting");

            Type profileType = FormType.GetNestedType("SavedBindingsFile", BindingFlags.NonPublic)!;
            const string profile = """{"Version":7,"ProfileName":"Test","InstructorModeEnabled":false,"Bindings":{"StoreCurrentTrim":{"Trigger":{"DeviceGuid":"11111111-1111-1111-1111-111111111111","DeviceName":"Test","Kind":"Button","Index":0,"PovDirection":"Center"}}}}""";
            object model = JsonSerializer.Deserialize(profile, profileType)!;
            Invoke(form, "ApplySavedProfile", model);
            var bindings = (IDictionary)Get(form, "_bindings")!;
            Yes(bindings.Contains(Enum.Parse(ActionType, "ToggleInstructor")), "1.3.0 combined binding migrates to Instructor");
            Yes(!bindings.Contains(Enum.Parse(ActionType, "StoreCurrentTrim")), "Migration does not duplicate a dangerous combo");
            Yes(!(bool)Get(form, "_instructorModeEnabled")!, "Migration preserves saved OFF preference");
            const string legacyInstructor = """{"Version":10,"AircraftType":"Prop Plane","InstructorModeEnabled":true,"GameTrimKey":84,"Instructor":{"PitchDirection":1,"Strength":100}}""";
            Invoke(form, "ApplySavedProfile", JsonSerializer.Deserialize(legacyInstructor, profileType)!);
            object migratedTuning = Get(form, "_instructorTuning")!;
            Yes((int)migratedTuning.GetType().GetProperty("PitchDirection")!.GetValue(migratedTuning)! == 1,
                "Old profiles use the standard vJoy direction");
            Yes((int)migratedTuning.GetType().GetProperty("AttitudeHoldStrength")!.GetValue(migratedTuning)! == 100,
                "Legacy Instructor uses the new default hold strength");

            // Exercise the real form output integration with synthetic samples.
            // The driver is not acquired and ProcessLiveAxes is never invoked.
            Set(form, "_instructorModeEnabled", true); Set(form, "_vJoyConnected", true);
            Set(form, "_rollInputAvailable", true); Set(form, "_pitchInputAvailable", true);
            object controller = Get(form, "_instructorController")!;
            Type sampleType = FormType.Assembly.GetType("HOTASTrimUtility.AircraftSample")!;
            double now = Now();
            object Sample(double time, double angle) => Activator.CreateInstance(sampleType,
                new object[] { time, "synthetic", angle, angle, 250d, 0d, 270d, false, false })!;
            controller.GetType().GetMethod("Accept")!.Invoke(controller, new[] { Sample(now - .10, 0) });
            controller.GetType().GetMethod("Accept")!.Invoke(controller, new[] { Sample(now, 1) });
            object[] step = { 0d, 0d, 0d, true, true, true, 0d, 0d, 0d };
            FormType.GetMethod("ApplyAutomaticInstructorMode", Private)!.Invoke(form, step);
            Yes((double)step[6] == 0 && (double)step[7] == 0, "Instructor never injects damping; unfocused game passes manual axes through");
            Near((double)step[8], 0, "Missing yaw instruments do not create yaw correction");
            object pitchTuning = Get(form, "_instructorTuning")!;
            pitchTuning.GetType().GetProperty("PitchDirection")!.SetValue(pitchTuning, 1);
            object[] HoldStep(double time, double physical = 0, bool focused = true, bool available = true)
            {
                object[] values = { time, focused, physical, available, .27, physical, -.43 };
                FormType.GetMethod("ApplyAutomaticInstructorModeCore", Private)!.Invoke(form, values);
                return values;
            }
            Set(form, "_pitchManualUntil", 0d);
            object[] held = HoldStep(now + .015);
            Yes((double)held[5] < 0, "Actual form path applies nose-up-rate damping to elevator");
            var livePanel = (Control)Get(form, "_instructorLive")!;
            Set(form, "_lastAxisSnapshotTime", Now());
            using (var meter = new Bitmap(livePanel.Width, livePanel.Height))
            {
                using var graphics = Graphics.FromImage(meter);
                Invoke(form, "PaintInstructorLive", livePanel, new PaintEventArgs(graphics, livePanel.ClientRectangle));
                int rowHeight = Math.Max(28, form.Font.Height + 12);
                int barY = rowHeight + (rowHeight - 25) / 2 + 12;
                int goldPixels = 0;
                for (int x = livePanel.Width / 3; x < livePanel.Width * 3 / 4; x++)
                    if (meter.GetPixel(x, barY).ToArgb() == Color.Gold.ToArgb()) goldPixels++;
                Yes(goldPixels > 0, "Correction meter paints a visible gold bar from real controller output");
            }
            Near((double)held[4], .27, "Pitch Instructor never changes roll output");
            Near((double)held[6], -.43, "Pitch Instructor never changes inverted rudder output");
            held = HoldStep(now + .03, .45);
            Near((double)held[5], .45, "Actual form path gives immediate manual pitch authority");
            Yes(controller.GetType().GetProperty("TargetPitch")!.GetValue(controller) is null, "Manual takeover clears target");
            HoldStep(now + .045);
            held = HoldStep(now + .06, 0, false);
            Near((double)held[5], 0, "Focus loss clears elevator correction");
            Yes(controller.GetType().GetProperty("TargetPitch")!.GetValue(controller) is null, "Focus loss clears target");
            HoldStep(now + .075);
            held = HoldStep(now + .09, 0, true, false);
            Near((double)held[5], 0, "Pitch controller disconnect clears correction");
            HoldStep(now + .105);
            Set(form, "_vJoyConnected", false);
            held = HoldStep(now + .12);
            Near((double)held[5], 0, "Virtual output disconnect clears correction");
            Yes(controller.GetType().GetProperty("TargetPitch")!.GetValue(controller) is null, "Virtual output disconnect clears target");

            var typeBox = (ComboBox)Get(form, "_profileTypeBox")!;
            Set(form, "_lastInstructorUiUpdate", double.NegativeInfinity);
            typeBox.SelectedItem = "Jet Plane"; Invoke(form, "UpdateInstructorModeUi");
            Yes((bool)Get(form, "_instructorModeEnabled")!, "Jet profiles support Flight Assistant");
            Action(form, "ToggleInstructor");
            Yes(!(bool)Get(form, "_instructorModeEnabled")!, "HOTAS toggle disables Flight Assistant for jets");
            Set(form, "_lastInstructorUiUpdate", double.NegativeInfinity);
            typeBox.SelectedItem = "Helicopter"; Invoke(form, "UpdateInstructorModeUi");
            Yes(!((Control)Get(form, "_instructorModeButton")!).Visible, "Helicopter profiles hide Instructor");
            typeBox.SelectedItem = "Prop Plane"; Invoke(form, "UpdateInstructorModeUi");
            Set(form, "_instructorModeEnabled", true);
            object tuning = Get(form, "_instructorTuning")!;
            tuning.GetType().GetProperty("AttitudeHoldStrength")!.SetValue(tuning, 63);
            tuning.GetType().GetProperty("PitchDirection")!.SetValue(tuning, -1);
            object captured = Invoke(form, "CaptureCurrentProfile")!;
            Yes((int)profileType.GetProperty("Version")!.GetValue(captured)! == 11, "New profile schema is version 11");
            Invoke(form, "ApplySavedProfile", captured);
            tuning = Get(form, "_instructorTuning")!;
            Yes((int)tuning.GetType().GetProperty("AttitudeHoldStrength")!.GetValue(tuning)! == 63, "Pitch hold strength roundtrips");
            Yes((int)tuning.GetType().GetProperty("PitchDirection")!.GetValue(tuning)! == 1, "Legacy direction is normalized to the standard mapping");
            Yes((bool)Get(form, "_instructorModeEnabled")!, "Prop profile preserves Instructor preference");
            string output = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "ui-checks");
            Directory.CreateDirectory(output);
            static IEnumerable<Control> Descendants(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Descendants(x)));
            foreach (Size size in new[] { new Size(1080, 820), new Size(1500, 1000), new Size(1900, 1100) })
            {
                form.ClientSize = size; Application.DoEvents();
                foreach (string page in new[] { "Trim Dashboard", "Devices & Output", "Profiles", "Axis Curves" })
                {
                    var button = Descendants(form).OfType<Button>().FirstOrDefault(x => x.Text == page);
                    if (button is null) continue;
                    button.PerformClick(); Application.DoEvents();
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(output, page.Replace(" & ", "-").Replace(" ", "-") + "-" + size.Width + ".png"));
                }
            }
            using (var captureDialog = new System.Windows.Forms.Timer { Interval = 200 })
            {
                captureDialog.Tick += (_, _) =>
                {
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text.StartsWith("Flight Assistant ·"));
                    if (dialog is null) return;
                    captureDialog.Stop();
                    Yes(Descendants(dialog).OfType<TrackBar>().Count() == 10, "All three axes expose their settings");
                    Yes(!Descendants(dialog).OfType<TextBox>().Any(box => box.ReadOnly), "Old read-only game trim key capture is removed");
                    using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(output, "instructor-setup.png"));
                    dialog.DialogResult = DialogResult.Cancel;
                };
                captureDialog.Start(); Invoke(form, "ShowInstructorSettings");
            }
            using (var icons = new Bitmap(660, 250))
            using (var g = Graphics.FromImage(icons))
            {
                g.Clear(Color.FromArgb(29,31,33));
                var draw = FormType.Assembly.GetType("HOTASTrimUtility.AircraftIcons")!.GetMethod("Draw")!;
                int x = 0;
                foreach (string type in new[] { "Prop Plane", "Jet Plane", "Helicopter" })
                {
                    draw.Invoke(null, new object[] { g, new Rectangle(x + 20, 10, 180, 180), type, Color.FromArgb(235,238,240) });
                    using var font = new Font("Segoe UI", 15);
                    g.DrawString(type, font, Brushes.White, x + 40, 210); x += 220;
                }
                icons.Save(Path.Combine(output, "aircraft-icons.png"));
            }
            Console.WriteLine($"PASS: {_passed} UI/integration assertions. Hardware and network were not started.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static object? Get(object o, string name) => FormType.GetField(name, Private)!.GetValue(o);
    private static void Set(object o, string name, object value) => FormType.GetField(name, Private)!.SetValue(o, value);
    private static object? Invoke(object o, string name, params object[] args) => FormType.GetMethod(name, Private)!.Invoke(o, args);
    private static void Action(object o, string name) => Invoke(o, "ApplyTrimAction", Enum.Parse(ActionType, name));
    private static double Now() => (double)FormType.GetProperty("MonotonicSeconds", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    private static void Yes(bool result, string name) { if (!result) throw new Exception(name); _passed++; Console.WriteLine("PASS " + name); }
    private static void Near(double actual, double expected, string name) => Yes(Math.Abs(actual - expected) < 1e-8, name);
}
