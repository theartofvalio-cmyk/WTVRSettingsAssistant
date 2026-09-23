using System.Reflection;

namespace WTVRSettingsAssistant;

/// <summary>
/// Central resource resolver for theme-aware artwork.
/// Theme folders may override only the artwork they need; missing files fall
/// back to the F-16 default theme, then shared resources, then the legacy Main
/// folder for compatibility with older source packages.
/// </summary>
internal static class AppThemeAssets
{
    public const string DefaultTheme = "F16";
    public const string LegacyTheme = "Main";

    private static readonly string[] ThemeOrder = ["F16", "MiG29"];

    public static string ActiveTheme { get; private set; } = DefaultTheme;

    public static IReadOnlyList<string> AvailableThemes => ThemeOrder;

    public static string DisplayName(string themeName) => NormalizeThemeName(themeName) switch
    {
        "MiG29" => "MiG-29",
        _ => "F-16"
    };

    public static void SetActiveTheme(string? themeName)
        => ActiveTheme = NormalizeThemeName(themeName);

    public static string NormalizeThemeName(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName)) return DefaultTheme;

        string cleaned = new string(themeName
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());

        if (cleaned.Equals(LegacyTheme, StringComparison.OrdinalIgnoreCase))
            return DefaultTheme;

        foreach (string known in ThemeOrder)
            if (cleaned.Equals(known, StringComparison.OrdinalIgnoreCase))
                return known;

        return DefaultTheme;
    }

    public static string? ResolveResourceName(Assembly assembly, string fileName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        string leafName = Path.GetFileName(fileName);
        string fileSuffix = "." + leafName;
        string[] names = assembly.GetManifestResourceNames();

        string? FindTheme(string theme) => names.FirstOrDefault(name =>
            name.Contains($".Assets.Themes.{theme}.", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase));

        string? themed = FindTheme(ActiveTheme);
        if (themed is not null) return themed;

        if (!ActiveTheme.Equals(DefaultTheme, StringComparison.OrdinalIgnoreCase))
        {
            string? defaultTheme = FindTheme(DefaultTheme);
            if (defaultTheme is not null) return defaultTheme;
        }

        const string sharedMarker = ".Assets.Shared.";
        string? shared = names.FirstOrDefault(name =>
            name.Contains(sharedMarker, StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase));
        if (shared is not null) return shared;

        // Older 2.x source packages used Assets/Themes/Main. Keep it as a final
        // compatibility source so an incremental update never loses artwork.
        string? legacy = FindTheme(LegacyTheme);
        if (legacy is not null) return legacy;

        // Backward-compatible fallback for module resources and any legacy package layout.
        return names.FirstOrDefault(name =>
                   name.Contains(".Assets.", StringComparison.OrdinalIgnoreCase) &&
                   name.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase))
               ?? names.FirstOrDefault(name =>
                   name.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase));
    }
}
