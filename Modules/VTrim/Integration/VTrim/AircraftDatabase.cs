using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
                string nation = row[2].GetString() ?? "";
                string? countryBackground = Regex.IsMatch(nation, @"^[a-zA-Z0-9_-]+$")
                    ? $"https://static.encyclopedia.warthunder.com/unit_tooltip/country_{nation}.png" : null;
                // Detail pages use /images/{id}.png for the large side-view aircraft art
                // shown in the Wiki hero. It is fetched lazily only when an aircraft
                // editor is opened; if a rare vehicle lacks it, the editor falls back
                // to the normal roster/slot icon.
                string heroUrl = $"https://static.encyclopedia.warthunder.com/images/{id}.png";
                all[id] = new(id, name, nation, role, br, icon.Success ? icon.Value : null,
                    BackgroundUrl: countryBackground, FrameUrl: heroUrl,
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
    private HashSet<string> _changedIconIds = new(StringComparer.Ordinal);
    public IReadOnlyList<AircraftInfo> Items => _items;
    public IReadOnlySet<string> ChangedIconIds => _changedIconIds;
    public event Action? Changed;
    public string? LastError { get; private set; }
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public AircraftDatabaseService(string portableSettings, IAircraftDataProvider provider)
    { _directory = Path.Combine(portableSettings, "Aircraft"); _provider = provider; }
    internal static readonly AircraftInfo[] UniversalAircraft = new[] { "Prop Plane", "Jet Plane", "Helicopter" }
        .Select(type => new AircraftInfo("universal-" + type.Replace(" ", "-").ToLowerInvariant(), "Universal " + type,
            "", type, new(), null, FlightCategory: type)).ToArray();
    public AircraftInfo? GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        AircraftInfo[] all = _items.Concat(UniversalAircraft).ToArray();
        AircraftInfo? exact = all.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        // Older telemetry uses underscores where Wiki IDs sometimes use hyphens.
        string key = AircraftSearchService.Normalize(id);
        AircraftInfo[] matches = all.Where(a => AircraftSearchService.Normalize(a.Id) == key).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    // Telemetry can create a profile before the Wiki index has loaded. Such a
    // profile has no AircraftId yet, but its detection key is the Wiki ID.
    public AircraftInfo? ResolveProfile(string? aircraftId, string? detectedKey, string? profileName = null)
    {
        AircraftInfo? byId = GetById(aircraftId);
        if (byId is not null) return byId;
        // Existing auto-created profiles may have neither an AircraftId nor a
        // detection key. Their saved name is often the original telemetry ID.
        foreach (string? candidate in new[] { detectedKey, profileName })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            string key = AircraftSearchService.Normalize(AircraftSearchService.CleanName(candidate));
            AircraftInfo[] matches = _items.Where(a =>
                AircraftSearchService.Normalize(a.Id) == key ||
                AircraftSearchService.Normalize(a.DisplayName) == key ||
                (a.SearchAliases?.Any(alias => AircraftSearchService.Normalize(alias) == key) ?? false)).ToArray();
            if (matches.Length == 1) return matches[0];
        }
        return null;
    }
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
            var oldById = _items.ToDictionary(a => a.Id, StringComparer.Ordinal);
            _changedIconIds = items.Where(a => !oldById.TryGetValue(a.Id, out AircraftInfo? old) ||
                    !string.Equals(old.IconUrl, a.IconUrl, StringComparison.Ordinal))
                .Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
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

internal sealed record AircraftAssetStamp(string SourceUrl, string? ETag = null, DateTimeOffset? LastModified = null);

internal sealed class AircraftAssetCache
{
    private readonly string _root;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _downloadGate = new(4, 4);
    private readonly object _refreshLock = new();
    private readonly HashSet<string> _refreshing = new(StringComparer.OrdinalIgnoreCase);

    public AircraftAssetCache(string portableSettings, HttpClient http)
    {
        _root = Path.Combine(portableSettings, "Aircraft");
        _http = http;
    }

    // Profile cards intentionally use the roster/slot image again. This is the
    // stable path used before 2.2.4: once downloaded, the icon is read directly
    // from Settings/Aircraft/Icons and does not depend on the network to render.
    public Task<string?> GetIconAsync(AircraftInfo aircraft, CancellationToken token) =>
        GetAsync(aircraft.Id, "Icons",
            aircraft.IconUrl ?? $"https://static.encyclopedia.warthunder.com/slots/{aircraft.Id}.png", token);

    public Task<string?> GetBackgroundAsync(AircraftInfo aircraft, CancellationToken token) =>
        GetAsync(aircraft.Id, "Backgrounds", aircraft.BackgroundUrl, token);

    public Task<string?> GetFrameAsync(AircraftInfo aircraft, CancellationToken token) =>
        GetAsync(aircraft.Id, "Frames",
            aircraft.FrameUrl ?? $"https://static.encyclopedia.warthunder.com/images/{aircraft.Id}.png", token);

    // Called after the Wiki aircraft index refreshes. Existing cached icons are
    // not downloaded again. Only missing/new aircraft, corrupt cache entries, or
    // entries whose Wiki source URL changed are refreshed.
    public async Task SynchronizeIconsAsync(IEnumerable<AircraftInfo> aircraft, IReadOnlySet<string>? changedIconIds,
        CancellationToken token)
    {
        foreach (AircraftInfo item in aircraft)
        {
            token.ThrowIfCancellationRequested();
            if (item.Id.StartsWith("universal-", StringComparison.Ordinal)) continue;
            try
            {
                if (changedIconIds?.Contains(item.Id) == true)
                    await RefreshIconAsync(item, token);
                else
                    await GetIconAsync(item, token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or HttpRequestException or ArgumentException or UnauthorizedAccessException) { }
        }
    }

    private async Task<string?> RefreshIconAsync(AircraftInfo aircraft, CancellationToken token)
    {
        string? url = aircraft.IconUrl ?? $"https://static.encyclopedia.warthunder.com/slots/{aircraft.Id}.png";
        if (!Regex.IsMatch(aircraft.Id, @"^[a-zA-Z0-9_-]+$") || !TryGetTrustedUri(url, out Uri uri)) return null;
        string directory = Path.Combine(_root, "Icons");
        string path = Path.Combine(directory, aircraft.Id + ".png");
        string stampPath = Path.Combine(directory, aircraft.Id + ".asset.json");
        return await DownloadAssetAsync(aircraft.Id, path, stampPath, uri, ReadStamp(stampPath), token, forceRefresh: true);
    }

    private async Task<string?> GetAsync(string id, string kind, string? url, CancellationToken token)
    {
        if (!Regex.IsMatch(id, @"^[a-zA-Z0-9_-]+$")) return null;
        if (!TryGetTrustedUri(url, out Uri uri)) return null;

        string directory = Path.Combine(_root, kind);
        string path = Path.Combine(directory, id + ".png");
        string stampPath = Path.Combine(directory, id + ".asset.json");

        // Cache lookup happens BEFORE the download semaphore. This is important:
        // hundreds of background Wiki downloads can never delay an already-cached
        // aircraft icon from appearing in the profile UI.
        if (TryValidateImage(path))
        {
            AircraftAssetStamp? stamp = ReadStamp(stampPath);
            if (stamp is null)
            {
                // Migrate caches created by older WT Assistant versions without
                // redownloading them.
                ScheduleStampWrite(stampPath, new(uri.AbsoluteUri));
            }
            else if (!string.Equals(stamp.SourceUrl, uri.AbsoluteUri, StringComparison.Ordinal))
            {
                // The Wiki now points this aircraft at different artwork. Keep the
                // old local image visible immediately and refresh it in background.
                ScheduleRefresh(id, path, stampPath, uri, stamp, token);
            }
            return path;
        }

        // Delete a corrupt local entry and obtain the current Wiki asset once.
        TryDelete(path);
        return await DownloadAssetAsync(id, path, stampPath, uri, null, token);
    }

    private void ScheduleRefresh(string id, string path, string stampPath, Uri uri,
        AircraftAssetStamp? oldStamp, CancellationToken token)
    {
        string key = path;
        lock (_refreshLock)
        {
            if (!_refreshing.Add(key)) return;
        }
        _ = RefreshInBackgroundAsync(key, id, path, stampPath, uri, oldStamp, token);
    }

    private async Task RefreshInBackgroundAsync(string key, string id, string path, string stampPath,
        Uri uri, AircraftAssetStamp? oldStamp, CancellationToken token)
    {
        try { await DownloadAssetAsync(id, path, stampPath, uri, oldStamp, token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or HttpRequestException or ArgumentException or
            OutOfMemoryException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException) { }
        finally { lock (_refreshLock) _refreshing.Remove(key); }
    }

    private async Task<string?> DownloadAssetAsync(string id, string path, string stampPath, Uri uri,
        AircraftAssetStamp? oldStamp, CancellationToken token, bool forceRefresh = false)
    {
        await _downloadGate.WaitAsync(token);
        try
        {
            // Another request may have completed while we were waiting.
            if (!forceRefresh && TryValidateImage(path))
            {
                AircraftAssetStamp? current = ReadStamp(stampPath);
                if (current is not null && string.Equals(current.SourceUrl, uri.AbsoluteUri, StringComparison.Ordinal))
                    return path;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (oldStamp is not null)
            {
                if (!string.IsNullOrWhiteSpace(oldStamp.ETag) &&
                    EntityTagHeaderValue.TryParse(oldStamp.ETag, out EntityTagHeaderValue? etag) && etag is not null)
                    request.Headers.IfNoneMatch.Add(etag);
                if (oldStamp.LastModified is { } modified) request.Headers.IfModifiedSince = modified;
            }
            using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.NotModified && TryValidateImage(path))
            {
                await WriteStampAsync(stampPath, oldStamp! with { SourceUrl = uri.AbsoluteUri }, token);
                return path;
            }
            if (!response.IsSuccessStatusCode) return TryValidateImage(path) ? path : null;
            if (response.Content.Headers.ContentLength > 8 * 1024 * 1024) return TryValidateImage(path) ? path : null;

            await using Stream stream = await response.Content.ReadAsStreamAsync(token);
            using var memory = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, token)) > 0)
            {
                if (memory.Length + read > 8 * 1024 * 1024) return TryValidateImage(path) ? path : null;
                memory.Write(buffer, 0, read);
            }
            memory.Position = 0;
            using var image = Image.FromStream(memory, true, true);
            if ((long)image.Width * image.Height > 16000000) return TryValidateImage(path) ? path : null;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                image.Save(temporary, System.Drawing.Imaging.ImageFormat.Png);
                File.Move(temporary, path, true);
            }
            finally { TryDelete(temporary); }

            var stamp = new AircraftAssetStamp(uri.AbsoluteUri,
                response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
            await WriteStampAsync(stampPath, stamp, token);
            return path;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or ArgumentException or OutOfMemoryException or
            UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        {
            return TryValidateImage(path) ? path : null;
        }
        finally { _downloadGate.Release(); }
    }

    private static bool TryGetTrustedUri(string? url, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) || parsed is null || parsed.Scheme != "https" ||
            parsed.Host != "static.encyclopedia.warthunder.com") return false;
        uri = parsed;
        return true;
    }

    private static bool TryValidateImage(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var image = Image.FromFile(path);
            return image.Width > 0 && image.Height > 0;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException or
            System.Runtime.InteropServices.ExternalException) { return false; }
    }

    private static AircraftAssetStamp? ReadStamp(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<AircraftAssetStamp>(File.ReadAllText(path), AircraftDatabaseService.JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static void ScheduleStampWrite(string path, AircraftAssetStamp stamp)
    {
        _ = Task.Run(async () =>
        {
            try { await WriteStampAsync(path, stamp, CancellationToken.None); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        });
    }

    private static async Task WriteStampAsync(string path, AircraftAssetStamp stamp, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(stamp, AircraftDatabaseService.JsonOptions), token);
        File.Move(temp, path, true);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
