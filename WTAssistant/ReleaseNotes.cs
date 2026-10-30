namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.14";
    public const string Features =
        "• VTrim now uses the same HOTAS sample for vJoy output and the live preview, " +
        "removing competing reads that caused stick pauses. Theme changes now apply without restarting the app.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• VTrim вече използва една и съща проба от HOTAS за vJoy и визуализацията, като премахва едновременните прочитания, причиняващи засичане на стика. Смяната на темата вече става без рестарт.",
        ["es"] = "• VTrim usa la misma muestra del HOTAS para vJoy y la vista previa, eliminando las lecturas simultáneas que causaban pausas en el mando. El tema cambia sin reiniciar la aplicación.",
        ["de"] = "• VTrim nutzt nun dieselbe HOTAS-Abfrage für vJoy und die Vorschau. Gleichzeitige Abfragen, die den Stick kurz anhielten, entfallen. Das Design wechselt ohne Neustart.",
        ["fr"] = "• VTrim utilise désormais le même échantillon HOTAS pour vJoy et l’aperçu, supprimant les lectures simultanées qui figeaient le manche. Le thème change sans redémarrer l’application.",
        ["pt"] = "• O VTrim usa agora a mesma leitura do HOTAS para o vJoy e a pré-visualização, eliminando leituras simultâneas que causavam pausas no manche. O tema muda sem reiniciar a aplicação.",
        ["pl"] = "• VTrim używa teraz tej samej próbki HOTAS dla vJoy i podglądu, usuwając równoczesne odczyty powodujące chwilowe zatrzymanie drążka. Motyw zmienia się bez ponownego uruchamiania.",
        ["ru"] = "• VTrim теперь использует один отсчёт HOTAS для vJoy и предпросмотра, устраняя одновременные чтения, вызывавшие задержки ручки. Тема меняется без перезапуска приложения.",
        ["uk"] = "• VTrim тепер використовує один зразок HOTAS для vJoy і попереднього перегляду, усуваючи одночасні зчитування, що спричиняли затримки ручки. Тема змінюється без перезапуску.",
        ["tr"] = "• VTrim artık vJoy ve canlı önizleme için aynı HOTAS örneğini kullanır; kumandadaki duraksamalara yol açan eşzamanlı okumalar kaldırıldı. Tema uygulama yeniden başlatılmadan değişir.",
        ["el"] = "• Το VTrim χρησιμοποιεί πλέον το ίδιο δείγμα HOTAS για το vJoy και την προεπισκόπηση, καταργώντας τις ταυτόχρονες αναγνώσεις που προκαλούσαν παύσεις στο χειριστήριο. Το θέμα αλλάζει χωρίς επανεκκίνηση.",
        ["ro"] = "• VTrim folosește acum aceeași citire HOTAS pentru vJoy și previzualizare, eliminând citirile simultane care blocau temporar manșa. Tema se schimbă fără repornirea aplicației.",
        ["he"] = "• VTrim משתמש כעת באותה דגימת HOTAS עבור vJoy והתצוגה המקדימה, ומונע קריאות מקבילות שגרמו לעצירות בסטיק. שינוי ערכת הנושא חל ללא הפעלה מחדש.",
        ["zh-Hans"] = "• VTrim 现在使用同一份 HOTAS 采样数据驱动 vJoy 和实时预览，消除了导致摇杆短暂停顿的重复读取。切换主题无需重启应用。"
    };

    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}