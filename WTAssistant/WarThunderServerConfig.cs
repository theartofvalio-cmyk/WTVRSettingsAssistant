using System.Text;

namespace WTVRSettingsAssistant;

internal enum GameServerChannel { Live, Test }

/// <summary>
/// Changes only the yunetwork{} block in config.blk. The user's graphics,
/// controls and every other custom setting remain untouched.
/// </summary>
internal static class WarThunderServerConfig
{
    private const string LiveBlock =
        "yunetwork{\r\n" +
        "  curCircuit:t=\"production\"\r\n" +
        "}";

    // Exact dev-server block requested by the project owner.
    private const string TestBlock =
        "yunetwork{\r\n" +
        "  curCircuit:t=\"dev\"\r\n" +
        "  isExpertMode:b=yes\r\n" +
        "  webStatusLocalhostOnly:b=yes\r\n" +
        "  webStatusPort:i=23456\r\n" +
        "  enableWebStatus:b=no\r\n" +
        "}";

    public static GameServerChannel Detect(string text)
    {
        if (!TryFindBlock(text, "yunetwork", out int start, out int length))
            return GameServerChannel.Live;

        string block = text.Substring(start, length);
        return System.Text.RegularExpressions.Regex.IsMatch(
            block,
            @"curCircuit\s*:\s*t\s*=\s*""dev""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            ? GameServerChannel.Test
            : GameServerChannel.Live;
    }

    public static string Apply(string text, GameServerChannel channel)
    {
        string replacement = channel == GameServerChannel.Test ? TestBlock : LiveBlock;
        string newline = DetectNewline(text);
        replacement = replacement.Replace("\r\n", newline, StringComparison.Ordinal);

        if (TryFindBlock(text, "yunetwork", out int start, out int length))
            return text.Remove(start, length).Insert(start, replacement);

        string suffix = text.EndsWith("\r\n", StringComparison.Ordinal) || text.EndsWith("\n", StringComparison.Ordinal)
            ? string.Empty
            : newline;
        return text + suffix + replacement + newline;
    }

    public static void ApplyToFile(string configPath, GameServerChannel channel)
    {
        string original = File.ReadAllText(configPath);
        string updated = Apply(original, channel);
        if (string.Equals(original, updated, StringComparison.Ordinal)) return;

        string directory = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, ".config." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, updated, new UTF8Encoding(false));
            string verified = File.ReadAllText(temporary);
            if (!string.Equals(verified, updated, StringComparison.Ordinal))
                throw new IOException("The temporary config.blk write could not be verified.");
            File.Move(temporary, configPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static bool TryFindBlock(string text, string name, out int start, out int length)
    {
        start = length = 0;
        int search = 0;
        while (search < text.Length)
        {
            int nameIndex = text.IndexOf(name, search, StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) return false;

            bool leftBoundary = nameIndex == 0 || !(char.IsLetterOrDigit(text[nameIndex - 1]) || text[nameIndex - 1] == '_');
            int afterName = nameIndex + name.Length;
            bool rightBoundary = afterName >= text.Length || !(char.IsLetterOrDigit(text[afterName]) || text[afterName] == '_');
            if (!leftBoundary || !rightBoundary)
            {
                search = afterName;
                continue;
            }

            int brace = afterName;
            while (brace < text.Length && char.IsWhiteSpace(text[brace])) brace++;
            if (brace >= text.Length || text[brace] != '{')
            {
                search = afterName;
                continue;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = brace; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                {
                    start = nameIndex;
                    length = i - nameIndex + 1;
                    return true;
                }
            }
            return false;
        }
        return false;
    }

    private static string DetectNewline(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
