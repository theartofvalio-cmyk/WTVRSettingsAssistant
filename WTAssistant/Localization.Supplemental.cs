namespace WTVRSettingsAssistant;

internal static partial class AppText
{
    // Shared additions are grouped by language so new UI text never depends
    // on the legacy partial catalogs or their English fallback copies.
    private static readonly Dictionary<string, Dictionary<string, string>> SupplementalTexts = new()
    {
        ["en"] = new() { ["Neck.RuntimeSupport"] = "Works automatically with SteamVR, OpenXR and VDXR." },
        ["bg"] = BulgarianCorrections(),
        ["es"] = new() { ["Neck.RuntimeSupport"] = "Funciona automáticamente con SteamVR, OpenXR y VDXR." },
        ["de"] = new() { ["Neck.RuntimeSupport"] = "Funktioniert automatisch mit SteamVR, OpenXR und VDXR." },
        ["fr"] = new() { ["Neck.RuntimeSupport"] = "Fonctionne automatiquement avec SteamVR, OpenXR et VDXR." },
        ["pt"] = new() { ["Neck.RuntimeSupport"] = "Funciona automaticamente com SteamVR, OpenXR e VDXR." },
        ["pl"] = new() { ["Neck.RuntimeSupport"] = "Działa automatycznie ze SteamVR, OpenXR i VDXR." },
        ["ru"] = new() { ["Neck.RuntimeSupport"] = "Автоматически работает со SteamVR, OpenXR и VDXR." },
        ["tr"] = new() { ["Neck.RuntimeSupport"] = "SteamVR, OpenXR ve VDXR ile otomatik olarak çalışır." },
        ["el"] = new() { ["Neck.RuntimeSupport"] = "Λειτουργεί αυτόματα με SteamVR, OpenXR και VDXR." },
        ["ro"] = new() { ["Neck.RuntimeSupport"] = "Funcționează automat cu SteamVR, OpenXR și VDXR." },
        ["zh-Hans"] = new() { ["Neck.RuntimeSupport"] = "自动支持 SteamVR、OpenXR 和 VDXR。" }
    };
}
