namespace HOTASTrimUtility;

internal interface IFlightOutput : IDisposable
{
    string Name { get; }
    void Connect();
    void SendFlightAxes(double roll, double pitch, double rudder);
    void Release();
}

internal static class FlightOutputFactory
{
    public static string[] AvailableKeys => new[] { "vJoy" };

    public static IFlightOutput Create(string key) =>
        string.Equals(key, "vJoy", StringComparison.OrdinalIgnoreCase)
            ? new VJoyFlightOutput()
            : throw new InvalidOperationException("VTrim is configured to use vJoy Device 1 in this build.");
}

// One owner for vJoy Device 1 across the embedded VTrim axes and WT Assistant's
// Advanced Switch Bindings. vJoy does not need to be acquired twice by two
// independent wrapper instances inside the same application.
public static class SharedVJoyOutputCoordinator
{
    private const uint DeviceId = 1;
    private static readonly object Gate = new();
    private static VirtualJoystick? _joystick;
    private static int _axisClients;
    private static int _buttonClients;
    private static readonly HashSet<int> HeldButtons = new();

    public static bool IsConnected { get { lock (Gate) return _joystick is not null; } }

    public static void AcquireAxisClient()
    {
        lock (Gate)
        {
            EnsureConnected();
            _axisClients++;
        }
    }

    public static void ReleaseAxisClient()
    {
        lock (Gate)
        {
            if (_axisClients > 0) _axisClients--;
            if (_joystick is not null)
            {
                try
                {
                    _joystick.SetJoystickAxis(16384, Axis.HID_USAGE_X);
                    _joystick.SetJoystickAxis(16384, Axis.HID_USAGE_Y);
                    _joystick.SetJoystickAxis(16384, Axis.HID_USAGE_RZ);
                }
                catch { }
            }
            ReleaseDeviceIfIdle();
        }
    }

    public static void AcquireButtonClient()
    {
        lock (Gate)
        {
            EnsureConnected();
            _buttonClients++;
        }
    }

    public static void ReleaseButtonClient()
    {
        lock (Gate)
        {
            if (_buttonClients > 0) _buttonClients--;
            if (_buttonClients == 0) ReleaseAllButtonsCore();
            ReleaseDeviceIfIdle();
        }
    }

    public static void SendFlightAxes(double roll, double pitch, double rudder)
    {
        lock (Gate)
        {
            EnsureConnected();
            _joystick!.SetJoystickAxis(Map(roll), Axis.HID_USAGE_X);
            _joystick.SetJoystickAxis(Map(pitch), Axis.HID_USAGE_Y);
            _joystick.SetJoystickAxis(Map(rudder), Axis.HID_USAGE_RZ);
        }
    }

    public static void ButtonDown(int id)
    {
        if (id is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(id), "This vJoy backend supports bindable buttons 1-32.");
        lock (Gate)
        {
            EnsureConnected();
            _joystick!.SetJoystickButton(true, (uint)id);
            HeldButtons.Add(id);
        }
    }

    public static void ButtonUp(int id)
    {
        if (id is < 1 or > 32) return;
        lock (Gate)
        {
            if (_joystick is null) return;
            try { _joystick.SetJoystickButton(false, (uint)id); } catch { }
            HeldButtons.Remove(id);
        }
    }

    public static void ReleaseAllButtons()
    {
        lock (Gate) ReleaseAllButtonsCore();
    }

    private static void ReleaseAllButtonsCore()
    {
        if (_joystick is null) { HeldButtons.Clear(); return; }
        foreach (int id in HeldButtons.ToArray())
        {
            try { _joystick.SetJoystickButton(false, (uint)id); } catch { }
        }
        HeldButtons.Clear();
    }

    private static void EnsureConnected()
    {
        if (_joystick is not null) return;
        VirtualJoystick joystick = new(DeviceId);
        try { joystick.Aquire(); _joystick = joystick; }
        catch { joystick.Dispose(); throw; }
    }

    private static void ReleaseDeviceIfIdle()
    {
        if (_axisClients > 0 || _buttonClients > 0 || _joystick is null) return;
        ReleaseAllButtonsCore();
        VirtualJoystick joystick = _joystick;
        _joystick = null;
        try { joystick.Release(); } catch { }
        joystick.Dispose();
    }

    private static int Map(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        value = Math.Clamp(value, -1.0, 1.0);
        return value >= 0
            ? 16384 + (int)Math.Round(value * 16384.0)
            : 16384 + (int)Math.Round(value * 16383.0);
    }
}

internal sealed class VJoyFlightOutput : IFlightOutput
{
    private bool _leased;
    public string Name => "vJoy Device 1";

    public void Connect()
    {
        Release();
        SharedVJoyOutputCoordinator.AcquireAxisClient();
        _leased = true;
        SendFlightAxes(0, 0, 0);
    }

    public void SendFlightAxes(double roll, double pitch, double rudder)
    {
        if (!_leased) throw new IOException("vJoy Device 1 is disconnected.");
        SharedVJoyOutputCoordinator.SendFlightAxes(roll, pitch, rudder);
    }

    public void Release()
    {
        if (!_leased) return;
        _leased = false;
        SharedVJoyOutputCoordinator.ReleaseAxisClient();
    }

    public void Dispose() => Release();
}
