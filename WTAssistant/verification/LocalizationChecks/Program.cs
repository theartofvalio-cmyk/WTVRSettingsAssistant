using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using WTVRSettingsAssistant;

internal static class Program
{
    private sealed record Gap(string Language, string Key, string English, string? Translation, string Kind);

    [STAThread]
    private static int Main(string[] args)
    {
        var catalogs = (Dictionary<string, Dictionary<string, string>>)typeof(AppText)
            .GetField("Texts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var gaps = new List<Gap>();
        var supplements = (Dictionary<string, Dictionary<string, string>>)typeof(AppText)
            .GetField("SupplementalTexts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        foreach (var language in AppText.Languages)
        {
            if (language.Code == "en") continue;
            var catalog = new Dictionary<string, string>(catalogs[language.Code]);
            if (supplements.TryGetValue(language.Code, out var additional))
                foreach (var pair in additional) catalog[pair.Key] = pair.Value;
            foreach (var (key, english) in catalogs["en"])
            {
                if (string.IsNullOrEmpty(english)) continue;
                if (!catalog.TryGetValue(key, out string? translation))
                    gaps.Add(new(language.Code, key, english, null, "missing"));
                else if (string.IsNullOrWhiteSpace(translation))
                    gaps.Add(new(language.Code, key, english, translation, "empty"));
                else if (english == translation)
                    gaps.Add(new(language.Code, key, english, translation, "unchanged-review-required"));
                else if (Placeholders(english) != Placeholders(translation))
                    gaps.Add(new(language.Code, key, english, translation, "placeholder-mismatch"));
            }
            Console.WriteLine($"{language.Code}: {gaps.Count(g => g.Language == language.Code)} entries require review");
        }
        string output = args.FirstOrDefault(arg => !arg.StartsWith("--")) ?? "artifacts/localization-audit.json";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            EnglishKeys = catalogs["en"].Count,
            Note = "Unchanged entries include legitimate product names. They require explicit review, not automatic translation. This catalog audit does not prove runtime text coverage.",
            Gaps = gaps
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        if (Directory.Exists("WTAssistant") && Directory.Exists("Modules/VTrim"))
            SourceInventory.Write(Environment.CurrentDirectory, Path.ChangeExtension(output, ".source.json"));

        using var form = new Form();
        object metadata = new();
        var label = new Label { Text = "Home", Tag = metadata };
        var input = new TextBox { Text = "Home", Tag = metadata };
        var combo = new ComboBox { Text = "Home", Tag = metadata };
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Home", Tag = metadata });
        form.Controls.AddRange(new Control[] { label, input, combo, grid });
        var map = new Dictionary<string, string> { ["Home"] = "Nav.Home" };
        using var tooltip = new ToolTip();
        UiLanguage.BindToolTip(tooltip, label, "Neck.Tooltip.Recenter", "en");
        UiLanguage.Apply(form, "bg", map);
        Require(tooltip.GetToolTip(label) == AppText.T("bg", "Neck.Tooltip.Recenter"), "Tooltip changes language");
        Require(label.Text == "Начало", "Label translated");
        Require(ReferenceEquals(label.Tag, metadata), "Control metadata preserved");
        Require(input.Text == "Home" && combo.Text == "Home", "User input preserved");
        Require(grid.Columns[0].HeaderText == "Начало", "Column header translated");
        Require(ReferenceEquals(grid.Columns[0].Tag, metadata), "Column metadata preserved");
        UiLanguage.Apply(form, "de", map);
        Require(label.Text == "Start", "Second language applied from stable key");
        UiLanguage.Apply(form, "en", map);
        Require(label.Text == "Home", "English roundtrip restored");
        Require(tooltip.GetToolTip(label) == AppText.T("en", "Neck.Tooltip.Recenter"), "Tooltip English roundtrip restored");
        UiLanguage.BindToolTip(tooltip, label, "Neck.Tooltip.Toggle", "en");
        UiLanguage.Apply(form, "es", map);
        Require(tooltip.GetToolTip(label) == AppText.T("es", "Neck.Tooltip.Toggle"), "Tooltip rebinding uses the current key");
        Console.WriteLine($"10 language-switch checks passed. Audit: {Path.GetFullPath(output)}");
        return args.Contains("--audit") || gaps.Count == 0 ? 0 : 1;
    }

    private static string Placeholders(string value) => string.Join("|",
        Regex.Matches(value, @"(?<!\{)\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}(?!\})")
            .Select(match => match.Groups[1].Value).Order());
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
