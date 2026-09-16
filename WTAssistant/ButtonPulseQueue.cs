namespace WTVRSettingsAssistant;

// Serialize pulses per button with a real key-up gap. A fast ON -> OFF must
// produce two presses, not extend the first press until both transitions end.
internal sealed class ButtonPulseQueue
{
    private readonly Action<int, bool> _send;
    private readonly Func<int, Task> _delay;
    private readonly Dictionary<int, Queue<int>> _queues = new();
    private int _generation;
    public event Action<Exception>? Failed;
    public ButtonPulseQueue(Action<int, bool> send, Func<int, Task>? delay = null)
    { _send = send; _delay = delay ?? Task.Delay; }
    public void Pulse(int button, int duration)
    {
        if (_queues.TryGetValue(button, out var queue)) { queue.Enqueue(duration); return; }
        queue = new(); queue.Enqueue(duration); _queues[button] = queue;
        try { _send(button, true); }
        catch { _queues.Remove(button); throw; }
        _ = Run(button, queue, _generation);
    }
    private async Task Run(int button, Queue<int> queue, int generation)
    {
        try
        {
            while (queue.Count > 0)
            {
                await _delay(Math.Clamp(queue.Dequeue(), 20, 500));
                if (generation != _generation) return;
                _send(button, false);
                await _delay(40);
                if (generation != _generation) return;
                if (queue.Count > 0) _send(button, true);
            }
            _queues.Remove(button);
        }
        catch (Exception ex)
        {
            if (generation != _generation) return;
            _queues.Remove(button);
            try { _send(button, false); } catch { }
            Failed?.Invoke(ex);
        }
    }
    public void Cancel()
    {
        ++_generation;
        int[] buttons = _queues.Keys.ToArray(); _queues.Clear();
        foreach (int button in buttons) { try { _send(button, false); } catch { } }
    }
}
