using System.Runtime.InteropServices;

namespace HOTASTrimUtility;

internal enum Axis : uint { HID_USAGE_X = 0x30, HID_USAGE_Y = 0x31, HID_USAGE_RZ = 0x35 }

// Bind directly to the official SDK shipped with the bundled driver. Avoid
// mixing the old NuGet wrapper's native DLL with a newer installed driver.
internal sealed class VirtualJoystick : IDisposable
{
    static VirtualJoystick() => VJoyNativeResolver.Ensure();
    private readonly uint _id;
    private bool _owned;
    public VirtualJoystick(uint id) => _id = id;
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern bool AcquireVJD(uint id);
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void RelinquishVJD(uint id);
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern bool SetAxis(int value, uint id, uint axis);
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern bool SetBtn(bool value, uint id, byte button);
    public void Aquire()
    {
        if (!VJoySetup.Ready) throw new IOException("vJoy Device 1 needs setup. Use Set up / Connect to install and configure it.");
        if (!AcquireVJD(_id)) throw new IOException("vJoy Device 1 could not be acquired. Close any other application using this virtual device.");
        _owned = true;
    }
    public void SetJoystickAxis(int value, Axis axis)
    {
        if (!_owned || !SetAxis(value, _id, (uint)axis)) throw new IOException("vJoy stopped accepting axis output. Reconnect the virtual device.");
    }
    public void SetJoystickButton(bool down, uint button)
    {
        if (!_owned || !SetBtn(down, _id, checked((byte)button))) throw new IOException("vJoy stopped accepting button output. Reconnect the virtual device.");
    }
    public void Release() { if (_owned) { _owned = false; RelinquishVJD(_id); } }
    public void Dispose() => Release();
}
