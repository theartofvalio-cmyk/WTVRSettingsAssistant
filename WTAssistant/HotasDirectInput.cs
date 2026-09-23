using Vortice.DirectInput;

namespace WTVRSettingsAssistant;

internal static class HotasDirectInput
{
    // DirectInput can occasionally miss a single background poll while a device
    // is being reacquired. Neck Assistant's Simple Hold must not interpret that
    // transient read failure as the user physically releasing the HOTAS button.
    private const int AggregateSnapshotGraceMilliseconds = 1000;
    private const int DeviceFailureGraceMilliseconds = 750;

    private sealed record DevicePressedSnapshot(HashSet<string> Pressed, long UpdatedAt);

    private static readonly Dictionary<Guid, IDirectInputDevice8> Devices = new();
    private static readonly Dictionary<Guid, DevicePressedSnapshot> DeviceSnapshots = new();
    private static IDirectInput8? _input;
    private static long _refresh;
    private static readonly Dictionary<Guid, string> Names = new();
    private static readonly object Sync = new();
    private static HashSet<string> _snapshot = new(StringComparer.OrdinalIgnoreCase);
    private static long _snapshotAt;
    private static int _reading;
    private static IntPtr _owner;
    private static bool _closed;

    internal static string DisplayBinding(string binding)
    {
        return string.Join(" + ", binding.Split(" + ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token =>
            {
                if (token.StartsWith("JOY", StringComparison.OrdinalIgnoreCase))
                    return HotasBindingDialog.DisplayLegacyJoystick(token);
                if (token.StartsWith("XI:", StringComparison.OrdinalIgnoreCase))
                    return XInputController.DisplayToken(token);
                if (!token.StartsWith("DI:", StringComparison.OrdinalIgnoreCase)) return token;
                string[] parts = token.Split(':');
                if (parts.Length < 3 || !Guid.TryParse(parts[1], out _)) return "Controller input";
                string input = parts[2];
                if (input.StartsWith("POV", StringComparison.OrdinalIgnoreCase))
                    input = "Hat " + input[3..] + (parts.Length > 3 ? " " + parts[3] : "");
                else if (input.StartsWith("B", StringComparison.OrdinalIgnoreCase)) input = "Button " + input[1..];
                return input;
            }));
    }

    static HotasDirectInput()
    {
        Application.ApplicationExit += (_, _) =>
        {
            lock (Sync) _closed = true;
        };
    }

    internal static HashSet<string> ReadPressed()
    {
        if (_owner == IntPtr.Zero)
            _owner = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.IsHandleCreated)?.Handle ?? IntPtr.Zero;
        if (_owner != IntPtr.Zero && !_closed && Interlocked.CompareExchange(ref _reading, 1, 0) == 0)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var snapshot = ReadDevices();
                    lock (Sync) { _snapshot = snapshot; _snapshotAt = Environment.TickCount64; }
                }
                finally
                {
                    if (_closed)
                    {
                        foreach (var device in Devices.Values) device.Dispose();
                        Devices.Clear(); DeviceSnapshots.Clear(); _input?.Dispose(); _input = null;
                    }
                    Volatile.Write(ref _reading, 0);
                }
            });
        }

        // Keep the last *successful* aggregate state briefly if the background
        // DirectInput worker is delayed. A genuine release is still reflected
        // immediately because a successful empty device snapshot replaces it.
        lock (Sync)
            return Environment.TickCount64 - _snapshotAt <= AggregateSnapshotGraceMilliseconds
                ? new HashSet<string>(_snapshot, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ReadDevices()
    {
        var pressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long now = Environment.TickCount64;
        try
        {
            _input ??= DInput.DirectInput8Create();
            if (now >= _refresh)
            {
                _refresh = now + 3000;
                var instances = _input.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
                    .Where(d => !IsVirtualName(d.ProductName) && !IsVirtualName(d.InstanceName))
                    .Where(d => !XInputController.AnyConnected || !LooksLikeXInputName(d.ProductName))
                    .ToList();
                foreach (var id in Devices.Keys.Except(instances.Select(d => d.InstanceGuid)).ToArray())
                {
                    Devices[id].Dispose();
                    Devices.Remove(id);
                    DeviceSnapshots.Remove(id);
                }
                foreach (var instance in instances)
                {
                    lock (Sync) Names[instance.InstanceGuid] = instance.ProductName;
                    if (Devices.ContainsKey(instance.InstanceGuid)) continue;
                    var device = _input.CreateDevice(instance.InstanceGuid);
                    try
                    {
                        device.SetCooperativeLevel(_owner, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                        if (device.SetDataFormat<RawJoystickState>().Failure) { device.Dispose(); continue; }
                        device.Acquire();
                        Devices.Add(instance.InstanceGuid, device);
                    }
                    catch { device.Dispose(); }
                }
            }

            foreach (var (id, device) in Devices)
            {
                try
                {
                    if (device.Poll().Failure && (device.Acquire().Failure || device.Poll().Failure))
                    {
                        AddRecentDeviceSnapshot(id, pressed, now);
                        continue;
                    }

                    var state = device.GetCurrentJoystickState();
                    var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var buttons = state.Buttons;
                    for (int i = 0; i < buttons.Length; i++)
                        if (buttons[i]) current.Add($"DI:{id:D}:B{i + 1}");
                    for (int i = 0; i < state.PointOfViewControllers.Length; i++)
                    {
                        int angle = state.PointOfViewControllers[i];
                        if (angle < 0 || (angle & 0xffff) == 0xffff) continue;
                        string[] directions = ["Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft"];
                        int direction = ((angle + 2250) / 4500) % 8;
                        current.Add($"DI:{id:D}:POV{i + 1}:{directions[direction]}");
                    }

                    // An empty successful snapshot is meaningful: it is a real
                    // release, so replace the cached held state immediately.
                    DeviceSnapshots[id] = new DevicePressedSnapshot(current, now);
                    pressed.UnionWith(current);
                }
                catch
                {
                    AddRecentDeviceSnapshot(id, pressed, now);
                }
            }

            pressed.UnionWith(XInputController.ReadPressed());
        }
        catch
        {
            // Enumeration itself can fail transiently. Preserve only recent
            // per-device states, never an unlimited "stuck button" state.
            foreach (Guid id in Devices.Keys)
                AddRecentDeviceSnapshot(id, pressed, now);
            pressed.UnionWith(XInputController.ReadPressed());
        }
        return pressed;
    }

    private static void AddRecentDeviceSnapshot(Guid id, HashSet<string> destination, long now)
    {
        if (DeviceSnapshots.TryGetValue(id, out DevicePressedSnapshot? cached) &&
            now - cached.UpdatedAt <= DeviceFailureGraceMilliseconds)
            destination.UnionWith(cached.Pressed);
    }

    private static bool LooksLikeXInputName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name.Contains("xbox", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("xinput", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("gamepad for windows", StringComparison.OrdinalIgnoreCase));

    private static bool IsVirtualName(string? name) =>
        name?.Contains("vJoy", StringComparison.OrdinalIgnoreCase) == true;
}
