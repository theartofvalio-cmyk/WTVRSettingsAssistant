using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WTVRSettingsAssistant;

internal static class AtomicFile
{
    public static void WriteText(string path, string text)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            File.Move(temporary, full, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void WriteTextWithBackup(string path, string text)
    {
        string full = Path.GetFullPath(path);
        string backup = full + ".bak";
        if (File.Exists(full))
        {
            try { File.Copy(full, backup, true); } catch { }
        }
        WriteText(full, text);
    }
}

internal enum SignalState { Unknown, Available, Unavailable }
internal sealed record ServerSignal(SignalState State, string Label, string Detail, DateTimeOffset CheckedAt)
{
    public static ServerSignal Unknown(string detail) => new(SignalState.Unknown, "UNKNOWN", detail, DateTimeOffset.UtcNow);
}
internal sealed record VersionCheck(Version? Installed, Version? Latest, string Detail, bool BranchMismatchDetected = false)
{
    public bool UpdateAvailable => Latest != null && Installed != Latest;
    public bool Current => Installed != null && Latest != null && Installed == Latest;
    public bool BranchMismatch => BranchMismatchDetected;
}

internal sealed record LauncherBranchValidation(
    GameServerChannel Channel,
    Version Version,
    DateTimeOffset LogTime);

internal sealed class GameServices : IDisposable
{
    private readonly HttpClient _http;
    internal const string VersionEndpoint = "https://yupmaster.gaijinent.com/yuitem/get_version.php?proj=warthunder&tag=";
    public GameServices() : this(new HttpClientHandler()) { }
    internal GameServices(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WTAssistant/2.0");
    }

    internal static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        Match m = Regex.Match(text, @"(?<!\d)(\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5})(?![\d.])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return m.Success && Version.TryParse(m.Groups[1].Value, out var version) ? version : null;
    }
    internal static Version? ReadYupVersion(string path)
    {
        // Gaijin's launcher treats warthunder.yup as the installed-build
        // manifest. The executable file version is NOT guaranteed to advance
        // on every content-only patch, so it must not be the primary source.
        // A .yup is bencoded; the build version is stored under "version".
        try
        {
            if (!File.Exists(path)) return null;
            long length = new FileInfo(path).Length;
            if (length <= 0 || length > 128L * 1024 * 1024) return null;

            byte[] data = File.ReadAllBytes(path);
            ReadOnlySpan<byte> key = "7:version"u8;
            for (int i = 0; i <= data.Length - key.Length; i++)
            {
                if (!data.AsSpan(i, key.Length).SequenceEqual(key)) continue;
                int cursor = i + key.Length;
                int valueLength = 0;
                int digits = 0;
                while (cursor < data.Length && data[cursor] >= (byte)'0' && data[cursor] <= (byte)'9')
                {
                    if (valueLength > 4096) break;
                    valueLength = checked(valueLength * 10 + (data[cursor] - (byte)'0'));
                    cursor++;
                    digits++;
                }
                if (digits == 0 || cursor >= data.Length || data[cursor] != (byte)':') continue;
                cursor++;
                if (valueLength <= 0 || valueLength > 128 || cursor + valueLength > data.Length) continue;
                Version? parsed = ParseVersion(Encoding.ASCII.GetString(data, cursor, valueLength));
                if (parsed != null) return parsed;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        {
            // Fall through to launcher log / executable metadata.
        }
        return null;
    }

    internal static Version? ReadLauncherLogVersion(string root)
    {
        // Launcher logs explicitly print the locally parsed .yup version as
        // "game version" / "yup_version". This is a useful fallback when a
        // launcher has just atomically replaced warthunder.yup and another
        // process briefly holds the file.
        try
        {
            string directory = Path.Combine(root, ".launcher_log");
            if (!Directory.Exists(directory)) return null;
            foreach (FileInfo file in new DirectoryInfo(directory).EnumerateFiles()
                         .OrderByDescending(f => f.LastWriteTimeUtc).Take(5))
            {
                string text;
                using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    const int maxTail = 2 * 1024 * 1024;
                    long offset = Math.Max(0, stream.Length - maxTail);
                    stream.Seek(offset, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: false);
                    text = reader.ReadToEnd();
                }
                MatchCollection matches = Regex.Matches(text,
                    @"(?:game\s+version\s*:\s*|yup_version\s*=\s*)(\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5})",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(200));
                for (int i = matches.Count - 1; i >= 0; i--)
                    if (Version.TryParse(matches[i].Groups[1].Value, out Version? parsed)) return parsed;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            // Continue through the remaining local metadata sources.
        }
        return null;
    }

    internal static LauncherBranchValidation? ParseLauncherBranchValidation(string text, DateTimeOffset logTime)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            MatchCollection circuitMatches = Regex.Matches(text,
                @"(?:onGameVersion:\s*)?curCircuit\s*=\s*(?<channel>production|dev)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(200));
            if (circuitMatches.Count == 0) return null;

            Match circuitMatch = circuitMatches[circuitMatches.Count - 1];
            GameServerChannel channel = string.Equals(
                circuitMatch.Groups["channel"].Value,
                "dev",
                StringComparison.OrdinalIgnoreCase)
                ? GameServerChannel.Test
                : GameServerChannel.Live;

            MatchCollection versionMatches = Regex.Matches(text,
                @"(?:game\s+version\s*:\s*|yup_version\s*=\s*)(?<version>\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(200));
            for (int i = versionMatches.Count - 1; i >= 0; i--)
            {
                // Never associate an earlier branch's version with a later
                // circuit selection that has not reported a version yet.
                if (versionMatches[i].Index < circuitMatch.Index) break;
                if (Version.TryParse(versionMatches[i].Groups["version"].Value, out Version? parsed))
                    return new LauncherBranchValidation(channel, parsed, logTime);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // A malformed launcher log must never block the normal version path.
        }
        return null;
    }

    internal static LauncherBranchValidation? ReadLauncherBranchValidation(
        string root,
        DateTimeOffset? notBefore = null)
    {
        try
        {
            string directory = Path.Combine(root, ".launcher_log");
            if (!Directory.Exists(directory)) return null;

            DateTimeOffset threshold = notBefore?.AddSeconds(-3) ?? DateTimeOffset.MinValue;
            foreach (FileInfo file in new DirectoryInfo(directory).EnumerateFiles()
                         .OrderByDescending(f => f.LastWriteTimeUtc).Take(8))
            {
                DateTimeOffset logTime = new(file.LastWriteTimeUtc, TimeSpan.Zero);
                if (logTime < threshold) continue;

                string text;
                using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    const int maxTail = 3 * 1024 * 1024;
                    long offset = Math.Max(0, stream.Length - maxTail);
                    stream.Seek(offset, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: false);
                    text = reader.ReadToEnd();
                }

                LauncherBranchValidation? validation = ParseLauncherBranchValidation(text, logTime);
                if (validation != null) return validation;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall back to the Gaijin version endpoint + local manifest comparison.
        }
        return null;
    }

    internal static LauncherBranchValidation? ReadValidatedInstalledBranch(
        string root,
        Version version,
        DateTimeOffset? notBefore = null)
    {
        LauncherBranchValidation? validation = ReadLauncherBranchValidation(root, notBefore);
        if (validation == null || validation.Version != version)
            return null;

        // Do not trust a launcher log that predates a subsequently replaced
        // manifest. A small tolerance covers the launcher's final bookkeeping.
        try
        {
            string manifest = Path.Combine(root, "warthunder.yup");
            if (File.Exists(manifest))
            {
                DateTimeOffset manifestTime = new(File.GetLastWriteTimeUtc(manifest), TimeSpan.Zero);
                if (validation.LogTime.AddMinutes(2) < manifestTime)
                    return null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return validation;
    }

    internal static bool IsLauncherValidatedBuild(
        string root,
        GameServerChannel channel,
        Version version,
        DateTimeOffset? notBefore = null)
    {
        LauncherBranchValidation? validation = ReadValidatedInstalledBranch(root, version, notBefore);
        return validation != null && validation.Channel == channel;
    }

    internal static Version? PreferInstalledVersion(
        Version? yupVersion,
        Version? launcherLogVersion,
        IEnumerable<Version> executableVersions,
        IEnumerable<Version> markerVersions)
    {
        // warthunder.yup is the launcher's installed-build manifest. A content
        // patch can legitimately update the .yup while leaving aces.exe's PE
        // version unchanged, which is exactly why 1.8.3/1.8.4 could report a
        // false update after the official launcher had already finished.
        if (yupVersion != null) return yupVersion;
        if (launcherLogVersion != null) return launcherLogVersion;
        Version? executable = executableVersions.OrderByDescending(v => v).FirstOrDefault();
        if (executable != null) return executable;
        return markerVersions.OrderByDescending(v => v).FirstOrDefault();
    }

    internal static Version? ReadInstalledVersion(string root)
    {
        Version? yupVersion = ReadYupVersion(Path.Combine(root, "warthunder.yup"));
        Version? launcherLogVersion = ReadLauncherLogVersion(root);
        var executableVersions = new List<Version>();
        var markerVersions = new List<Version>();

        foreach (string name in new[] { "win64/aces.exe", "aces.exe" })
        {
            string path = Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                foreach (string? value in new[] { info.ProductVersion, info.FileVersion })
                {
                    Version? parsed = ParseVersion(value);
                    if (parsed != null) executableVersions.Add(parsed);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                // Continue through other local metadata sources.
            }
        }

        foreach (string name in new[] { "version", "version.txt" })
        {
            string path = Path.Combine(root, name);
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length >= 4096) continue;
                Version? parsed = ParseVersion(File.ReadAllText(path));
                if (parsed != null) markerVersions.Add(parsed);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A stale/inaccessible marker must not hide a usable manifest.
            }
        }

        return PreferInstalledVersion(yupVersion, launcherLogVersion, executableVersions, markerVersions);
    }
    public async Task<Version?> LatestAsync(GameServerChannel channel, CancellationToken token)
    {
        string body = (await _http.GetStringAsync(VersionEndpoint + (channel == GameServerChannel.Test ? "dev" : ""), token)).Trim();
        // Accept only a bare version response. HTML error pages are not versions.
        return Regex.IsMatch(body, @"^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            ? ParseVersion(body) : null;
    }
    public async Task<VersionCheck> CheckVersionAsync(string root, GameServerChannel channel, CancellationToken token)
    {
        Version? installed = null;
        LauncherBranchValidation? validated = null;
        try
        {
            installed = ReadInstalledVersion(root);
            validated = installed == null ? null : ReadValidatedInstalledBranch(root, installed);
            var latest = await LatestAsync(channel, token);

            // Keep launcher circuit information for diagnostics. A branch
            // switch alone does not require an update when versions match.
            if (installed != null)
            {
                if (validated != null && validated.Channel != channel)
                {
                    return new(installed, latest,
                        $"Official launcher last validated {validated.Channel} build {installed}; selected branch is {channel}.",
                        BranchMismatchDetected: true);
                }

                // The public version endpoint can briefly lag behind the launcher
                // during a rollout. Only accept the newer/different installed
                // build when the launcher validated the SAME selected branch.
                if (latest != null && installed > latest && validated != null && validated.Channel == channel)
                {
                    return new(installed, installed,
                        $"Official launcher validated newer {channel} build {installed}; version service reported older {latest}.");
                }
            }

            return new(installed, latest, latest == null ? "Update service returned an unsupported response." : "Gaijin version service.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or UnauthorizedAccessException)
        { return new(installed, null, ex.Message, BranchMismatchDetected: validated != null && validated.Channel != channel); }
    }
    public async Task<ServerSignal> LiveSignalAsync(CancellationToken token)
    {
        try
        {
            var version = await LatestAsync(GameServerChannel.Live, token);
            return version == null ? ServerSignal.Unknown("No valid version response. Gameplay availability is not verified.") :
                new(SignalState.Available, "UPDATE SERVICE OK", $"Gaijin update service responded: {version}. This is NOT an authentication or matchmaking health check.", DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex) { return new(SignalState.Unavailable, "UNREACHABLE", "Update service is unreachable from this computer, not proof of a game outage. " + ex.Message, DateTimeOffset.UtcNow); }
        catch (OperationCanceledException) { return ServerSignal.Unknown("Update-service request timed out. Game status is unknown."); }
    }
    public async Task<ServerSignal> TestSignalAsync(CancellationToken token)
    {
        // The red/green indicator is driven only by a live connectivity probe.
        // The official opening announcement is fetched separately and appended
        // to Detail for the tooltip/info text; it never controls the light.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool reachable = await ProbeDevServiceAsync(token);
        string schedule = await GetLatestDevScheduleInfoAsync(token);
        return new(
            reachable ? SignalState.Available : SignalState.Unavailable,
            reachable ? "ONLINE" : "OFFLINE",
            $"Connectivity probe: {(reachable ? "dev service reachable" : "dev service not reachable")} at {now:u}. " + schedule,
            now);
    }

    private static async Task<bool> ProbeDevServiceAsync(CancellationToken token)
    {
        // Gaijin documents TCP 20443 and 33333 as additional ports required
        // specifically for the War Thunder dev version. Probe those first.
        // ICMP is intentionally not used to turn the light green because many
        // hosts/firewalls block ping while services remain available.
        foreach (int port in new[] { 20443, 33333 })
        {
            try
            {
                using var client = new TcpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2.5));
                await client.ConnectAsync("warthunder.com", port, timeout.Token);
                if (client.Connected) return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException) { }
        }
        return false;
    }

    private async Task<string> GetLatestDevScheduleInfoAsync(CancellationToken token)
    {
        try
        {
            string query = Uri.EscapeDataString("\"The Dev Server is Opening\" order:latest");
            using var search = JsonDocument.Parse(await _http.GetStringAsync("https://forum.warthunder.com/search.json?q=" + query, token));
            if (!search.RootElement.TryGetProperty("topics", out var topics)) return "Official schedule: unavailable.";
            JsonElement? candidate = null;
            foreach (var topic in topics.EnumerateArray())
            {
                string title = topic.GetProperty("title").GetString() ?? "";
                if (!title.StartsWith("The Dev Server is Opening", StringComparison.OrdinalIgnoreCase)) continue;
                if (candidate == null || string.CompareOrdinal(topic.GetProperty("created_at").GetString(), candidate.Value.GetProperty("created_at").GetString()) > 0) candidate = topic.Clone();
            }
            if (candidate == null) return "Official schedule: no current opening announcement found.";
            int id = candidate.Value.GetProperty("id").GetInt32();
            string url = "https://forum.warthunder.com/t/" + id;
            using var document = JsonDocument.Parse(await _http.GetStringAsync(url + ".json", token));
            var post = document.RootElement.GetProperty("post_stream").GetProperty("posts")[0];
            if (!post.TryGetProperty("staff", out var staff) || staff.ValueKind != JsonValueKind.True)
                return "Official schedule: latest matching thread was not staff-authored.";
            var published = DateTimeOffset.Parse(post.GetProperty("created_at").GetString()!, CultureInfo.InvariantCulture);
            string text = WebUtility.HtmlDecode(Regex.Replace(post.GetProperty("cooked").GetString() ?? "", "<[^>]+>", " ", RegexOptions.Singleline, TimeSpan.FromMilliseconds(200)));
            if (!TryOpeningWindow(text, published.Year, out var open, out var close))
                return "Official schedule: announcement found, but its opening/closing times could not be parsed. Source: " + url;
            return $"Official announced window (information only): opens {open:u}, closes {close:u}. The status light does not use this schedule. Source: {url}";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OperationCanceledException)
        {
            return "Official schedule: unavailable (" + ex.Message + ").";
        }
    }
    internal static bool TryOpeningWindow(string text, int year, out DateTimeOffset start, out DateTimeOffset end)
    {
        start = end = default;
        const string date = @"(?<month>[A-Za-z]+)\s+(?<day>\d{1,2})(?:st|nd|rd|th)?\s*\(\s*(?<time>\d{1,2}:\d{2})\s*(?:GMT|UTC)\s*\)";
        Match from = Regex.Match(text, @"When\s*:\s*From\s+" + date, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        Match to = Regex.Match(text, @"Until\s*:\s*" + date, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        bool Parse(Match m, int y, out DateTimeOffset value) => DateTimeOffset.TryParseExact(
            $"{m.Groups["month"].Value} {m.Groups["day"].Value} {y} {m.Groups["time"].Value}",
            new[] { "MMMM d yyyy H:mm", "MMMM d yyyy HH:mm" }, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
        if (!from.Success || !to.Success || !Parse(from, year, out start) || !Parse(to, year, out end)) return false;
        if (end < start && start.Month == 12 && end.Month == 1) end = end.AddYears(1);
        return end > start && end - start < TimeSpan.FromDays(31);
    }
    public void Dispose() => _http.Dispose();
}

internal sealed class GameInstallations
{
    public string LiveRoot { get; set; } = "";
    public string TestRoot { get; set; } = "";
    internal static GameInstallations Load(string path)
    {
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (File.Exists(candidate) && JsonSerializer.Deserialize<GameInstallations>(File.ReadAllText(candidate)) is { } saved)
                    return saved;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return new();
    }
    public string Root(GameServerChannel channel) => channel == GameServerChannel.Test ? TestRoot : LiveRoot;
    public void Set(GameServerChannel channel, string path) { if (channel == GameServerChannel.Test) TestRoot = path; else LiveRoot = path; }
    public void SetBoth(string path) { LiveRoot = path; TestRoot = path; }
    internal static string CanonicalRoot(string path)
    {
        var info = new DirectoryInfo(Path.GetFullPath(path));
        // Resolve every existing ancestor, including junctions to the same installation.
        var parts = new Stack<string>();
        for (var current = info; current != null; current = current.Parent) parts.Push(current.Name);
        string result = Path.GetPathRoot(info.FullName)!;
        foreach (string part in parts.Skip(1))
        {
            result = Path.Combine(result, part);
            var directory = new DirectoryInfo(result);
            if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                result = directory.ResolveLinkTarget(true)?.FullName ?? throw new IOException("Cannot resolve installation junction: " + result);
        }
        return Path.TrimEndingDirectorySeparator(result);
    }
    internal static bool Overlaps(string a, string b)
    {
        a = Path.TrimEndingDirectorySeparator(a); b = Path.TrimEndingDirectorySeparator(b);
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
