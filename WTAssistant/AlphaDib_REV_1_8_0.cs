using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace WTVRSettingsAssistant;

internal static class AlphaDib
{
    [StructLayout(LayoutKind.Sequential)] private struct Header
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public Header Header; public uint Color; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte Operation, Flags, Alpha, Format; }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr dst, ref Point position, ref Size size, IntPtr src, ref Point origin, uint key, ref Blend blend, uint flags);
    public static void Paint(IntPtr window, Point position, Bitmap frame)
    {
        if (frame.PixelFormat != PixelFormat.Format32bppPArgb) throw new ArgumentException("A premultiplied-alpha bitmap is required.", nameof(frame));
        IntPtr screen = GetDC(IntPtr.Zero), dc = IntPtr.Zero, bitmap = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            dc = CreateCompatibleDC(screen);
            if (dc == IntPtr.Zero) throw new Win32Exception();
            var info = new BitmapInfo { Header = new Header { Size = 40, Width = frame.Width, Height = -frame.Height, Planes = 1, BitCount = 32 } };
            bitmap = CreateDIBSection(dc, ref info, 0, out IntPtr pixels, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || pixels == IntPtr.Zero) throw new Win32Exception();
            var locked = frame.LockBits(new Rectangle(Point.Empty, frame.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                byte[] row = new byte[checked(frame.Width * 4)];
                for (int y = 0; y < frame.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(locked.Scan0, y * locked.Stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(pixels, y * row.Length), row.Length);
                }
            }
            finally { frame.UnlockBits(locked); }
            previous = SelectObject(dc, bitmap);
            var size = frame.Size; var origin = Point.Empty; var blend = new Blend { Alpha = 255, Format = 1 };
            if (!UpdateLayeredWindow(window, screen, ref position, ref size, dc, ref origin, 0, ref blend, 2)) throw new Win32Exception();
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(dc, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
