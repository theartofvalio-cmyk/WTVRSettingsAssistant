using System.Text.Json;

namespace WTVRSettingsAssistant;

/// <summary>
/// Idempotent on-disk migrations for obsolete layout state and exact known
/// files from older app packages. Functional profiles, bindings, game paths,
/// VTrim data, and unrecognized user files are never removed here.
/// </summary>
internal static class AppUpgradeMigration
{
    private const int CurrentSchema = 234;

    // Exact files shipped by older releases. Never sweep a directory: users
    // sometimes store exported controls and other personal files beside the app.
    private static readonly string[] RetiredAppFiles =
    [
        "WTVRSettingsAssistant.deps.json",
        "WTVRSettingsAssistant.dll",
        "WTVRSettingsAssistant.pdb",
        "WTVRSettingsAssistant.runtimeconfig.json",
        "Microsoft.Web.WebView2.Wpf.dll",
        "Microsoft.Web.WebView2.Core.xml",
        "Microsoft.Web.WebView2.WinForms.xml",
        "Microsoft.Web.WebView2.Wpf.xml",
        "NeckAssist/OpenXR/LICENSE-XRNeckSafer.txt",
        "NeckAssist/OpenXR/XR_APILAYER_NOVENDOR_XRNeckSafer.dll",
        "NeckAssist/OpenXR/XR_APILAYER_NOVENDOR_XRNeckSafer.json"
    ];

    private sealed class MigrationState
    {
        public int Schema { get; set; }
        public DateTime LastRunUtc { get; set; }
    }

    private static readonly string[] RetiredLayoutFiles =
    [
        "layout.json",
        "about_layout.json",
        "settings_layout.json",
        "recommended_layout.json",
        "recommended_selection_layout_v3.json",
        "recommended_viewer_layout.json",
        "illustrated_layout.json",
        "illustrated_profiles.json",
        "illustrated_info.json"
    ];

    public static void Run(string settingsFolder)
    {
        try
        {
            Directory.CreateDirectory(settingsFolder);
            string statePath = Path.Combine(settingsFolder, "migration_state.json");
            int previousSchema = ReadSchema(statePath);

            if (previousSchema < CurrentSchema)
            {
                // A 1.5/2.0 updater can leave former runtime files behind. The
                // new single-file release removes only known obsolete files.
                // In the published single-file app this is empty by design.
#pragma warning disable IL3000
                if (string.IsNullOrEmpty(typeof(AppUpgradeMigration).Assembly.Location))
#pragma warning restore IL3000
                    CleanRetiredAppFiles(Path.GetDirectoryName(settingsFolder)!);

                foreach (string fileName in RetiredLayoutFiles)
                {
                    string path = Path.Combine(settingsFolder, fileName);
                    try
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                    catch
                    {
                        // A stale layout file is non-critical. Never fail startup
                        // or risk functional settings because cleanup was blocked.
                    }
                }

                // 2.3.1 retires the generic control-mirroring WebView surface used
                // by early 2.3.0 test builds. It never contained user data. Keeping
                // the cache can make an upgraded install appear to reuse stale CSS.
                try
                {
                    string pageCache = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WTVRSettingsAssistant", "WebUiCache", "Pages");
                    if (Directory.Exists(pageCache)) Directory.Delete(pageCache, recursive: true);
                }
                catch { }
            }

            MigrationState state = new()
            {
                Schema = CurrentSchema,
                LastRunUtc = DateTime.UtcNow
            };
            AtomicFile.WriteText(statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Migration must never prevent the app from starting.
        }
    }

    private static int ReadSchema(string path)
    {
        try
        {
            if (!File.Exists(path)) return 0;
            MigrationState? state = JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(path));
            return state?.Schema ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    internal static IReadOnlyList<string> CleanRetiredAppFiles(string installRoot)
    {
        List<string> removed = new();
        foreach (string relativePath in RetiredAppFiles)
        {
            string path = Path.Combine(installRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (!File.Exists(path)) continue;
                File.Delete(path);
                removed.Add(relativePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        // Remove empty former runtime directories, preserving any user file
        // placed there under an unrecognized name.
        foreach (string relativePath in new[] { "NeckAssist/OpenXR", "NeckAssist" })
        {
            string path = Path.Combine(installRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                    Directory.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return removed;
    }
}
