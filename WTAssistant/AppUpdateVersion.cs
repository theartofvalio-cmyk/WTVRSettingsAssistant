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

    internal static bool IsNewer(Version current, Version latest) => Normalize(latest) > Normalize(current);

    internal static bool IsSameRelease(Version left, Version right) => Normalize(left) == Normalize(right);

    private static Version Normalize(Version value) =>
        new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
}
