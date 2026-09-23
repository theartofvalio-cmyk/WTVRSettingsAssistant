using System.Runtime.InteropServices;
using Vortice.DirectInput;

namespace HOTASTrimUtility;

/// <summary>
/// Small dependency-free XInput bridge for Xbox-compatible controllers.
/// VTrim still supports all DirectInput HOTAS/yokes/pedals; XInput is added so
/// Xbox controllers are first-class inputs instead of relying on DirectInput's
/// incomplete compatibility view of XInput devices.
/// </summary>
internal static class XInputGamepad
{
    private const ushort DPadUp = 0x0001;
    private const ushort DPadDown = 0x0002;
    private const ushort DPadLeft = 0x0004;
    private const ushort DPadRight = 0x0008;
    private const ushort Start = 0x0010;
    private const ushort Back = 0x0020;
    private const ushort LeftThumb = 0x0040;
    private const ushort RightThumb = 0x0080;
    private const ushort LeftShoulder = 0x0100;
    private const ushort RightShoulder = 0x0200;
    private const ushort A = 0x1000;
    private const ushort B = 0x2000;
    private const ushort X = 0x4000;
    private const ushort Y = 0x8000;
    private const uint ErrorSuccess = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint PacketNumber;
        public Gamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out State state);

    public static bool IsConnected(int index)
    {
        if ((uint)index > 3) return false;
        try { return XInputGetState((uint)index, out _) == ErrorSuccess; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    public static bool TryGetState(int index, out JoystickState state)
    {
        state = new JoystickState();
        if ((uint)index > 3) return false;
        try
        {
            if (XInputGetState((uint)index, out State native) != ErrorSuccess) return false;
            state = Translate(native.Gamepad);
            return true;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    // Kept separate from the P/Invoke call so the mapping is deterministic and
    // can be regression-tested without an Xbox controller attached.
    internal static JoystickState CreateStateForTest(short lx, short ly, short rx, short ry,
        byte lt, byte rt, ushort buttons) =>
        Translate(new Gamepad
        {
            ThumbLX = lx,
            ThumbLY = ly,
            ThumbRX = rx,
            ThumbRY = ry,
            LeftTrigger = lt,
            RightTrigger = rt,
            Buttons = buttons
        });

    private static JoystickState Translate(Gamepad pad)
    {
        var state = new JoystickState
        {
            X = pad.ThumbLX,
            Y = pad.ThumbLY,
            RotationX = pad.ThumbRX,
            RotationY = pad.ThumbRY,
            Z = ScaleTrigger(pad.LeftTrigger),
            RotationZ = ScaleTrigger(pad.RightTrigger)
        };

        bool[] buttons = state.Buttons;
        Set(buttons, 0, pad.Buttons, A);
        Set(buttons, 1, pad.Buttons, B);
        Set(buttons, 2, pad.Buttons, X);
        Set(buttons, 3, pad.Buttons, Y);
        Set(buttons, 4, pad.Buttons, LeftShoulder);
        Set(buttons, 5, pad.Buttons, RightShoulder);
        Set(buttons, 6, pad.Buttons, Back);
        Set(buttons, 7, pad.Buttons, Start);
        Set(buttons, 8, pad.Buttons, LeftThumb);
        Set(buttons, 9, pad.Buttons, RightThumb);

        // Keep D-pad exposed as a POV hat so existing combo/binding logic works
        // exactly like it does for a HOTAS hat switch.
        state.PointOfViewControllers[0] = PovFromButtons(pad.Buttons);
        for (int i = 1; i < state.PointOfViewControllers.Length; i++)
            state.PointOfViewControllers[i] = -1;
        return state;
    }

    private static int ScaleTrigger(byte trigger) => (int)Math.Round(trigger / 255.0 * 65535.0);

    private static void Set(bool[] target, int index, ushort value, ushort mask)
    {
        if ((uint)index < (uint)target.Length) target[index] = (value & mask) != 0;
    }

    private static int PovFromButtons(ushort buttons)
    {
        bool up = (buttons & DPadUp) != 0;
        bool down = (buttons & DPadDown) != 0;
        bool left = (buttons & DPadLeft) != 0;
        bool right = (buttons & DPadRight) != 0;
        if (up && right) return 4500;
        if (right && down) return 13500;
        if (down && left) return 22500;
        if (left && up) return 31500;
        if (up) return 0;
        if (right) return 9000;
        if (down) return 18000;
        if (left) return 27000;
        return -1;
    }
}
