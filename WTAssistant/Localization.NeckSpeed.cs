namespace WTVRSettingsAssistant;

internal static partial class AppText
{
    private static void AddNeckSpeedTranslations()
    {
        (string Code, string Unlock, string UnlockHelp, string InputHelp)[] rows =
        [
            ("en", "Unlock rotation speed up to 300%", "Allow speeds above 100%. Turning this off limits the current speed to 100%.", "Enter rotation speed from 1% to 300%."),
            ("bg", "Отключи скорост на завъртане до 300%", "Разрешава скорост над 100%. Изключването ограничава текущата скорост до 100%.", "Въведи скорост на завъртане от 1% до 300%."),
            ("es", "Desbloquear velocidad de giro hasta el 300%", "Permite velocidades superiores al 100%. Al desactivarlo, la velocidad actual se limita al 100%.", "Introduce una velocidad de giro del 1% al 300%."),
            ("de", "Drehgeschwindigkeit bis 300% freigeben", "Erlaubt Geschwindigkeiten über 100%. Beim Ausschalten wird die aktuelle Geschwindigkeit auf 100% begrenzt.", "Drehgeschwindigkeit von 1% bis 300% eingeben."),
            ("fr", "Débloquer la vitesse de rotation jusqu’à 300%", "Autorise une vitesse supérieure à 100%. La désactivation limite la vitesse actuelle à 100%.", "Saisissez une vitesse de rotation de 1% à 300%."),
            ("pt", "Desbloquear velocidade de rotação até 300%", "Permite velocidades acima de 100%. Ao desativar, a velocidade atual fica limitada a 100%.", "Introduz uma velocidade de rotação de 1% a 300%."),
            ("pl", "Odblokuj prędkość obrotu do 300%", "Pozwala przekroczyć 100%. Wyłączenie ogranicza bieżącą prędkość do 100%.", "Wpisz prędkość obrotu od 1% do 300%."),
            ("ru", "Разрешить скорость поворота до 300%", "Разрешает скорость выше 100%. При отключении текущая скорость ограничивается 100%.", "Введите скорость поворота от 1% до 300%."),
            ("uk", "Дозволити швидкість повороту до 300%", "Дозволяє швидкість понад 100%. Вимкнення обмежує поточну швидкість до 100%.", "Введіть швидкість повороту від 1% до 300%."),
            ("tr", "Dönüş hızını %300’e kadar aç", "%100 üzerindeki hızlara izin verir. Kapatıldığında mevcut hız %100 ile sınırlanır.", "%1 ile %300 arasında dönüş hızı girin."),
            ("el", "Ξεκλείδωμα ταχύτητας περιστροφής έως 300%", "Επιτρέπει ταχύτητες πάνω από 100%. Η απενεργοποίηση περιορίζει την τρέχουσα ταχύτητα στο 100%.", "Εισαγάγετε ταχύτητα περιστροφής από 1% έως 300%."),
            ("ro", "Deblochează viteza de rotație până la 300%", "Permite viteze peste 100%. Dezactivarea limitează viteza curentă la 100%.", "Introdu o viteză de rotație între 1% și 300%."),
            ("he", "אפשר מהירות סיבוב עד 300%", "מאפשר מהירות מעל 100%. כיבוי האפשרות מגביל את המהירות הנוכחית ל־100%.", "הזן מהירות סיבוב בין 1% ל־300%."),
            ("zh-Hans", "解锁最高300%的旋转速度", "允许速度超过100%。关闭此选项会将当前速度限制为100%。", "输入1%到300%的旋转速度。")
        ];
        foreach (var row in rows)
        {
            var text = SupplementalTexts[row.Code];
            text["Neck.UnlockTransitionSpeed"] = row.Unlock;
            text["Neck.Tooltip.UnlockTransitionSpeed"] = row.UnlockHelp;
            text["Neck.Tooltip.TransitionSpeedInput"] = row.InputHelp;
        }
    }
}
