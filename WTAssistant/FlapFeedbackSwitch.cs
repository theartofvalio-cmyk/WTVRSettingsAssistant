namespace WTVRSettingsAssistant;

internal sealed class FlapFeedbackSwitch
{
    private readonly AdvancedFlapsController _controller = new();
    private int? _position;
    private DateTime _changed;
    public string Status => _controller.StatusKey;
    public void Reset() { _position = null; _controller.Reset(); }
    public void FailOutput() => _controller.FailOutput();

    public FlapCommand? Step(int position, DateTime now, FlapSample? telemetry, AdvancedFlapsSettings settings)
    {
        if (_position != position) { _position = position; _changed = now; }
        // Suppress intermediate positions during a quick down-centre-up sweep.
        if (now - _changed < TimeSpan.FromMilliseconds(200)) return null;
        var mode = position == 1 ? AdvancedFlapMode.Raised :
            position == 0 ? AdvancedFlapMode.Combat : AdvancedFlapMode.AutoTakeoffLanding;
        return _controller.Step(now, mode, telemetry, settings);
    }
}
