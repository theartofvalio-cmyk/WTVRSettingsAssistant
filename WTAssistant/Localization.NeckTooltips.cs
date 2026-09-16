namespace WTVRSettingsAssistant;

internal static partial class AppText
{
    private static void AddNeckTooltipTranslations()
    {
        string[] keys = { "YawStart", "YawRelease", "YawResume", "YawMaximum", "YawSoftness", "PitchStart", "PitchRelease", "PitchResume", "PitchMaximum", "PitchSoftness", "RestoreDefaults" };
        var translations = new Dictionary<string, string[]>
        {
            ["en"] = new[] {
                "Yellow: normal 1:1 motion ends and the rear-view boost begins.",
                "Orange: assistance releases here when returning to center.",
                "Cyan: the boost is complete and natural 1:1 movement continues with the added offset.",
                "Green: final view angle produced by the boost.",
                "Controls how gently the boost accelerates and settles.",
                "Yellow: vertical boost starts here.", "Orange: vertical assistance releases here.",
                "Cyan: natural 1:1 vertical movement resumes.", "Green: maximum vertical view angle.",
                "Controls vertical boost softness.",
                "Restore the recommended Advanced movement values. Your keyboard, mouse, and HOTAS bindings are preserved." },
            ["bg"] = new[] {
                "Жълто: естественото движение 1:1 приключва и започва усилването на задния изглед.",
                "Оранжево: помощта се освобождава тук при връщане към центъра.",
                "Светлосиньо: усилването е завършено и естественото движение 1:1 продължава с добавеното отместване.",
                "Зелено: крайният ъгъл на изгледа след усилването.", "Определя колко плавно усилването се ускорява и установява.",
                "Жълто: вертикалното усилване започва тук.", "Оранжево: вертикалната помощ се освобождава тук.",
                "Светлосиньо: естественото вертикално движение 1:1 се възстановява.", "Зелено: максималният вертикален ъгъл на изгледа.",
                "Определя плавността на вертикалното усилване.",
                "Възстанови препоръчаните стойности за движение в разширен режим. Командите от клавиатурата, мишката и HOTAS се запазват." },
            ["es"] = new[] {
                "Amarillo: termina el movimiento normal 1:1 y comienza el refuerzo de visión trasera.",
                "Naranja: la asistencia se desactiva aquí al volver al centro.",
                "Cian: el refuerzo ha terminado y el movimiento natural 1:1 continúa con el desplazamiento añadido.",
                "Verde: ángulo final de visión producido por el refuerzo.", "Controla la suavidad con la que el refuerzo acelera y se estabiliza.",
                "Amarillo: el refuerzo vertical comienza aquí.", "Naranja: la asistencia vertical se desactiva aquí.",
                "Cian: se reanuda el movimiento vertical natural 1:1.", "Verde: ángulo máximo de visión vertical.",
                "Controla la suavidad del refuerzo vertical.",
                "Restablece los valores de movimiento recomendados del modo Avanzado. Se conservan las asignaciones de teclado, ratón y HOTAS." },
            ["de"] = new[] {
                "Gelb: Die normale 1:1-Bewegung endet und die verstärkte Rücksicht beginnt.",
                "Orange: Beim Zurückkehren zur Mitte wird die Unterstützung hier beendet.",
                "Türkis: Die Verstärkung ist vollständig; die natürliche 1:1-Bewegung wird mit dem zusätzlichen Versatz fortgesetzt.",
                "Grün: endgültiger Blickwinkel durch die Verstärkung.", "Bestimmt, wie sanft die Verstärkung beschleunigt und ausklingt.",
                "Gelb: Hier beginnt die vertikale Verstärkung.", "Orange: Hier endet die vertikale Unterstützung.",
                "Türkis: Die natürliche vertikale 1:1-Bewegung setzt wieder ein.", "Grün: maximaler vertikaler Blickwinkel.",
                "Bestimmt die Sanftheit der vertikalen Verstärkung.",
                "Stellt die empfohlenen Bewegungswerte des erweiterten Modus wieder her. Tastatur-, Maus- und HOTAS-Belegungen bleiben erhalten." },
            ["fr"] = new[] {
                "Jaune : le mouvement normal 1:1 se termine et l'amplification de la vue arrière commence.",
                "Orange : l'assistance se désactive ici lors du retour au centre.",
                "Cyan : l'amplification est complète et le mouvement naturel 1:1 continue avec le décalage ajouté.",
                "Vert : angle de vue final produit par l'amplification.", "Règle la douceur de l'accélération et de la stabilisation de l'amplification.",
                "Jaune : l'amplification verticale commence ici.", "Orange : l'assistance verticale se désactive ici.",
                "Cyan : le mouvement vertical naturel 1:1 reprend.", "Vert : angle de vue vertical maximal.",
                "Règle la douceur de l'amplification verticale.",
                "Rétablit les valeurs de mouvement recommandées du mode Avancé. Les affectations du clavier, de la souris et du HOTAS sont conservées." },
            ["pt"] = new[] {
                "Amarelo: o movimento normal 1:1 termina e começa a amplificação da visão traseira.",
                "Laranja: a assistência é desativada aqui ao retornar ao centro.",
                "Ciano: a amplificação está completa e o movimento natural 1:1 continua com o deslocamento adicional.",
                "Verde: ângulo final de visão produzido pela amplificação.", "Controla a suavidade da aceleração e da estabilização da amplificação.",
                "Amarelo: a amplificação vertical começa aqui.", "Laranja: a assistência vertical é desativada aqui.",
                "Ciano: o movimento vertical natural 1:1 é retomado.", "Verde: ângulo máximo de visão vertical.",
                "Controla a suavidade da amplificação vertical.",
                "Restaura os valores de movimento recomendados do modo Avançado. As atribuições do teclado, mouse e HOTAS são preservadas." },
            ["pl"] = new[] {
                "Żółty: kończy się normalny ruch 1:1 i zaczyna się wzmocnienie widoku do tyłu.",
                "Pomarańczowy: podczas powrotu do środka wspomaganie wyłącza się w tym miejscu.",
                "Błękitny: wzmocnienie jest pełne, a naturalny ruch 1:1 jest kontynuowany z dodatkowym przesunięciem.",
                "Zielony: końcowy kąt widoku uzyskany dzięki wzmocnieniu.", "Określa płynność narastania i stabilizacji wzmocnienia.",
                "Żółty: tutaj zaczyna się wzmocnienie pionowe.", "Pomarańczowy: tutaj wyłącza się wspomaganie pionowe.",
                "Błękitny: wznawia się naturalny ruch pionowy 1:1.", "Zielony: maksymalny pionowy kąt widoku.",
                "Określa płynność wzmocnienia pionowego.",
                "Przywraca zalecane wartości ruchu w trybie zaawansowanym. Przypisania klawiatury, myszy i HOTAS pozostają zachowane." },
            ["ru"] = new[] {
                "Жёлтый: обычное движение 1:1 заканчивается и начинается усиление обзора назад.",
                "Оранжевый: при возвращении к центру помощь отключается здесь.",
                "Голубой: усиление завершено, и естественное движение 1:1 продолжается с добавленным смещением.",
                "Зелёный: конечный угол обзора после усиления.", "Определяет плавность нарастания и стабилизации усиления.",
                "Жёлтый: здесь начинается вертикальное усиление.", "Оранжевый: здесь отключается вертикальная помощь.",
                "Голубой: естественное вертикальное движение 1:1 возобновляется.", "Зелёный: максимальный вертикальный угол обзора.",
                "Определяет плавность вертикального усиления.",
                "Восстановить рекомендуемые параметры движения расширенного режима. Назначения клавиатуры, мыши и HOTAS сохраняются." },
            ["tr"] = new[] {
                "Sarı: normal 1:1 hareket sona erer ve arkaya bakış desteği başlar.",
                "Turuncu: merkeze dönerken destek burada devreden çıkar.",
                "Camgöbeği: artırma tamamlanır ve doğal 1:1 hareket eklenen açı farkıyla devam eder.",
                "Yeşil: desteğin oluşturduğu son görüş açısı.", "Desteğin ne kadar yumuşak hızlanıp dengelendiğini ayarlar.",
                "Sarı: dikey destek burada başlar.", "Turuncu: dikey destek burada devreden çıkar.",
                "Camgöbeği: doğal 1:1 dikey hareket yeniden başlar.", "Yeşil: en büyük dikey görüş açısı.",
                "Dikey desteğin yumuşaklığını ayarlar.",
                "Gelişmiş mod için önerilen hareket değerlerini geri yükler. Klavye, fare ve HOTAS atamaları korunur." },
            ["el"] = new[] {
                "Κίτρινο: η κανονική κίνηση 1:1 σταματά και αρχίζει η ενίσχυση της πίσω θέας.",
                "Πορτοκαλί: η υποβοήθηση απενεργοποιείται εδώ κατά την επιστροφή στο κέντρο.",
                "Κυανό: η ενίσχυση έχει ολοκληρωθεί και η φυσική κίνηση 1:1 συνεχίζεται με την πρόσθετη μετατόπιση.",
                "Πράσινο: τελική γωνία θέας που παράγεται από την ενίσχυση.", "Ρυθμίζει πόσο ομαλά επιταχύνεται και σταθεροποιείται η ενίσχυση.",
                "Κίτρινο: η κατακόρυφη ενίσχυση αρχίζει εδώ.", "Πορτοκαλί: η κατακόρυφη υποβοήθηση απενεργοποιείται εδώ.",
                "Κυανό: η φυσική κατακόρυφη κίνηση 1:1 συνεχίζεται.", "Πράσινο: μέγιστη κατακόρυφη γωνία θέας.",
                "Ρυθμίζει την ομαλότητα της κατακόρυφης ενίσχυσης.",
                "Επαναφέρει τις προτεινόμενες τιμές κίνησης της προηγμένης λειτουργίας. Διατηρούνται οι αντιστοιχίσεις πληκτρολογίου, ποντικιού και HOTAS." },
            ["ro"] = new[] {
                "Galben: mișcarea normală 1:1 se încheie și începe amplificarea vederii în spate.",
                "Portocaliu: asistența se dezactivează aici la revenirea în centru.",
                "Turcoaz: amplificarea este completă, iar mișcarea naturală 1:1 continuă cu decalajul adăugat.",
                "Verde: unghiul final al vederii produs de amplificare.", "Controlează cât de lin accelerează și se stabilizează amplificarea.",
                "Galben: amplificarea verticală începe aici.", "Portocaliu: asistența verticală se dezactivează aici.",
                "Turcoaz: mișcarea verticală naturală 1:1 se reia.", "Verde: unghiul maxim al vederii verticale.",
                "Controlează finețea amplificării verticale.",
                "Restabilește valorile de mișcare recomandate pentru modul Avansat. Asocierile tastaturii, mouse-ului și HOTAS sunt păstrate." },
            ["zh-Hans"] = new[] {
                "黄色：正常的 1:1 运动结束，后视增强开始。", "橙色：回到中心时，辅助在此处停止。",
                "青色：增强已完成，保持附加偏移量并恢复自然的 1:1 运动。", "绿色：增强后的最终视角。",
                "控制增强加速和稳定过程的平滑程度。", "黄色：垂直增强在此处开始。", "橙色：垂直辅助在此处停止。",
                "青色：恢复自然的 1:1 垂直运动。", "绿色：最大垂直视角。", "控制垂直增强的平滑程度。",
                "恢复高级模式推荐的运动参数。保留键盘、鼠标和 HOTAS 绑定。" }
        };
        foreach (var (language, values) in translations)
        {
            if (values.Length != keys.Length) throw new InvalidOperationException($"Invalid Neck tooltip catalog: {language}");
            for (int i = 0; i < keys.Length; i++) Texts[language]["Neck.Tooltip." + keys[i]] = values[i];
        }
    }
}
