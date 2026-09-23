using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace HOTASTrimUtility;

internal static class VJoyNativeResolver
{
    private const string VJoyLibrary = "vJoyInterface.dll";
    private static int _initialized;

    public static void Ensure()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return;
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(VJoyNativeResolver).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // A host/test may already have installed a resolver for this assembly.
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, VJoyLibrary, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "Drivers", "vJoy", VJoyLibrary),
            Path.Combine(baseDirectory, "Components", "Drivers", "vJoy", VJoyLibrary),
            // Compatibility fallback for older portable builds.
            Path.Combine(baseDirectory, VJoyLibrary)
        ];

        foreach (string candidate in candidates)
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out IntPtr handle))
                return handle;

        return IntPtr.Zero;
    }
}
