using System.Reflection;
using System.Runtime.CompilerServices;

namespace WTVRSettingsAssistant;

// Buffer managed container surfaces locally. Do not composite the whole native
// window: it also contains WebView2 and live controller preview child windows.
internal static class BufferedUi
{
    private static readonly ConditionalWeakTable<Control, object> Attached = new();
    private static readonly PropertyInfo? BufferProperty = typeof(Control).GetProperty(
        "DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Attach(Control control)
    {
        if (Attached.TryGetValue(control, out _)) return;
        Attached.Add(control, new object());
        if (control is Panel or Form or UserControl)
            BufferProperty?.SetValue(control, true);
        control.ControlAdded += (_, e) => { if (e.Control is not null) Attach(e.Control); };
        foreach (Control child in control.Controls) Attach(child);
    }
}
