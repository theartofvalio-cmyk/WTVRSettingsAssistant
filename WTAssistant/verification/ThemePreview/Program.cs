using System.Reflection;
using System.Drawing.Imaging;
using WTVRSettingsAssistant;

internal static class Preview
{
    [STAThread]
    static void Main(string[] args)
    {
        // Runs in this harness's output directory, never against installed user data.
        string settings = Path.Combine(AppContext.BaseDirectory,"Settings");
        Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings,"appearance.json"),args.Contains("classic") ? "{\"Theme\":\"Classic\"}" : "{\"Theme\":\"Illustrated\"}");
        string output = Path.GetFullPath(args.FirstOrDefault(a => a != "classic" && !a.StartsWith("--")) ?? "artifacts/theme-preview");
        Directory.CreateDirectory(output);
        ApplicationConfiguration.Initialize();
        using MainForm main = new(args.Contains("--startup-smoke"));
        if (args.Contains("--startup-smoke"))
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            main.Show();
            Application.DoEvents();
            if (main.Visible || main.ShowInTaskbar || main.WindowState != FormWindowState.Minimized)
                throw new Exception("Windows startup displayed the main window instead of staying in the tray.");

            var home = (Control)typeof(MainForm).GetField("_aviationHome", flags)!.GetValue(main)!;
            var webViewField = home.GetType().GetField("_webView", flags)!;
            if (webViewField.GetValue(home) is not null)
                throw new Exception("WebView2 initialized while Windows startup was hidden.");

            typeof(MainForm).GetMethod("RestoreFromExternalLaunch", flags)!.Invoke(main, null);
            Application.DoEvents();
            if (!main.Visible || !main.ShowInTaskbar || main.WindowState != FormWindowState.Normal)
                throw new Exception("Restoring the startup instance did not show a normal window.");
            if (webViewField.GetValue(home) is null)
                throw new Exception("WebView2 was not started after restoring the Home page.");
            var homePanel = (Panel)typeof(MainForm).GetField("_mainPanel", flags)!.GetValue(main)!;
            Rectangle expected = new(main.Padding.Left, main.Padding.Top,
                main.ClientSize.Width - main.Padding.Horizontal,
                main.ClientSize.Height - main.Padding.Vertical);
            if (homePanel.Bounds != expected)
                throw new Exception($"Restored Home layout is clipped: actual={homePanel.Bounds}, expected={expected}.");

            Size restoredSize = main.ClientSize;
            typeof(MainForm).GetMethod("HideToTray", flags)!.Invoke(main, [false]);
            Application.DoEvents();
            if (main.Visible) throw new Exception("Hiding to the tray left the window visible.");
            typeof(MainForm).GetMethod("RestoreFromExternalLaunch", flags)!.Invoke(main, null);
            Application.DoEvents();
            if (!main.Visible || main.WindowState != FormWindowState.Normal || homePanel.Bounds != expected)
                throw new Exception($"The Home page did not recover after a tray hide and restore: visible={main.Visible}, state={main.WindowState}, home={homePanel.Bounds}, expected={expected}.");
            typeof(MainForm).GetMethod("HideToTray", flags)!.Invoke(main, [false]);
            typeof(MainForm).GetField("_allowExit", flags)!.SetValue(main, true);
            main.Close();
            string savedState = File.ReadAllText(Path.Combine(settings, "settings.json"));
            using var state = System.Text.Json.JsonDocument.Parse(savedState);
            if (state.RootElement.GetProperty("WindowWidth").GetInt32() != restoredSize.Width ||
                state.RootElement.GetProperty("WindowHeight").GetInt32() != restoredSize.Height)
                throw new Exception("Exiting from the tray overwrote the restored window size.");
            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            Console.WriteLine("PASS: Windows startup stays hidden, defers WebView2, restores Home, and preserves window size on tray exit.");
            return;
        }
        Prepare(main);
        if (args.Contains("--footer-only"))
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            if (typeof(MainForm).GetField("_aircraftHomeFilter", flags) is not null)
                throw new Exception("The misplaced Home Profiles filter is still present.");
            typeof(MainForm).GetField("_showHomeAircraft", flags)!.SetValue(main, true);
            var layout = typeof(MainForm).GetMethod("LayoutAircraftHomeFilter", flags)!;
            foreach (Size size in new[] { new Size(1300, 800), new Size(1920, 1080) })
            {
                main.ClientSize = size;
                layout.Invoke(main, null);
                Render(main, $"home-footer-{size.Width}");
            }
            var footerNavigation = (Panel)typeof(MainForm).GetField("_themeNavigation", flags)!.GetValue(main)!;
            var child = footerNavigation.Controls.OfType<Button>().Single(button =>
                (string?)button.GetType().GetProperty("PageKey")?.GetValue(button) == "vtrim-profiles");
            var trim = footerNavigation.Controls.OfType<Button>().Single(button => button.Text == "VTrim Assistant");
            if (child.Visible) throw new Exception("VTrim Profiles submenu must start collapsed.");
            var trimMouseDown = trim.GetType().GetMethod("OnMouseDown", flags)!;
            trimMouseDown.Invoke(trim, [new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)]);
            trimMouseDown.Invoke(trim, [new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)]);
            if (!child.Visible || child.Top <= trim.Top) throw new Exception("VTrim double-click did not open its Profiles submenu.");
            Render(main, "home-vtrim-profiles-expanded");
            Console.WriteLine("PASS: footer filter removed and VTrim Profiles submenu expands.");
            return;
        }
        if (args.Contains("--strip-only"))
        {
            string sandbox = Path.Combine(Path.GetTempPath(), "WTA-strip-preview-" + Guid.NewGuid().ToString("N"));
            try
            {
                string profiles = Path.Combine(sandbox, "Profiles");
                Directory.CreateDirectory(profiles);
                foreach (string name in new[] { "AH-1G", "AH-64A", "AH-64E", "AV-8B Plus", "F-84F", "MiG-29 Sniper" })
                    File.WriteAllText(Path.Combine(profiles, Uri.EscapeDataString(name) + ".json"),
                        System.Text.Json.JsonSerializer.Serialize(new { ProfileName = name }));
                File.WriteAllText(Path.Combine(profiles, "_active-profile.txt"), "F-84F");
                using var trim = new HOTASTrimUtility.Form1(sandbox, embedded: true);
                using var host = new Form { ClientSize = new Size(830, 145), ShowInTaskbar = false };
                Control strip = trim.CreateAircraftProfileBrowser(true);
                host.Controls.Add(strip);
                Prepare(host);
                Application.DoEvents();
                var cards = Descendants(strip).Where(control => control.GetType().Name == "AircraftCard").ToArray();
                var visible = cards.Where(card => card.Visible).ToArray();
                if (cards.Length != 6 || visible.Length != 3 ||
                    !visible.Any(card => (string?)card.Tag == "F-84F"))
                    throw new Exception($"Home strip expected 3 cards including active F-84F; found {cards.Length} total, {visible.Length} visible.");
                Render(host, "home-strip-three-active");
                typeof(HOTASTrimUtility.Form1).GetField("_activeProfileName", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(trim, "MiG-29 Sniper");
                ((Delegate?)typeof(HOTASTrimUtility.Form1).GetField("AircraftProfileSelectionChanged", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(trim))?.DynamicInvoke();
                visible = cards.Where(card => card.Visible).ToArray();
                if (visible.Length != 3 || !visible.Any(card => (string?)card.Tag == "MiG-29 Sniper" &&
                    (bool)card.GetType().GetProperty("Active")!.GetValue(card)!))
                    throw new Exception("Switching aircraft did not reveal and select its Home card.");
                Render(host, "home-strip-switched-active");
                Console.WriteLine("PASS: Home strip shows 3 aircraft and follows the active profile.");
            }
            finally { try { Directory.Delete(sandbox, recursive: true); } catch { } }
            return;
        }
        foreach (string typeName in new[] { "ThemeButton", "ThemeCheckBox" })
        {
            var type = typeof(MainForm).Assembly.GetType("WTVRSettingsAssistant." + typeName)!;
            using var control = (Control)Activator.CreateInstance(type)!;
            if (control is CheckBox check) check.Appearance = Appearance.Button;
            foreach (Size tiny in new[] { new Size(12, 5), new Size(5, 12), new Size(1, 1), new Size(0, 0), new Size(120, 38) })
            {
                control.Size = tiny;
                using var bitmap = new Bitmap(120, 38);
                using var graphics = Graphics.FromImage(bitmap);
                type.GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control,
                    [new PaintEventArgs(graphics, new Rectangle(Point.Empty, tiny))]);
            }
        }
        Console.WriteLine("PASS: themed buttons tolerate zero-height, tiny and restored paint sizes.");
        if (main.DeviceDpi != 96) throw new Exception("The UI must use a fixed 96-DPI coordinate space.");
        var languageField = typeof(MainForm).GetField("_languageCode", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var applyLocalization = typeof(MainForm).GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Instance)!;
        if (args.Contains("--localization"))
        {
            string? selectedLanguage = args.FirstOrDefault(a => a.StartsWith("--language="))?.Split('=', 2)[1];
            var options = (Array)typeof(MainForm).Assembly.GetType("WTVRSettingsAssistant.AppText")!
                .GetField("Options", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var codes = options.Cast<object>().Select(option => (string)option.GetType().GetProperty("Code")!.GetValue(option)!);
            foreach (string code in codes.Where(code => selectedLanguage is null || code == selectedLanguage))
            {
                languageField.SetValue(main, code);
                applyLocalization.Invoke(main, null);
                foreach (Size size in new[] { new Size(1300, 800), new Size(1920, 1080) })
                {
                    main.ClientSize = size;
                    foreach (string page in new[] { "_mainPanel", "_settingsPanel", "_aboutPanel" })
                    {
                        var panel = (Panel)typeof(MainForm).GetField(page, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
                        typeof(MainForm).GetMethod("ShowScreen", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, [panel]);
                        Render(main, $"{code}-{page}-{size.Width}");
                    }
                }
                foreach (string type in new[] { "NeckAssistForm", "HiddenKeybindsForm" })
                {
                    object?[] parameters = type == "NeckAssistForm" ? [AppContext.BaseDirectory, true] :
                        [AppContext.BaseDirectory, AssetManager.LoadImage("Home.png"), AssetManager.LoadImage("Info.png"), (Action)(() => { })];
                    using Form page = (Form)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("WTVRSettingsAssistant." + type)!, parameters)!;
                    Prepare(page);
                    page.GetType().GetMethod("ApplyLanguage")!.Invoke(page, [code]);
                    foreach (Size size in new[] { new Size(1080, 780), new Size(1380, 980) })
                    {
                        page.ClientSize = size;
                        Render(page, $"{code}-{type}-{size.Width}");
                    }
                }
            }
            Console.WriteLine("Localized main/Neck/Keybind screenshots rendered; VTrim and modal dialogs require separate coverage.");
            return;
        }
        languageField.SetValue(main, "bg");
        applyLocalization.Invoke(main, null);
        var navigation = (Panel)typeof(MainForm).GetField("_themeNavigation", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
        if (!navigation.Controls.OfType<Button>().Any(button => button.Text == "Начало"))
            throw new Exception("Bulgarian sidebar language did not apply.");
        var aboutPanelForLanguage = (Panel)typeof(MainForm).GetField("_aboutPanel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
        var textType = typeof(MainForm).Assembly.GetType("WTVRSettingsAssistant.AppText")!;
        string expectedTitle = (string)textType.GetMethod("T", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, ["bg", "Info.Title"])!;
        var localizedInfo = Descendants(aboutPanelForLanguage).OfType<Label>().FirstOrDefault(label => label.Text == expectedTitle);
        if (localizedInfo is null) throw new Exception("Info page localization did not keep a readable title.");
        Render(main, "home-bg-1300");
        languageField.SetValue(main, "en");
        applyLocalization.Invoke(main, null);
        VerifyControlProfileSelectors(main, settings);
        VerifyFooterProfiles();
        var trimNav = navigation.Controls.OfType<Button>().Single(b => b.Text == "VTrim Assistant");
        var expanded = typeof(MainForm).GetField("_vtrimNavigationExpanded", BindingFlags.NonPublic | BindingFlags.Instance)!;
        if ((bool)expanded.GetValue(main)!) throw new Exception("VTrim folder must start folded");
        var mouseDown = trimNav.GetType().GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance)!;
        mouseDown.Invoke(trimNav, [new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)]);
        if ((bool)expanded.GetValue(main)!) throw new Exception("Single click must not expand VTrim folder");
        mouseDown.Invoke(trimNav, [new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)]);
        if (!(bool)expanded.GetValue(main)!) throw new Exception("Double click must expand VTrim folder");
        expanded.SetValue(main, false);
        if (!navigation.Controls.OfType<Button>().Any(b => b.Text == "Settings")) throw new Exception("Top profiles navigation must be Settings");
        var aircraftHomeTest = (Control)typeof(MainForm).GetField("_aviationHome", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
        using var testStrip = new Panel();
        aircraftHomeTest.GetType().GetMethod("SetAircraftProfiles")!.Invoke(aircraftHomeTest, [testStrip]);
        var visibleSetting = typeof(MainForm).GetField("_showHomeAircraft", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var updateHome = typeof(MainForm).GetMethod("LayoutAircraftHomeFilter", BindingFlags.NonPublic | BindingFlags.Instance)!;
        visibleSetting.SetValue(main, false); updateHome.Invoke(main, null);
        if (testStrip.Visible) throw new Exception("Hidden aircraft must hide the Home strip");
        visibleSetting.SetValue(main, true); updateHome.Invoke(main, null);
        if (!testStrip.Visible) throw new Exception("Aircraft visibility must restore the Home strip");
        Console.WriteLine("PASS: folded navigation, double-click expansion, Settings label, and Home strip visibility.");
        var neckField = typeof(MainForm).GetField("_neckAssistForm", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using (var previewNeck = (Form)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("WTVRSettingsAssistant.NeckAssistForm")!, [AppContext.BaseDirectory, true])!)
        {
            previewNeck.TopLevel = false;
            main.Controls.Add(previewNeck);
            neckField.SetValue(main, previewNeck);
            int visibilityChanges = 0;
            previewNeck.VisibleChanged += (_, _) => visibilityChanges++;
            var homePanel = (Panel)typeof(MainForm).GetField("_mainPanel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            homePanel.VisibleChanged += (_, _) => visibilityChanges++;
            foreach (string cardName in new[] { "_neck", "_keys" })
            {
                var card = aircraftHomeTest.GetType().GetField(cardName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(aircraftHomeTest)!;
                var toggle = (Action)card.GetType().GetProperty("ToggleClick")!.GetValue(card)!;
                toggle(); toggle();
                if (visibilityChanges != 0 || !homePanel.Visible) throw new Exception("Home assistant toggle must not hide/show pages");
                if (!navigation.Controls.OfType<Button>().Any(b => b.Text == "Home" && (bool)b.GetType().GetProperty("Selected")!.GetValue(b)!))
                    throw new Exception("Home toggle must preserve navigation selection");
            }
            neckField.SetValue(main, null);
        }
        Console.WriteLine("PASS: Home assistant toggles preserve page visibility and navigation.");
        if(args.Contains("--interactive"))
        {
            main.ClientSize=new Size(1400,900); main.Location=new Point(80,80); main.ShowInTaskbar=true;
            Application.Run(main);
            return;
        }
        foreach (Size size in new[] { new Size(1920,1080),new Size(2560,1440),new Size(1600,1000),new Size(1300,800) })
        {
            // Exercise live-resize repainting before every capture. This specifically
            // guards against stale repeated card outlines from intermediate widths.
            foreach (int width in new[] { 1320, 1760, 1460, size.Width })
            {
                main.ClientSize = new Size(width, Math.Max(720, (int)Math.Round(width / 1.625)));
                main.PerformLayout();
                main.Refresh();
            }
            main.ClientSize = size; main.CreateControl(); main.PerformLayout();
            Render(main,$"home-{size.Width}");
            if (size.Width == 1920)
            {
                object? aviationHomeForUpdate = typeof(MainForm).GetField("_aviationHome",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(main);
                MethodInfo? setGameUpdateAvailable = aviationHomeForUpdate?.GetType().GetMethod("SetGameUpdateAvailable", BindingFlags.Public | BindingFlags.Instance);
                setGameUpdateAvailable?.Invoke(aviationHomeForUpdate, new object[] { true });
                main.PerformLayout();
                Render(main, "home-update-1920");
                setGameUpdateAvailable?.Invoke(aviationHomeForUpdate, new object[] { false });
            }
            var aviationHome = (ScrollableControl?)typeof(MainForm).GetField("_aviationHome",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(main);
            if (size.Width >= 1300 && aviationHome is not null && (aviationHome.HorizontalScroll.Visible || aviationHome.VerticalScroll.Visible))
                throw new Exception("Home must not show scrollbars at the supported 1300 x 800 window size.");
            foreach(string page in new[]{"_settingsPanel","_aboutPanel"})
            {
                var panel = (Panel)typeof(MainForm).GetField(page,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(main)!;
                typeof(MainForm).GetMethod("ShowScreen",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,[panel]);
                main.PerformLayout();
                Rectangle expected = new(main.Padding.Left, main.Padding.Top,
                    main.ClientSize.Width-main.Padding.Horizontal,
                    main.ClientSize.Height-main.Padding.Vertical);
                if (panel.Bounds != expected) throw new Exception($"{page} did not use the explicit padded content viewport. Actual={panel.Bounds}, expected={expected}");
                if (page == "_aboutPanel")
                {
                    var info = Descendants(panel).OfType<ScrollableControl>().FirstOrDefault(c=>c.GetType().Name=="AssistantInfoPage");
                    if (info?.HorizontalScroll.Visible == true) throw new Exception("Info page must never require horizontal scrolling.");
                }
                Render(main,$"{page}-{size.Width}");
            }
            var home = (Panel)typeof(MainForm).GetField("_mainPanel",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(main)!;
            typeof(MainForm).GetMethod("ShowScreen",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,[home]);
            var logo = Descendants(home).OfType<PictureBox>().FirstOrDefault(p => p.Image != null);
            if (logo?.Image is Bitmap logoBitmap)
            {
                int[] alpha = [logoBitmap.GetPixel(0,0).A, logoBitmap.GetPixel(logoBitmap.Width-1,0).A,
                    logoBitmap.GetPixel(0,logoBitmap.Height-1).A, logoBitmap.GetPixel(logoBitmap.Width-1,logoBitmap.Height-1).A];
                if (alpha.Any(value => value != 0)) throw new Exception("Illustrated logo backdrop is not genuinely transparent at every corner.");
            }
        }
        foreach(string type in new[]{"NeckAssistForm","HiddenKeybindsForm"})
        {
            object?[] parameters = type == "NeckAssistForm" ? [AppContext.BaseDirectory, true] : [AppContext.BaseDirectory,AssetManager.LoadImage("Home.png"),AssetManager.LoadImage("Info.png"),(Action)(()=>{})];
            using Form page = (Form)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("WTVRSettingsAssistant."+type)!,parameters)!;
            Prepare(page);
            var resizeWatch = System.Diagnostics.Stopwatch.StartNew();
            for (int step = 0; step < 100; step++)
                page.ClientSize = new Size(1080 + step * 3, 780 + step);
            resizeWatch.Stop();
            Console.WriteLine($"{type}: {resizeWatch.Elapsed.TotalMilliseconds / 100:F1} ms per layout resize (100 steps).");
            // Shown handlers removed: no backend registration or polling in visual QA.
            foreach(Size size in new[]{new Size(1380,980),new Size(1080,780),new Size(850,560)})
            {
                page.ClientSize=size; page.CreateControl(); page.PerformLayout();
                Render(page,$"{type}-{size.Width}");
                if(type == "NeckAssistForm")
                {
                    var enable = (CheckBox)page.GetType().GetField("_enabled", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!;
                    Rectangle switchBounds = enable.Bounds;
                    string switchText = enable.Text;
                    foreach (bool enabled in new[] { false, true })
                    {
                        enable.Checked = enabled;
                        page.PerformLayout();
                        if (enable.Bounds != switchBounds || enable.Text != switchText)
                            throw new Exception("Neck enable switch changes layout between ON and OFF.");
                        Render(page, $"{type}-{(enabled ? "on" : "off")}-{size.Width}");
                    }
                    page.AutoScrollPosition=new Point(0,10000);
                    Render(page,$"{type}-bottom-{size.Width}");
                    page.AutoScrollPosition=Point.Empty;
                    var simple=Descendants(page).OfType<RadioButton>().First(b=>b.Text=="SIMPLE");
                    simple.Checked=true; page.PerformLayout();
                    Render(page,$"{type}-simple-{size.Width}");
                    foreach(var label in Descendants(page).OfType<Label>().Where(l=>l.Text=="8°"))
                        if(label.Visible && label.Right > label.Parent!.ClientSize.Width) throw new Exception("Deadzone value is clipped.");
                    Descendants(page).OfType<RadioButton>().First(b=>b.Text=="ADVANCED").Checked=true;
                    page.PerformLayout();
                }
            }
        }
        foreach (string languageCode in new[] { "bg", "de", "fr" })
        {
            using Form localizedNeck = (Form)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("WTVRSettingsAssistant.NeckAssistForm")!, [AppContext.BaseDirectory, true])!;
            Prepare(localizedNeck);
            localizedNeck.ClientSize = new Size(850, 560);
            localizedNeck.GetType().GetMethod("ApplyLanguage")!.Invoke(localizedNeck, [languageCode]);
            localizedNeck.PerformLayout();
            Render(localizedNeck, $"neck-{languageCode}-850");
        }
        using (Form trim = (Form)Activator.CreateInstance(typeof(HOTASTrimUtility.Form1), BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object?[] { true, null, true }, null)!)
        {
            trim.MinimumSize = Size.Empty;
            Prepare(trim);
            foreach (Size size in new[] { new Size(1080, 780), new Size(1380, 980) })
            {
                trim.ClientSize = size;
                trim.PerformLayout();
                Render(trim, $"vtrim-{size.Width}");
                foreach (string tab in new[] { "Devices & Output", "Axis Curves", "Profiles", "Trim Dashboard" })
                {
                    Button? navigationButton = Descendants(trim).OfType<Button>().FirstOrDefault(b => b.Text == tab);
                    if (navigationButton == null) throw new Exception($"Missing VTrim tab: {tab}");
                    navigationButton.PerformClick();
                    trim.PerformLayout();
                    Render(trim, $"vtrim-{tab.Replace(" & ", "-").Replace(' ', '-')}-{size.Width}");
                    var scrollPage = Descendants(trim).OfType<Panel>().FirstOrDefault(p => p.Visible && p.GetType().Name == "ScrollPagePanel");
                    if (scrollPage != null)
                    {
                        foreach (int offset in new[] { 120, 300, 10000, 0 })
                        {
                            scrollPage.AutoScrollPosition = new Point(0, offset);
                            scrollPage.Invalidate(true);
                            scrollPage.Update();
                            Render(trim, $"vtrim-{tab.Replace(" & ", "-").Replace(' ', '-')}-{size.Width}-scroll-{offset}");
                        }
                    }
                }
            }
        }
        using (var captureOptions = new System.Windows.Forms.Timer { Interval = 150 })
        {
            captureOptions.Tick += (_, _) =>
            {
                Form? dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Modal && f != main);
                if (dialog == null) return;
                captureOptions.Stop();
                Render(dialog, "app-options");
                dialog.ClientSize = new Size(860, 560);
                dialog.PerformLayout();
                Render(dialog, "app-options-small");
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            };
            captureOptions.Start();
            typeof(MainForm).GetMethod("ShowApplicationOptionsDialog", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, null);
        }
        Type recommendedType = typeof(MainForm).GetNestedType("RecommendedVrSettingsForm", BindingFlags.NonPublic)!;
        string recommendedLayout = (string)typeof(MainForm).GetField("BakedRecommendedGpuLayoutJson", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        using (Form recommended = (Form)Activator.CreateInstance(recommendedType, [null, Color.FromArgb(29,31,32), Color.White, recommendedLayout, "en"])!)
        {
            Prepare(recommended);
            foreach (Size size in new[] { new Size(1000, 520), new Size(1625, 844) })
            {
                recommended.ClientSize = size;
                recommended.PerformLayout();
                Render(recommended, $"recommended-{size.Width}");
                var viewport = recommended.Controls.OfType<Panel>().Single();
                if (size.Height < 650 && !viewport.VerticalScroll.Visible)
                    throw new Exception("Short recommended dialog must scroll to its footer.");
                viewport.AutoScrollPosition = new Point(0, 10000);
                Render(recommended, $"recommended-bottom-{size.Width}");
                viewport.AutoScrollPosition = Point.Empty;
            }
        }
        var toggleFullscreen = typeof(MainForm).GetMethod("ToggleFullScreen", BindingFlags.NonPublic|BindingFlags.Instance)!;
        Rectangle beforeFullscreen = main.Bounds;
        toggleFullscreen.Invoke(main, null);
        if (main.FormBorderStyle != FormBorderStyle.None || main.Bounds != Screen.FromControl(main).Bounds)
            throw new Exception("F11 did not fill the entire screen.");
        toggleFullscreen.Invoke(main, null);
        if (main.Bounds != beforeFullscreen) throw new Exception("Fullscreen did not restore window bounds.");
        if ((Rectangle)typeof(Form).GetProperty("MaximizedBounds", BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(main)! != Rectangle.Empty) throw new Exception("Maximization is still aspect constrained.");
        Console.WriteLine("PASS: fullscreen screen coverage, restore bounds, and unconstrained maximization.");
        // Reproduce the original crash sequence: resize pages, then create the
        // first manager/editor and a textbox that uses Windows' shared font.
        var assembly = typeof(MainForm).Assembly;
        using (var service = (IDisposable)Activator.CreateInstance(assembly.GetType("WTVRSettingsAssistant.AdvancedSwitchService")!, [Path.Combine(settings,"qa-switches.json")])!)
        for (int cycle = 0; cycle < 3; cycle++)
        {
            using Form manager = (Form)Activator.CreateInstance(assembly.GetType("WTVRSettingsAssistant.AdvancedSwitchManagerForm")!, [service])!;
            Prepare(manager);
            manager.ClientSize = new Size(1500, 1000); manager.ClientSize = new Size(1220, 800);
            Render(manager, "advanced-manager");
            object definition = Activator.CreateInstance(assembly.GetType("WTVRSettingsAssistant.SwitchDefinition")!)!;
            using Form editor = (Form)Activator.CreateInstance(assembly.GetType("WTVRSettingsAssistant.SwitchEditorForm")!, [definition])!;
            Prepare(editor);
            editor.ClientSize = new Size(1500, 1000); editor.ClientSize = new Size(1220, 860);
            Render(editor, "advanced-editor");
            using TextBox probe = new();
            if (Control.DefaultFont.GetHeight() <= 0 || probe.PreferredHeight <= 0) throw new Exception("Shared font was corrupted by scaling.");
        }
        Console.WriteLine("PASS: repeated Advanced Controls creation/resizing and shared default font survival.");
        Console.WriteLine("Rendered previews without starting VR or input polling: " + output);
        static IEnumerable<Control> Descendants(Control c) => c.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Descendants(child)));
        void VerifyFooterProfiles()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(MainForm);
            type.GetField("_showHomeKeybinds", flags)!.SetValue(main, true);
            var layout = type.GetMethod("LayoutAircraftHomeFilter", flags)!;
            layout.Invoke(main, null);
            var desktop = (Control)type.GetField("_desktopFooterButton", flags)!.GetValue(main)!;
            var vr = (Control)type.GetField("_vrFooterButton", flags)!.GetValue(main)!;
            if (!desktop.Visible || !vr.Visible || desktop.Parent == vr.Parent || desktop.Parent?.GetType().Name != "AviationActionCard")
                throw new Exception("Selectors must be on separate mode cards");
            var modeType = type.GetNestedType("AppliedMode", BindingFlags.NonPublic)!;
            foreach (string mode in new[] { "Monitor", "VR" })
            {
                using var menu = (ContextMenuStrip)type.GetMethod("CreateControlProfileMenu", flags)!.Invoke(main, [Enum.Parse(modeType, mode)])!;
                if (menu.Items.Count != 3 || menu.Items[0].Text != "No profile") throw new Exception("Existing profiles plus No profile should appear");
                ((ToolStripMenuItem)menu.Items[2]).PerformClick();
                string field = mode == "Monitor" ? "_desktopControlsBlkPath" : "_vrControlsBlkPath";
                if (!((string)type.GetField(field, flags)!.GetValue(main)!).EndsWith("two.blk")) throw new Exception("Shared selection failed");
                ((ToolStripMenuItem)menu.Items[0]).PerformClick();
                if (((string)type.GetMethod("GetSelectedControlsPath", flags)!.Invoke(main, [Enum.Parse(modeType, mode)])!).Length != 0)
                    throw new Exception("No profile must clear the controls override");
            }
            Render(main, "card-profile-selectors");
            var modeBefore = type.GetField("_lastAppliedMode", flags)!.GetValue(main);
            desktop.GetType().GetMethod("PerformClick")!.Invoke(desktop, null);
            var popup = (ContextMenuStrip)type.GetField("_controlProfileMenu", flags)!.GetValue(main)!;
            if (!popup.Visible || !Equals(modeBefore, type.GetField("_lastAppliedMode", flags)!.GetValue(main)))
                throw new Exception("Opening selector must not change display mode");
            Render(popup, "card-profile-popup"); popup.Close();
            var profiles = (System.Collections.IList)type.GetField("_desktopControlProfiles", flags)!.GetValue(main)!;
            object first = profiles[0]!;
            var name = first.GetType().GetProperty("Name")!;
            string oldName = (string)name.GetValue(first)!;
            name.SetValue(first, string.Join(" ", Enumerable.Repeat("Long aircraft controls preset", 12)));
            using (var wrapped = (ContextMenuStrip)type.GetMethod("CreateControlProfileMenu", flags)!.Invoke(main, [Enum.Parse(modeType, "Monitor")])!)
            {
                if (wrapped.Items[1].Height <= wrapped.Items[2].Height) throw new Exception("Long names must increase menu row height");
                wrapped.Show(desktop, new Point(0, desktop.Height)); Render(wrapped, "wrapped-profile-popup"); wrapped.Close();
            }
            name.SetValue(first, oldName);
            var saved = profiles.Cast<object>().ToArray(); profiles.Clear();
            type.GetMethod("RefreshControlProfileFooter", flags)!.Invoke(main, null);
            if (desktop.Enabled || !desktop.Visible) throw new Exception("Empty selector must remain visible but disabled");
            Render(main, "disabled-card-selector");
            foreach (var item in saved) profiles.Add(item);
            type.GetField("_showHomeKeybinds", flags)!.SetValue(main, false); layout.Invoke(main, null);
            if (desktop.Visible || vr.Visible) throw new Exception("Option must hide both triangles");
            Console.WriteLine("PASS: card selectors, existing profiles, selection, mode isolation, wrapping and empty states.");
        }
        static void VerifyControlProfileSelectors(MainForm main, string settings)
        {
            Type formType = typeof(MainForm);
            Type modeType = formType.GetNestedType("AppliedMode", BindingFlags.NonPublic)!;
            object monitorMode = Enum.Parse(modeType, "Monitor");
            object vrMode = Enum.Parse(modeType, "VR");
            MethodInfo addOrSelect = formType.GetMethod("AddOrSelectControlProfile", BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo refreshCombos = formType.GetMethod("RefreshControlProfileCombos", BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo showScreen = formType.GetMethod("ShowScreen", BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo updateVisualStates = formType.GetMethod("UpdateVisualStates", BindingFlags.NonPublic | BindingFlags.Instance)!;

            string monitorOne = Path.Combine(settings, "qa-monitor-one.blk");
            string monitorTwo = Path.Combine(settings, "qa-monitor-two.blk");
            string vrOne = Path.Combine(settings, "qa-vr-one.blk");
            string vrTwo = Path.Combine(settings, "qa-vr-two.blk");
            foreach (string file in new[] { monitorOne, monitorTwo, vrOne, vrTwo })
                File.WriteAllText(file, "controls{ }" + Environment.NewLine + "settings{ }" + Environment.NewLine);

            object desktopProfiles = formType.GetField("_desktopControlProfiles", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            object vrProfiles = formType.GetField("_vrControlProfiles", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            addOrSelect.Invoke(main, new[] { desktopProfiles, monitorOne, monitorMode });
            addOrSelect.Invoke(main, new[] { desktopProfiles, monitorTwo, monitorMode });
            addOrSelect.Invoke(main, new[] { vrProfiles, vrOne, vrMode });
            addOrSelect.Invoke(main, new[] { vrProfiles, vrTwo, vrMode });
            refreshCombos.Invoke(main, null);
            formType.GetField("_customVrEnabled", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(main, false);

            var settingsPanel = (Panel)formType.GetField("_settingsPanel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            showScreen.Invoke(main, new[] { settingsPanel });

            var desktopCombo = (ComboBox)formType.GetField("_desktopControlsCombo", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            var vrCombo = (ComboBox)formType.GetField("_vrControlsCombo", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            if (!desktopCombo.Visible || desktopCombo.Items.Count != 3)
                throw new Exception("Monitor controls dropdown must be visible and include more than one saved file.");
            if (vrCombo.Visible)
                throw new Exception("Custom VR controls dropdown must stay hidden until Custom VR is enabled.");

            desktopCombo.SelectedIndex = 1;
            string selectedMonitorPath = (string)formType.GetField("_desktopControlsBlkPath", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            if (!Path.GetFullPath(selectedMonitorPath).Equals(Path.GetFullPath(monitorOne), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Monitor controls dropdown did not switch the selected controls file.");

            formType.GetField("_customVrEnabled", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(main, true);
            updateVisualStates.Invoke(main, null);
            if (!vrCombo.Visible || vrCombo.Items.Count != 3)
                throw new Exception("Custom VR controls dropdown must be visible in Custom VR mode and include more than one saved file.");

            vrCombo.SelectedIndex = 1;
            string selectedVrPath = (string)formType.GetField("_vrControlsBlkPath", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            if (!Path.GetFullPath(selectedVrPath).Equals(Path.GetFullPath(vrOne), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Custom VR controls dropdown did not switch the selected controls file.");

            var mainPanel = (Panel)formType.GetField("_mainPanel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            showScreen.Invoke(main, new[] { mainPanel });
        }
        void Render(Control control,string name)
        {
            using Bitmap image=new(control.Width,control.Height);
            control.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            image.Save(Path.Combine(output,name+".png"),ImageFormat.Png);
        }
        void Prepare(Form form)
        {
            var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;
            foreach(var field in typeof(Form).GetFields(BindingFlags.NonPublic|BindingFlags.Static).Where(f=>f.Name.Contains("shown",StringComparison.OrdinalIgnoreCase) || f.Name.Contains("closed",StringComparison.OrdinalIgnoreCase)))
            {
                object? key=field.GetValue(null);
                if(key!=null && events[key] is Delegate handler) events.RemoveHandler(key,handler);
            }
            form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-30000,-30000); form.ShowInTaskbar=false;
            form.Show();
        }
    }
}


