namespace WTVRSettingsAssistant;

internal sealed record AircraftFlapLayout(bool Combat, bool Takeoff);

internal static class AircraftFlapLayouts
{
    // Exact variant IDs only. Sources checked 2026-09-13:
    // https://wiki.warthunder.com/unit/f-4s
    // https://wiki.warthunder.com/unit/f_16a_block_10
    // https://wiki.warthunder.com/unit/f_16c_block_50
    private static readonly Dictionary<string, AircraftFlapLayout> BuiltIn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["f-4s"] = new(true, true),
        ["f_4s"] = new(true, true),
        ["f_16a_block_10"] = new(false, true),
        ["f_16c_block_50"] = new(false, true)
    };

    public static AircraftFlapLayout? Find(string aircraft, AdvancedFlapsSettings settings)
    {
        if (settings.AircraftLayouts?.TryGetValue(aircraft, out var saved) == true && saved is not null) return saved;
        return BuiltIn.GetValueOrDefault(aircraft);
    }
}
