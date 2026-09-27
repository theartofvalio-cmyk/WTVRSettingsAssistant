namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    // The Info page shows only changes in this version. Update these together
    // with the app version for the next release.
    public const string Version = "2.0.6";
    public const string Features =
        "• Added configurable Hover UP and Hover Down bindings that hold Left Shift and Left Ctrl.\n\n" +
        "• Added Score Board Mouse Fix: assign the same button as the in-game scoreboard to move the cursor to the screen's top-left.\n\n" +
        "• Fixed the Home aircraft strip to show at most three centered profiles, including in full screen, and removed its footer filter and star.\n\n" +
        "• The grey launch button now shows RUNNING in muted beige while War Thunder is running.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• Добавени са настройки за Hover UP и Hover Down, които задържат съответно левия Shift и левия Ctrl.\n\n" +
                 "• Добавена е поправка на мишката за таблото: задай същия бутон като в играта, за да преместваш курсора горе вляво.\n\n" +
                 "• Поправена е лентата с профили на началния екран: показва до три центрирани самолета и на цял екран; премахнати са филтърът и звездата от долната лента.\n\n" +
                 "• Сивият бутон за стартиране вече показва RUNNING в приглушено бежово, докато War Thunder работи.",
        ["es"] = "• Se añadieron asignaciones configurables Hover UP y Hover Down que mantienen pulsados Shift izquierdo y Ctrl izquierdo.\n\n" +
                 "• Se añadió Score Board Mouse Fix: asigna el mismo botón que el marcador del juego para mover el cursor a la esquina superior izquierda.\n\n" +
                 "• La franja de perfiles de Inicio muestra como máximo tres aviones centrados, también a pantalla completa; se quitaron el filtro y la estrella del pie.\n\n" +
                 "• El botón gris de inicio muestra RUNNING en beige apagado mientras War Thunder está en ejecución.",
        ["de"] = "• Konfigurierbare Hover UP- und Hover Down-Belegungen halten die linke Umschalt- bzw. Strg-Taste gedrückt.\n\n" +
                 "• Score Board Mouse Fix: Dieselbe Taste wie für die Anzeigetafel im Spiel belegen, um den Mauszeiger nach links oben zu setzen.\n\n" +
                 "• Die Profilleiste auf der Startseite zeigt auch im Vollbild höchstens drei zentrierte Flugzeuge; Filter und Stern in der Fußleiste wurden entfernt.\n\n" +
                 "• Die graue Startschaltfläche zeigt bei laufendem War Thunder RUNNING in gedämpftem Beige.",
        ["fr"] = "• Ajout des raccourcis configurables Hover UP et Hover Down, qui maintiennent Maj gauche et Ctrl gauche enfoncés.\n\n" +
                 "• Ajout de Score Board Mouse Fix : attribuez le même bouton que le tableau des scores du jeu pour placer le curseur en haut à gauche.\n\n" +
                 "• La barre des profils de l'accueil affiche au plus trois avions centrés, même en plein écran ; le filtre et l'étoile du pied de page ont été retirés.\n\n" +
                 "• Le bouton de lancement gris affiche RUNNING en beige atténué lorsque War Thunder est en cours d'exécution.",
        ["pt"] = "• Adicionadas as atribuições configuráveis Hover UP e Hover Down, que mantêm Shift esquerdo e Ctrl esquerdo premidos.\n\n" +
                 "• Adicionado Score Board Mouse Fix: atribui o mesmo botão do placar no jogo para mover o cursor para o canto superior esquerdo.\n\n" +
                 "• A faixa de perfis no Início mostra até três aviões centrados, também em ecrã inteiro; o filtro e a estrela do rodapé foram removidos.\n\n" +
                 "• O botão cinzento de iniciar mostra RUNNING em bege suave enquanto War Thunder está em execução.",
        ["pl"] = "• Dodano konfigurowalne przypisania Hover UP i Hover Down, które przytrzymują lewy Shift i lewy Ctrl.\n\n" +
                 "• Dodano Score Board Mouse Fix: przypisz ten sam przycisk co do tabeli wyników w grze, aby przenieść kursor w lewy górny róg.\n\n" +
                 "• Pasek profili na stronie głównej pokazuje najwyżej trzy wyśrodkowane samoloty, również na pełnym ekranie; usunięto filtr i gwiazdkę ze stopki.\n\n" +
                 "• Szary przycisk uruchamiania pokazuje RUNNING w stonowanym beżu, gdy War Thunder działa.",
        ["ru"] = "• Добавлены настраиваемые привязки Hover UP и Hover Down, удерживающие левый Shift и левый Ctrl.\n\n" +
                 "• Добавлена Score Board Mouse Fix: назначьте ту же кнопку, что и для таблицы результатов в игре, чтобы перемещать курсор в левый верхний угол.\n\n" +
                 "• На главной странице теперь не более трёх выровненных по центру самолётов даже в полноэкранном режиме; фильтр и звезда внизу удалены.\n\n" +
                 "• Серая кнопка запуска показывает RUNNING приглушённым бежевым цветом, пока работает War Thunder.",
        ["uk"] = "• Додано налаштовувані прив'язки Hover UP і Hover Down, які утримують ліві Shift і Ctrl.\n\n" +
                 "• Додано Score Board Mouse Fix: призначте ту саму кнопку, що й для таблиці результатів у грі, щоб переміщувати курсор у верхній лівий кут.\n\n" +
                 "• Смуга профілів на головній сторінці показує щонайбільше три центровані літаки навіть на весь екран; фільтр і зірку внизу прибрано.\n\n" +
                 "• Сіра кнопка запуску показує RUNNING приглушеним бежевим кольором, поки працює War Thunder.",
        ["tr"] = "• Sol Shift ve sol Ctrl tuşlarını basılı tutan, ayarlanabilir Hover UP ve Hover Down atamaları eklendi.\n\n" +
                 "• Score Board Mouse Fix eklendi: imleci ekranın sol üstüne taşımak için oyundaki skor tablosuyla aynı düğmeyi atayın.\n\n" +
                 "• Ana sayfadaki profil şeridi tam ekranda da en fazla üç ortalanmış uçak gösteriyor; alt kısımdaki filtre ve yıldız kaldırıldı.\n\n" +
                 "• War Thunder çalışırken gri başlat düğmesi RUNNING yazısını soluk bej renkte gösteriyor.",
        ["el"] = "• Προστέθηκαν ρυθμιζόμενες συνδέσεις Hover UP και Hover Down που κρατούν πατημένα το αριστερό Shift και Ctrl.\n\n" +
                 "• Προστέθηκε το Score Board Mouse Fix: ορίστε το ίδιο κουμπί με τον πίνακα σκορ του παιχνιδιού για μετακίνηση του δείκτη επάνω αριστερά.\n\n" +
                 "• Η λωρίδα προφίλ στην αρχική σελίδα δείχνει έως τρία κεντραρισμένα αεροσκάφη και σε πλήρη οθόνη· αφαιρέθηκαν το φίλτρο και το αστέρι από κάτω.\n\n" +
                 "• Το γκρι κουμπί εκκίνησης δείχνει RUNNING σε απαλό μπεζ όσο εκτελείται το War Thunder.",
        ["ro"] = "• Au fost adăugate legături configurabile Hover UP și Hover Down, care țin apăsate Shift stânga și Ctrl stânga.\n\n" +
                 "• A fost adăugat Score Board Mouse Fix: atribuie același buton ca pentru tabela de scor din joc pentru a muta cursorul în colțul stânga sus.\n\n" +
                 "• Banda de profiluri de pe pagina principală arată cel mult trei avioane centrate și pe ecran complet; filtrul și steaua din subsol au fost eliminate.\n\n" +
                 "• Butonul gri de lansare afișează RUNNING în bej estompat cât timp rulează War Thunder.",
        ["he"] = "• נוספו הקצאות ניתנות להגדרה ל-Hover UP ול-Hover Down, שמחזיקות את Shift השמאלי ואת Ctrl השמאלי לחוצים.\n\n" +
                 "• נוסף Score Board Mouse Fix: הקצה את אותו כפתור של לוח התוצאות במשחק כדי להעביר את הסמן לפינה השמאלית העליונה.\n\n" +
                 "• רצועת הפרופילים במסך הבית מציגה עד שלושה מטוסים ממורכזים גם במסך מלא; המסנן והכוכב בתחתית הוסרו.\n\n" +
                 "• כפתור ההפעלה האפור מציג RUNNING בצבע בז' מעומעם בזמן ש-War Thunder פועל.",
        ["zh-Hans"] = "• 新增可自定义的 Hover UP 和 Hover Down 绑定，分别按住左 Shift 和左 Ctrl。\n\n" +
                      "• 新增 Score Board Mouse Fix：绑定与游戏记分板相同的按键，将光标移到屏幕左上角。\n\n" +
                      "• 主页飞机配置条即使在全屏下也最多显示三架居中的飞机；移除了底部筛选框和星标。\n\n" +
                      "• War Thunder 运行时，灰色启动按钮以柔和的米色显示 RUNNING。"
    };

    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
