namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.10";
    public const string Features =
        "• Controls Assist now reads up to 128 physical controller buttons, including PTO2 buttons 35–37.\n\n" +
        "• Action sequences can repeat while a physical button is held, stopping on release, disconnect or loss of game focus.\n\n" +
        "• Sequence intervals are measured between press starts, with distinct key releases and no accumulated release-delay overhead.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• Controls Assist вече поддържа до 128 физически бутона, повторение на последователност при задържане и коригирани интервали между натисканията.",
        ["es"] = "• Controls Assist ahora admite hasta 128 botones físicos, repetición de secuencias mientras se mantiene el botón e intervalos corregidos entre pulsaciones.",
        ["de"] = "• Controls Assist unterstützt jetzt bis zu 128 physische Tasten, Sequenzwiederholung beim Halten und korrigierte Abstände zwischen Tastendrücken.",
        ["fr"] = "• Controls Assist prend désormais en charge jusqu’à 128 boutons physiques, la répétition pendant le maintien et des intervalles corrigés entre les pressions.",
        ["pt"] = "• Controls Assist suporta agora até 128 botões físicos, repetição de sequências enquanto o botão é mantido e intervalos corrigidos entre pressões.",
        ["pl"] = "• Controls Assist obsługuje teraz do 128 fizycznych przycisków, powtarzanie sekwencji podczas trzymania i poprawione odstępy między naciśnięciami.",
        ["ru"] = "• Controls Assist теперь поддерживает до 128 физических кнопок, повторение последовательности при удержании и исправленные интервалы между нажатиями.",
        ["uk"] = "• Controls Assist тепер підтримує до 128 фізичних кнопок, повторення послідовності при утриманні та виправлені інтервали між натисканнями.",
        ["tr"] = "• Controls Assist artık 128 fiziksel düğmeyi, basılı tutarken sıra tekrarını ve düzeltilmiş basış aralıklarını destekler.",
        ["el"] = "• Το Controls Assist υποστηρίζει πλέον έως 128 φυσικά κουμπιά, επανάληψη ακολουθίας κατά το κράτημα και διορθωμένα διαστήματα μεταξύ πατημάτων.",
        ["ro"] = "• Controls Assist acceptă acum până la 128 de butoane fizice, repetarea secvențelor cât timp butonul este ținut și intervale corectate între apăsări.",
        ["he"] = "• Controls Assist תומך כעת בעד 128 כפתורים פיזיים, בחזרה על רצפים בזמן החזקה ובמרווחים מתוקנים בין לחיצות.",
        ["zh-Hans"] = "• Controls Assist现支持最多128个实体按钮、按住时重复序列，并修正了按键间隔。"
    };
    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
