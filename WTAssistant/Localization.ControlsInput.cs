namespace WTVRSettingsAssistant;

internal static partial class AppText
{
    private static void AddControlsInputTranslations()
    {
        (string Code, string Repeat, string Interval, string Help, string Required)[] rows =
        [
            ("en", "Repeat while held", "Press interval (ms, 0–5000)", "Repeat the sequence while the selected position is held. The interval is between press starts: 100 ms ≈ 10 presses/s. Values below 40 ms use 40 ms so each key can release. The game may register fewer presses.", "Repeat while held requires at least one physical button condition set to ON."),
            ("bg", "Повтаряй при задържане", "Интервал (ms, 0–5000)", "Повтаря последователността, докато позицията е задържана. Интервалът е между началата на натисканията: 100 ms ≈ 10/сек. Стойности под 40 ms използват 40 ms за отпускане на клавиша. Играта може да отчете по-малко натискания.", "Повторението изисква поне един физически бутон с условие ON."),
            ("es", "Repetir mientras se mantiene", "Intervalo (ms, 0–5000)", "Repite la secuencia mientras se mantiene la posición. Intervalo entre inicios: 100 ms ≈ 10 pulsaciones/s. Los valores inferiores a 40 ms usan 40 ms para soltar la tecla. El juego puede registrar menos pulsaciones.", "La repetición requiere al menos un botón físico con condición ON."),
            ("de", "Beim Halten wiederholen", "Tastenintervall (ms, 0–5000)", "Wiederholt die Sequenz beim Halten der Position. Abstand zwischen Tastendrücken: 100 ms ≈ 10/s. Werte unter 40 ms verwenden 40 ms zum Loslassen. Das Spiel kann weniger Eingaben erkennen.", "Die Wiederholung erfordert mindestens eine physische Taste mit Bedingung ON."),
            ("fr", "Répéter pendant le maintien", "Intervalle (ms, 0–5000)", "Répète la séquence pendant le maintien de la position. Intervalle entre débuts : 100 ms ≈ 10 pressions/s. Les valeurs sous 40 ms utilisent 40 ms pour relâcher la touche. Le jeu peut détecter moins de pressions.", "La répétition exige au moins un bouton physique avec la condition ON."),
            ("pt", "Repetir enquanto pressionado", "Intervalo (ms, 0–5000)", "Repete a sequência enquanto a posição é mantida. Intervalo entre inícios: 100 ms ≈ 10 pressões/s. Valores abaixo de 40 ms usam 40 ms para soltar a tecla. O jogo pode registar menos pressões.", "A repetição exige pelo menos um botão físico com condição ON."),
            ("pl", "Powtarzaj podczas trzymania", "Odstęp (ms, 0–5000)", "Powtarza sekwencję podczas trzymania pozycji. Odstęp między początkami naciśnięć: 100 ms ≈ 10/s. Wartości poniżej 40 ms używają 40 ms, aby zwolnić klawisz. Gra może wykryć mniej naciśnięć.", "Powtarzanie wymaga co najmniej jednego fizycznego przycisku z warunkiem ON."),
            ("ru", "Повторять при удержании", "Интервал (мс, 0–5000)", "Повторяет последовательность при удержании позиции. Интервал между началами нажатий: 100 мс ≈ 10/с. Значения менее 40 мс используют 40 мс для отпускания клавиши. Игра может распознать меньше нажатий.", "Для повторения нужен хотя бы один физический кнопочный вход с условием ON."),
            ("uk", "Повторювати при утриманні", "Інтервал (мс, 0–5000)", "Повторює послідовність при утриманні позиції. Інтервал між початками натискань: 100 мс ≈ 10/с. Значення менше 40 мс використовують 40 мс для відпускання клавіші. Гра може розпізнати менше натискань.", "Повторення потребує хоча б однієї фізичної кнопки з умовою ON."),
            ("tr", "Basılı tutulurken tekrarla", "Basış aralığı (ms, 0–5000)", "Konum tutulurken sırayı tekrarlar. Basış başlangıçları arası: 100 ms ≈ 10 basış/sn. Tuşun bırakılması için 40 ms altı değerler 40 ms kullanır. Oyun daha az basış algılayabilir.", "Tekrarlama için en az bir fiziksel düğmenin koşulu ON olmalıdır."),
            ("el", "Επανάληψη όσο κρατιέται", "Διάστημα (ms, 0–5000)", "Επαναλαμβάνει την ακολουθία όσο κρατιέται η θέση. Διάστημα μεταξύ ενάρξεων: 100 ms ≈ 10 πατήματα/δ. Τιμές κάτω από 40 ms χρησιμοποιούν 40 ms για απελευθέρωση. Το παιχνίδι μπορεί να καταγράψει λιγότερα πατήματα.", "Η επανάληψη απαιτεί τουλάχιστον ένα φυσικό κουμπί με συνθήκη ON."),
            ("ro", "Repetă cât timp este ținut", "Interval (ms, 0–5000)", "Repetă secvența cât timp poziția este ținută. Interval între începuturi: 100 ms ≈ 10 apăsări/s. Valorile sub 40 ms folosesc 40 ms pentru eliberare. Jocul poate înregistra mai puține apăsări.", "Repetarea necesită cel puțin un buton fizic cu condiția ON."),
            ("he", "חזרה בזמן החזקה", "מרווח לחיצות (ms, 0–5000)", "חזרה על הרצף בזמן החזקת המצב. המרווח נמדד בין התחלות לחיצה: 100 ms ≈ 10 לשנייה. ערכים מתחת ל־40 ms משתמשים ב־40 ms לשחרור המקש. המשחק עשוי לקלוט פחות לחיצות.", "חזרה מחייבת לפחות כפתור פיזי אחד עם תנאי ON."),
            ("zh-Hans", "按住时重复", "按键间隔（ms，0–5000）", "保持所选位置时重复序列。间隔从每次按下开始计算：100 ms ≈ 每秒10次。低于40 ms的值使用40 ms，以便松开按键。游戏实际识别次数可能更少。", "按住重复要求至少一个实体按钮条件设为ON。")
        ];
        foreach (var row in rows)
        {
            var text = SupplementalTexts[row.Code];
            text["Switch.RepeatWhileHeld"] = row.Repeat;
            text["Switch.SequenceDelay"] = row.Interval;
            text["Switch.SequenceTimingHelp"] = row.Help;
            text["Switch.RepeatNeedsHeldButton"] = row.Required;
        }
    }
}
