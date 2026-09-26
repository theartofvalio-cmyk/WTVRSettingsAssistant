namespace WTVRSettingsAssistant;

internal static class AppUpdateVersion
{
    internal static Version? ParseReleaseVersion(string tag)
    {
        string normalized = tag.Trim().TrimStart('v', 'V');
        int prereleaseSeparator = normalized.IndexOf('-');
        if (prereleaseSeparator >= 0) normalized = normalized[..prereleaseSeparator];
        return Version.TryParse(normalized, out Version? version) ? version : null;
    }

    // GitHub release titles and asset labels may use a separate build number.
    // Prefer the app version encoded in a versioned ZIP name when available.
    internal static Version? ParsePackageVersion(string assetName)
    {
        const string prefix = "WTVRSettingsAssistant-";
        const string suffix = ".zip";
        if (!assetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !assetName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;

        string version = assetName[prefix.Length..^suffix.Length];
        return ParseReleaseVersion(version);
    }

    internal static Version? ReleaseAppVersion(string tag, string? packageName) =>
        packageName is null ? ParseReleaseVersion(tag) :
        ParsePackageVersion(packageName) ?? ParseReleaseVersion(tag);

    internal static bool IsNewer(Version current, Version latest) => Normalize(latest) > Normalize(current);

    internal static bool IsSameRelease(Version left, Version right) => Normalize(left) == Normalize(right);

    private static Version Normalize(Version value) =>
        new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
}
