using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace HOTASTrimUtility;

public static class VJoySetup
{
    private static readonly SemaphoreSlim SetupGate = new(1, 1);
    private const string InstallerHash = "EF569A3105CD301B89580F18F60C66B339E95296ACF2C0DFCAF4B4BBF8AB68FE";
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern bool vJoyEnabled();
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern bool GetVJDAxisExist(uint id, uint axis);
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int GetVJDButtonNumber(uint id);
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int GetVJDStatus(uint id);
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)] private static extern bool DriverMatch(out ushort dllVersion, out ushort driverVersion);
    public static bool Ready
    {
        get
        {
            try
            {
                return vJoyEnabled() &&
                       DriverMatch(out _, out _) &&
                       GetVJDAxisExist(1, 0x30) &&
                       GetVJDAxisExist(1, 0x31) &&
                       GetVJDAxisExist(1, 0x35) &&
                       GetVJDButtonNumber(1) >= 32;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { return false; }
        }
    }
    public static async Task EnsureAsync(Action<string> progress)
    {
        await SetupGate.WaitAsync();
        try
        {
            if (Ready) { progress("vJoy Device 1 is ready: X / Y / Rz and 32 buttons."); return; }
            if (SharedVJoyOutputCoordinator.IsConnected)
                throw new InvalidOperationException("Turn off VTrim and switch output before repairing vJoy.");
            try
            {
                if (GetVJDStatus(1) == 2)
                    throw new InvalidOperationException("vJoy Device 1 is in use by another application. Close that feeder before setup.");
            }
            catch (DllNotFoundException) { }
            string? config = FindConfig();
            bool installed;
            try { installed = vJoyEnabled() && DriverMatch(out _, out _); } catch (DllNotFoundException) { installed = false; }
            if (!installed || config is null)
            {
                string installer = Path.Combine(AppContext.BaseDirectory, "Drivers", "vJoy", "vJoySetup.exe");
                if (!File.Exists(installer)) throw new FileNotFoundException("The bundled vJoy installer is missing. Extract the complete Assistant release.", installer);
                using (var file = File.OpenRead(installer))
                    if (!Convert.ToHexString(SHA256.HashData(file)).Equals(InstallerHash, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("The vJoy installer failed its integrity check. Restore it from the Assistant release.");
                progress("Installing signed vJoy driver. Accept the Windows administrator prompt; setup will not restart Windows.");
                int result = await RunElevated(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010 /COMPONENTS=Apps,Apps\\vJoyConf");
                if (result == 3010) throw new InvalidOperationException("vJoy installed. Restart Windows, then enable VTrim to finish setup.");
                if (result != 0) throw new IOException($"vJoy setup exited with code {result}. Restart Windows and retry setup.");
                config = FindConfig();
            }
            if (config is null) throw new FileNotFoundException("vJoy was installed but its configuration tool is unavailable. Restart Windows and retry setup.");
            if (!Ready)
            {
                progress("Configuring vJoy Device 1. VTrim will use only X / Y / Rz output and virtual buttons.");
                int result = await RunElevated(config, "1 -f -a x y z rx ry rz sl0 sl1 -b 32 -s 1");
                if (result != 0) throw new IOException($"vJoy configuration exited with code {result}.");
            }
            for (int n = 0; n < 20 && !Ready; n++) await Task.Delay(300);
            if (!Ready) throw new IOException("Windows has not exposed the configured vJoy device yet. Restart Windows, then enable VTrim again.");
            progress("vJoy setup complete. Device 1 is ready for trim and switch bindings.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { throw new InvalidOperationException("Setup was canceled at the Windows administrator prompt. Use Set up vJoy to retry.", ex); }
        finally { SetupGate.Release(); }
    }

    public static async Task UninstallAsync(Action<string> progress)
    {
        await SetupGate.WaitAsync();
        try
        {
            if (SharedVJoyOutputCoordinator.IsConnected)
                throw new InvalidOperationException("Turn off VTrim and switch output before uninstalling vJoy.");
            string? uninstaller = FindUninstaller();
            if (uninstaller is null)
                throw new FileNotFoundException("vJoy uninstaller was not found. vJoy may already be removed, or it was installed in a custom location.");
            progress("Uninstalling vJoy. Accept the Windows administrator prompt.");
            int result = await RunElevated(uninstaller, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
            if (result != 0) throw new IOException($"vJoy uninstall exited with code {result}.");
            progress("vJoy uninstall finished. Restart Windows if the device still appears in game controllers.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { throw new InvalidOperationException("Uninstall was canceled at the Windows administrator prompt.", ex); }
        finally { SetupGate.Release(); }
    }

    private static string? FindConfig()
    {
        foreach (string root in new[] { Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.OfType<string>().Distinct())
            foreach (string suffix in new[] { @"vJoy\x64\vJoyConfig.exe", @"vJoy\vJoyConfig.exe", @"vJoy\x86\vJoyConfig.exe" })
            { string path = Path.Combine(root, suffix); if (File.Exists(path)) return path; }
        return null;
    }

    private static string? FindUninstaller()
    {
        foreach (string root in InstalledRoots())
        {
            foreach (string file in Directory.EnumerateFiles(root, "unins*.exe"))
                return file;
        }
        return null;
    }

    private static IEnumerable<string> InstalledRoots()
    {
        foreach (string root in new[] { Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.OfType<string>().Distinct())
        {
            string path = Path.Combine(root, "vJoy");
            if (Directory.Exists(path)) yield return path;
        }
    }

    private static async Task<int> RunElevated(string path, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(path, arguments)
            { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden })
            ?? throw new IOException("Windows did not start vJoy setup.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
