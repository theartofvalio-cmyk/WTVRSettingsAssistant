using System.Runtime.CompilerServices;

namespace WTVRSettingsAssistant;

// Control.Font can be inherited, including the process-wide DefaultFont.
// Never dispose it merely because a resize assigns a replacement.
internal static class ResponsiveFonts
{
    private sealed class Owner
    {
        public Font? Font;
    }
    private static readonly ConditionalWeakTable<Control, Owner> Owners = new();

    public static void Set(Control control, float size, FontStyle style, GraphicsUnit unit)
    {
        if (control.IsDisposed || control.Disposing) return;
        size = MathF.Round(size * 2f) / 2f;
        Font current = control.Font;
        if (Math.Abs(current.Size-size) < .05f && current.Style == style && current.Unit == unit) return;
        Owner owner = Owners.GetValue(control, c =>
        {
            var value = new Owner();
            c.Disposed += (_, _) => { value.Font?.Dispose(); value.Font = null; };
            return value;
        });
        Font replacement = new("Segoe UI", Math.Max(1, size), style, unit);
        Font? previousOwned = owner.Font;
        control.Font = replacement;
        owner.Font = replacement;
        previousOwned?.Dispose();
    }
}
