namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.13";
    public const string Features =
        "• VTrim reads HOTAS flight axes and updates vJoy on a dedicated 8 ms loop, " +
        "so window redraws cannot delay flight controls. Live stick and rudder markers now update on every UI poll.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• VTrim чете полетните оси на HOTAS и обновява vJoy в отделен цикъл на всеки 8 ms, така че обновяването на прозореца да не забавя управлението. Маркерите за джойстика и педалите вече се обновяват при всяко прочитане на интерфейса.",
        ["es"] = "• VTrim lee los ejes de vuelo del HOTAS y actualiza vJoy en un ciclo independiente de 8 ms, para que el redibujado de la ventana no retrase los controles. Los indicadores del mando y los pedales se actualizan en cada lectura de la interfaz.",
        ["de"] = "• VTrim liest die HOTAS-Flugachsen und aktualisiert vJoy in einem eigenen 8-ms-Takt, damit das Neuzeichnen des Fensters die Steuerung nicht verzögert. Die Anzeigen für Stick und Pedale werden bei jeder UI-Abfrage aktualisiert.",
        ["fr"] = "• VTrim lit les axes de vol du HOTAS et met à jour vJoy dans une boucle dédiée de 8 ms, afin que le rafraîchissement de la fenêtre ne retarde pas les commandes. Les repères du manche et du palonnier sont actualisés à chaque lecture de l’interface.",
        ["pt"] = "• O VTrim lê os eixos de voo do HOTAS e atualiza o vJoy num ciclo dedicado de 8 ms, para que o redesenho da janela não atrase os comandos. Os indicadores do manche e dos pedais são atualizados em cada leitura da interface.",
        ["pl"] = "• VTrim odczytuje osie lotu HOTAS i aktualizuje vJoy w osobnej pętli co 8 ms, więc odświeżanie okna nie opóźnia sterowania. Wskaźniki drążka i pedałów są aktualizowane przy każdym odczycie interfejsu.",
        ["ru"] = "• VTrim считывает оси HOTAS и обновляет vJoy в отдельном цикле каждые 8 мс, поэтому перерисовка окна не задерживает управление. Индикаторы ручки и педалей обновляются при каждом опросе интерфейса.",
        ["uk"] = "• VTrim зчитує осі HOTAS і оновлює vJoy в окремому циклі кожні 8 мс, тому перемальовування вікна не затримує керування. Індикатори ручки й педалей оновлюються під час кожного опитування інтерфейсу.",
        ["tr"] = "• VTrim, HOTAS uçuş eksenlerini ayrı bir 8 ms döngüsünde okuyup vJoy'u günceller; pencerenin yeniden çizilmesi kontrolleri geciktirmez. Kumanda ve pedal göstergeleri her arayüz yoklamasında yenilenir.",
        ["el"] = "• Το VTrim διαβάζει τους άξονες HOTAS και ενημερώνει το vJoy σε ξεχωριστό κύκλο 8 ms, ώστε η ανανέωση του παραθύρου να μην καθυστερεί τον χειρισμό. Οι δείκτες χειριστηρίου και πεντάλ ενημερώνονται σε κάθε έλεγχο του περιβάλλοντος.",
        ["ro"] = "• VTrim citește axele HOTAS și actualizează vJoy într-o buclă separată de 8 ms, astfel încât redesenarea ferestrei să nu întârzie comenzile. Indicatorii manșei și pedalelor se actualizează la fiecare citire a interfeței.",
        ["he"] = "• VTrim קורא את צירי הטיסה של HOTAS ומעדכן את vJoy בלולאה ייעודית של 8 מילישניות, כך שרענון החלון אינו מעכב את השליטה. סמני הסטיק והדוושות מתעדכנים בכל דגימה של הממשק.",
        ["zh-Hans"] = "• VTrim 在独立的 8 毫秒循环中读取 HOTAS 飞行轴并更新 vJoy，因此窗口重绘不会延迟操纵。摇杆和方向舵踏板指示器在每次界面轮询时更新。"
    };

    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
