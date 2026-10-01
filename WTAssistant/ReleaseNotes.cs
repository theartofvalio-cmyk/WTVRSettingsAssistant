namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.9";
    public const string Features =
        "• Home aircraft cards now place the aircraft artwork on the left and its name at the upper right, matching the Profiles page.\n\n" +
        "• Home cards now show the Default or Custom controls badge in the lower right.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• Картите на самолетите в началния екран вече изглеждат като тези в Профили: изображението е вляво, името е горе вдясно, а режимът По подразбиране или Персонализиран е долу вдясно.",
        ["es"] = "• Las tarjetas de aviones de Inicio ahora coinciden con las de Perfiles: imagen a la izquierda, nombre arriba a la derecha y modo Predeterminado o Personalizado abajo a la derecha.",
        ["de"] = "• Die Flugzeugkarten auf der Startseite entsprechen nun denen unter Profile: Bild links, Name oben rechts und Modus Standard oder Benutzerdefiniert unten rechts.",
        ["fr"] = "• Les cartes d’avions de l’accueil correspondent désormais à celles des Profils : image à gauche, nom en haut à droite et mode Par défaut ou Personnalisé en bas à droite.",
        ["pt"] = "• Os cartões de aeronaves no Início agora correspondem aos de Perfis: imagem à esquerda, nome no canto superior direito e modo Predefinido ou Personalizado no canto inferior direito.",
        ["pl"] = "• Karty samolotów na stronie głównej odpowiadają teraz kartom w Profilach: obraz po lewej, nazwa u góry po prawej, a tryb Domyślny lub Niestandardowy na dole po prawej.",
        ["ru"] = "• Карточки самолётов на главной странице теперь выглядят как в Профилях: изображение слева, название вверху справа, режим По умолчанию или Пользовательский внизу справа.",
        ["uk"] = "• Картки літаків на головній сторінці тепер відповідають карткам у Профілях: зображення ліворуч, назва вгорі праворуч, режим За замовчуванням або Власний унизу праворуч.",
        ["tr"] = "• Ana Sayfa uçak kartları artık Profiller ile aynı düzendedir: görsel solda, ad sağ üstte, Varsayılan veya Özel mod etiketi sağ altta.",
        ["el"] = "• Οι κάρτες αεροσκαφών στην αρχική σελίδα ταιριάζουν πλέον με τα Προφίλ: εικόνα αριστερά, όνομα επάνω δεξιά και λειτουργία Προεπιλογή ή Προσαρμοσμένη κάτω δεξιά.",
        ["ro"] = "• Cardurile aeronavelor de pe pagina principală corespund acum celor din Profiluri: imaginea în stânga, numele sus în dreapta și modul Implicit sau Personalizat jos în dreapta.",
        ["he"] = "• כרטיסי המטוסים בדף הבית תואמים כעת לאלה שבפרופילים: תמונה משמאל, שם בפינה הימנית העליונה ותג מצב ברירת מחדל או מותאם אישית בפינה הימנית התחתונה.",
        ["zh-Hans"] = "• 首页飞机卡片现与配置页面一致：飞机图片在左侧，名称在右上角，默认或自定义模式标记在右下角。"
    };
    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
