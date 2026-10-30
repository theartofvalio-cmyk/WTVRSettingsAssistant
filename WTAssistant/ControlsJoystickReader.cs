using Vortice.DirectInput;

namespace WTVRSettingsAssistant;

// Controls Assist needs the extended DirectInput state; WinMM exposes only B1-B32.
// Instance GUIDs keep saved bindings attached to the same device across USB reordering.
internal static class ControlsJoystickReader
{
    private sealed record Entry(int Id, Guid Guid, string Name, IDirectInputDevice8 Device);
    private readonly record struct DeviceKey(int Id, string Name, Guid Guid);
    private sealed record CachedState(PhysicalJoystick? Device, long ReadAt);
    private const int SnapshotGraceMilliseconds = 250;
    private static readonly Dictionary<Guid, Entry> Devices = new();
    private static readonly object DeviceGate = new();
    private static readonly object CacheGate = new();
    private static readonly Dictionary<DeviceKey, CachedState> CachedStates = new();
    private static readonly HashSet<DeviceKey> PendingReads = new();
    private static IDirectInput8? _input;
    private static IntPtr _owner;
    private static volatile bool _closed;
    private static long _refresh;
    private static int _nextId = 2000;

    static ControlsJoystickReader() => Application.ApplicationExit += (_, _) =>
    {
        _closed = true;
        lock (DeviceGate)
        {
            foreach (Entry entry in Devices.Values) entry.Device.Dispose();
            Devices.Clear(); _input?.Dispose(); _input = null;
        }
    };

    // Advanced switches poll continuously. Return the most recent button mask
    // immediately and do DirectInput/WinMM work away from the UI message pump.
    public static PhysicalJoystick? ReadBackground(int id, string name, Guid instanceGuid = default)
    {
        if (_closed) return null;
        if (_owner == IntPtr.Zero)
            _owner = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.IsHandleCreated)?.Handle ?? IntPtr.Zero;
        var key = new DeviceKey(id, name, instanceGuid);
        long now = Environment.TickCount64;
        lock (CacheGate)
        {
            if (!PendingReads.Contains(key) &&
                (!CachedStates.TryGetValue(key, out CachedState? recent) || now - recent.ReadAt >= 20))
            {
                PendingReads.Add(key);
                _ = Task.Run(() =>
                {
                    PhysicalJoystick? current = null;
                    try { current = Read(id, name, instanceGuid); }
                    catch { /* A driver fault is an unavailable button state. */ }
                    finally
                    {
                        lock (CacheGate)
                        {
                            CachedStates[key] = new CachedState(current, Environment.TickCount64);
                            PendingReads.Remove(key);
                        }
                    }
                });
            }
            return CachedStates.TryGetValue(key, out CachedState? snapshot) &&
                now - snapshot.ReadAt <= SnapshotGraceMilliseconds ? snapshot.Device : null;
        }
    }

    private static void Refresh()
    {
        long now = Environment.TickCount64;
        if (_input is not null && now < _refresh) return;
        _refresh = now + 1500;
        IntPtr owner = _owner;
        if (owner == IntPtr.Zero)
            owner = _owner = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.IsHandleCreated)?.Handle ?? IntPtr.Zero;
        if (owner == IntPtr.Zero) return;
        try
        {
            _input ??= DInput.DirectInput8Create();
            var instances = _input.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
                .Where(d => !IsVirtual(d.ProductName) && !IsVirtual(d.InstanceName))
                .Where(d => !XInputController.AnyConnected || !IsXInput(d.ProductName)).ToList();
            foreach (Guid guid in Devices.Keys.Except(instances.Select(d => d.InstanceGuid)).ToArray())
            { Devices[guid].Device.Dispose(); Devices.Remove(guid); }
            foreach (var instance in instances)
            {
                if (Devices.ContainsKey(instance.InstanceGuid)) continue;
                var device = _input.CreateDevice(instance.InstanceGuid);
                try
                {
                    device.SetCooperativeLevel(owner, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                    if (device.SetDataFormat<RawJoystickState>().Failure) { device.Dispose(); continue; }
                    device.Acquire();
                    Devices.Add(instance.InstanceGuid, new(_nextId++, instance.InstanceGuid, instance.ProductName, device));
                }
                catch { device.Dispose(); }
            }
        }
        catch { /* Keep existing devices usable during a transient enumeration failure. */ }
    }

    public static List<PhysicalJoystick> Enumerate()
    {
        lock (DeviceGate)
        {
            if (_closed) return new List<PhysicalJoystick>();
            Refresh();
            var result = Devices.Values.Select(Read).OfType<PhysicalJoystick>().ToList();
            // Keep Xbox's established numbering and legacy devices when DirectInput is unavailable.
            foreach (PhysicalJoystick legacy in WinMmJoystickReader.Enumerate())
                if (legacy.Id >= 1000 || !result.Any(d => d.Name.Equals(legacy.Name, StringComparison.OrdinalIgnoreCase)))
                    result.Add(legacy);
            return result;
        }
    }

    public static PhysicalJoystick? Read(int id, string name, Guid instanceGuid = default)
    {
        lock (DeviceGate)
        {
            if (_closed) return null;
            Refresh();
            if (instanceGuid != Guid.Empty)
                return Devices.TryGetValue(instanceGuid, out Entry? exact) ? Read(exact) : null;
            Entry? entry = Devices.Values.FirstOrDefault(e => e.Id == id && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var named = Devices.Values.Where(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            // An old WinMM binding has no GUID. Only migrate by name when unambiguous.
            if (entry is null && named.Length == 1) entry = named[0];
            if (entry is not null) return Read(entry);
            if (id >= 2000) return null;
            PhysicalJoystick? legacy = WinMmJoystickReader.Read(id, name);
            if (legacy?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) == true) return legacy;
            return WinMmJoystickReader.Enumerate().FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static PhysicalJoystick? Read(Entry entry)
    {
        try
        {
            if (entry.Device.Poll().Failure && (entry.Device.Acquire().Failure || entry.Device.Poll().Failure)) return null;
            var state = entry.Device.GetCurrentJoystickState();
            return new(entry.Id, entry.Name, ToMask(state.Buttons), true, entry.Guid);
        }
        catch { return null; } // Never preserve a stale held button for repeating actions.
    }

    internal static UInt128 ToMask(bool[] buttons)
    {
        UInt128 mask = 0;
        for (int i = 0; i < Math.Min(buttons.Length, 128); i++)
            if (buttons[i]) mask |= (UInt128)1 << i;
        return mask;
    }
    private static bool IsVirtual(string? name) => name?.Contains("vJoy", StringComparison.OrdinalIgnoreCase) == true;
    private static bool IsXInput(string? name) => name is not null &&
        (name.Contains("xbox", StringComparison.OrdinalIgnoreCase) || name.Contains("xinput", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("gamepad for windows", StringComparison.OrdinalIgnoreCase));
}
