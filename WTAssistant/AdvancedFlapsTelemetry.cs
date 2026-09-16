using System.Net.Http;
using System.Text.Json;

namespace WTVRSettingsAssistant;

internal interface IFlapTelemetry : IDisposable
{
    FlapSample? Sample { get; }
    void Refresh(DateTime now);
}

internal sealed class AdvancedFlapsTelemetry : IFlapTelemetry
{
    private readonly HttpClient _client = new(new HttpClientHandler { UseProxy = false })
    { BaseAddress = new Uri("http://127.0.0.1:8111/"), Timeout = TimeSpan.FromMilliseconds(350) };
    private readonly CancellationTokenSource _shutdown = new();
    private bool _pending, _disposed;
    private DateTime _requested;
    public FlapSample? Sample { get; private set; }

    public async void Refresh(DateTime now)
    {
        if (_disposed || _pending || now - _requested < TimeSpan.FromMilliseconds(50)) return;
        _pending = true; _requested = now;
        try
        {
            Task<string> state = _client.GetStringAsync("state", _shutdown.Token);
            Task<string> indicators = _client.GetStringAsync("indicators", _shutdown.Token);
            await Task.WhenAll(state, indicators);
            if (!_disposed) Sample = Parse(await state, await indicators, now);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ObjectDisposedException)
        { Sample = null; }
        finally { _pending = false; }
    }

    internal static FlapSample? Parse(string state, string indicators, DateTime time)
    {
        try
        {
            using var s = JsonDocument.Parse(state);
            using var i = JsonDocument.Parse(indicators);
            var flight = s.RootElement; var instruments = i.RootElement;
            if (!Valid(flight) || !Valid(instruments) ||
                !instruments.TryGetProperty("army", out var army) || army.GetString() != "air" ||
                !instruments.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                Number(flight, "flaps, %") is not { } flaps) return null;
            // H, m is altitude above sea level, NOT ground clearance. Only use
            // the explicitly named radio-altimeter field for the ground heuristic.
            var sample = new FlapSample(time, type.GetString()!, flaps, Number(flight, "IAS, km/h") ?? 0, Optional(flight, "gear, %", 0, 100),
                Optional(flight, "Vy, m/s", -1000, 1000), Optional(instruments, "radio_altitude", 0, 100000));
            return sample.Valid ? sample : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return null; }
    }
    private static bool Valid(JsonElement e) => e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty("valid", out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n == 1);
    private static double? Number(JsonElement e, string key) => e.TryGetProperty(key, out var v) &&
        v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double n) && double.IsFinite(n) ? n : null;
    private static double? Optional(JsonElement e, string key, double minimum, double maximum) =>
        Number(e, key) is { } n && n >= minimum && n <= maximum ? n : null;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Sample = null; _shutdown.Cancel(); _client.Dispose(); _shutdown.Dispose();
    }
}
