namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.8";
    public const string Features =
        "• Starting with Windows now keeps the app in the tray without showing the main window.\n\n" +
        "• Fixed the disposed WebView2 error during tray hide, restore, and shutdown.\n\n" +
        "• Restoring the app refreshes the Home layout and preserves the chosen window size.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• При стартиране с Windows приложението остава в системната област, без да показва главния прозорец.\n\n• Поправена е грешката с освободен WebView2 при скриване, възстановяване и затваряне.\n\n• При възстановяване началният екран се пренарежда и запазва избрания размер на прозореца.",
        ["es"] = "• Al iniciar con Windows, la aplicación permanece en la bandeja sin mostrar la ventana principal.\n\n• Corregido el error de WebView2 descartado al ocultar, restaurar y cerrar.\n\n• Al restaurar, Inicio actualiza su diseño y conserva el tamaño de ventana elegido.",
        ["de"] = "• Beim Windows-Start bleibt die App im Infobereich, ohne das Hauptfenster anzuzeigen.\n\n• Der Fehler durch ein freigegebenes WebView2 beim Ausblenden, Wiederherstellen und Beenden wurde behoben.\n\n• Beim Wiederherstellen wird die Startseite neu angeordnet und die gewählte Fenstergröße beibehalten.",
        ["fr"] = "• Au démarrage de Windows, l’application reste dans la zone de notification sans afficher la fenêtre principale.\n\n• L’erreur liée à WebView2 libéré lors du masquage, de la restauration et de la fermeture est corrigée.\n\n• La restauration réorganise l’accueil et conserve la taille de fenêtre choisie.",
        ["pt"] = "• Ao iniciar com o Windows, a aplicação fica na área de notificação sem mostrar a janela principal.\n\n• Corrigido o erro de WebView2 descartado ao ocultar, restaurar e fechar.\n\n• Ao restaurar, o Início reorganiza-se e mantém o tamanho escolhido para a janela.",
        ["pl"] = "• Przy uruchamianiu z systemem Windows aplikacja pozostaje w zasobniku bez pokazywania głównego okna.\n\n• Naprawiono błąd zwolnionego WebView2 przy ukrywaniu, przywracaniu i zamykaniu.\n\n• Przywrócenie odświeża układ strony głównej i zachowuje wybrany rozmiar okna.",
        ["ru"] = "• При запуске с Windows приложение остаётся в области уведомлений, не показывая главное окно.\n\n• Исправлена ошибка освобождённого WebView2 при скрытии, восстановлении и закрытии.\n\n• При восстановлении обновляется расположение главного экрана и сохраняется выбранный размер окна.",
        ["uk"] = "• Під час запуску з Windows програма залишається в області сповіщень, не показуючи головне вікно.\n\n• Виправлено помилку звільненого WebView2 під час приховування, відновлення та закриття.\n\n• Під час відновлення оновлюється розташування головного екрана й зберігається вибраний розмір вікна.",
        ["tr"] = "• Windows ile başlatıldığında uygulama ana pencereyi göstermeden sistem tepsisinde kalır.\n\n• Gizleme, geri yükleme ve kapatma sırasında oluşan atılmış WebView2 hatası düzeltildi.\n\n• Geri yükleme Ana Sayfa düzenini yeniler ve seçilen pencere boyutunu korur.",
        ["el"] = "• Κατά την εκκίνηση με τα Windows, η εφαρμογή μένει στην περιοχή ειδοποιήσεων χωρίς να εμφανίζει το κύριο παράθυρο.\n\n• Διορθώθηκε το σφάλμα αποδεσμευμένου WebView2 κατά την απόκρυψη, επαναφορά και έξοδο.\n\n• Η επαναφορά ανανεώνει τη διάταξη της αρχικής σελίδας και διατηρεί το επιλεγμένο μέγεθος παραθύρου.",
        ["ro"] = "• La pornirea cu Windows, aplicația rămâne în zona de notificare fără a afișa fereastra principală.\n\n• A fost corectată eroarea WebView2 eliminat la ascundere, restaurare și închidere.\n\n• Restaurarea rearanjează pagina principală și păstrează dimensiunea aleasă a ferestrei.",
        ["he"] = "• בעת הפעלה עם Windows, היישום נשאר באזור ההודעות בלי להציג את החלון הראשי.\n\n• תוקנה שגיאת WebView2 ששוחרר בעת הסתרה, שחזור וסגירה.\n\n• שחזור החלון מרענן את פריסת דף הבית ושומר על גודל החלון שנבחר.",
        ["zh-Hans"] = "• 随 Windows 启动时，应用留在系统托盘，不显示主窗口。\n\n• 修复了隐藏、恢复和关闭时访问已释放 WebView2 的错误。\n\n• 恢复窗口时会刷新主页布局并保留所选窗口大小。"
    };

    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
