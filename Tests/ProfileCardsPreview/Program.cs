using System.Reflection;
using System.Drawing.Imaging;
using HOTASTrimUtility;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        string output = Path.GetFullPath("artifacts/profile-cards-preview");
        string settings = Path.Combine(output, "fixture-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(settings, "Profiles"));
        var assembly = typeof(Form1).Assembly;
        using var http = new System.Net.Http.HttpClient();
        object provider = Activator.CreateInstance(assembly.GetType("HOTASTrimUtility.WarThunderWikiAircraftProvider")!, http)!;
        var fetch = (Task)provider.GetType().GetMethod("FetchAsync")!.Invoke(provider, [CancellationToken.None])!;
        fetch.GetAwaiter().GetResult();
        var aircraft = (Array)fetch.GetType().GetProperty("Result")!.GetValue(fetch)!;
        foreach (object item in aircraft)
        {
            string id = (string)item.GetType().GetProperty("Id")!.GetValue(item)!;
            if (!new[] { "f-4s", "f_16a_block_10", "f-5a_china", "a_10a_early", "wyvern_s4" }.Contains(id)) continue;
            string name = (string)item.GetType().GetProperty("DisplayName")!.GetValue(item)!;
            File.WriteAllText(Path.Combine(settings, "Profiles", Uri.EscapeDataString(name) + ".json"), System.Text.Json.JsonSerializer.Serialize(new { ProfileName = name, AircraftId = id }));
            string url = (string)item.GetType().GetProperty("IconUrl")!.GetValue(item)!;
            Directory.CreateDirectory(Path.Combine(settings, "Aircraft", "Icons"));
            File.WriteAllBytes(Path.Combine(settings, "Aircraft", "Icons", id + ".png"), http.GetByteArrayAsync(url).GetAwaiter().GetResult());
        }
        var constructor = typeof(Form1).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(bool), typeof(string), typeof(bool)], null)!;
        using var vtrim = (Form1)constructor.Invoke([true, settings, true]);
        object database = Activator.CreateInstance(assembly.GetType("HOTASTrimUtility.AircraftDatabaseService")!, settings, provider)!;
        database.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(database, aircraft);
        typeof(Form1).GetField("_aircraftDatabase", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vtrim, database);
        typeof(Form1).GetField("_aircraftAssets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vtrim,
            Activator.CreateInstance(assembly.GetType("HOTASTrimUtility.AircraftAssetCache")!, settings, http));
        typeof(Form1).GetField("_activeProfileName", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vtrim, "F-4S Phantom II");
        var favoriteSet = (HashSet<string>)typeof(Form1).GetField("_favoriteAircraft", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vtrim)!;
        var favoriteProfile = typeof(Form1).GetMethod("ReadProfile", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vtrim, ["F-4S Phantom II"])!;
        favoriteSet.Add((string)favoriteProfile.GetType().GetProperty("AircraftId")!.GetValue(favoriteProfile)!);
        foreach (bool compact in new[] { false, true })
        foreach (int width in compact ? new[] { 700, 1600 } : new[] { 1100, 3100 })
        {
            using var host = new Form { ClientSize = new Size(width, compact ? (int)(width / 5f * .43) : 700), ShowInTaskbar = false };
            var browser = vtrim.CreateAircraftProfileBrowser(compact);
            host.Controls.Add(browser);
            host.StartPosition = FormStartPosition.Manual;
            host.Location = new Point(-30000, -30000);
            host.Show();
            host.CreateControl();
            _ = host.Handle;
            browser.CreateControl();
            foreach (Control c in browser.Controls) c.CreateControl();
            Application.DoEvents();
            var marked = browser.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>())
                .Where(c => c.GetType().Name == "AircraftCard")
                .Count(c => (bool)c.GetType().GetProperty("Favorite")!.GetValue(c)!);
            if (marked != (compact ? 0 : 1)) throw new Exception("Favorite stars must appear only on Profiles cards");
            using var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height);
            browser.DrawToBitmap(bitmap, browser.ClientRectangle);
            bitmap.Save(Path.Combine(output, (compact ? "home-strip" : "profiles") + width + ".png"), ImageFormat.Png);
            if (compact)
            {
                var arrows = browser.Controls.OfType<Button>().Where(b => b.GetType().Name == "AircraftNavigationButton").ToArray();
                if (width == 1600 && arrows.Any(b => b.Visible)) throw new Exception("Arrows must disappear when all aircraft fit");
                if (width == 700)
                {
                    if (!arrows.All(b => b.Visible) || arrows[0].Enabled || !arrows[1].Enabled) throw new Exception("Initial arrow availability incorrect");
                    using var disabled = new Bitmap(arrows[0].Width, arrows[0].Height);
                    arrows[0].DrawToBitmap(disabled, arrows[0].ClientRectangle);
                    var sample = disabled.GetPixel(disabled.Width / 2, disabled.Height / 2);
                    var bg = arrows[0].BackColor;
                    int expected = (int)Math.Round(bg.R * (191 / 255d) + Color.Wheat.R * (64 / 255d));
                    if (Math.Abs(sample.R - expected) > 3) throw new Exception("Disabled arrow must retain its ivory color at 25% opacity");
                    arrows[1].PerformClick();
                    if (!arrows[0].Enabled) throw new Exception("Next page should enable previous");
                }
                var toolbar = (Control)browser.Tag!;
                var search = toolbar.Controls.OfType<TextBox>().Single();
                var favorites = toolbar.Controls.OfType<Button>().Single(b => b.GetType().Name == "AircraftFavoriteButton");
                bool HasAddTile() => browser.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).Any(c => c.Text == "+");
                search.Text = "no-matching-aircraft";
                if (HasAddTile()) throw new Exception("Empty Home search must never offer profile creation");
                favorites.PerformClick();
                if (HasAddTile()) throw new Exception("Empty Home favorites search must never offer profile creation");
                search.Clear();
                if (HasAddTile()) throw new Exception("Empty Home favorites must never offer profile creation");
            }
            else if (!browser.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).Any(c => c.Text == "+"))
                throw new Exception("Profiles section must retain profile creation");
        }
        using var timer = new System.Windows.Forms.Timer { Interval = 150 };
        Exception? failure = null;
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "New Profile");
            if (dialog is null) return;
            timer.Stop();
            try
            {
                var input = (TextBox)dialog.Controls.Find("ProfileName", true).Single();
                var query = (TextBox)dialog.Controls.Find("AircraftSearch", true).Single();
                var results = (ComboBox)dialog.Controls.Find("AircraftResults", true).Single();
                var save = (Button)dialog.Controls.Find("SaveAircraftProfile", true).Single();
                if (save.Enabled || results.Items.Count != aircraft.Length + 3) throw new Exception("Fresh picker should show full roster plus universal profiles and require a selection");
                query.Text = "F16";
                results.SelectedIndex = 0;
                typeof(ComboBox).GetMethod("OnSelectionChangeCommitted", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(results, [EventArgs.Empty]);
                if (!save.Enabled || input.Text.Length == 0) throw new Exception("Aircraft selection should enable Create and auto-name");
                using var screenshot = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
                screenshot.Save(Path.Combine(output, "picker.png"), ImageFormat.Png);
                query.Clear();
                if (results.Items.Count != aircraft.Length + 3 || save.Enabled) throw new Exception("Clearing search should reset selection");
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.DialogResult = DialogResult.Cancel; }
        };
        timer.Start();
        typeof(Form1).GetMethod("ChooseAircraftProfile", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vtrim, ["", null, null, null]);
        if (failure is not null) throw failure;
        var profileType = typeof(Form1).GetNestedType("SavedBindingsFile", BindingFlags.NonPublic)!;
        var apply = typeof(Form1).GetMethod("ApplySavedProfile", BindingFlags.NonPublic | BindingFlags.Instance)!;
        string guid = Guid.NewGuid().ToString();
        object firstProfile = System.Text.Json.JsonSerializer.Deserialize("{\"PitchStep\":0.7,\"RollAxis\":{\"DeviceGuid\":\"" + guid + "\",\"DeviceName\":\"Physical test stick\",\"AxisKey\":\"X\"}}", profileType)!;
        object secondProfile = System.Text.Json.JsonSerializer.Deserialize("{\"PitchStep\":0.2}", profileType)!;
        apply.Invoke(vtrim, [firstProfile]);
        object? firstAxis = typeof(Form1).GetField("_rollAxis", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vtrim);
        apply.Invoke(vtrim, [secondProfile]);
        object? secondAxis = typeof(Form1).GetField("_rollAxis", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vtrim);
        if (firstAxis is null || secondAxis is null) throw new Exception("Global axis routing lost on profile switch");
        var step = typeof(Form1).GetField("_pitchStepBox", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vtrim)!;
        if ((decimal)step.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(step)! != .2m) throw new Exception("Aircraft tuning was not restored independently");
        var clone = typeof(Form1).GetMethod("CloneToAircraft", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var read = typeof(Form1).GetMethod("ReadProfile", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Func<string, bool> yes = _ => true, no = _ => false;
        if (!(bool)clone.Invoke(vtrim, ["Clone test", "test-aircraft", yes])!) throw new Exception("Clone creation failed");
        var created = read.Invoke(vtrim, ["Clone test"])!;
        var clonedId = profileType.GetProperty("Id")!.GetValue(created);
        if ((decimal)profileType.GetProperty("PitchStep")!.GetValue(created)! != .2m) throw new Exception("Clone lost tuning");
        if ((bool)clone.Invoke(vtrim, ["Clone test", "test-aircraft", no])!) throw new Exception("Declined overwrite was accepted");
        if (!(bool)clone.Invoke(vtrim, ["Clone test", "test-aircraft", yes])!) throw new Exception("Confirmed overwrite failed");
        if (!Equals(clonedId, profileType.GetProperty("Id")!.GetValue(read.Invoke(vtrim, ["Clone test"])))) throw new Exception("Clone changed destination identity");
        if (!ReferenceEquals(secondAxis, typeof(Form1).GetField("_rollAxis", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vtrim))) throw new Exception("Clone changed global axes");
        Console.WriteLine("Verified clone creation, overwrite confirmation, destination identity and global axes. Rendered profile cards and compact Home strip.");
    }
}
