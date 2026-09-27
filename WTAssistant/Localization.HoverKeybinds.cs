namespace WTVRSettingsAssistant;

internal static partial class AppText
{
    private static void AddHoverKeybindTranslations()
    {
        SupplementalTexts["en"] = Merge(SupplementalTexts["en"], new Dictionary<string, string>
        {
            ["Keybind.Description"] = "Choose a keyboard key, mouse button, HOTAS button, or combination for each action below. The first three actions send War Thunder's hidden keyboard commands."
        });
        SupplementalTexts["bg"] = Merge(SupplementalTexts["bg"], new Dictionary<string, string>
        {
            ["Keybind.Description"] = "Избери клавиш, бутон на мишката, HOTAS бутон или комбинация за всяко действие по-долу. Първите три действия изпращат скрити клавиатурни команди на War Thunder.",
            ["Keybind.HoverUp"] = "Hover нагоре",
            ["Keybind.HoverDown"] = "Hover надолу",
            ["Keybind.ScoreBoardMouseFix"] = "Поправка на мишката в таблото",
            ["Keybind.LeftShift"] = "Ляв Shift",
            ["Keybind.LeftCtrl"] = "Ляв Ctrl",
            ["Keybind.MouseTopLeft"] = "Мишка горе вляво",
            ["Keybind.ScoreBoardHint"] = "Задай същия бутон, който използваш за таблото с резултати в War Thunder. При натискане курсорът веднага се премества горе вляво на този екран."
        });
    }

    private static Dictionary<string, string> Merge(Dictionary<string, string> target, Dictionary<string, string> additions)
    {
        foreach ((string key, string value) in additions) target[key] = value;
        return target;
    }
}
