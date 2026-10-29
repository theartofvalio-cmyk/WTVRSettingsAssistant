namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.12";
    public const string Features =
        "• Improved VTrim HOTAS response: flight axes now reach vJoy before dashboard updates. " +
        "Controller reads shared by multiple switch bindings are performed once per cycle. " +
        "Neck Assistant no longer clones settings or refreshes hidden graphs on every input tick.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• Подобрен е откликът на HOTAS във VTrim: осите се изпращат към vJoy преди обновяването на екрана. Няколко настройки за превключватели споделят едно прочитане на контролера за цикъл. Neck Assistant вече не копира настройките и не обновява скрити графики при всеки входен цикъл.",
        ["es"] = "• Mejor respuesta del HOTAS en VTrim: los ejes llegan a vJoy antes de actualizar la pantalla. Varias asignaciones de interruptores comparten una lectura del mando por ciclo. Neck Assistant ya no copia los ajustes ni actualiza gráficos ocultos en cada ciclo de entrada.",
        ["de"] = "• Verbesserte HOTAS-Reaktion in VTrim: Die Achsen erreichen vJoy vor der Anzeigeaktualisierung. Mehrere Schalterbelegungen nutzen eine gemeinsame Controller-Abfrage pro Zyklus. Neck Assistant kopiert Einstellungen und aktualisiert ausgeblendete Grafiken nicht mehr bei jedem Eingabetakt.",
        ["fr"] = "• Réponse HOTAS améliorée dans VTrim : les axes sont envoyés à vJoy avant l’actualisation de l’affichage. Plusieurs affectations de commutateurs partagent une lecture du contrôleur par cycle. Neck Assistant ne copie plus les réglages ni n’actualise les graphiques masqués à chaque cycle d’entrée.",
        ["pt"] = "• Resposta HOTAS melhorada no VTrim: os eixos chegam ao vJoy antes da atualização do ecrã. Várias atribuições de interruptores partilham uma leitura do controlador por ciclo. O Neck Assistant já não copia as definições nem atualiza gráficos ocultos em cada ciclo de entrada.",
        ["pl"] = "• Poprawiono reakcję HOTAS w VTrim: osie trafiają do vJoy przed odświeżeniem ekranu. Kilka przypisań przełączników korzysta z jednego odczytu kontrolera na cykl. Neck Assistant nie kopiuje już ustawień ani nie odświeża ukrytych wykresów przy każdym cyklu wejścia.",
        ["ru"] = "• Улучшен отклик HOTAS в VTrim: оси передаются в vJoy до обновления экрана. Несколько назначений переключателей используют одно чтение контроллера за цикл. Neck Assistant больше не копирует настройки и не обновляет скрытые графики при каждом цикле ввода.",
        ["uk"] = "• Покращено відгук HOTAS у VTrim: осі передаються до vJoy перед оновленням екрана. Кілька призначень перемикачів використовують одне зчитування контролера за цикл. Neck Assistant більше не копіює налаштування й не оновлює приховані графіки під час кожного циклу вводу.",
        ["tr"] = "• VTrim HOTAS tepkisi iyileştirildi: eksenler ekran güncellenmeden önce vJoy’a gönderilir. Birden fazla anahtar ataması her döngüde tek bir denetleyici okumasını paylaşır. Neck Assistant artık her giriş döngüsünde ayarları kopyalamaz veya gizli grafikleri yenilemez.",
        ["el"] = "• Βελτιώθηκε η απόκριση HOTAS στο VTrim: οι άξονες αποστέλλονται στο vJoy πριν ανανεωθεί η οθόνη. Πολλές αντιστοιχίσεις διακοπτών μοιράζονται μία ανάγνωση του χειριστηρίου ανά κύκλο. Το Neck Assistant δεν αντιγράφει πλέον τις ρυθμίσεις ούτε ανανεώνει κρυφά γραφήματα σε κάθε κύκλο εισόδου.",
        ["ro"] = "• Răspuns HOTAS îmbunătățit în VTrim: axele ajung la vJoy înainte de actualizarea ecranului. Mai multe atribuiri de comutatoare folosesc o singură citire a controlerului pe ciclu. Neck Assistant nu mai copiază setările și nu mai actualizează graficele ascunse la fiecare ciclu de intrare.",
        ["he"] = "• תגובת HOTAS ב־VTrim שופרה: הצירים נשלחים אל vJoy לפני רענון התצוגה. כמה מיפויי מתגים משתפים קריאה אחת של הבקר בכל מחזור. Neck Assistant אינו מעתיק עוד הגדרות או מרענן גרפים מוסתרים בכל מחזור קלט.",
        ["zh-Hans"] = "• 改进了 VTrim 的 HOTAS 响应：飞行轴先发送到 vJoy，再更新界面。多个开关绑定在每个周期共用一次控制器读取。Neck Assistant 不再在每次输入轮询时复制设置或刷新隐藏图表。"
    };

    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
