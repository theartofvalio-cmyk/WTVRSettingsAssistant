namespace WTVRSettingsAssistant;

// Sample the transit through neutral before normal switch debouncing can hide it.
// Only a settled final endpoint commits a gesture; neutral has a short grace period.
internal sealed class FlapGestureDetector
{
    public const int GestureMilliseconds = 350;
    private AdvancedFlapMode? _position, _origin, _destination;
    private DateTime _changed, _centreAt, _last;
    public AdvancedFlapMode Mode { get; private set; } = AdvancedFlapMode.Raised;
    public void Reset()
    { _position = _origin = _destination = null; Mode = AdvancedFlapMode.Raised; _last = default; }
    public AdvancedFlapMode Observe(AdvancedFlapMode position, DateTime now, int debounceMs)
    {
        if (_last != default && (now < _last || now - _last > TimeSpan.FromMilliseconds(500))) Reset();
        var sampleGap = now - _last;
        _last = now;
        if (_position != position)
        {
            var previous = _position;
            _position = position; _changed = now; _destination = null;
            if (position == AdvancedFlapMode.Raised)
            {
                _origin = previous; _centreAt = now;
            }
            else if (previous == AdvancedFlapMode.Raised && _origin is { } origin && origin != position &&
                now - _centreAt <= TimeSpan.FromMilliseconds(GestureMilliseconds))
                _destination = position;
            // A fast physical sweep can cross neutral entirely between timer ticks.
            // Accept opposite endpoints only across a short, uninterrupted sample gap.
            else if (previous is { } endpoint && endpoint != AdvancedFlapMode.Raised &&
                endpoint != position && sampleGap <= TimeSpan.FromMilliseconds(100))
                _destination = position;
            else
            {
                _origin = null;
                // Ordinary endpoint changes do not select a deployment mode.
                Mode = AdvancedFlapMode.Raised;
            }
        }
        if (position == AdvancedFlapMode.Raised && now - _changed >= TimeSpan.FromMilliseconds(GestureMilliseconds))
        { Mode = AdvancedFlapMode.Raised; _origin = null; }
        if (_destination is { } destination && now - _changed >= TimeSpan.FromMilliseconds(debounceMs))
        { Mode = destination; _destination = null; _origin = null; }
        return Mode;
    }
}
