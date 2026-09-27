namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.7";
    public const string Features =
        "• Centered the Home aircraft row beneath the logo and kept it clear of the right-side controls.\n\n" +
        "• Separated aircraft names from their images in the compact Home cards.\n\n" +
        "• Enlarged the app update progress window so its status text is fully visible.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• Центрирана е лентата със самолети под логото, без да застъпва десните контроли.\n\n• Имената и изображенията в компактните карти вече не се застъпват.\n\n• Прозорецът за напредъка на обновяването е увеличен, за да се вижда целият текст.",
        ["es"] = "• La fila de aviones de Inicio está centrada bajo el logotipo y no invade los controles de la derecha.\n\n• Los nombres y las imágenes de las tarjetas compactas ya no se superponen.\n\n• La ventana de progreso de actualización es más grande para mostrar todo el texto.",
        ["de"] = "• Die Flugzeugleiste auf der Startseite ist unter dem Logo zentriert und überlappt die rechten Bedienelemente nicht.\n\n• Namen und Bilder der kompakten Karten überlappen sich nicht mehr.\n\n• Das Fenster für den Updatefortschritt wurde vergrößert, damit der gesamte Text sichtbar ist.",
        ["fr"] = "• La rangée d’avions de l’accueil est centrée sous le logo sans chevaucher les commandes de droite.\n\n• Les noms et les images des cartes compactes ne se chevauchent plus.\n\n• La fenêtre de progression de la mise à jour a été agrandie pour afficher tout le texte.",
        ["pt"] = "• A fila de aviões no Início está centrada sob o logótipo, sem sobrepor os controlos à direita.\n\n• Os nomes e as imagens dos cartões compactos já não se sobrepõem.\n\n• A janela de progresso da atualização foi ampliada para mostrar todo o texto.",
        ["pl"] = "• Rząd samolotów na stronie głównej jest wyśrodkowany pod logo i nie nachodzi na elementy po prawej.\n\n• Nazwy i obrazy na małych kartach już się nie nakładają.\n\n• Okno postępu aktualizacji powiększono, aby cały tekst był widoczny.",
        ["ru"] = "• Ряд самолётов на главной странице выровнен под логотипом и не перекрывает элементы справа.\n\n• Названия и изображения на компактных карточках больше не накладываются друг на друга.\n\n• Окно обновления увеличено, чтобы текст отображался полностью.",
        ["uk"] = "• Ряд літаків на головній сторінці центровано під логотипом без перекриття елементів праворуч.\n\n• Назви й зображення на компактних картках більше не накладаються.\n\n• Вікно оновлення збільшено, щоб увесь текст був видимий.",
        ["tr"] = "• Ana sayfadaki uçak sırası logonun altında ortalandı ve sağdaki denetimlerle çakışmıyor.\n\n• Küçük kartlardaki adlar ve görseller artık üst üste gelmiyor.\n\n• Güncelleme ilerleme penceresi tüm metni gösterecek şekilde büyütüldü.",
        ["el"] = "• Η σειρά αεροσκαφών στην αρχική σελίδα κεντραρίστηκε κάτω από το λογότυπο χωρίς να επικαλύπτει τα δεξιά στοιχεία.\n\n• Τα ονόματα και οι εικόνες στις μικρές κάρτες δεν επικαλύπτονται πλέον.\n\n• Το παράθυρο ενημέρωσης μεγάλωσε ώστε να εμφανίζεται όλο το κείμενο.",
        ["ro"] = "• Rândul de aeronave de pe pagina principală este centrat sub siglă și nu se suprapune peste comenzile din dreapta.\n\n• Numele și imaginile de pe cardurile compacte nu se mai suprapun.\n\n• Fereastra de progres a actualizării a fost mărită pentru a afișa tot textul.",
        ["he"] = "• שורת המטוסים בדף הבית ממורכזת מתחת לסמל ואינה חופפת לפקדים שמימין.\n\n• השמות והתמונות בכרטיסים הקטנים כבר אינם חופפים.\n\n• חלון התקדמות העדכון הוגדל כדי להציג את כל הטקסט.",
        ["zh-Hans"] = "• 首页飞机栏已在徽标下方居中，不再遮挡右侧控件。\n\n• 紧凑卡片中的名称与图片不再重叠。\n\n• 更新进度窗口已扩大，可完整显示状态文字。"
    };

    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
