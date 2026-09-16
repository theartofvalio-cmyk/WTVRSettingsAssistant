using System.Text.Json;
using WTVRSettingsAssistant;

internal static class AdvancedFlapsChecks
{
    internal static AdvancedFlapsSettings Settings() => new()
    {
        Enabled = true,
        FlapsUp = new() { OutputKind = SwitchActionKind.KeyboardKey, OutputKey = "O" },
        FlapsDown = new() { OutputKind = SwitchActionKind.KeyboardKey, OutputKey = "I" }
    };

    public static int Run()
    {
        int count = 0;
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); count++; }
        var now = DateTime.UtcNow;
        const string state = """{"valid":true,"flaps, %":43}""";
        const string indicators = """{"valid":true,"army":"air","type":"test"}""";
        Check(AdvancedFlapsTelemetry.Parse(state, indicators, now) is { Percent: 43 }, "Feedback works without speed");
        Check(AdvancedFlapsTelemetry.Parse(state.Replace("43", "101"), indicators, now) is null, "Out-of-range flap percent rejected");
        Check(AdvancedFlapsTelemetry.Parse(state.Replace("true", "false"), indicators, now) is null, "Invalid state rejected");
        Check(AdvancedFlapsTelemetry.Parse(state, indicators.Replace("air", "tank"), now) is null, "Non-aircraft rejected");
        Check(AdvancedFlapsTelemetry.Parse("[]", "{}", now) is null, "Wrong JSON shape rejected");
        Check(AdvancedFlapsTelemetry.Parse(state.Replace("flaps, %", "engine flaps"), indicators, now) is null, "Missing flaps rejected");
        Check(AdvancedFlapsTelemetry.Parse(state, indicators.Replace("\"air\"", "2"), now) is null, "Malformed army rejected");
        var settings = Settings();
        Check(settings.IsValid, "Settings valid without speed limits or profiles");
        settings.FlapsDown.OutputKey = settings.FlapsUp.OutputKey;
        Check(!settings.IsValid, "Identical output commands rejected");
        settings = Settings(); settings.FlapsDown.OutputKind = SwitchActionKind.VJoyButton;
        Check(!settings.IsValid, "Flaps cannot silently route to a virtual button");
        var old = JsonSerializer.Serialize(Settings()).TrimEnd('}') + ",\"CombatDeployKmh\":-10,\"LandingRetractKmh\":0,\"AircraftProfiles\":[]}";
        var migrated = JsonSerializer.Deserialize<AdvancedFlapsSettings>(old)!;
        Check(migrated.IsValid, "Old thresholds ignored when loading existing settings");
        Check(!JsonSerializer.Serialize(migrated).Contains("Kmh"), "Obsolete thresholds no longer saved");
        Check(!JsonSerializer.Deserialize<SwitchDefinition>("""{"Name":"Old","States":[]}""")!.AdvancedFlaps.Enabled, "Legacy switches not opted in");
        var controller = new AdvancedFlapsController();
        var sample = new FlapSample(now, "test", 0, 0, null, null, null);
        for (int i = 0; i < 4; i++)
            Check(controller.Step(now.AddMilliseconds(i * 100), AdvancedFlapMode.Combat, sample, Settings()) is null, "Repeated timestamp cannot establish stability");
        Check(controller.Step(now.AddSeconds(2), AdvancedFlapMode.Combat, sample, Settings()) is null && !controller.Pending, "Stale data clears pending output");
        Console.WriteLine($"PASS: {count} flap parser/settings/freshness checks.");
        return count;
    }
}

internal sealed class FlapTestTelemetry : IFlapTelemetry
{
    public FlapSample? Sample { get; set; }
    public bool Disposed { get; private set; }
    public void Refresh(DateTime now) { }
    public void Dispose() => Disposed = true;
}
