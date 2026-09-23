using System.Runtime.InteropServices;

namespace WTVRSettingsAssistant;

/// <summary>
/// XInput button/hat reader used by Neck Assist and KeyBind Assist. This keeps
/// Xbox-compatible controllers usable even when DirectInput exposes an
/// incomplete compatibility device.
/// </summary>
internal static class XInputController
{
    private const uint ErrorSuccess = 0;
    private const ushort DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008;
    private const ushort Start = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080;
    private const ushort LeftShoulder = 0x0100, RightShoulder = 0x0200;
    private const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint PacketNumber;
        public Gamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out State state);

    public static bool AnyConnected => Enumerable.Range(0, 4).Any(IsConnected);

    public static bool IsConnected(int index)
    {
        if ((uint)index > 3) return false;
        try { return XInputGetState((uint)index, out _) == ErrorSuccess; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }


    public static bool TryGetButtonMask(int index, out uint mask)
    {
        mask = 0;
        if ((uint)index > 3) return false;
        State state;
        try
        {
            if (XInputGetState((uint)index, out state) != ErrorSuccess) return false;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }

        ushort b = state.Gamepad.Buttons;
        SetMask(ref mask, 1, b, A);
        SetMask(ref mask, 2, b, B);
        SetMask(ref mask, 3, b, X);
        SetMask(ref mask, 4, b, Y);
        SetMask(ref mask, 5, b, LeftShoulder);
        SetMask(ref mask, 6, b, RightShoulder);
        SetMask(ref mask, 7, b, Back);
        SetMask(ref mask, 8, b, Start);
        SetMask(ref mask, 9, b, LeftThumb);
        SetMask(ref mask, 10, b, RightThumb);
        SetMask(ref mask, 11, b, DPadUp);
        SetMask(ref mask, 12, b, DPadRight);
        SetMask(ref mask, 13, b, DPadDown);
        SetMask(ref mask, 14, b, DPadLeft);
        return true;
    }
    public static HashSet<string> ReadPressed()
    {
        var pressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < 4; index++)
        {
            State state;
            try
            {
                if (XInputGetState((uint)index, out state) != ErrorSuccess) continue;
            }
            catch (DllNotFoundException) { break; }
            catch (EntryPointNotFoundException) { break; }

            ushort b = state.Gamepad.Buttons;
            AddButton(pressed, index, 1, b, A);
            AddButton(pressed, index, 2, b, B);
            AddButton(pressed, index, 3, b, X);
            AddButton(pressed, index, 4, b, Y);
            AddButton(pressed, index, 5, b, LeftShoulder);
            AddButton(pressed, index, 6, b, RightShoulder);
            AddButton(pressed, index, 7, b, Back);
            AddButton(pressed, index, 8, b, Start);
            AddButton(pressed, index, 9, b, LeftThumb);
            AddButton(pressed, index, 10, b, RightThumb);

            string? pov = PovName(b);
            if (pov is not null) pressed.Add($"XI:{index}:POV1:{pov}");
        }
        return pressed;
    }

    public static string DisplayToken(string token)
    {
        string[] parts = token.Split(':');
        if (parts.Length < 3 || !int.TryParse(parts[1], out int index)) return "Xbox controller input";
        string input = parts[2];
        if (input.StartsWith("POV", StringComparison.OrdinalIgnoreCase))
            return $"Xbox Controller {index + 1} - D-pad {(parts.Length > 3 ? parts[3] : "")}".TrimEnd();
        if (input.StartsWith("B", StringComparison.OrdinalIgnoreCase))
            return $"Xbox Controller {index + 1} - Button {input[1..]}";
        return $"Xbox Controller {index + 1} - {input}";
    }

    private static void SetMask(ref uint target, int buttonNumber, ushort value, ushort sourceMask)
    {
        if ((value & sourceMask) != 0) target |= 1u << (buttonNumber - 1);
    }

    private static void AddButton(HashSet<string> target, int user, int buttonNumber, ushort value, ushort mask)
    {
        if ((value & mask) != 0) target.Add($"XI:{user}:B{buttonNumber}");
    }

    private static string? PovName(ushort buttons)
    {
        bool up = (buttons & DPadUp) != 0, down = (buttons & DPadDown) != 0;
        bool left = (buttons & DPadLeft) != 0, right = (buttons & DPadRight) != 0;
        if (up && right) return "UpRight";
        if (right && down) return "DownRight";
        if (down && left) return "DownLeft";
        if (left && up) return "UpLeft";
        if (up) return "Up";
        if (right) return "Right";
        if (down) return "Down";
        if (left) return "Left";
        return null;
    }

    internal static HashSet<string> CreatePressedForTest(int user, ushort buttons)
    {
        var pressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddButton(pressed, user, 1, buttons, A);
        AddButton(pressed, user, 2, buttons, B);
        AddButton(pressed, user, 3, buttons, X);
        AddButton(pressed, user, 4, buttons, Y);
        string? pov = PovName(buttons);
        if (pov is not null) pressed.Add($"XI:{user}:POV1:{pov}");
        return pressed;
    }
}
