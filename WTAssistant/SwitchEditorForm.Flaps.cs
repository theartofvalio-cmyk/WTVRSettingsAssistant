using System.Text.Json;
namespace WTVRSettingsAssistant;
internal sealed partial class SwitchEditorForm
{
    private readonly CheckBox _flapsToggle = new() { Visible = false };
    private readonly Button _flapsSetup = AdvancedSwitchManagerForm.B("ACTION SEQUENCE");
    private readonly Button _recordAdvanced = AdvancedSwitchManagerForm.B("RECORD ADVANCED");
    private AdvancedFlapsSettings _flapsDraft = new();
    private void InitializeFlapsEditor(Label guide)
    {
        _flapsDraft.Enabled = false;
        guide.Text = "";
        Controls.AddRange([_flapsSetup, _recordAdvanced]);
        _flapsSetup.Click += (_, _) => EditSequence();
        _recordAdvanced.Click += (_, _) => RecordAdvanced();
        _states.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _states.Columns[e.ColumnIndex].Name != "Conditions" ||
                _states.Rows[e.RowIndex].Tag is not SwitchStateDefinition state || state.Gesture.Count == 0) return;
            e.Value = FormatGesture(state);
            e.FormattingApplied = true;
        };
        _states.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _states.Columns[e.ColumnIndex].Name == "Summary") EditSequence(); };
    }
    private bool ValidateFlaps(List<SwitchStateDefinition> states) => true;
    private static string FormatGesture(SwitchStateDefinition state)
    {
        uint varying = 0;
        foreach (var step in state.Gesture) varying |= step.Mask ^ state.Gesture[0].Mask;
        return string.Join(Environment.NewLine, state.Gesture.Select((step, index) =>
            $"{index + 1}. {step.ElapsedMs} ms: " + FormatConditions(Enumerable.Range(1, 32)
                .Where(b => (varying & (1u << (b - 1))) != 0)
                .Select(b => new InputCondition { ButtonId = b, RequiredState = (step.Mask & (1u << (b - 1))) != 0 }))));
    }
    private void LearnFlapPositions(List<DataGridViewRow> rows) { }
    protected override void Dispose(bool disposing)
    { if (disposing) _liveTimer.Dispose(); base.Dispose(disposing); }

    private void EditSequence()
    {
        if (_states.CurrentRow is not { } row) return;
        var draft = row.Tag is SwitchStateDefinition saved
            ? JsonSerializer.Deserialize<SwitchStateDefinition>(JsonSerializer.Serialize(saved))!
            : new SwitchStateDefinition();
        using var dialog = new SwitchSequenceForm(draft, _languageCode);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        row.Tag = draft;
        row.Cells["Kind"].Value = "KEY";
        row.Cells["Action"].Value = draft.Sequence[0].Key;
        draft.OutputKind = SwitchActionKind.KeyboardKey;
        draft.OutputKey = draft.Sequence[0].Key;
        draft.Behavior = SwitchOutputBehavior.Pulse;
        UpdateRowSummaries();
    }
    private void RecordAdvanced()
    {
        if (_device.SelectedIndex < 0) return;
        using var recorder = new SwitchGestureRecorder(_devices[_device.SelectedIndex]);
        if (recorder.ShowDialog(this) != DialogResult.OK) return;
        var gesture = recorder.Result;
        int firstMovement = gesture[1].ElapsedMs;
        gesture[0].ElapsedMs = 0;
        for (int i = 1; i < gesture.Count; i++) gesture[i].ElapsedMs -= firstMovement;
        uint varying = 0;
        foreach (var step in gesture) varying |= step.Mask ^ gesture[0].Mask;
        if (varying == 0) return;
        foreach (var step in gesture) step.Mask &= varying;
        var conditions = Enumerable.Range(1, 32).Where(b => (varying & (1u << (b - 1))) != 0)
            .Select(b => new InputCondition { ButtonId = b, RequiredState = (gesture[^1].Mask & (1u << (b - 1))) != 0 }).ToList();
        var state = new SwitchStateDefinition
        {
            Name = "Gesture " + (_states.Rows.Count + 1), Conditions = conditions,
            Gesture = gesture, OutputKind = SwitchActionKind.KeyboardKey
        };
        AddPositionRow(state.Name, FormatConditions(conditions), SwitchActionKind.KeyboardKey, "");
        var row = _states.Rows[_states.Rows.Count - 1]; row.Tag = state;
        _states.CurrentCell = row.Cells["State"];
        UpdateRowSummaries();
        EditSequence();
    }
}
