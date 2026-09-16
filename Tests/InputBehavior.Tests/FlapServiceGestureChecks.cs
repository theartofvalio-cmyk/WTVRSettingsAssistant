using WTVRSettingsAssistant;
using System.Windows.Forms;

internal static class FlapServiceGestureChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool valid, string label) { if (!valid) throw new Exception(label); checks++; }
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var keys = new List<Keys>();
        var time = DateTime.UtcNow;
        PhysicalJoystick? physical = new(1, "HOTAS", 0, true);
        using var service = new AdvancedSwitchService(path, new FakeOutput(), _ => physical, () => time,
            (key, _) => keys.Add(key));
        // No game focus and no telemetry, exactly like testing in Notepad.
        service.SetFlapTestEnvironment(new FlapTestTelemetry(), () => false);
        service.Settings.Enabled = true;
        var conditions = FlapPositionCaptureForm.BuildConditions([1u << 21, 0, 1u << 22])!;
        var definition = new SwitchDefinition
        {
            Name = "Flaps", DeviceId = 1, AdvancedFlaps = AdvancedFlapsChecks.Settings(),
            States = conditions.Select((c, i) => new SwitchStateDefinition
            { Name = i.ToString(), Conditions = c.ToList() }).ToList()
        };
        service.Settings.Switches.Add(definition);
        void Poll(uint mask, int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                time = time.AddMilliseconds(25);
                physical = new(1, "HOTAS", mask, true);
                service.Poll();
            }
        }
        const uint up = 1u << 21, down = 1u << 22;
        Poll(0); Poll(down, 3); Poll(0); Poll(up, 30);
        Check(keys.SequenceEqual([Keys.I, Keys.O, Keys.O, Keys.O]), "Fast down-centre-up types iooo without focus/telemetry");
        keys.Clear(); Poll(up, 40);
        Check(keys.Count == 0, "Held endpoint does not type repeatedly");
        Poll(0, 35);
        Check(keys.SequenceEqual([Keys.I, Keys.I]), "Return from special Up types ii once");
        keys.Clear(); Poll(0, 40);
        Check(keys.Count == 0, "Held centre does not type repeatedly");
        Poll(up, 3); Poll(0); Poll(down, 30);
        Check(keys.SequenceEqual([Keys.O, Keys.I, Keys.I, Keys.I]), "Fast up-centre-down types oiii");
        keys.Clear(); Poll(0, 35);
        Check(keys.SequenceEqual([Keys.O, Keys.O]), "Return from reverse gesture types oo");
        keys.Clear(); service.ResetRuntime(); Poll(down); Poll(up, 30);
        Check(keys.SequenceEqual([Keys.I, Keys.O, Keys.O, Keys.O]), "Missed centre sample preserves full sweep sequence");
        keys.Clear(); service.ResetRuntime(); Poll(0); Poll(down, 3);
        using (service.SuspendFlaps()) { Poll(0); Poll(up, 30); }
        Check(keys.SequenceEqual([Keys.I]), "Editor cancels queued keys");
        keys.Clear(); service.ResetRuntime(); Poll(0); Poll(up, 3); Poll(0); Poll(down, 3);
        physical = null; service.Poll(); Poll(down, 30);
        Check(keys.SequenceEqual([Keys.O]), "Disconnect cancels pending sequence and reconnect cannot replay");
        keys.Clear(); service.ResetRuntime(); Poll(0); Poll(up, 3); Poll(0, 20); Poll(down, 30);
        Check(keys.SequenceEqual([Keys.O, Keys.I, Keys.I]), "Slow centre transit uses single reverse then single endpoint key");
        keys.Clear(); service.ResetRuntime(); Poll(0); Poll(down, 12); Poll(0, 20);
        Check(keys.SequenceEqual([Keys.I, Keys.O]), "Normal Down then Centre sends one key each");
        keys.Clear(); Poll(up, 12); Poll(0, 20);
        Check(keys.SequenceEqual([Keys.O, Keys.I]), "Normal Up then Centre sends one key each");
        keys.Clear(); Poll(down, 3); Poll(0); Poll(up, 35); Poll(0, 30);
        Check(keys.SequenceEqual([Keys.I, Keys.O, Keys.O, Keys.O, Keys.I, Keys.I]), "Special Up reverses twice on its first return");
        keys.Clear(); Poll(up, 12); Poll(0, 20); Poll(0, 40);
        Check(keys.SequenceEqual([Keys.O, Keys.I]), "Special memory clears: next normal return is single and never repeats");
        File.Delete(Path.ChangeExtension(path, ".flaps.log"));
        Console.WriteLine($"PASS: {checks} direct keyboard flap sequence checks.");
        return checks;
    }
}
