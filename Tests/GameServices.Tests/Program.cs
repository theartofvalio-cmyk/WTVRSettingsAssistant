using System.Text;
using WTVRSettingsAssistant;

int checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException(description);
}

Check(GameServices.ParseVersion("2.58.0.27") == new Version(2, 58, 0, 27), "Four-part version");
Check(GameServices.ParseVersion("NOITEM") == null, "Missing version");
Check(GameServices.ParseVersion("2.58.0") == null, "Reject incomplete version");
Check(GameServices.ParseVersion("") == null, "Empty version");
Check(new VersionCheck(new(2, 58, 0, 9), new(2, 58, 0, 10), "").UpdateAvailable, "Numeric, not lexical, comparison");
Check(new VersionCheck(new(2, 58, 0, 10), new(2, 58, 0, 10), "").Current, "Matching versions");
Check(!new VersionCheck(null, new(2, 58, 0, 10), "").Current, "Unknown is not current");
Check(!new VersionCheck(new(2, 58, 0, 10), null, "").UpdateAvailable, "Unknown is not an update");

Check(GameServices.PreferInstalledVersion(
    new Version(2, 57, 1, 135),
    new Version(2, 57, 1, 134),
    new[] { new Version(2, 57, 1, 133) },
    new[] { new Version(2, 57, 1, 132) }) == new Version(2, 57, 1, 135),
    "warthunder.yup wins over stale executable/log/marker versions");
Check(GameServices.PreferInstalledVersion(
    null,
    new Version(2, 57, 1, 135),
    new[] { new Version(2, 57, 1, 134) },
    new[] { new Version(2, 57, 1, 133) }) == new Version(2, 57, 1, 135),
    "launcher yup_version log wins when the local manifest is temporarily unavailable");
Check(GameServices.PreferInstalledVersion(
    null,
    null,
    new[] { new Version(2, 57, 1, 135) },
    new[] { new Version(2, 57, 1, 134) }) == new Version(2, 57, 1, 135),
    "aces.exe is a fallback only");
Check(GameServices.PreferInstalledVersion(
    null,
    null,
    Array.Empty<Version>(),
    new[] { new Version(2, 57, 1, 134), new Version(2, 57, 1, 135) }) == new Version(2, 57, 1, 135),
    "highest root marker is final fallback");

Check(GameServices.TryOpeningWindow("When: From September 4th (16:30 GMT) Until: September 10th (07:00 GMT)", 2026, out var start, out var end), "Official schedule format");
Check(start == new DateTimeOffset(2026, 9, 4, 16, 30, 0, TimeSpan.Zero), "Opening date UTC");
Check(end == new DateTimeOffset(2026, 9, 10, 7, 0, 0, TimeSpan.Zero), "Closing date UTC");
Check(!GameServices.TryOpeningWindow("Unrecognized schedule", 2026, out _, out _), "Unknown format");
Check(GameServices.TryOpeningWindow("When: From December 30th (16:30 GMT) Until: January 2nd (07:00 GMT)", 2026, out start, out end) && end.Year == 2027, "Year rollover");
Check(!GameServices.TryOpeningWindow("When: From September 10th (16:30 GMT) Until: September 4th (07:00 GMT)", 2026, out _, out _), "Reject backwards schedule");

string original = "video{driver:t=\"dx12\"}\nyunetwork { curCircuit : t = \"dev\"\n customPort:i=1234\n}\naudio{volume:r=0.5}\n";
Check(WarThunderServerConfig.Detect(original) == GameServerChannel.Test, "Whitespace in circuit");
string live = WarThunderServerConfig.Apply(original, GameServerChannel.Live);
Check(WarThunderServerConfig.Detect(live) == GameServerChannel.Live, "Change circuit");
Check(live.Contains("curCircuit:t=\"production\""), "Live circuit value");
Check(!live.Contains("customPort:i=1234"), "Old yunetwork block replaced exactly");
Check(live.Contains("video{driver:t=\"dx12\"}"), "Preserve graphics block");
Check(live.Contains("audio{volume:r=0.5}"), "Preserve settings after yunetwork");
Check(WarThunderServerConfig.Apply(live, GameServerChannel.Live) == live, "Idempotent live circuit update");
string dev = WarThunderServerConfig.Apply(live, GameServerChannel.Test);
Check(WarThunderServerConfig.Detect(dev) == GameServerChannel.Test, "Change back to dev circuit");
Check(dev.Contains("isExpertMode:b=yes"), "Dev expert mode");
Check(dev.Contains("webStatusLocalhostOnly:b=yes"), "Dev web status localhost");
Check(dev.Contains("webStatusPort:i=23456"), "Dev web status port");
Check(dev.Contains("enableWebStatus:b=no"), "Dev web status disabled");
Check(dev.Contains("video{driver:t=\"dx12\"}") && dev.Contains("audio{volume:r=0.5}"), "Preserve custom non-network settings");

string root = Path.Combine(Path.GetTempPath(), "WTAssistant-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    AtomicFile.WriteText(Path.Combine(root, "version"), "2.58.0.27");
    Check(GameServices.ReadInstalledVersion(root) == new Version(2, 58, 0, 27), "Version marker fallback");

    // Real .yup files are bencoded. The installed build is nested under the
    // yup/version key; content-only patches can change this while aces.exe's
    // PE file version remains unchanged.
    File.WriteAllBytes(Path.Combine(root, "warthunder.yup"),
        Encoding.ASCII.GetBytes("d3:yupd7:version9:2.58.0.29e4:infodee"));
    Check(GameServices.ReadYupVersion(Path.Combine(root, "warthunder.yup")) == new Version(2, 58, 0, 29), "Parse bencoded yup/version");
    Check(GameServices.ReadInstalledVersion(root) == new Version(2, 58, 0, 29), "YUP manifest overrides stale marker");

    File.Delete(Path.Combine(root, "warthunder.yup"));
    string logDir = Path.Combine(root, ".launcher_log");
    Directory.CreateDirectory(logDir);
    File.WriteAllText(Path.Combine(logDir, "latest.log"), "initGameVersionSettings: yup_version = 2.58.0.30\n");
    Check(GameServices.ReadLauncherLogVersion(root) == new Version(2, 58, 0, 30), "Parse launcher yup_version fallback");
    Check(GameServices.ReadInstalledVersion(root) == new Version(2, 58, 0, 30), "Launcher log overrides stale marker when YUP unavailable");

    AtomicFile.WriteText(Path.Combine(root, "version"), "2.58.0.28");
    Check(File.ReadAllText(Path.Combine(root, "version")) == "2.58.0.28", "Atomic replacement");
    Check(Directory.GetFiles(root, "*.tmp").Length == 0, "No temporary files left");
    Check(GameInstallations.Overlaps(root, root), "Same folder rejected");
    Check(GameInstallations.Overlaps(root, Path.Combine(root, "dev")), "Nested folder rejected");
    Check(!GameInstallations.Overlaps(root, root + "-dev"), "Sibling allowed");
}
finally { Directory.Delete(root, true); }

Console.WriteLine($"PASS: {checks} source-linked game-service checks.");
