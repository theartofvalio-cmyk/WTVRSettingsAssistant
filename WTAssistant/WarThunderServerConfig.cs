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

        string block = MaskComments(text.Substring(start, length), maskStrings: false);
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
        string code = MaskComments(text, maskStrings: true);
        int depth = 0;
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '{') { depth++; continue; }
            if (c == '}') { depth--; continue; }
            if (depth != 0 || !(char.IsLetter(c) || c == '_')) continue;
            int wordStart = i;
            while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
            if (!code.AsSpan(wordStart, i - wordStart).Equals(name.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                i--;
                continue;
            }
            while (i < code.Length && char.IsWhiteSpace(code[i])) i++;
            if (i >= code.Length || code[i] != '{') { i--; continue; }
            int blockDepth = 1;
            for (int end = i + 1; end < code.Length; end++)
            {
                if (code[end] == '{') blockDepth++;
                else if (code[end] == '}' && --blockDepth == 0)
                {
                    start = wordStart;
                    length = end - wordStart + 1;
                    return true;
                }
            }
            throw new InvalidDataException("The yunetwork block is incomplete; config.blk was not changed.");
        }
        return false;
    }

    // Keep offsets stable while excluding comments and (for block discovery)
    // quoted values. Braces or circuit examples in comments are not settings.
    private static string MaskComments(string text, bool maskStrings)
    {
        char[] result = text.ToCharArray();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                if (maskStrings) result[i] = ' ';
                for (i++; i < text.Length; i++)
                {
                    char c = text[i];
                    if (maskStrings) result[i] = ' ';
                    if (c == '\\' && i + 1 < text.Length)
                    {
                        i++;
                        if (maskStrings) result[i] = ' ';
                    }
                    else if (c == '"') break;
                }
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') result[i++] = ' ';
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                result[i++] = ' ';
                result[i] = ' ';
                while (++i < text.Length)
                {
                    if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    {
                        result[i] = result[i + 1] = ' ';
                        i++;
                        break;
                    }
                    if (text[i] != '\r' && text[i] != '\n') result[i] = ' ';
                }
            }
        }
        return new string(result);
    }

    private static string DetectNewline(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
