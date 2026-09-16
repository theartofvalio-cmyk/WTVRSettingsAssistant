using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WTVRSettingsAssistant;

internal static class NativeOutputChecks
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    public static void Run()
    {
        if (Process.GetProcessesByName("aces").Length != 0 || Process.GetProcessesByName("WTVRSettingsAssistant").Length != 0)
            throw new Exception("Close game and app before the isolated native-output test.");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var window = new Form { Text = "Keyboard output verification", Width = 480, Height = 180, TopMost = true };
                var box = new TextBox { Dock = DockStyle.Fill, Multiline = true };
                var events = new List<string>();
                box.KeyDown += (_, e) => events.Add("down:" + e.KeyCode);
                box.KeyUp += (_, e) => events.Add("up:" + e.KeyCode);
                window.Controls.Add(box);
                window.Show(); window.Activate(); box.Focus(); Application.DoEvents();
                foreach (var key in new[] { Keys.O, Keys.I, Keys.G })
                {
                    if (GetForegroundWindow() != window.Handle) throw new Exception("Test window is not foreground; no keys sent.");
                    KeyboardSwitchOutput.Tap(key, "en");
                    var until = Stopwatch.StartNew();
                    while (until.ElapsedMilliseconds < 200) { Application.DoEvents(); Thread.Sleep(5); }
                }
                if (!events.SequenceEqual(new[] { "down:O", "up:O", "down:I", "up:I", "down:G", "up:G" }))
                    throw new Exception("Unexpected native key sequence: " + string.Join(",", events));
                Console.WriteLine("PASS: Windows received O, I and G key-down/key-up events through the production sender.");
                box.Clear();
                PhysicalJoystick? physical = new(1, "Test", 0, true);
                using (var service = new AdvancedSwitchService(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"),
                    new FakeOutput(), _ => physical, () => DateTime.UtcNow))
                {
                    service.Settings.Enabled = true;
                    var conditions = FlapPositionCaptureForm.BuildConditions([1u, 0u, 2u])!;
                    service.Settings.Switches.Add(new SwitchDefinition
                    {
                        AdvancedFlaps = AdvancedFlapsChecks.Settings(),
                        States = conditions.Select(c => new SwitchStateDefinition { Conditions = c }).ToList()
                    });
                    void Poll(uint mask, int count)
                    {
                        for (int n = 0; n < count; n++)
                        {
                            if (GetForegroundWindow() != window.Handle) throw new Exception("Test window lost foreground; stopping.");
                            physical = new(1, "Test", mask, true); service.Poll();
                            Application.DoEvents(); Thread.Sleep(25);
                        }
                    }
                    Poll(0, 2); Poll(2, 3); Poll(0, 1); Poll(1, 30); Poll(0, 35);
                    if (box.Text.ToLowerInvariant() != "ioooii") throw new Exception("Down-up-centre typed: " + box.Text);
                    box.Clear(); service.ResetRuntime();
                    Poll(0, 2); Poll(1, 3); Poll(0, 1); Poll(2, 30); Poll(0, 35);
                    if (box.Text.ToLowerInvariant() != "oiiioo") throw new Exception("Up-down-centre typed: " + box.Text);
                }
                Console.WriteLine("PASS: Full flap service typed iooo + ii and oiii + oo into a real Windows text box without game or telemetry.");
                window.Close();

                string settings = Path.Combine(Path.GetTempPath(), "vtrim-output-test-" + Guid.NewGuid());
                using (var trim = new HOTASTrimUtility.Form1(settings, true))
                {
                    trim.Show(); Application.DoEvents();
                    if (!trim.OutputEnabled || !trim.OutputConnected) throw new Exception("Enabled VTrim did not connect on startup.");
                    trim.SetOutputEnabled(false);
                    if (trim.OutputEnabled || trim.OutputConnected) throw new Exception("Disabled VTrim retained output.");
                    trim.Close();
                }
                using (var trim = new HOTASTrimUtility.Form1(settings, true))
                {
                    trim.Show(); Application.DoEvents();
                    if (trim.OutputEnabled || trim.OutputConnected) throw new Exception("VTrim did not preserve disabled preference.");
                    if (!trim.SetOutputEnabled(true) || !trim.OutputConnected) throw new Exception("VTrim could not reconnect after enabling.");
                    trim.SetOutputEnabled(false); trim.Close();
                }
                Console.WriteLine("PASS: Real vJoy connected at enabled startup, disconnected on disable, preserved OFF on restart, and reconnected on enable.");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
