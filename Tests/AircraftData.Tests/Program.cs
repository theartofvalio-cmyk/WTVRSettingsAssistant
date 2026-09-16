using HOTASTrimUtility;
using System.Net;
using System.Net.Http;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
using var http = new HttpClient(new FixtureHandler());
var provider = new WarThunderWikiAircraftProvider(http);
var data = await provider.FetchAsync(default);
Check(data.Count == 2, "Both rosters parsed");
Check(data[0].BattleRatings["rb"] == "11.0", "Wiki BR conversion");
Check(AircraftSearchService.Search(data, "F16 C").Count() == 1, "Normalized search");
Check(data.Any(a => a.IconUrl is not null), "Source icon extracted");
Check(AircraftSearchService.CleanName("\u2417F-5A") == "F-5A", "Nation prefixes hidden");
Check(AircraftSearchService.Search(Enumerable.Range(0, 150).Select(i => data[0] with { Id = "test" + i }), "").Count() == 150, "Full roster searchable without result cap");
var root = Path.Combine(Path.GetTempPath(), "WTVR-AircraftTests-" + Guid.NewGuid());
var stub = new StubProvider(data);
var service = new AircraftDatabaseService(root, stub);
await service.InitializeAsync(default);
var index = Path.Combine(root, "Aircraft", "aircraft-index.json");
string saved = File.ReadAllText(index);
stub.Items = [];
await service.RefreshAsync(default);
Check(saved == File.ReadAllText(index) && service.Items.Count == 2, "Empty refresh preserves cache");
stub.Fail = true;
var offline = new AircraftDatabaseService(root, stub);
await offline.InitializeAsync(default);
Check(offline.Items.Count == 2, "Offline startup uses cache");
Check(saved == File.ReadAllText(index), "Offline refresh preserves file");
if (args.Contains("--live"))
{
    using var live = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var roster = await new WarThunderWikiAircraftProvider(live).FetchAsync(default);
    Check(roster.Count > 1000, "Complete live roster");
    Check(roster.Any(a => a.DisplayName.Contains("F-16")), "Live F-16 variants");
    Check(roster.Single(a => a.Id == "f-4s").IsPremium, "F-4S premium metadata");
    Check(!roster.Single(a => a.Id == "f_16a_block_10").IsPremium, "Research F-16 stays non-premium");
    Console.WriteLine($"Live roster: {roster.Count} aircraft, {roster.Count(a => a.IconUrl is not null)} icons.");
    foreach (var missing in roster.Where(a => a.IconUrl is null)) Console.WriteLine("No source image: " + missing.Id);
    if (args.Contains("--assets"))
    {
        var cache = new AircraftAssetCache(Path.GetFullPath("artifacts/aircraft-source-validation"), live);
        int downloaded = 0;
        foreach (var aircraft in roster)
        {
            Check(await cache.GetIconAsync(aircraft, default) is not null, "Image unavailable: " + aircraft.Id);
            if (++downloaded % 100 == 0) Console.WriteLine($"Validated {downloaded}/{roster.Count} cached images");
        }
        Console.WriteLine($"All {downloaded} individual aircraft images cached and decoded.");
        using var disconnected = new HttpClient(new OfflineHandler());
        var restarted = new AircraftAssetCache(Path.GetFullPath("artifacts/aircraft-source-validation"), disconnected);
        foreach (var aircraft in roster)
            Check(await restarted.GetIconAsync(aircraft, default) is not null, "Restart cache miss: " + aircraft.Id);
        Console.WriteLine("Restart reused every image without a network request.");
    }
}
Console.WriteLine("Aircraft data checks passed. No installed settings or hardware touched.");

sealed class StubProvider(IReadOnlyList<AircraftInfo> items) : IAircraftDataProvider
{
    public IReadOnlyList<AircraftInfo> Items = items;
    public bool Fail;
    public Task<IReadOnlyList<AircraftInfo>> FetchAsync(CancellationToken token) =>
        Fail ? throw new HttpRequestException("Offline") : Task.FromResult(Items);
}
sealed class OfflineHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        throw new InvalidOperationException("Cached artwork must not require the network: " + request.RequestUri);
}
sealed class FixtureHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        string id = request.RequestUri!.AbsolutePath == "/aviation" ? "f16c" : "ah1";
        string name = id == "f16c" ? "F-16C" : "AH-1";
        string page = "window.WT_UnitList = '[[\"" + id + "\",\"" + name + "\",\"usa\",8,{\"rb\":30},0,[],[[\"fighter\",\"Fighter\"]]]]';" +
            "https://static.encyclopedia.warthunder.com/slots/" + id + ".png";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page) });
    }
}
