namespace WTVRSettingsAssistant;

internal static class ReleaseNotes
{
    public const string Version = "2.0.11";
    public const string Features =
        "• Simple mode: enable ‘Unlock rotation speed up to 300%’ to extend the camera speed slider.\n\n" +
        "• Advanced mode: enter a camera rotation speed from 1% to 300% directly in the speed field.\n\n" +
        "• Speeds above 100% now increase the actual rotation rate and response. Your selected speed is saved; turning off the Simple unlock caps it at 100%.";

    private static readonly IReadOnlyDictionary<string, string> Translations = new Dictionary<string, string>
    {
        ["bg"] = "• В Simple режим можеш да отключиш скорост на завъртане до 300%. В Advanced режим въведи стойност от 1% до 300% директно в полето.\n\n• Скоростите над 100% увеличават реалната скорост и реакция на камерата. Настройката се запазва; изключването на отключването в Simple ограничава скоростта до 100%.",
        ["es"] = "• El modo Simple permite desbloquear la velocidad de giro hasta el 300%. En Avanzado, introduce del 1% al 300% directamente.\n\n• Los valores superiores al 100% aumentan la velocidad y respuesta reales. La velocidad se guarda; desactivar el desbloqueo en Simple la limita al 100%.",
        ["de"] = "• Im einfachen Modus lässt sich die Drehgeschwindigkeit bis 300% freigeben. Im erweiterten Modus kann ein Wert von 1% bis 300% direkt eingegeben werden.\n\n• Über 100% steigen tatsächliche Drehgeschwindigkeit und Reaktion. Der Wert wird gespeichert; das Ausschalten der Freigabe begrenzt ihn auf 100%.",
        ["fr"] = "• En mode Simple, débloquez la vitesse de rotation jusqu’à 300%. En mode Avancé, saisissez directement une valeur de 1% à 300%.\n\n• Au-delà de 100%, la vitesse et la réactivité réelles augmentent. Le réglage est enregistré ; désactiver le déblocage en mode Simple le limite à 100%.",
        ["pt"] = "• No modo Simples, desbloqueia a velocidade de rotação até 300%. No modo Avançado, introduz diretamente um valor de 1% a 300%.\n\n• Acima de 100%, a velocidade e a resposta reais aumentam. O valor é guardado; desativar o desbloqueio no modo Simples limita-o a 100%.",
        ["pl"] = "• W trybie prostym można odblokować prędkość obrotu do 300%. W trybie zaawansowanym wpisz wartość od 1% do 300%.\n\n• Powyżej 100% rośnie rzeczywista prędkość i szybkość reakcji. Ustawienie jest zapisywane; wyłączenie odblokowania ogranicza je do 100%.",
        ["ru"] = "• В простом режиме можно разрешить скорость поворота до 300%. В расширенном режиме введите значение от 1% до 300%.\n\n• Выше 100% увеличиваются реальная скорость поворота и отклик. Настройка сохраняется; отключение разрешения в простом режиме ограничивает скорость до 100%.",
        ["uk"] = "• У простому режимі можна дозволити швидкість повороту до 300%. У розширеному режимі введіть значення від 1% до 300%.\n\n• Понад 100% зростають реальна швидкість повороту та відгук. Налаштування зберігається; вимкнення дозволу в простому режимі обмежує швидкість до 100%.",
        ["tr"] = "• Basit modda dönüş hızını %300’e kadar açabilirsiniz. Gelişmiş modda %1 ile %300 arasında bir değer girin.\n\n• %100 üzerinde gerçek dönüş hızı ve tepki artar. Ayar kaydedilir; Basit moddaki kilidi kapatmak hızı %100 ile sınırlar.",
        ["el"] = "• Στην απλή λειτουργία ξεκλειδώστε ταχύτητα περιστροφής έως 300%. Στη σύνθετη λειτουργία εισαγάγετε τιμή από 1% έως 300%.\n\n• Πάνω από 100% αυξάνονται η πραγματική ταχύτητα και η απόκριση. Η ρύθμιση αποθηκεύεται· η απενεργοποίηση του ξεκλειδώματος την περιορίζει στο 100%.",
        ["ro"] = "• În modul Simplu poți debloca viteza de rotație până la 300%. În modul Avansat introdu direct o valoare între 1% și 300%.\n\n• Peste 100% cresc viteza și răspunsul reale. Setarea se salvează; dezactivarea deblocării în modul Simplu limitează viteza la 100%.",
        ["he"] = "• במצב פשוט ניתן לאפשר מהירות סיבוב עד 300%. במצב מתקדם הזן ערך בין 1% ל־300%.\n\n• מעל 100% מהירות הסיבוב והתגובה בפועל גדלות. ההגדרה נשמרת; כיבוי האפשרות במצב פשוט מגביל את המהירות ל־100%.",
        ["zh-Hans"] = "• 简单模式可解锁最高300%的旋转速度。高级模式可直接输入1%到300%的速度。\n\n• 超过100%时会提高实际旋转速度和响应。设置会保存；在简单模式关闭解锁会将速度限制为100%。"
    };
    public static string ForLanguage(string languageCode) =>
        Translations.TryGetValue(languageCode, out string? text) ? text : Features;
}
