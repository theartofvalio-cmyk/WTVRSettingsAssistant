using System.Reflection;
using WTVRSettingsAssistant;

internal static class AdvancedFlapsUiChecks
{
    public static int Run()
    {
        Exception? failure = null; int checks = 0;
        var thread = new Thread(() =>
        {
            try
            {
                string output = Path.GetFullPath("artifacts/advanced-flaps-preview"); Directory.CreateDirectory(output);
                using (var capture = new FlapPositionCaptureForm(["UP", "CENTER", "DOWN"], new(0, "Test HOTAS", 0, true), "en"))
                {
                    capture.Show(); Application.DoEvents();
                    var positions = Descendants(capture).OfType<ListView>().Single();
                    if (positions.Items.Count != 3 || positions.Items.Cast<ListViewItem>().Any(i => i.SubItems[1].Text != "Not captured"))
                        throw new Exception("Capture wizard must show all pending positions");
                    checks++;
                    using var shot = new Bitmap(capture.Width, capture.Height);
                    capture.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size)); shot.Save(Path.Combine(output, "capture-positions.png"));
                    capture.Close();
                }
                foreach (string language in new[] { "en", "fr" })
                {
                    var telemetry = new FlapTestTelemetry { Sample = new(DateTime.UtcNow, "test", 43, 180, 100, 0, 0) };
                    using var form = new AdvancedFlapsSettingsForm(AdvancedFlapsChecks.Settings(), ["UP", "CENTER", "DOWN"], language, telemetry);
                    form.Show();
                    var tabs = Descendants(form).OfType<TabControl>().Single();
                    foreach (var size in new[] { new Size(820, 680), new Size(1100, 820) })
                    {
                        form.ClientSize = size;
                        for (int i = 0; i < tabs.TabPages.Count; i++)
                        {
                            tabs.SelectedIndex = i; form.PerformLayout(); Application.DoEvents();
                            telemetry.Sample = new(DateTime.UtcNow, "test", 43, 180, 100, 0, 0);
                            typeof(AdvancedFlapsSettingsForm).GetMethod("RefreshTelemetry", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null);
                            using var image = new Bitmap(form.Width, form.Height);
                            form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                            image.Save(Path.Combine(output, $"{language}-flaps-{i}-{size.Width}.png"));
                            if (Descendants(form).Any(c => c.Visible && c.Text.StartsWith("Flaps."))) throw new Exception("Missing translated flap UI key");
                            checks++;
                        }
                    }
                    if (Descendants(form).OfType<NumericUpDown>().Count() != 1) throw new Exception("Expected only key spacing and keyboard bindings");
                    if (Descendants(form).Any(c => c.Text.Contains("IAS"))) throw new Exception("IAS controls must not remain in flap settings");
                    checks++;
                    form.Close();
                    if (!telemetry.Disposed) throw new Exception("Flap setup did not dispose telemetry");
                    checks++;
                }
                var definition = new SwitchDefinition { AdvancedFlaps = AdvancedFlapsChecks.Settings(), States = new()
                {
                    new() { Name = "UP", Conditions = new() { new() { ButtonId = 1, RequiredState = true }, new() { ButtonId = 2, RequiredState = false } } },
                    new() { Name = "CENTER", Conditions = new() { new() { ButtonId = 1, RequiredState = false }, new() { ButtonId = 2, RequiredState = false } } },
                    new() { Name = "DOWN", Conditions = new() { new() { ButtonId = 1, RequiredState = false }, new() { ButtonId = 2, RequiredState = true } } }
                } };
                using var editor = new SwitchEditorForm(definition, "en"); editor.Show();
                var toggle = (CheckBox)typeof(SwitchEditorForm).GetField("_flapsToggle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!;
                var setup = (Button)typeof(SwitchEditorForm).GetField("_flapsSetup", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!;
                var states = Descendants(editor).OfType<DataGridView>().Single();
                if (!setup.Visible || states.Columns["Action"]!.Visible) throw new Exception("Flap mode must suppress normal action editing"); checks++;
                toggle.Checked = false;
                if (setup.Visible || !states.Columns["Action"]!.Visible) throw new Exception("Normal mode must hide flap settings and restore actions"); checks++;
                toggle.Checked = true;
                // Save both modes through the real editor, with an injected device list.
                var deviceList = (List<PhysicalJoystick>)typeof(SwitchEditorForm).GetField("_devices", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!;
                deviceList.Clear(); deviceList.Add(new(0, "Test HOTAS", 0, true));
                var deviceBox = (ComboBox)typeof(SwitchEditorForm).GetField("_device", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!;
                deviceBox.Items.Clear(); deviceBox.Items.Add("Test HOTAS"); deviceBox.SelectedIndex = 0;
                var commit = typeof(SwitchEditorForm).GetMethod("Commit", BindingFlags.NonPublic | BindingFlags.Instance)!;
                if (!(bool)commit.Invoke(editor, null)! || !definition.AdvancedFlaps.Enabled || definition.States[0].OutputKind != SwitchActionKind.VJoyButton)
                    throw new Exception("Flap editor failed to preserve normal output on save"); checks++;
                toggle.Checked = false;
                if (!(bool)commit.Invoke(editor, null)! || definition.AdvancedFlaps.Enabled || definition.States[0].OutputButton != 1)
                    throw new Exception("Disabling flaps failed to preserve original virtual output"); checks++;
                toggle.Checked = true;
                editor.ClientSize = new Size(1120, 780); editor.PerformLayout(); Application.DoEvents();
                var footer = editor.Controls.OfType<Button>().Where(b => b.Top > 600).ToArray();
                for (int a = 0; a < footer.Length; a++) for (int b = a + 1; b < footer.Length; b++)
                    if (footer[a].Bounds.IntersectsWith(footer[b].Bounds)) throw new Exception("Switch editor footer overlaps");
                using var bitmap = new Bitmap(editor.Width, editor.Height);
                editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "switch-editor-flaps.png")); checks++;
                editor.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
        Console.WriteLine($"PASS: {checks} flap settings/editor UI checks. Preview images: artifacts/advanced-flaps-preview");
        return checks;
    }
    private static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>()
        .SelectMany(c => new[] { c }.Concat(Descendants(c)));
}
