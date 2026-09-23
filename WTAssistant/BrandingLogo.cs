namespace WTVRSettingsAssistant;

/// <summary>Loads the final theme-specific WT VR Assistant logo artwork.</summary>
internal static class BrandingLogo
{
    public static Bitmap Create()
    {
        using Image source = AssetManager.LoadImage("MainLogo.png");
        return new Bitmap(source);
    }
}
