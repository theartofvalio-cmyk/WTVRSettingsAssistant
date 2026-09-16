using System.Globalization;
using System.Text.Json;

namespace WTVRSettingsAssistant;

internal sealed class AdvancedFlapsSettingsForm : Form
{
    private readonly string _language;
    private readonly IFlapTelemetry _telemetry;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };
    private readonly FlapStageDetector _detector = new(600);
    private readonly Label _live = new() { Dock = DockStyle.Fill, AutoSize = true };
    private string? _detectedAircraft;
    public AdvancedFlapsSettings Result { get; }
    private string T(string key) => AppText.T(_language, "Flaps." + key);

    internal AdvancedFlapsSettingsForm(AdvancedFlapsSettings settings, string[] positions, string language, IFlapTelemetry? telemetry = null)
    {
        _language = language;
        Result = JsonSerializer.Deserialize<AdvancedFlapsSettings>(JsonSerializer.Serialize(settings))!;
        _telemetry = telemetry ?? new AdvancedFlapsTelemetry();
        Text = T("Setup"); ClientSize = new Size(920, 760); MinimumSize = new Size(820, 680);
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = IllustratedTheme.Background; ForeColor = IllustratedTheme.Ivory; Font = new Font("Segoe UI", 10);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var tabs = new TabControl { Dock = DockStyle.Fill };
        TableLayoutPanel Page(string key)
        {
            var page = new TabPage(T(key)) { BackColor = BackColor, ForeColor = ForeColor };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            page.AutoScroll = true; page.Controls.Add(table); tabs.TabPages.Add(page); return table;
        }
        void Row(TableLayoutPanel table, string label, Control value)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var caption = new Label { Text = T(label), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 12, 8) };
            value.Dock = DockStyle.Fill; value.Margin = new Padding(3, 7, 3, 7);
            table.Controls.Add(caption, 0, row); table.Controls.Add(value, 1, row);
        }
        void Note(TableLayoutPanel table, string key)
        {
            var label = new Label { Text = T(key), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 12) };
            int row = table.RowCount++; table.Controls.Add(label, 0, row); table.SetColumnSpan(label, 2);
        }
        var input = Page("Commands"); Note(input, "PositionGuide"); Note(input, "RemoveNativeBinding");
        // Display the mapped roles instead of generic ON/OFF/POSITION 3 names.
        // Keep row indices and saved physical conditions unchanged.
        string[] positionLabels = (string[])positions.Clone();
        int[] mappedPositions = [Result.UpPosition, Result.CentrePosition, Result.DownPosition];
        if (positions.Length == 3 && mappedPositions.Order().SequenceEqual(new[] { 0, 1, 2 }))
        {
            positionLabels[Result.UpPosition] = T("UpPosition");
            positionLabels[Result.CentrePosition] = T("CentrePosition");
            positionLabels[Result.DownPosition] = T("DownPosition");
        }
        ComboBox Position(string key, int selected)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            box.Items.AddRange(positionLabels.Cast<object>().ToArray()); box.SelectedIndex = Math.Clamp(selected, 0, positions.Length - 1);
            Row(input, key, box); return box;
        }
        var up = Position("UpPosition", Result.UpPosition);
        var centre = Position("CentrePosition", Result.CentrePosition);
        var down = Position("DownPosition", Result.DownPosition);
        Row(input, "CommandUp", OutputEditor(Result.FlapsUp)); Row(input, "CommandDown", OutputEditor(Result.FlapsDown));
        var stages = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        stages.Items.AddRange([T("LayoutFull"), T("LayoutNoCombat"), T("LayoutNoTakeoff"), T("LayoutLanding")]);
        stages.SelectedIndex = Result.HasCombatStage ? (Result.HasTakeoffStage ? 0 : 2) : (Result.HasTakeoffStage ? 1 : 3);
        Row(input, "StageLayout", stages);
        Note(input, "ThreePositions");
        var limits = Page("Response");
        NumericUpDown Number(string key, int value, int minimum, int maximum)
        {
            var box = new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum) };
            Row(limits, key, box); return box;
        }
        var cooldown = Number("Cooldown", Result.CommandCooldownMs, 150, 2000);
        
        
        Note(limits, "DecisionHelp");
        
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = AdvancedSwitchManagerForm.B(AppText.T(language, "Switch.Save")); save.AutoSize = true;
        var cancel = AdvancedSwitchManagerForm.B(AppText.T(language, "Common.Cancel")); cancel.AutoSize = true; cancel.DialogResult = DialogResult.Cancel;
        save.Click += (_, _) =>
        {
            Result.UpPosition = up.SelectedIndex; Result.CentrePosition = centre.SelectedIndex; Result.DownPosition = down.SelectedIndex;
            Result.CommandCooldownMs = (int)cooldown.Value;
            Result.HasCombatStage = stages.SelectedIndex is 0 or 2;
            Result.HasTakeoffStage = stages.SelectedIndex is 0 or 1;
            
            if (!Result.IsValid) { MessageBox.Show(this, T("InvalidSettings"), Text); return; }
            DialogResult = DialogResult.OK;
        };
        footer.Controls.Add(save); footer.Controls.Add(cancel); root.Controls.Add(tabs, 0, 0); root.Controls.Add(footer, 0, 1); Controls.Add(root);
        
    }

    private Control OutputEditor(SwitchStateDefinition output)
    {
        if (output.OutputKind != SwitchActionKind.KeyboardKey) output.OutputKey = "";
        output.OutputKind = SwitchActionKind.KeyboardKey;
        var key = AdvancedSwitchManagerForm.B(string.IsNullOrWhiteSpace(output.OutputKey) ? T("Bind") : output.OutputKey);
        key.AutoSize = true;
        key.MinimumSize = new Size(0, 38);
        key.Click += (_, _) =>
        {
            if (SwitchEditorForm.CaptureKey(this, T("Bind"), _language, out Keys captured))
            { output.OutputKey = captured.ToString(); key.Text = output.OutputKey; }
        };
        return key;
    }
    private void RefreshTelemetry()
    {
        var now = DateTime.UtcNow; _telemetry.Refresh(now);
        if (_telemetry.Sample is not { } sample || !sample.Fresh(now))
        { _detectedAircraft = null; _live.Text = T("AircraftWaiting"); return; }
        _detectedAircraft = sample.Aircraft;
        _live.Text = sample.Aircraft + " | " + sample.Percent.ToString("0.##", CultureInfo.CurrentCulture) + "%";
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Stop(); _timer.Dispose(); _telemetry.Dispose(); }
        base.Dispose(disposing);
    }
}
