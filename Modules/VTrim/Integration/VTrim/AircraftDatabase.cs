using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HOTASTrimUtility;

internal sealed record AircraftInfo(string Id, string DisplayName, string Nation, string VehicleType,
    Dictionary<string, string> BattleRatings, string? IconUrl, string? BackgroundUrl = null,
    string? FrameUrl = null, string[]? SearchAliases = null, bool IsPremium = false, string? FlightCategory = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string SearchLabel => $"{DisplayName} - {Nation} ({VehicleType})";
}
internal sealed record AircraftIndexMetadata(int SchemaVersion, string Provider, DateTime LastSuccessfulRefreshUtc,
    string ContentHash, string? ETag = null, string? LastModified = null);
internal interface IAircraftDataProvider
{
    Task<IReadOnlyList<AircraftInfo>> FetchAsync(CancellationToken token);
}

internal sealed class WarThunderWikiAircraftProvider : IAircraftDataProvider
{
    private readonly HttpClient _http;
    public WarThunderWikiAircraftProvider(HttpClient http) => _http = http;
    // Verified 2026-09-15: /aviation embeds window.WT_UnitList as a JSON array
    // in a JS string. Rows: ID, name, nation, rank, BR indices, status, tags, role.
    // Wiki common.js convertBR uses (index / 3 + 1).toFixed(1).
    // This adapter is the only component aware of Wiki page structure.
    public async Task<IReadOnlyList<AircraftInfo>> FetchAsync(CancellationToken token)
    {
        var all = new Dictionary<string, AircraftInfo>(StringComparer.Ordinal);
        foreach (string roster in new[] { "aviation", "helicopters" })
        {
            string page = await _http.GetStringAsync("https://wiki.warthunder.com/" + roster, token);
            Match embedded = Regex.Match(page, @"window\.WT_UnitList\s*=\s*'(?<json>.*?)';", RegexOptions.Singleline, TimeSpan.FromSeconds(2));
            if (!embedded.Success) throw new InvalidDataException("Wiki aircraft dataset is unavailable.");
            using var data = JsonDocument.Parse(embedded.Groups["json"].Value);
            int count = 0;
            foreach (JsonElement row in data.RootElement.EnumerateArray())
            {
                string id = row[0].GetString() ?? "";
                if (!Regex.IsMatch(id, @"^[a-zA-Z0-9_-]+$")) throw new InvalidDataException("Invalid aircraft identifier.");
                // Some event vehicles have no BR and expose [] instead of an object.
                var br = row[4].ValueKind == JsonValueKind.Object
                    ? row[4].EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number).ToDictionary(p => p.Name,
                        p => (p.Value.GetDouble() / 3 + 1).ToString("F1", CultureInfo.InvariantCulture))
                    : new Dictionary<string, string>();
                string name = AircraftSearchService.CleanName(WebUtility.HtmlDecode(row[1].GetString() ?? ""));
                string role = roster == "helicopters" ? "Helicopter" : row[7][0][1].GetString() ?? "Aircraft";
                // Use only icon URLs actually present in the roster, never fabricated assets.
                Match icon = Regex.Match(page, @"https://static\.encyclopedia\.warthunder\.com/slots/" + Regex.Escape(id) + @"\.png", RegexOptions.None, TimeSpan.FromSeconds(1));
                all[id] = new(id, name, row[2].GetString() ?? "", role, br, icon.Success ? icon.Value : null,
                    SearchAliases: new[] { AircraftSearchService.Normalize(name) }, IsPremium: row[5].GetInt32() == 1,
                    FlightCategory: roster == "helicopters" ? "Helicopter" : row[6].EnumerateArray().Any(t => t.GetInt32() is 8 or 19) ? "Jet Plane" : "Prop Plane");
                count++;
            }
            if (count == 0) throw new InvalidDataException("Empty aircraft roster.");
        }
        foreach (var aircraft in all.Values.Where(a => a.IconUrl is null).ToArray())
        {
            try
            {
                string detail = await _http.GetStringAsync("https://wiki.warthunder.com/unit/" + aircraft.Id, token);
                Match image = Regex.Match(detail, @"https://static\.encyclopedia\.warthunder\.com/(?:slots|images)/" +
                    Regex.Escape(aircraft.Id) + @"\.png", RegexOptions.None, TimeSpan.FromSeconds(1));
                if (!image.Success)
                {
                    Match primary = Regex.Match(detail, "<img\\s+class=\"game-unit_template-image\"\\s+src=\"(?<url>https://static\\.encyclopedia\\.warthunder\\.com/images/[a-zA-Z0-9_-]+\\.png)\"", RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (primary.Success) all[aircraft.Id] = aircraft with { IconUrl = primary.Groups["url"].Value };
                }
                if (image.Success) all[aircraft.Id] = aircraft with { IconUrl = image.Value };
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        }
        return all.Values.OrderBy(a => a.DisplayName, StringComparer.Ordinal).ToArray();
    }
}

internal sealed class AircraftDatabaseService
{
    private readonly string _directory;
    private readonly IAircraftDataProvider _provider;
    private readonly SemaphoreSlim _refresh = new(1);
    private AircraftInfo[] _items = [];
    public IReadOnlyList<AircraftInfo> Items => _items;
    public event Action? Changed;
    public string? LastError { get; private set; }
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public AircraftDatabaseService(string portableSettings, IAircraftDataProvider provider)
    { _directory = Path.Combine(portableSettings, "Aircraft"); _provider = provider; }
    internal static readonly AircraftInfo[] UniversalAircraft = new[] { "Prop Plane", "Jet Plane", "Helicopter" }
        .Select(type => new AircraftInfo("universal-" + type.Replace(" ", "-").ToLowerInvariant(), "Universal " + type,
            "", type, new(), null, FlightCategory: type)).ToArray();
    public AircraftInfo? GetById(string? id) => _items.Concat(UniversalAircraft).FirstOrDefault(a => a.Id == id);
    public async Task InitializeAsync(CancellationToken token)
    {
        try
        {
            string path = Path.Combine(_directory, "aircraft-index.json");
            if (File.Exists(path))
            {
                var cached = JsonSerializer.Deserialize<AircraftInfo[]>(await File.ReadAllTextAsync(path, token), JsonOptions);
                Validate(cached); _items = cached!; Changed?.Invoke();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { LastError = ex.Message; }
        await RefreshAsync(token);
    }
    public async Task RefreshAsync(CancellationToken token)
    {
        if (!await _refresh.WaitAsync(0, token)) return;
        try
        {
            var items = (await _provider.FetchAsync(token)).ToArray(); Validate(items);
            if (_items.Length > 0 && items.Length < _items.Length * 0.8)
                throw new InvalidDataException("Incomplete roster; keeping the previous aircraft cache.");
            string json = JsonSerializer.Serialize(items, JsonOptions);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            string current = JsonSerializer.Serialize(_items, JsonOptions);
            if (current != json)
            {
                Directory.CreateDirectory(_directory);
                await WriteAtomicAsync(Path.Combine(_directory, "aircraft-index.json"), items, token);
                _items = items; Changed?.Invoke();
            }
            await WriteAtomicAsync(Path.Combine(_directory, "aircraft-index.meta.json"),
                new AircraftIndexMetadata(1, "WarThunderWiki", DateTime.UtcNow, hash), token);
            LastError = null;
        }
        // An optional provider must never propagate changed source formats into the app.
        catch (Exception ex)
        { LastError = ex.Message; }
        finally { _refresh.Release(); }
    }
    private static void Validate(AircraftInfo[]? items)
    {
        if (items is not { Length: > 0 } || items.Any(a => a is null || string.IsNullOrWhiteSpace(a.Id) || string.IsNullOrWhiteSpace(a.DisplayName)) ||
            items.Select(a => a.Id).Distinct().Count() != items.Length) throw new InvalidDataException("Invalid aircraft index; previous cache retained.");
    }
    internal static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        string json = JsonSerializer.Serialize(value, JsonOptions);
        using (JsonDocument.Parse(json)) { }
        await File.WriteAllTextAsync(temporary, json, token);
        using (JsonDocument.Parse(await File.ReadAllTextAsync(temporary, token))) { }
        token.ThrowIfCancellationRequested();
        File.Move(temporary, path, true);
    }
}

internal static class AircraftSearchService
{
    internal static string CleanName(string? name)
    {
        string value = (name ?? "").Replace('\u00a0', ' ').Trim();
        int first = 0;
        while (first < value.Length && !char.IsLetterOrDigit(value[first])) first++;
        return value[first..].Trim();
    }
    internal static string Normalize(string s) => string.Concat(s.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    internal static IEnumerable<AircraftInfo> Search(IEnumerable<AircraftInfo> aircraft, string query)
    {
        string key = Normalize(query);
        return aircraft.Where(a => Normalize(a.DisplayName).Contains(key) || Normalize(a.Id).Contains(key) ||
                (a.SearchAliases?.Any(alias => Normalize(alias).Contains(key)) ?? false))
            .OrderByDescending(a => Normalize(a.DisplayName) == key).ThenBy(a => CleanName(a.DisplayName));
    }
}

internal sealed class AircraftAssetCache
{
    private readonly string _root;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1);
    public AircraftAssetCache(string portableSettings, HttpClient http) { _root = Path.Combine(portableSettings, "Aircraft"); _http = http; }
    public Task<string?> GetIconAsync(AircraftInfo aircraft, CancellationToken token) => GetAsync(aircraft.Id, "Icons", aircraft.IconUrl, token);
    public Task<string?> GetBackgroundAsync(AircraftInfo aircraft, CancellationToken token) => GetAsync(aircraft.Id, "Backgrounds", aircraft.BackgroundUrl, token);
    public Task<string?> GetFrameAsync(AircraftInfo aircraft, CancellationToken token) => GetAsync(aircraft.Id, "Frames", aircraft.FrameUrl, token);
    private async Task<string?> GetAsync(string id, string kind, string? url, CancellationToken token)
    {
        if (!Regex.IsMatch(id, @"^[a-zA-Z0-9_-]+$")) return null;
        await _gate.WaitAsync(token);
        try
        {
            string path = Path.Combine(_root, kind, id + ".png");
            if (File.Exists(path))
            {
                try { using var cached = Image.FromFile(path); return path; }
                catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) { }
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                uri.Host != "static.encyclopedia.warthunder.com") return null;
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 8 * 1024 * 1024) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var memory = new MemoryStream();
            byte[] buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer, token)) > 0)
            { if (memory.Length + read > 8 * 1024 * 1024) return null; memory.Write(buffer, 0, read); }
            memory.Position = 0;
            using var image = Image.FromStream(memory, true, true);
            if ((long)image.Width * image.Height > 16000000) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            image.Save(path + ".tmp", System.Drawing.Imaging.ImageFormat.Png);
            File.Move(path + ".tmp", path, true); return path;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or ArgumentException or OutOfMemoryException or OperationCanceledException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException) { return null; }
        finally { _gate.Release(); }
    }
}
