namespace WTVRSettingsAssistant;

internal sealed class FlapKeySequence
{
    public const int SweepMilliseconds = 200;
    private readonly Queue<FlapCommand> _queue = new();
    private int? _raw, _settled, _origin;
    private bool _sweep;
    private DateTime _changed, _centreAt, _last, _sent;
    public int Pending => _queue.Count;
    public bool CombatUnavailable { get; private set; }
    public void Reset()
    {
        _queue.Clear(); _raw = _settled = _origin = null;
        _sweep = false; CombatUnavailable = false; _last = default;
    }

    // Wait for a settled destination before issuing any deployment keys.
    public FlapCommand? Step(int position, DateTime now, int debounceMs, int intervalMs,
        bool hasCombat = true, bool hasTakeoff = true)
    {
        if (_last != default && (now < _last || now - _last > TimeSpan.FromMilliseconds(500))) Reset();
        var gap = now - _last;
        _last = now;
        if (_raw is null)
        {
            _raw = _settled = position; _changed = now;
            if (position != 1) _origin = position;
            return null;
        }
        if (_raw != position)
        {
            _sweep = position != 1 && _origin is { } origin && origin != position &&
                (_raw == 1 ? now - _centreAt <= TimeSpan.FromMilliseconds(SweepMilliseconds) :
                    gap <= TimeSpan.FromMilliseconds(100));
            if (position == 1) _centreAt = now;
            else _origin = position;
            _raw = position; _changed = now;
            // A changed request cancels unsent keys from the previous selection.
            _queue.Clear();
            _settled = null;
        }
        if (_settled != position && now - _changed >= TimeSpan.FromMilliseconds(Math.Max(SweepMilliseconds, debounceMs)))
        {
            bool combat = position == 0 && _sweep;
            CombatUnavailable = combat && !hasCombat;
            for (int i = 0; i < 3; i++) _queue.Enqueue(FlapCommand.Up);
            int steps = position == 2 ? 1 + (hasCombat ? 1 : 0) + (hasTakeoff ? 1 : 0) :
                combat ? (hasCombat ? 1 : 0) :
                position == 0 && hasTakeoff ? 1 + (hasCombat ? 1 : 0) : 0;
            for (int i = 0; i < steps; i++) _queue.Enqueue(FlapCommand.Down);
            _settled = position;
            if (position == 1) _origin = null;
        }
        if (_queue.Count == 0 || now - _sent < TimeSpan.FromMilliseconds(intervalMs)) return null;
        _sent = now;
        return _queue.Dequeue();
    }
}
