namespace WTVRSettingsAssistant;

internal static partial class AppText
{
    private static void ApplyCurrentReleaseNotes()
    {
        foreach (LanguageOption language in Options)
        {
            if (!SupplementalTexts.TryGetValue(language.Code, out Dictionary<string, string>? text))
            {
                text = new Dictionary<string, string>(StringComparer.Ordinal);
                SupplementalTexts[language.Code] = text;
            }
            text["Info.ChangesText"] = ReleaseNotes.ForLanguage(language.Code);
        }
    }
}
