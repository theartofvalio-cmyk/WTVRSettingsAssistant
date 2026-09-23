using System.Globalization;
using System.Text.Json;

namespace HOTASTrimUtility;

internal static class WarThunderTelemetry
{
    public static LateralSample? ParseLateral(string indicators, string state, double time)
    {
        try
        {
            using var instruments = JsonDocument.Parse(indicators);
            using var flight = JsonDocument.Parse(state);
            var i = instruments.RootElement; var s = flight.RootElement;
            if (!Valid(i) || !Valid(s) || !double.IsFinite(time) ||
                !i.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(type.GetString()) ||
                !Number(s, "IAS, km/h", out double speed) || speed < 0 || speed > 5000) return null;
            double? bank = Number(i, "aviahorizon_roll", out double roll) && Math.Abs(roll) <= 180 ? -roll : null;
            double? slip = Number(s, "AoS, deg", out double aos) && Math.Abs(aos) <= 180 ? aos : null;
            return new(time, type.GetString()!, speed, bank, slip);
        }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
    }


    public static bool TryGetAircraftIdentity(string indicators, string state, out string aircraftType)
    {
        aircraftType = string.Empty;
        try
        {
            using var instruments = JsonDocument.Parse(indicators);
            using var flight = JsonDocument.Parse(state);
            JsonElement i = instruments.RootElement, s = flight.RootElement;
            if (!Valid(i) || !Valid(s)) return false;
            if (!i.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(type.GetString())) return false;
            // IAS is specific to aircraft flight telemetry and prevents tank/ship
            // sessions from creating VTrim aircraft profiles.
            if (!Number(s, "IAS, km/h", out double speed) || speed < 0 || speed > 5000) return false;
            aircraftType = type.GetString()!.Trim();
            return aircraftType.Length > 0;
        }
        catch (JsonException) { return false; }
        catch (ArgumentException) { return false; }
    }

    public static bool TryParse(string indicators, string state, double time, out AircraftSample sample) =>
        TryParse(indicators, state, time, out sample, out _);

    public static bool TryParse(string indicators, string state, double time,
        out AircraftSample sample, out string reason)
    {
        sample = default;
        reason = "Waiting for valid pitch telemetry.";
        try
        {
            using var instruments = JsonDocument.Parse(indicators);
            using var flight = JsonDocument.Parse(state);
            JsonElement i = instruments.RootElement, s = flight.RootElement;
            if (!Valid(i) || !Valid(s) || !double.IsFinite(time))
            { reason = "War Thunder is not reporting a valid aircraft flight."; return false; }
            if (!i.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(type.GetString()))
            { reason = "Aircraft type is missing from /indicators."; return false; }
            if (!Number(i, "aviahorizon_pitch", out double instrumentPitch) || Math.Abs(instrumentPitch) > 90)
            { reason = "No usable aviahorizon_pitch in /indicators. Pitch hold is unavailable."; return false; }
            if (!Number(s, "IAS, km/h", out double speed) || speed < 0 || speed > 5000)
            { reason = "No valid IAS, km/h in /state."; return false; }

            // The artificial horizon moves opposite the nose. This convention
            // is also used by PowerBroker2/WarThunder telemetry.py.
            // Do NOT use engine 'pitch 1, deg', AoA, or Wx as aircraft pitch/rate.
            // Rate is derived from successive nose-up-positive attitude samples.
            sample = new(time, type.GetString()!, 0, -instrumentPitch, speed, 0, 0, false, false);
            reason = "Pitch: -aviahorizon_pitch; pitch rate: measured attitude slope.";
            return true;
        }
        catch (JsonException) { reason = "Invalid JSON from local flight telemetry."; return false; }
        catch (ArgumentException) { reason = "Empty telemetry document."; return false; }
    }

    private static bool Valid(JsonElement root) => root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("valid", out var value) &&
        (value.ValueKind == JsonValueKind.True ||
         value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && n == 1);

    private static bool Number(JsonElement root, string key, out double value)
    {
        value = 0;
        return root.TryGetProperty(key, out var field) &&
            (field.ValueKind == JsonValueKind.Number && field.TryGetDouble(out value) ||
             field.ValueKind == JsonValueKind.String && double.TryParse(field.GetString(),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && double.IsFinite(value);
    }
}
