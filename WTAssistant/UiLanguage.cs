using System.Runtime.CompilerServices;

namespace WTVRSettingsAssistant;

internal static class UiLanguage
{
    private sealed record LocalizedText(string Key);
    private sealed record LocalizedColumnHeader(string Key);
    private static readonly ConditionalWeakTable<Control, LocalizedText> ControlKeys = new();
    private static readonly ConditionalWeakTable<DataGridViewColumn, LocalizedColumnHeader> ColumnKeys = new();
    private sealed record LocalizedToolTip(WeakReference<ToolTip> Provider, string Key);
    private static readonly ConditionalWeakTable<Control, List<LocalizedToolTip>> ToolTipKeys = new();

    public static void BindToolTip(ToolTip provider, Control control, string key, string languageCode)
    {
        var entries = ToolTipKeys.GetOrCreateValue(control);
        entries.RemoveAll(entry => !entry.Provider.TryGetTarget(out var target) || ReferenceEquals(target, provider));
        entries.Add(new(new WeakReference<ToolTip>(provider), key));
        provider.SetToolTip(control, AppText.T(languageCode, key));
    }

    public static void Bind(Control control, string key, string languageCode)
    {
        ControlKeys.Remove(control);
        ControlKeys.Add(control, new LocalizedText(key));
        control.Text = AppText.T(languageCode, key);
    }

    public static void Apply(Control root, string languageCode, IReadOnlyDictionary<string, string> exactTextToKey)
    {
        foreach (Control control in All(root))
        {
            if (ToolTipKeys.TryGetValue(control, out var tooltips))
                foreach (var tooltip in tooltips)
                    if (tooltip.Provider.TryGetTarget(out var provider))
                        provider.SetToolTip(control, AppText.T(languageCode, tooltip.Key));
            // Input contents and selected values belong to the user, not the
            // translation catalog. Tag is also application-owned metadata.
            if (control is not TextBoxBase && control is not ComboBox && control is not ListControl &&
                !ControlKeys.TryGetValue(control, out _))
            {
                string text = control.Text ?? "";
                if (exactTextToKey.TryGetValue(text, out string? key))
                    ControlKeys.Add(control, new LocalizedText(key));
            }
            if (ControlKeys.TryGetValue(control, out LocalizedText? localized))
                control.Text = AppText.T(languageCode, localized.Key);
            if (control is DataGridView grid)
            {
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!ColumnKeys.TryGetValue(column, out _))
                    {
                        string headerText = column.HeaderText ?? "";
                        if (exactTextToKey.TryGetValue(headerText, out string? key))
                            ColumnKeys.Add(column, new LocalizedColumnHeader(key));
                    }
                    if (ColumnKeys.TryGetValue(column, out LocalizedColumnHeader? localizedColumn))
                        column.HeaderText = AppText.T(languageCode, localizedColumn.Key);
                }
            }
        }
    }

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
            foreach (Control nested in All(child))
                yield return nested;
    }
}
