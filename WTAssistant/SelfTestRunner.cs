using System.Reflection;
using System.Text.Json;
using System.Net.Http;
using Microsoft.Web.WebView2.Core;
using HOTASTrimUtility;

namespace WTVRSettingsAssistant;

/// <summary>
/// Non-interactive verification used by Build.cmd and Windows CI. It never
/// opens the app UI and never requires a physical HOTAS or vJoy device.
/// </summary>
internal static class SelfTestRunner
{
    private sealed class VersionHandler(string? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (body == null) throw new HttpRequestException("Simulated unavailable update service");
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
    private sealed class FakeOutput : IInputOutput
    {
        public string Name => "SelfTest";
        public bool IsConnected => true;
        public void ButtonDown(int id) { }
        public void ButtonUp(int id) { }
        public void Pulse(int id, int durationMs) { }
        public void ReleaseAll() { }
        public void Dispose() { }
    }

    public static int Run()
    {
        List<string> report = new();
        List<string> failures = new();
        List<string> warnings = new();
        string tempRoot = Path.Combine(Path.GetTempPath(), "WTA231-SelfTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        void Pass(string name) => report.Add("PASS  " + name);
        void Fail(string name, string detail)
        {
            failures.Add(name + ": " + detail);
            report.Add("FAIL  " + name + " - " + detail);
        }
        void Warn(string name, string detail)
        {
            warnings.Add(name + ": " + detail);
            report.Add("WARN  " + name + " - " + detail);
        }
        void Check(string name, bool condition, string detail = "condition was false")
        {
            if (condition) Pass(name); else Fail(name, detail);
        }

        try
        {
            string oldInstall = Path.Combine(tempRoot, "upgrade-fixture");
            Directory.CreateDirectory(Path.Combine(oldInstall, "NeckAssist", "OpenXR"));
            File.WriteAllText(Path.Combine(oldInstall, "WTVRSettingsAssistant.dll"), "old runtime");
            File.WriteAllText(Path.Combine(oldInstall, "NeckAssist", "OpenXR", "XR_APILAYER_NOVENDOR_XRNeckSafer.dll"), "old layer");
            File.WriteAllText(Path.Combine(oldInstall, "custom-controls-random-name.blk"), "personal controls");
            File.WriteAllText(Path.Combine(oldInstall, "NeckAssist", "OpenXR", "my-notes.txt"), "personal notes");
            var retired = AppUpgradeMigration.CleanRetiredAppFiles(oldInstall);
            Check("Known old app files removed", retired.Count == 2 &&
                  !File.Exists(Path.Combine(oldInstall, "WTVRSettingsAssistant.dll")));
            Check("Unknown upgrade files preserved",
                File.Exists(Path.Combine(oldInstall, "custom-controls-random-name.blk")) &&
                File.Exists(Path.Combine(oldInstall, "NeckAssist", "OpenXR", "my-notes.txt")));

            // Atomic persistence + backup recovery primitive used by app settings.
            string atomic = Path.Combine(tempRoot, "atomic.json");
            AtomicFile.WriteTextWithBackup(atomic, "{\"value\":1}");
            AtomicFile.WriteTextWithBackup(atomic, "{\"value\":2}");
            Check("Atomic settings write", File.ReadAllText(atomic).Contains("\"value\":2", StringComparison.Ordinal));
            Check("Atomic settings backup", File.Exists(atomic + ".bak") &&
                  File.ReadAllText(atomic + ".bak").Contains("\"value\":1", StringComparison.Ordinal));

            // Advanced Switch save/load + .bak recovery, with vJoy fully mocked.
            string switchPath = Path.Combine(tempRoot, "advanced_switches.json");
            using (AdvancedSwitchService service = NewSwitchService(switchPath))
            {
                service.Settings.Enabled = true;
                service.Settings.Switches.Add(new SwitchDefinition
                {
                    Name = "SELFTEST SWITCH",
                    DeviceId = 3,
                    DeviceName = "SIMULATED HOTAS",
                    States = new List<SwitchStateDefinition>
                    {
                        new() { Name = "OFF", OutputButton = 1 },
                        new() { Name = "ON", OutputButton = 2 }
                    }
                });
                service.Save();
                service.Settings.Switches[0].Name = "CURRENT SWITCH";
                service.Save(); // creates backup containing SELFTEST SWITCH
            }
            using (AdvancedSwitchService loaded = NewSwitchService(switchPath))
            {
                Check("Advanced Switch round-trip", loaded.Settings.Switches.Count == 1 &&
                      loaded.Settings.Switches[0].Name == "CURRENT SWITCH" &&
                      loaded.Settings.Switches[0].States.Count == 2);
            }
            File.WriteAllText(switchPath, "{broken json");
            using (AdvancedSwitchService recovered = NewSwitchService(switchPath))
            {
                Check("Advanced Switch backup recovery", recovered.Settings.Switches.Count == 1 &&
                      recovered.Settings.Switches[0].Name == "SELFTEST SWITCH");
            }

            // Theme resource checks prove both final user-approved logos and fallback assets exist.
            Assembly appAssembly = typeof(Program).Assembly;
            AppThemeAssets.SetActiveTheme("F16");
            string? f16Logo = AppThemeAssets.ResolveResourceName(appAssembly, "MainLogo.png");
            Check("F-16 final logo resource", f16Logo?.Contains(".Themes.F16.", StringComparison.OrdinalIgnoreCase) == true);
            using (Image logo = BrandingLogo.Create())
                Check("F-16 final logo dimensions", logo.Width == 1173 && logo.Height == 1341);
            Check("F-16 app icon resource", AppThemeAssets.ResolveResourceName(appAssembly, "Icon.ico") is not null);
            AppThemeAssets.SetActiveTheme("MiG29");
            string? migLogo = AppThemeAssets.ResolveResourceName(appAssembly, "MainLogo.png");
            string? migIcon = AppThemeAssets.ResolveResourceName(appAssembly, "Icon.ico");
            Check("MiG-29 final logo resource", migLogo?.Contains(".Themes.MiG29.", StringComparison.OrdinalIgnoreCase) == true);
            using (Image logo = BrandingLogo.Create())
                Check("MiG-29 final logo dimensions", logo.Width == 1173 && logo.Height == 1341);
            Check("MiG-29 app icon resource", migIcon?.Contains(".Themes.MiG29.", StringComparison.OrdinalIgnoreCase) == true);
            Check("MiG-29 fallback home resource", AppThemeAssets.ResolveResourceName(appAssembly, "Home_Monitor_New.png") is not null);
            AppThemeAssets.SetActiveTheme(AppThemeAssets.DefaultTheme);

            // Every selectable language must resolve key UI strings.
            foreach (LanguageOption language in AppText.Languages)
            {
                string home = AppText.T(language.Code, "Nav.Home");
                string save = AppText.T(language.Code, "Options.Save");
                string addSwitch = AppText.T(language.Code, "Switch.AddSwitch");
                Check("Localization " + language.Code,
                    !string.IsNullOrWhiteSpace(home) && home != "Nav.Home" &&
                    !string.IsNullOrWhiteSpace(save) && save != "Options.Save" &&
                    !string.IsNullOrWhiteSpace(addSwitch) && addSwitch != "Switch.AddSwitch");
            }

            Check("War Thunder version parser",
                GameServices.ParseVersion("game version: 2.59.0.13") == new Version(2, 59, 0, 13));

            LauncherBranchValidation? productionValidation = GameServices.ParseLauncherBranchValidation(
                "onGameVersion: curCircuit = production\ninitGameVersionSettings: yup_version = 2.59.0.27",
                DateTimeOffset.UtcNow);
            Check("War Thunder launcher branch validation",
                productionValidation is not null &&
                productionValidation.Channel == GameServerChannel.Live &&
                productionValidation.Version == new Version(2, 59, 0, 27));

            LauncherBranchValidation? devValidation = GameServices.ParseLauncherBranchValidation(
                "onGameVersion: curCircuit = dev\ngame version: 2.59.0.31",
                DateTimeOffset.UtcNow);
            Check("War Thunder dev branch validation",
                devValidation is not null &&
                devValidation.Channel == GameServerChannel.Test &&
                devValidation.Version == new Version(2, 59, 0, 31));

            Version sharedVersion = new(2, 59, 0, 27);
            Check("Missing installed version offers update",
                new VersionCheck(null, sharedVersion, "missing manifest").UpdateAvailable);
            Check("Unfinished branch log cannot reuse previous branch version",
                GameServices.ParseLauncherBranchValidation(
                    "curCircuit = production\ngame version: 2.59.0.27\ncurCircuit = dev", DateTimeOffset.UtcNow) == null);

            GameInstallations savedInstallation = new();
            savedInstallation.SetBoth(tempRoot);
            string installationsPath = Path.Combine(tempRoot, "installations.json");
            File.WriteAllText(installationsPath + ".bak", JsonSerializer.Serialize(savedInstallation));
            File.WriteAllText(installationsPath, "{broken");
            Check("Installation settings recover from backup",
                GameInstallations.Load(installationsPath).LiveRoot == tempRoot);

            string configSample = "// yunetwork{curCircuit:t=\"dev\"}\n" +
                "video{driver:t=\"dx12\"}\n" +
                "yunetwork{\n// curCircuit:t=\"dev\" }\ncurCircuit:t=\"production\"\n}\n" +
                "custom{value:t=\"keep { this }\"}\n";
            Check("Server detection ignores commented dev examples", WarThunderServerConfig.Detect(configSample) == GameServerChannel.Live);
            string testConfig = WarThunderServerConfig.Apply(configSample, GameServerChannel.Test);
            Check("Server switch preserves graphics and custom settings",
                testConfig.StartsWith("// yunetwork{curCircuit:t=\"dev\"}\nvideo{driver:t=\"dx12\"}\n", StringComparison.Ordinal) &&
                testConfig.EndsWith("custom{value:t=\"keep { this }\"}\n", StringComparison.Ordinal) &&
                WarThunderServerConfig.Detect(testConfig) == GameServerChannel.Test);
            Check("Server switch round-trip and idempotence",
                WarThunderServerConfig.Apply(testConfig, GameServerChannel.Test) == testConfig &&
                WarThunderServerConfig.Detect(WarThunderServerConfig.Apply(testConfig, GameServerChannel.Live)) == GameServerChannel.Live);
            bool rejectedIncomplete = false;
            try { WarThunderServerConfig.Apply("yunetwork{curCircuit:t=\"dev\"", GameServerChannel.Live); }
            catch (InvalidDataException) { rejectedIncomplete = true; }
            Check("Incomplete server config is rejected without rewriting", rejectedIncomplete);
            string configFile = Path.Combine(tempRoot, "config.blk");
            File.WriteAllText(configFile, configSample);
            WarThunderServerConfig.ApplyToFile(configFile, GameServerChannel.Test);
            Check("Atomic server config file switch", File.ReadAllText(configFile) == testConfig);

            string logRoot = Path.Combine(tempRoot, "branch-validation");
            Directory.CreateDirectory(Path.Combine(logRoot, ".launcher_log"));
            string logFile = Path.Combine(logRoot, ".launcher_log", "old.log");
            File.WriteAllText(logFile, "curCircuit = dev\ngame version: 2.59.0.27");
            File.SetLastWriteTimeUtc(logFile, DateTime.UtcNow.AddMinutes(-10));
            Check("Old launcher log cannot complete a fresh switch",
                !GameServices.IsLauncherValidatedBuild(logRoot, GameServerChannel.Test, sharedVersion, DateTimeOffset.UtcNow));
            using (GameServices offline = new(new VersionHandler(null)))
            {
                VersionCheck result = offline.CheckVersionAsync(logRoot, GameServerChannel.Live, CancellationToken.None).GetAwaiter().GetResult();
                Check("Offline service reports unknown version without forcing update",
                    result.Latest == null && !result.UpdateAvailable);
            }
            using (GameServices online = new(new VersionHandler("2.59.0.27")))
            {
                VersionCheck wrong = online.CheckVersionAsync(logRoot, GameServerChannel.Live, CancellationToken.None).GetAwaiter().GetResult();
                VersionCheck correct = online.CheckVersionAsync(logRoot, GameServerChannel.Test, CancellationToken.None).GetAwaiter().GetResult();
                Check("Same-number server switch does not force update", wrong.BranchMismatch && wrong.Current && !wrong.UpdateAvailable);
                Check("Version service accepts matching branch and build", correct.Current && !correct.UpdateAvailable);
            }
            using (GameServices invalidResponse = new(new VersionHandler("<html>error 2.59.0.27</html>")))
                Check("HTML update service response is not a version",
                    invalidResponse.LatestAsync(GameServerChannel.Live, CancellationToken.None).GetAwaiter().GetResult() == null);
            VersionCheck sameNumberWrongBranch = new(sharedVersion, sharedVersion, "branch mismatch", BranchMismatchDetected: true);
            Check("Same-number Live/Test branch switch stays launchable",
                !sameNumberWrongBranch.UpdateAvailable && sameNumberWrongBranch.Current && sameNumberWrongBranch.BranchMismatch);
            VersionCheck ordinaryPatch = new(new Version(2, 59, 0, 22), sharedVersion, "version update");
            Check("Numeric patch mismatch is not mislabeled as branch mismatch",
                ordinaryPatch.UpdateAvailable && !ordinaryPatch.Current && !ordinaryPatch.BranchMismatch);
            Check("App updater detects patch releases",
                AppUpdateVersion.IsNewer(new Version(2, 1), new Version(2, 1, 1)));
            Check("App updater treats equivalent versions as current",
                !AppUpdateVersion.IsNewer(new Version(2, 1), new Version(2, 1, 0)) &&
                !AppUpdateVersion.IsNewer(new Version(2, 1, 1), new Version(2, 1)) &&
                AppUpdateVersion.IsSameRelease(new Version(2, 1, 1), new Version(2, 1, 1, 0)));
            Check("App updater parses release tags",
                AppUpdateVersion.ParseReleaseVersion("v2.1.1") == new Version(2, 1, 1) &&
                AppUpdateVersion.ParseReleaseVersion("not-a-version") is null);
            Check("App updater uses the ZIP app version when the GitHub build tag differs",
                AppUpdateVersion.ReleaseAppVersion("v2.2", "WTVRSettingsAssistant-v2.0.2.zip") == new Version(2, 0, 2) &&
                AppUpdateVersion.ReleaseAppVersion("v2.0.3", "WTVRSettingsAssistant-v2.0.3.zip") == new Version(2, 0, 3) &&
                AppUpdateVersion.IsNewer(new Version(2, 0, 2), new Version(2, 0, 3)) &&
                !AppUpdateVersion.IsNewer(new Version(2, 0, 2), new Version(2, 0, 2)));

            HashSet<string> xboxButtons = XInputController.CreatePressedForTest(0, 0x1000 | 0x0001);
            Check("Xbox binding token mapping", xboxButtons.Contains("XI:0:B1") &&
                  xboxButtons.Contains("XI:0:POV1:Up") &&
                  XInputController.DisplayToken("XI:0:B1").Contains("Xbox Controller 1", StringComparison.Ordinal));
            // Advanced Switches use a compact one-based button mask. Exercise the
            // deterministic mapping helper as well (without requiring hardware).
            Check("Xbox advanced-switch mapping",
                  XInputController.CreatePressedForTest(1, 0x2000 | 0x0008).Contains("XI:1:B2") &&
                  XInputController.CreatePressedForTest(1, 0x2000 | 0x0008).Contains("XI:1:POV1:Right"));

            IReadOnlyList<string> vtrimFailures = VTrimSelfTest.Run();
            if (vtrimFailures.Count == 0) Pass("VTrim simulated control logic");
            else Fail("VTrim simulated control logic", string.Join(" | ", vtrimFailures));

            // Publish layout checks. These are intentionally tolerant of the
            // native WebView2 loader/runtime layout chosen by the SDK.
            string baseDir = AppContext.BaseDirectory;
            Check("Published executable present", File.Exists(Path.Combine(baseDir, "WTVRSettingsAssistant.exe")) ||
                  string.Equals(Path.GetFileName(Environment.ProcessPath), "WTVRSettingsAssistant.exe", StringComparison.OrdinalIgnoreCase));
            Check("No unused WebView2 WPF wrapper", !File.Exists(Path.Combine(baseDir, "Microsoft.Web.WebView2.Wpf.dll")));
            Check("No WebView2 XML clutter", !Directory.EnumerateFiles(baseDir, "Microsoft.Web.WebView2*.xml", SearchOption.TopDirectoryOnly).Any());

            try
            {
                string browserVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrWhiteSpace(browserVersion))
                    Warn("WebView2 runtime", "runtime version was empty; native Home fallback remains available");
                else
                    Pass("WebView2 runtime " + browserVersion);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                Warn("WebView2 runtime", "Evergreen runtime not installed; native Home fallback remains available");
            }
            catch (Exception ex)
            {
                Warn("WebView2 runtime", ex.GetType().Name + ": " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Fail("Self-test runner", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            AppThemeAssets.SetActiveTheme(AppThemeAssets.DefaultTheme);
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }

        report.Add("");
        report.Add($"RESULT: {(failures.Count == 0 ? "PASS" : "FAIL")}  |  failures={failures.Count}  warnings={warnings.Count}");
        report.Add("Hardware note: DirectInput/XInput/vJoy logic is simulated here; physical HOTAS/Xbox devices still require a real-device acceptance test.");

        try
        {
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "self-test-report.txt"), report);
        }
        catch { }

        return failures.Count == 0 ? 0 : 1;
    }

    private static AdvancedSwitchService NewSwitchService(string path) =>
        new(path, new FakeOutput(), _ => null, () => DateTime.UtcNow, (_, _) => { });
}
