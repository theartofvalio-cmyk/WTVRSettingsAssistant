using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WTVRSettingsAssistant;

internal static class KeyboardTapSender
{
    private static int _pendingKeyboardReleases;
    internal static bool KeyboardTapInProgress => Volatile.Read(ref _pendingKeyboardReleases) > 0;
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeybdInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventScanCode = 0x0008;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    // Used by hover controls, which must stay down for as long as the assigned
    // physical button is held rather than producing a short tap.
    public static bool TrySetKeyState(Keys key, bool down, string languageCode, out string? error)
    {
        int virtualKey = (int)(key & Keys.KeyCode);
        ushort scanCode = (ushort)MapVirtualKey((uint)virtualKey, 0);
        if (virtualKey == 0 || scanCode == 0)
        {
            error = "No keyboard key is assigned.";
            return false;
        }

        SyntheticKeyGuard.Suppress(virtualKey);
        uint flags = KeyEventScanCode | (down ? 0u : KeyEventKeyUp);
        if (IsExtended((Keys)virtualKey)) flags |= KeyEventExtendedKey;
        Input input = new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeybdInput { ScanCode = scanCode, Flags = flags } } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) == 1)
        {
            error = null;
            return true;
        }

        int code = Marshal.GetLastWin32Error();
        error = code == 5 ? AppText.T(languageCode, "Keybind.InputBlocked") : new Win32Exception(code).Message;
        return false;
    }

    public static bool TryTap(Keys key, string languageCode, out string? error)
    {
        int virtualKey = (int)(key & Keys.KeyCode);
        if (virtualKey == 0)
        {
            error = "No keyboard key is assigned.";
            return false;
        }

        SyntheticKeyGuard.Suppress(virtualKey);
        ushort scanCode = (ushort)MapVirtualKey((uint)virtualKey, 0);
        uint flags = KeyEventScanCode;
        if (IsExtended((Keys)virtualKey)) flags |= KeyEventExtendedKey;
        Input down = new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeybdInput { ScanCode = scanCode, Flags = flags } } };
        uint sent = SendInput(1, [down], Marshal.SizeOf<Input>());
        if (sent != 1)
        {
            int code = Marshal.GetLastWin32Error();
            error = code == 5 ? AppText.T(languageCode, "Keybind.InputBlocked") : new Win32Exception(code).Message;
            return false;
        }

        Interlocked.Increment(ref _pendingKeyboardReleases);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(90).ConfigureAwait(false);
                Input up = new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeybdInput { ScanCode = scanCode, Flags = flags | KeyEventKeyUp } } };
                SendInput(1, [up], Marshal.SizeOf<Input>());
            }
            finally { Interlocked.Decrement(ref _pendingKeyboardReleases); }
        });
        error = null;
        return true;
    }

    public static bool IsSupportedMouseButton(string value) => NormalizeMouseButton(value) is not null;

    public static void ClickMouse(string value, string languageCode)
    {
        if (!TryClickMouse(value, languageCode, out string? error)) throw new InvalidOperationException(error);
    }

    public static bool TryClickMouse(string value, string languageCode, out string? error)
    {
        MouseCommand? command = NormalizeMouseButton(value);
        if (command is null)
        {
            error = "No mouse button is assigned.";
            return false;
        }

        Input down = new() { Type = InputMouse, Data = new InputUnion { Mouse = new MouseInput { Flags = command.Value.DownFlag, MouseData = command.Value.MouseData } } };
        uint sent = SendInput(1, [down], Marshal.SizeOf<Input>());
        if (sent != 1)
        {
            int code = Marshal.GetLastWin32Error();
            error = code == 5 ? AppText.T(languageCode, "Keybind.InputBlocked") : new Win32Exception(code).Message;
            return false;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(90).ConfigureAwait(false);
            Input up = new() { Type = InputMouse, Data = new InputUnion { Mouse = new MouseInput { Flags = command.Value.UpFlag, MouseData = command.Value.MouseData } } };
            SendInput(1, [up], Marshal.SizeOf<Input>());
        });
        error = null;
        return true;
    }

    private readonly record struct MouseCommand(uint DownFlag, uint UpFlag, uint MouseData);

    private static MouseCommand? NormalizeMouseButton(string value) => value.Trim().ToUpperInvariant() switch
    {
        "MOUSELEFT" or "LEFT" or "LMB" => new MouseCommand(MouseEventLeftDown, MouseEventLeftUp, 0),
        "MOUSERIGHT" or "RIGHT" or "RMB" => new MouseCommand(MouseEventRightDown, MouseEventRightUp, 0),
        "MOUSEMIDDLE" or "MIDDLE" or "MMB" => new MouseCommand(MouseEventMiddleDown, MouseEventMiddleUp, 0),
        "MOUSEX1" or "X1" => new MouseCommand(MouseEventXDown, MouseEventXUp, XButton1),
        "MOUSEX2" or "X2" => new MouseCommand(MouseEventXDown, MouseEventXUp, XButton2),
        _ => null
    };

    public static void Tap(Keys key, string languageCode)
    {
        if (!TryTap(key, languageCode, out string? error)) throw new InvalidOperationException(error);
    }

    private static bool IsExtended(Keys key) => key is Keys.Insert or Keys.Delete or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
        or Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Divide or Keys.NumLock;
}
