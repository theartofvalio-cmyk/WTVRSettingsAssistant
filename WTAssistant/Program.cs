using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: SupportedOSPlatform("windows")]

namespace WTVRSettingsAssistant;

internal static class Program
{
    private const string InstanceMutexName = "Local\\WTVRSettingsAssistant.SingleInstance";
    private const string ActivateEventName = "Local\\WTVRSettingsAssistant.Activate";

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = SelfTestRunner.Run();
            return;
        }

        using Mutex instanceMutex = new(initiallyOwned: true, InstanceMutexName, out bool isFirstInstance);
        using EventWaitHandle activateEvent = new(false, EventResetMode.AutoReset, ActivateEventName);
        if (!isFirstInstance)
        {
            try { activateEvent.Set(); } catch { }
            return;
        }

        ApplicationConfiguration.Initialize();
        MainForm mainForm = new();
        RegisteredWaitHandle activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            activateEvent,
            (_, _) =>
            {
                if (!mainForm.IsDisposed && mainForm.IsHandleCreated)
                {
                    try { mainForm.BeginInvoke((Action)mainForm.RestoreFromExternalLaunch); } catch { }
                }
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        try
        {
            Application.Run(mainForm);
        }
        finally
        {
            activationRegistration.Unregister(null);
        }
    }

}

public static class AssetManager
{
    public static Image LoadImage(string fileName)
    {
        if (Path.GetFileName(fileName).Equals("VRA.png", StringComparison.OrdinalIgnoreCase))
            return BrandingLogo.Create();
        Image? themed = IllustratedTheme.Asset(fileName);
        if (themed != null) return themed;
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = AppThemeAssets.ResolveResourceName(assembly, fileName);

        if (resourceName == null)
        {
            throw new FileNotFoundException($"Embedded image not found: {fileName}");
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded image stream not found: {fileName}");
        }

        using Image tempImage = Image.FromStream(stream);
        return new Bitmap(tempImage);
    }

    public static Stream LoadResourceStream(string fileName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = AppThemeAssets.ResolveResourceName(assembly, fileName);

        if (resourceName == null)
        {
            throw new FileNotFoundException($"Embedded resource not found: {fileName}");
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource stream not found: {fileName}");
        }

        MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    public static Icon LoadIcon(string fileName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = AppThemeAssets.ResolveResourceName(assembly, fileName);

        if (resourceName == null)
        {
            throw new FileNotFoundException($"Embedded icon not found: {fileName}");
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded icon stream not found: {fileName}");
        }

        using Icon tempIcon = new Icon(stream);
        return (Icon)tempIcon.Clone();
    }
}

public partial class MainForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string? pszSound, IntPtr hmod, int fdwSound);

    private const int SND_SYNC = 0x0000;
    private const int SND_NODEFAULT = 0x0002;
    private const int SND_FILENAME = 0x00020000;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const float AppScale = 1f;

    // Layout editor code is still inside the app, but disabled for release/customized builds.
    // Change these to true later if you want to re-enable F12 layout editing and external layout files.
    private static readonly bool LayoutEditorEnabled = false;
    private static readonly bool LoadExternalLayoutFiles = false;
    private const string CurrentVersion = "2.0.4";
    private const string BuildChannelLabel = "";
    private const string GitHubLatestReleaseApi = "https://api.github.com/repos/theartofvalio-cmyk/WTVRSettingsAssistant/releases/latest";
    private const string GitHubReleasesApi = "https://api.github.com/repos/theartofvalio-cmyk/WTVRSettingsAssistant/releases?per_page=30";
    private const string GitHubReleasesUrl = "https://github.com/theartofvalio-cmyk/WTVRSettingsAssistant/releases";
    private const string UpdateExecutableName = "WTVRSettingsAssistant.exe";

    private const string BakedMainLayoutJson = """"
{
  "MainLogo": {
    "X": 10,
    "Y": -14,
    "Width": 854,
    "Height": 893,
    "ZOrder": 0,
    "FontSize": 0
  },
  "VersionText": {
    "X": 268,
    "Y": 675,
    "Width": 360,
    "Height": 72,
    "ZOrder": 1,
    "FontSize": 32
  },
  "VRButton": {
    "X": 920,
    "Y": 310,
    "Width": 560,
    "Height": 241,
    "ZOrder": 2,
    "FontSize": 0
  },
  "MonitorButton": {
    "X": 920,
    "Y": 55,
    "Width": 560,
    "Height": 234,
    "ZOrder": 3,
    "FontSize": 0
  },
  "PlayButton": {
    "X": 830,
    "Y": 570,
    "Width": 740,
    "Height": 200,
    "ZOrder": 4,
    "FontSize": 0
  },
  "InfoIcon": {
    "X": 1516,
    "Y": 150,
    "Width": 81,
    "Height": 81,
    "ZOrder": 5,
    "FontSize": 0
  },
  "SettingsIcon": {
    "X": 1500,
    "Y": 25,
    "Width": 114,
    "Height": 115,
    "ZOrder": 4,
    "FontSize": 0
  },
  "NeckAssistButton": {
    "X": 1516,
    "Y": 245,
    "Width": 81,
    "Height": 81,
    "ZOrder": 6,
    "FontSize": 0
  },
  "HiddenKeybindsButton": {
    "X": 1516,
    "Y": 340,
    "Width": 81,
    "Height": 81,
    "ZOrder": 7,
    "FontSize": 0
  },
  "UpdateButton": {
    "X": 1516,
    "Y": 435,
    "Width": 81,
    "Height": 81,
    "ZOrder": 8,
    "FontSize": 0
  }
}
"""";

    private const string BakedSettingsLayoutJson = """"
{
  "SettingsTip": {
    "X": 425,
    "Y": 3,
    "Width": 772,
    "Height": 50,
    "ZOrder": 0,
    "FontSize": 23.753845,
    "Text": "Tip: You can drag and drop .blk files onto the BROWSE buttons."
  },
  "InfoIcon": {
    "X": 1408,
    "Y": 45,
    "Width": 81,
    "Height": 81,
    "ZOrder": 1,
    "FontSize": 0,
    "Text": null
  },
  "HomeIcon": {
    "X": 1498,
    "Y": 28,
    "Width": 114,
    "Height": 106,
    "ZOrder": 2,
    "FontSize": 0,
    "Text": null
  },
  "ConfigTitle": {
    "X": 75,
    "Y": 102,
    "Width": 684,
    "Height": 60,
    "ZOrder": 3,
    "FontSize": 35.515385,
    "Text": "WarThunder/Config.blk"
  },
  "ConfigField": {
    "X": 72,
    "Y": 138,
    "Width": 500,
    "Height": 50,
    "ZOrder": 4,
    "FontSize": 0,
    "Text": null
  },
  "ConfigPathText": {
    "X": 84,
    "Y": 141,
    "Width": 475,
    "Height": 40,
    "ZOrder": 5,
    "FontSize": 16,
    "Text": ""
  },
  "ConfigBrowse": {
    "X": 592,
    "Y": 120,
    "Width": 280,
    "Height": 86,
    "ZOrder": 6,
    "FontSize": 0,
    "Text": null
  },
  "ConfigDescription": {
    "X": 75,
    "Y": 55,
    "Width": 806,
    "Height": 51,
    "ZOrder": 7,
    "FontSize": 24,
    "Text": "Locate the config.blk in your War Thunder directory."
  },
  "DesktopTitle": {
    "X": 75,
    "Y": 395,
    "Width": 700,
    "Height": 60,
    "ZOrder": 8,
    "FontSize": 36.346153,
    "Text": "DESCTOP .blk"
  },
  "DesktopField": {
    "X": 75,
    "Y": 310,
    "Width": 500,
    "Height": 50,
    "ZOrder": 9,
    "FontSize": 0,
    "Text": null
  },
  "DesktopPathText": {
    "X": 85,
    "Y": 315,
    "Width": 475,
    "Height": 40,
    "ZOrder": 10,
    "FontSize": 16,
    "Text": ""
  },
  "DesktopBrowse": {
    "X": 592,
    "Y": 385,
    "Width": 280,
    "Height": 86,
    "ZOrder": 11,
    "FontSize": 0,
    "Text": null
  },
  "DesktopCaptureSettings": {
    "X": 309,
    "Y": 400,
    "Width": 292,
    "Height": 52,
    "ZOrder": 12,
    "FontSize": 0,
    "Text": null
  },
  "DesktopRemoveSettings": {
    "X": 876,
    "Y": 400,
    "Width": 200,
    "Height": 50,
    "ZOrder": 13,
    "FontSize": 0,
    "Text": null
  },
  "DesktopDescription": {
    "X": 75,
    "Y": 340,
    "Width": 901,
    "Height": 48,
    "ZOrder": 14,
    "FontSize": 24,
    "Text": "Your custom settings for War Thunder when playing on flat screen."
  },
  "CustomToggleTitle": {
    "X": 1215,
    "Y": 102,
    "Width": 360,
    "Height": 46,
    "ZOrder": 15,
    "FontSize": 31,
    "Text": "CUSTOM VR .blk"
  },
  "CustomToggle": {
    "X": 1250,
    "Y": 185,
    "Width": 260,
    "Height": 140,
    "ZOrder": 16,
    "FontSize": 0,
    "Text": null
  },
  "PresetTitle": {
    "X": 72,
    "Y": 464,
    "Width": 887,
    "Height": 91,
    "ZOrder": 17,
    "FontSize": 39.68158,
    "Text": "VR PRESETS"
  },
  "LowButton": {
    "X": 72,
    "Y": 540,
    "Width": 427,
    "Height": 283,
    "ZOrder": 18,
    "FontSize": 0,
    "Text": null
  },
  "MediumButton": {
    "X": 525,
    "Y": 540,
    "Width": 424,
    "Height": 281,
    "ZOrder": 19,
    "FontSize": 0,
    "Text": null
  },
  "HighButton": {
    "X": 976,
    "Y": 540,
    "Width": 425,
    "Height": 282,
    "ZOrder": 20,
    "FontSize": 0,
    "Text": null
  },
  "MoreInfoButton": {
    "X": 1165,
    "Y": 360,
    "Width": 430,
    "Height": 126,
    "ZOrder": 21,
    "FontSize": 0,
    "Text": null
  },
  "HelpButton": {
    "X": 292,
    "Y": 488,
    "Width": 43,
    "Height": 43,
    "ZOrder": 22,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrTitle": {
    "X": 75,
    "Y": 565,
    "Width": 671,
    "Height": 60,
    "ZOrder": 23,
    "FontSize": 38.71154,
    "Text": "VR .blk"
  },
  "CustomVrField": {
    "X": 72,
    "Y": 500,
    "Width": 500,
    "Height": 50,
    "ZOrder": 24,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrPathText": {
    "X": 84,
    "Y": 505,
    "Width": 475,
    "Height": 40,
    "ZOrder": 25,
    "FontSize": 16,
    "Text": ""
  },
  "CustomVrBrowse": {
    "X": 592,
    "Y": 555,
    "Width": 280,
    "Height": 86,
    "ZOrder": 26,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrCaptureSettings": {
    "X": 211,
    "Y": 570,
    "Width": 300,
    "Height": 54,
    "ZOrder": 27,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrRemoveSettings": {
    "X": 876,
    "Y": 570,
    "Width": 200,
    "Height": 50,
    "ZOrder": 28,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrDescription": {
    "X": 75,
    "Y": 500,
    "Width": 911,
    "Height": 39,
    "ZOrder": 29,
    "FontSize": 24,
    "Text": "Your custom settings for War Thunder when playing in VR."
  }
}
"""";

    private const string BakedAboutLayoutJson = """"
{
  "AboutText": {
    "X": 11,
    "Y": 10,
    "Width": 1511,
    "Height": 570,
    "ZOrder": 0,
    "FontSize": 23.744286,
    "Text": "Hi, you probably haven\u2019t heard of me, and that\u2019s completely fine. :)\n\nMy name is Valentin, and I\u2019m a War Thunder VR player and content creator. I enjoy making guides\nand helping people optimize their game for a smoother and more enjoyable VR experience.\n\nI created this tool to help the War Thunder VR community switch more easily between VR and flat-screen play,\nwithout having to manually change settings every time. The goal is simple: make the process smoother, faster, and less frustrating.\n\nI\u2019ll also be making an updated VR video guide that covers additional settings outside the game. For now, the included LOW, MEDIUM,\nand HIGH presets are based on real settings I\u2019ve tested with people from our Discord community. I\u2019ve worked with players one-on-one,\noptimizing their games across different types of hardware and testing what works best in practice.\n\nIf you\u2019re new to VR, these presets should help you get started much more easily and give you a solid foundation for your first War Thunder VR experience.\n\nCheers,\nVal"
  },
  "SupportText": {
    "X": 9,
    "Y": 583,
    "Width": 809,
    "Height": 235,
    "ZOrder": 1,
    "FontSize": 27.381538,
    "Text": "This tool is completely free and open source.\nIf you like what I do and you want to help me out\nto make more stuff like this, feel free to click Subscribe\nor buy me a Beer! :)"
  },
  "YoutubeButton": {
    "X": 781,
    "Y": 505,
    "Width": 345,
    "Height": 282,
    "ZOrder": 2,
    "FontSize": 0,
    "Text": null
  },
  "BeerButton": {
    "X": 1180,
    "Y": 457,
    "Width": 301,
    "Height": 359,
    "ZOrder": 3,
    "FontSize": 0,
    "Text": null
  },
  "InfoIcon": {
    "X": 1408,
    "Y": 45,
    "Width": 81,
    "Height": 81,
    "ZOrder": 4,
    "FontSize": 0,
    "Text": null
  },
  "HomeIcon": {
    "X": 1498,
    "Y": 28,
    "Width": 114,
    "Height": 115,
    "ZOrder": 5,
    "FontSize": 0,
    "Text": null
  }
}
"""";

    private const string BakedRecommendedGpuLayoutJson = """"
{
  "IntroText": {
    "X": 35,
    "Y": 25,
    "Width": 1547,
    "Height": 129,
    "ZOrder": 0,
    "FontSize": 26.689655,
    "Text": "The RECOMMENDED VR SETTINGS presets were created with different PC hardware limitations in mind. These presets are\nbased on settings that have worked well for different players in the War Thunder VR community after manual testing,\ntweaking, and optimization. If you are unsure which preset to choose, check the SPEC Recommendations below to help\nyou decide which preset best matches your system."
  },
  "GpuLabel": {
    "X": 35,
    "Y": 390,
    "Width": 120,
    "Height": 60,
    "ZOrder": 1,
    "FontSize": 30,
    "Text": "GPU\u0027S:"
  },
  "LowLine": {
    "X": 160,
    "Y": 175,
    "Width": 31,
    "Height": 349,
    "ZOrder": 2,
    "FontSize": 0,
    "Text": null
  },
  "LowTitle": {
    "X": 210,
    "Y": 178,
    "Width": 330,
    "Height": 75,
    "ZOrder": 3,
    "FontSize": 56,
    "Text": "LOW"
  },
  "LowBody": {
    "X": 210,
    "Y": 275,
    "Width": 420,
    "Height": 430,
    "ZOrder": 4,
    "FontSize": 28,
    "Text": "NVIDIA:\nGTX 1080 / 1080 Ti\nRTX 20 series\nRTX 30 series up to RTX 3070\n\nAMD:\nRX 5700 XT\nRX 6600 XT / 6650 XT\nRX 6700 / 6700 XT / 6750 XT\nRX 7600 XT"
  },
  "MediumLine": {
    "X": 610,
    "Y": 175,
    "Width": 31,
    "Height": 349,
    "ZOrder": 5,
    "FontSize": 0,
    "Text": null
  },
  "MediumTitle": {
    "X": 660,
    "Y": 178,
    "Width": 330,
    "Height": 75,
    "ZOrder": 6,
    "FontSize": 56,
    "Text": "MEDIUM"
  },
  "MediumBody": {
    "X": 660,
    "Y": 275,
    "Width": 420,
    "Height": 430,
    "ZOrder": 7,
    "FontSize": 28,
    "Text": "NVIDIA:\nRTX 30 series: 3080 / 3090 Ti\nRTX 40 series up to RTX 4070\nRTX 50 series up to RTX 5060\n\nAMD:\nRX 6800 / 6800 XT\nRX 6900 XT / 6950 XT\nRX 7700 XT\nRX 7800 XT\nRX 7900 GRE"
  },
  "HighLine": {
    "X": 1060,
    "Y": 175,
    "Width": 31,
    "Height": 349,
    "ZOrder": 8,
    "FontSize": 0,
    "Text": null
  },
  "HighTitle": {
    "X": 1110,
    "Y": 178,
    "Width": 330,
    "Height": 75,
    "ZOrder": 9,
    "FontSize": 56,
    "Text": "HIGH"
  },
  "HighBody": {
    "X": 1110,
    "Y": 275,
    "Width": 420,
    "Height": 430,
    "ZOrder": 10,
    "FontSize": 28,
    "Text": "NVIDIA:\nRTX 40 series: 4080 / 4080 Super / 4090\nRTX 50 series: 5070 / 5080 / 5090\n\nAMD:\nRX 7900 XT\nRX 7900 XTX\nRX 9070\nRX 9070 XT"
  },
  "BottomInfo": {
    "X": 35,
    "Y": 721,
    "Width": 1376,
    "Height": 160,
    "ZOrder": 11,
    "FontSize": 28.899395,
    "Text": "Info: Your GPU plays the bigger role when it comes to VR play in War Thunder, but overall the difference between\nMedium and High settings is not that noticeable. Even with an RTX 5090, you can still use Medium settings\nfor better stability without a problem."
  },
  "OkButtonBox": {
    "X": 1425,
    "Y": 765,
    "Width": 150,
    "Height": 50,
    "ZOrder": 12,
    "FontSize": 0,
    "Text": null
  },
  "OkButtonText": {
    "X": 1425,
    "Y": 765,
    "Width": 150,
    "Height": 50,
    "ZOrder": 13,
    "FontSize": 26,
    "Text": "OK"
  }
}
"""";

    private const string BakedRecommendedSelectionLayoutJson = """"
{
  "RecommendedBackArrow": {
    "X": 45,
    "Y": 72,
    "Width": 98,
    "Height": 80,
    "ZOrder": 0,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedInfoIcon": {
    "X": 1408,
    "Y": 45,
    "Width": 81,
    "Height": 81,
    "ZOrder": 1,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSettingsIcon": {
    "X": 1498,
    "Y": 28,
    "Width": 114,
    "Height": 114,
    "ZOrder": 2,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSelectTitle": {
    "X": 145,
    "Y": 6,
    "Width": 1260,
    "Height": 542,
    "ZOrder": 3,
    "FontSize": 39.574173,
    "Text": "Click on the software you use\nto connect your vr headset to your PC."
  },
  "RecommendedVDChoice": {
    "X": 330,
    "Y": 395,
    "Width": 235,
    "Height": 235,
    "ZOrder": 4,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedMetaChoice": {
    "X": 620,
    "Y": 380,
    "Width": 330,
    "Height": 231,
    "ZOrder": 5,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSteamVRChoice": {
    "X": 1010,
    "Y": 380,
    "Width": 360,
    "Height": 203,
    "ZOrder": 6,
    "FontSize": 0,
    "Text": null
  }
}
"""";

    private const string BakedRecommendedViewerLayoutJson = """"
{
  "RecommendedBackArrow": {
    "X": 45,
    "Y": 72,
    "Width": 98,
    "Height": 80,
    "ZOrder": 0,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedInfoIcon": {
    "X": 1408,
    "Y": 45,
    "Width": 81,
    "Height": 81,
    "ZOrder": 1,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSettingsIcon": {
    "X": 1498,
    "Y": 28,
    "Width": 114,
    "Height": 114,
    "ZOrder": 2,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSelectedSoftwareIcon": {
    "X": 693,
    "Y": 28,
    "Width": 238,
    "Height": 166,
    "ZOrder": 3,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedPageImage": {
    "X": 0,
    "Y": 230,
    "Width": 1625,
    "Height": 517,
    "ZOrder": 4,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedBottomBack": {
    "X": 333,
    "Y": 754,
    "Width": 340,
    "Height": 81,
    "ZOrder": 5,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedBottomHome": {
    "X": 773,
    "Y": 758,
    "Width": 78,
    "Height": 73,
    "ZOrder": 6,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedBottomNext": {
    "X": 955,
    "Y": 754,
    "Width": 340,
    "Height": 81,
    "ZOrder": 7,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSelectedSoftwareIcon_SteamVR": {
    "X": 642,
    "Y": 16,
    "Width": 341,
    "Height": 192,
    "ZOrder": 3,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSelectedSoftwareIcon_VD": {
    "X": 704,
    "Y": 4,
    "Width": 216,
    "Height": 216,
    "ZOrder": 3,
    "FontSize": 0,
    "Text": null
  },
  "RecommendedSelectedSoftwareIcon_Meta": {
    "X": 687,
    "Y": 28,
    "Width": 251,
    "Height": 175,
    "ZOrder": 3,
    "FontSize": 0,
    "Text": null
  }
}
"""";

    private const int BaseClientWidth = 1625;
    private const int BaseClientHeight = 844;
    // Main application chrome/home view uses a true 16:9 design surface.
    // The older 1625x844 constants are retained only for legacy profile/settings layouts.
    private const float LockedWindowAspectRatio = 16F / 9F;
    private const int WM_SIZING = 0x0214;
    private const int WMSZ_LEFT = 1;
    private const int WMSZ_RIGHT = 2;
    private const int WMSZ_TOP = 3;
    private const int WMSZ_TOPLEFT = 4;
    private const int WMSZ_TOPRIGHT = 5;
    private const int WMSZ_BOTTOM = 6;
    private const int WMSZ_BOTTOMLEFT = 7;
    private const int WMSZ_BOTTOMRIGHT = 8;

    private enum VrPreset
    {
        None,
        Low,
        Medium,
        High
    }

    private enum AppliedMode
    {
        None,
        VR,
        Monitor
    }

    private enum GraphicsApi
    {
        DX11,
        DX12
    }

    private enum FileSlot
    {
        Config,
        Desktop,
        CustomVr
    }

    private const string EmbeddedVrLowBlk = """"
use_eac:b=yes
use_release_candidate:b=yes
releaseChannel:t="ReleaseCandidate"
rdseed:i=1958454175
language:t="English"
forcedLauncher:i=0
graphicsQuality:t="custom"
use_gamepad_interface:b=no

download{
  upl_speed_rate:i=5000
  seeding_on:b=no
  peer_exchange:b=yes
  dnl_limit:b=no
  DHT:b=yes
  upl_limit:b=no
  dnl_speed_rate:i=5000
  UTP2:b=no
}

launcher{
  startup_with_windows:b=no
  bg_tray:b=yes
  hide_to_tray_option:b=no
  bg_update:b=no
}

sound{
  speakerMode:t="auto"
  fmod_sound_enable:b=yes
}

video{
  vsync:b=no
  windowed:b=no
  mode:t="fullscreenwindowed"
  resolution:t="2560 x 1440"
  driver:t="dx12"
  antialiasing_mode:t="off"
  antialiasing_upscaling:t="native"
  compatibilityMode:b=no
  fonts:t="compact"
  vreye:t="right"
  antialiasing_fgc:i=0
  enableHdr:b=no
  vrStreamerMode:b=yes
  latency:i=2
  antialiasing_sharpening:i=0
  perfMetrics:i=1
  fpsLimit:i=0
  rayReconstruction:b=no
  menuFpsLimit:i=0
  dlssLegacyMode:t="off"
}

yunetwork{
  curCircuit:t="production"
}

gameplay{
  enableVR:b=yes
}

debug{
  enableNvHighlights:t="auto"
  netLogerr:b=yes
  screenshotHiRes:b=no
  screenshotAsJpeg:b=yes
}

graphics{
  texquality:t="medium"
  shadowQuality:t="medium"
  waterEffectsQuality:t="low"
  anisotropy:i=16
  ssaa:r=1
  contactShadowsQuality:i=0
  lenseFlares:b=yes
  waterQuality:t="medium"
  giQuality:t="low"
  displacementQuality:i=0
  tireTracksQuality:i=1
  lastClipSize:i=8192
  landquality:i=0
  rendinstDistMul:r=0.83
  fxQuality:t="low"
  grassRadiusMul:r=0.1
  backgroundScale:r=1
  physicsQuality:i=5
  advancedShore:b=yes
  panoramaResolution:i=1024
  mirrorQuality:i=10
  cloudsQuality:i=2
  skyQuality:i=2
  riGpuObjects:b=yes
  bloomQuality:i=3
  RTRWaterRes:t="half"
  RTAOQuality:t="off"
  RTSMQuality:t="low"
  bvhRiGenRange:i=6000
  enableBVH:b=no
  bvhMode:t="off"
  RTRRes:t="half"
  PTGIQuality:t="off"
  motionBlurStrength:i=0
  RTRTranslucent:t="off"
  enableRTSM:t="off"
  RTRQuality:t="off"
  RTRWater:b=no
  motionBlurCancelCamera:b=no
  fxDistortionStrength:r=1
  fxTarget:t="ultrahigh"
  fxDensityMul:r=0.5
  RTDecals:b=no
}

render{
  ssaoQuality:i=0
  ssrQuality:i=0
  shadows:b=yes
  selfReflection:b=yes
}
"""";

    private const string EmbeddedVrMediumBlk = """"
use_eac:b=yes
use_release_candidate:b=yes
releaseChannel:t="ReleaseCandidate"
rdseed:i=1958454175
language:t="English"
forcedLauncher:i=0
graphicsQuality:t="custom"
use_gamepad_interface:b=no

download{
  upl_speed_rate:i=5000
  seeding_on:b=no
  peer_exchange:b=yes
  dnl_limit:b=no
  DHT:b=yes
  upl_limit:b=no
  dnl_speed_rate:i=5000
  UTP2:b=no
}

launcher{
  startup_with_windows:b=no
  bg_tray:b=yes
  hide_to_tray_option:b=no
  bg_update:b=no
}

sound{
  speakerMode:t="auto"
  fmod_sound_enable:b=yes
}

video{
  vsync:b=no
  windowed:b=no
  mode:t="fullscreenwindowed"
  resolution:t="2560 x 1440"
  driver:t="dx12"
  antialiasing_mode:t="off"
  antialiasing_upscaling:t="native"
  compatibilityMode:b=no
  fonts:t="compact"
  vreye:t="right"
  antialiasing_fgc:i=0
  enableHdr:b=no
  vrStreamerMode:b=yes
  latency:i=2
  antialiasing_sharpening:i=0
  perfMetrics:i=1
  fpsLimit:i=0
  rayReconstruction:b=no
  menuFpsLimit:i=0
  dlssLegacyMode:t="off"
}

yunetwork{
  curCircuit:t="production"
}

gameplay{
  enableVR:b=yes
}

debug{
  enableNvHighlights:t="auto"
  netLogerr:b=yes
  screenshotHiRes:b=no
  screenshotAsJpeg:b=yes
}

graphics{
  texquality:t="medium"
  shadowQuality:t="high"
  waterEffectsQuality:t="medium"
  anisotropy:i=16
  ssaa:r=1
  contactShadowsQuality:i=0
  lenseFlares:b=yes
  waterQuality:t="medium"
  giQuality:t="medium"
  displacementQuality:i=0
  tireTracksQuality:i=1
  lastClipSize:i=8192
  landquality:i=0
  rendinstDistMul:r=0.83
  fxQuality:t="medium"
  grassRadiusMul:r=0.1
  backgroundScale:r=1
  physicsQuality:i=5
  advancedShore:b=yes
  panoramaResolution:i=2560
  mirrorQuality:i=10
  cloudsQuality:i=1
  skyQuality:i=1
  riGpuObjects:b=yes
  bloomQuality:i=3
  RTRWaterRes:t="half"
  RTAOQuality:t="off"
  RTSMQuality:t="low"
  bvhRiGenRange:i=6000
  enableBVH:b=no
  bvhMode:t="off"
  RTRRes:t="half"
  PTGIQuality:t="off"
  motionBlurStrength:i=0
  RTRTranslucent:t="off"
  enableRTSM:t="off"
  RTRQuality:t="off"
  RTRWater:b=no
  motionBlurCancelCamera:b=no
  fxDistortionStrength:r=1
  fxTarget:t="ultrahigh"
  fxDensityMul:r=0.5
  RTDecals:b=no
}

render{
  ssaoQuality:i=0
  ssrQuality:i=0
  shadows:b=yes
  selfReflection:b=yes
}
"""";

    private const string EmbeddedVrHighBlk = """"
use_eac:b=yes
use_release_candidate:b=yes
releaseChannel:t="ReleaseCandidate"
rdseed:i=1958454175
language:t="English"
forcedLauncher:i=0
graphicsQuality:t="custom"
use_gamepad_interface:b=no

download{
  upl_speed_rate:i=5000
  seeding_on:b=no
  peer_exchange:b=yes
  dnl_limit:b=no
  DHT:b=yes
  upl_limit:b=no
  dnl_speed_rate:i=5000
  UTP2:b=no
}

launcher{
  startup_with_windows:b=no
  bg_tray:b=yes
  hide_to_tray_option:b=no
  bg_update:b=no
}

sound{
  speakerMode:t="auto"
  fmod_sound_enable:b=yes
}

video{
  vsync:b=no
  windowed:b=no
  mode:t="fullscreenwindowed"
  resolution:t="2560 x 1440"
  driver:t="dx12"
  antialiasing_mode:t="dlss"
  antialiasing_upscaling:t="native"
  compatibilityMode:b=no
  fonts:t="compact"
  vreye:t="right"
  antialiasing_fgc:i=0
  enableHdr:b=no
  vrStreamerMode:b=yes
  latency:i=2
  antialiasing_sharpening:i=0
  perfMetrics:i=1
  fpsLimit:i=0
  rayReconstruction:b=no
  menuFpsLimit:i=0
  dlssLegacyMode:t="off"
}

yunetwork{
  curCircuit:t="production"
}

gameplay{
  enableVR:b=yes
}

debug{
  enableNvHighlights:t="auto"
  netLogerr:b=yes
  screenshotHiRes:b=no
  screenshotAsJpeg:b=yes
}

graphics{
  texquality:t="high"
  shadowQuality:t="ultrahigh"
  waterEffectsQuality:t="high"
  anisotropy:i=16
  ssaa:r=1
  contactShadowsQuality:i=0
  lenseFlares:b=yes
  waterQuality:t="high"
  giQuality:t="high"
  displacementQuality:i=0
  tireTracksQuality:i=1
  lastClipSize:i=8192
  landquality:i=0
  rendinstDistMul:r=0.9
  fxQuality:t="high"
  grassRadiusMul:r=0.1
  backgroundScale:r=1
  physicsQuality:i=5
  advancedShore:b=yes
  panoramaResolution:i=3584
  mirrorQuality:i=10
  cloudsQuality:i=0
  skyQuality:i=1
  riGpuObjects:b=yes
  bloomQuality:i=3
  RTRWaterRes:t="half"
  RTAOQuality:t="off"
  RTSMQuality:t="low"
  bvhRiGenRange:i=6000
  enableBVH:b=no
  bvhMode:t="off"
  RTRRes:t="half"
  PTGIQuality:t="off"
  motionBlurStrength:i=0
  RTRTranslucent:t="off"
  enableRTSM:t="off"
  RTRQuality:t="off"
  RTRWater:b=no
  motionBlurCancelCamera:b=no
  fxDistortionStrength:r=1
  fxTarget:t="ultrahigh"
  fxDensityMul:r=0.5
  RTDecals:b=no
}

render{
  ssaoQuality:i=0
  ssrQuality:i=0
  shadows:b=yes
  selfReflection:b=yes
}
"""";

    private enum LayoutResizeMode
    {
        None,
        Move,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ResizeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed class SavedState
    {
        public string ConfigBlkPath { get; set; } = "";
        public string DesktopBlkPath { get; set; } = "";
        public string CustomVrBlkPath { get; set; } = "";
        public string MachineBlkPath { get; set; } = "";
        public string DesktopControlsBlkPath { get; set; } = "";
        public string VrControlsBlkPath { get; set; } = "";
        public List<ControlProfileEntry> DesktopControlProfiles { get; set; } = new();
        public List<ControlProfileEntry> VrControlProfiles { get; set; } = new();
        public string SelectedDesktopControlProfileId { get; set; } = "";
        public string SelectedVrControlProfileId { get; set; } = "";
        public string WarThunderExePath { get; set; } = "";
        public GraphicsApi DesktopGraphicsApi { get; set; } = GraphicsApi.DX12;
        public GraphicsApi VrGraphicsApi { get; set; } = GraphicsApi.DX12;
        public bool SwitchControlsWithProfile { get; set; }
        public bool CustomVrEnabled { get; set; }
        public bool ShowHighWarning { get; set; } = true;
        public VrPreset SelectedVrPreset { get; set; } = VrPreset.None;
        public AppliedMode LastAppliedMode { get; set; } = AppliedMode.None;
        public bool UseBetaBuilds { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool StartWithWindows { get; set; }
        public bool ShowHomeAircraft { get; set; }
        public bool ShowHomeKeybinds { get; set; }
        public bool StartMinimizedToTray { get; set; }
        public bool UseTestServer { get; set; }
        public string LanguageCode { get; set; } = "en";
        public string UiTheme { get; set; } = AppThemeAssets.DefaultTheme;
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        // Incremented when the recommended startup geometry changes. This lets
        // 1.8.3 migrate the too-small 1.8.1 window once without fighting future
        // user resizing.
        public int WindowLayoutRevision { get; set; }
    }

    private sealed class LayoutRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ZOrder { get; set; }
        public float FontSize { get; set; }
        public string? Text { get; set; }
    }

    private sealed class MainCanvasItem
    {
        public string Key { get; init; } = "";
        public Rectangle Bounds { get; set; }
        public Image? Image { get; set; }
        public string Text { get; set; } = "";
        public float FontSize { get; set; }
        public FontStyle FontStyle { get; set; }
        public StringAlignment TextAlignment { get; set; } = StringAlignment.Center;
        public StringAlignment LineAlignment { get; set; } = StringAlignment.Center;
        public Action? ClickAction { get; set; }
        public Action<string>? DropAction { get; set; }
        public Color? FillColor { get; set; }
        public bool Visible { get; set; } = true;
        public bool IsText { get; init; }
        public bool HoverZoom { get; set; }
        public string? ToolTipText { get; set; }
    }

    private sealed class MainPageCanvas : Control
    {
        private readonly List<MainCanvasItem> _items = new();
        private readonly HashSet<MainCanvasItem> _selectedItems = new();
        private readonly Dictionary<MainCanvasItem, Rectangle> _dragStartBoundsByItem = new();
        private readonly Stack<Dictionary<string, LayoutRect>> _undoStack = new();
        private readonly Stack<Dictionary<string, LayoutRect>> _redoStack = new();
        private string _layoutFilePath;
        private readonly Func<float, FontStyle, Font> _fontFactory;
        private readonly Color _textColor;
        private readonly Color _handleColor = ThemePalette.FromArgb(0, 210, 255);
        private readonly ToolTip _toolTip = new()
        {
            InitialDelay = 350,
            ReshowDelay = 100,
            AutoPopDelay = 6000,
            ShowAlways = true
        };
        private string? _visibleToolTipKey;

        private MainCanvasItem? _selectedItem;
        private MainCanvasItem? _dragItem;
        private MainCanvasItem? _hoverZoomItem;
        private Point _dragMouseStart;
        private Rectangle _dragStartBounds;
        private float _dragStartFontSize;
        private LayoutResizeMode _resizeMode = LayoutResizeMode.None;
        private bool _mouseDragged;
        private const int HandleSize = 12;
        private const int SnapDistance = 8;
        private const int DesignCanvasWidth = 1625;
        private bool IsIllustratedCanvas => IllustratedTheme.Enabled && Path.GetFileName(_layoutFilePath).StartsWith("illustrated_",StringComparison.OrdinalIgnoreCase);
        private bool IsIllustratedHome => IllustratedTheme.Enabled && Path.GetFileName(_layoutFilePath) == "illustrated_layout.json";
        private int DesignCanvasHeight => IsIllustratedCanvas ? 1000 : 844;

        public bool LayoutEditMode { get; private set; }

        public void CompactThemeHome()
        {
            _items.RemoveAll(i => i.Bounds.X < 240);
            foreach (var item in _items)
                item.Bounds = new Rectangle((int)((item.Bounds.X - 240) * 1.17f), item.Bounds.Y, (int)(item.Bounds.Width * 1.17f), item.Bounds.Height);
        }

        public MainPageCanvas(string layoutFilePath, Color backgroundColor, Color textColor, Func<float, FontStyle, Font> fontFactory)
        {
            _layoutFilePath = layoutFilePath;
            _textColor = textColor;
            _fontFactory = fontFactory;

            BackColor = backgroundColor;
            DoubleBuffered = true;
            TabStop = true;
            AllowDrop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SizeChanged += (_,_) => { if(IsIllustratedHome) LayoutIllustratedHome(); };
        }

        public void LayoutIllustratedHome()
        {
            if(!IsIllustratedHome || Width<400 || Height<400) return;
            int margin=18, gap=18;
            int rightX=(int)(Width*.51), rightWidth=Width-rightX-margin;
            int cardWidth=(rightWidth-gap)/2;
            int modeTop=70, modeHeight=Math.Clamp(Height/5,140,190);
            int cardTop=modeTop+modeHeight+gap;
            int launchHeight=100, footerTop=Height-58;
            int launchTop=footerTop-gap-launchHeight;
            int cardHeight=launchTop-gap-cardTop;
            int logoSize=Math.Min(rightX-8,Height-72);
            void Put(string key,int x,int y,int w,int h,float font=0)
            {
                var item=_items.FirstOrDefault(i=>i.Key==key); if(item==null) return;
                item.Bounds=new Rectangle(x,y,Math.Max(1,w),Math.Max(1,h));
                if(font>0) item.FontSize=font;
            }
            Put("MainLogo",0,(Height-logoSize-35)/2,logoSize,logoSize);
            Put("ModeHeading",rightX,15,rightWidth,48,28);
            Put("MonitorButton",rightX,modeTop,cardWidth,modeHeight);
            Put("VRButton",rightX+cardWidth+gap,modeTop,cardWidth,modeHeight);
            Put("PlayButton",rightX,launchTop,rightWidth,launchHeight);
            int iconSize=Math.Max(90,Math.Min(cardWidth-42,cardHeight-118));
            for(int i=0;i<2;i++)
            {
                int x=rightX+i*(cardWidth+gap);
                Put(i==0?"NeckCard":"KeyCard",x,cardTop,cardWidth,cardHeight);
                Put(i==0?"NeckCardTitle":"KeyCardTitle",x+10,cardTop+15,cardWidth-20,48,22);
                Put(i==0?"NeckAssistButton":"HiddenKeybindsButton",x+(cardWidth-iconSize)/2,cardTop+64,iconSize,iconSize);
                Put(i==0?"NeckCardState":"KeyCardState",x+10,cardTop+cardHeight-51,cardWidth-20,40,22);
            }
            Put("VersionText",18,footerTop,rightX-30,40,18);
            int third=rightWidth/3;
            Put("DiscordButton",rightX,footerTop,third,40,18);
            Put("YoutubeButton",rightX+third,footerTop,third,40,18);
            Put("BeerButton",rightX+third*2,footerTop,third,40,18);
            Invalidate();
        }

        public void AddImage(string key, Image image, Rectangle bounds, Action? clickAction = null, Action<string>? dropAction = null, bool hoverZoom = false)
        {
            MainCanvasItem item = new MainCanvasItem
            {
                Key = key,
                Image = image,
                Bounds = bounds,
                ClickAction = clickAction,
                DropAction = dropAction,
                IsText = false,
                HoverZoom = hoverZoom
            };

            FixImageAspect(item);
            _items.Add(item);
        }

        public void AddRectangle(string key, Rectangle bounds, Color fillColor, Action? clickAction = null, Action<string>? dropAction = null)
        {
            _items.Add(new MainCanvasItem
            {
                Key = key,
                Bounds = bounds,
                FillColor = fillColor,
                ClickAction = clickAction,
                DropAction = dropAction,
                IsText = false
            });
        }

        public void AddText(
            string key,
            string text,
            Rectangle bounds,
            float fontSize,
            FontStyle fontStyle,
            StringAlignment textAlignment = StringAlignment.Center,
            StringAlignment lineAlignment = StringAlignment.Center,
            Action? clickAction = null,
            Action<string>? dropAction = null)
        {
            _items.Add(new MainCanvasItem
            {
                Key = key,
                Text = text,
                Bounds = bounds,
                FontSize = fontSize,
                FontStyle = fontStyle,
                TextAlignment = textAlignment,
                LineAlignment = lineAlignment,
                ClickAction = clickAction,
                DropAction = dropAction,
                IsText = true
            });
        }

        public void SetImage(string key, Image image)
        {
            MainCanvasItem? item = FindItem(key);
            if (item != null)
            {
                // Do not auto-correct aspect here. The layout editor/baked JSON owns the final size.
                // Calling FixImageAspect during state changes was overriding the user's saved scale.
                item.Image = image;
                Invalidate();
            }
        }

        public void SetText(string key, string text)
        {
            MainCanvasItem? item = FindItem(key);
            if (item != null)
            {
                item.Text = text;
                Invalidate();
            }
        }

        public void SetItemVisible(string key, bool visible)
        {
            MainCanvasItem? item = FindItem(key);
            if (item != null && item.Visible != visible)
            {
                item.Visible = visible;
                Invalidate();
            }
        }

        public void SetItemClickAction(string key, Action? clickAction)
        {
            MainCanvasItem? item = FindItem(key);
            if (item != null && !ReferenceEquals(item.ClickAction, clickAction))
            {
                item.ClickAction = clickAction;
                Invalidate();
            }
        }

        public void SetItemToolTip(string key, string toolTipText)
        {
            MainCanvasItem? item = FindItem(key);
            if (item != null)
            {
                item.ToolTipText = toolTipText;
            }
        }

        public void ClearItems()
        {
            _items.Clear();
            _selectedItems.Clear();
            _dragStartBoundsByItem.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            _selectedItem = null;
            _dragItem = null;
            _resizeMode = LayoutResizeMode.None;
            Invalidate();
        }

        public void SetLayoutFilePath(string layoutFilePath)
        {
            _layoutFilePath = layoutFilePath;
        }

        private MainCanvasItem? FindItem(string key)
        {
            return _items.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        private float GetCanvasScale()
        {
            if(IsIllustratedHome) return 1;
            if (Width <= 0 || Height <= 0)
            {
                return 1f;
            }

            float scaleX = Width / (float)DesignCanvasWidth;
            float scaleY = Height / (float)DesignCanvasHeight;
            return Math.Max(0.1f, Math.Min(scaleX, scaleY));
        }

        private PointF GetCanvasOffset(float scale)
        {
            if(IsIllustratedHome) return PointF.Empty;
            float drawnWidth = DesignCanvasWidth * scale;
            float drawnHeight = DesignCanvasHeight * scale;
            return new PointF((Width - drawnWidth) / 2f, (Height - drawnHeight) / 2f);
        }

        private Point ToDesignPoint(Point point)
        {
            float scale = GetCanvasScale();
            PointF offset = GetCanvasOffset(scale);

            int x = (int)Math.Round((point.X - offset.X) / scale);
            int y = (int)Math.Round((point.Y - offset.Y) / scale);

            return new Point(x, y);
        }

        public bool HandleKeyDown(KeyEventArgs e, Action showPage, Action<string> setTitle)
        {
            if (e.KeyCode == Keys.F12)
            {
                LayoutEditMode = !LayoutEditMode;
                Focus();
                Invalidate();

                if (LayoutEditMode)
                {
                    showPage();
                    setTitle("War Thunder VR Assistant  -  CANVAS EDIT MODE | Ctrl+Click multi-select | Drag | Snap | Ctrl+L/R/T/B align | Ctrl+E distribute | Alt resize = stretch | Ctrl+S save");
                }
                else
                {
                    SaveLayout();
                    setTitle("War Thunder VR Assistant");
                }

                return true;
            }

            if (!LayoutEditMode)
            {
                return false;
            }

            if (e.Control && e.KeyCode == Keys.Z)
            {
                if (e.Shift)
                {
                    RedoLayout();
                }
                else
                {
                    UndoLayout();
                }

                return true;
            }

            if (e.Control && e.KeyCode == Keys.Y)
            {
                RedoLayout();
                return true;
            }

            if (e.KeyCode == Keys.Escape)
            {
                SaveLayout();
                LayoutEditMode = false;
                setTitle("War Thunder VR Assistant");
                Invalidate();
                return true;
            }

            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveLayout();
                setTitle("War Thunder VR Assistant  -  LAYOUT SAVED");
                return true;
            }

            if (_selectedItem == null)
            {
                return true;
            }

            if (e.KeyCode == Keys.F2)
            {
                PushUndoSnapshot();
                EditSelectedText();
                return true;
            }

            if (e.Control && HandleAlignmentShortcut(e.KeyCode))
            {
                return true;
            }

            if (e.KeyCode == Keys.Home)
            {
                PushUndoSnapshot();
                BringSelectedToFront();
                SaveLayout();
                return true;
            }

            if (e.KeyCode == Keys.End)
            {
                PushUndoSnapshot();
                SendSelectedToBack();
                SaveLayout();
                return true;
            }

            if (e.KeyCode == Keys.PageUp)
            {
                PushUndoSnapshot();
                MoveSelectedLayer(1);
                SaveLayout();
                return true;
            }

            if (e.KeyCode == Keys.PageDown)
            {
                PushUndoSnapshot();
                MoveSelectedLayer(-1);
                SaveLayout();
                return true;
            }

            int step = e.Shift ? 10 : 2;

            if (e.Control && (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus))
            {
                PushUndoSnapshot();
                AdjustSelectedTextScale(step);
                return true;
            }

            if (e.Control && (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus))
            {
                PushUndoSnapshot();
                AdjustSelectedTextScale(-step);
                return true;
            }

            int dx = 0;
            int dy = 0;
            bool resize = false;
            Rectangle bounds = _selectedItem.Bounds;

            if (e.KeyCode == Keys.Left) dx = -step;
            else if (e.KeyCode == Keys.Right) dx = step;
            else if (e.KeyCode == Keys.Up) dy = -step;
            else if (e.KeyCode == Keys.Down) dy = step;
            else if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus)
            {
                bounds = GrowBounds(_selectedItem, step * 3);
                resize = true;
            }
            else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)
            {
                bounds = GrowBounds(_selectedItem, -step * 3);
                resize = true;
            }
            else
            {
                return false;
            }

            PushUndoSnapshot();

            if (resize)
            {
                SetItemBounds(_selectedItem, bounds, scaleTextWithBounds: true);
            }
            else
            {
                MoveSelectedItems(dx, dy);
            }

            return true;
        }

        private Dictionary<string, LayoutRect> CaptureLayoutSnapshot()
        {
            Dictionary<string, LayoutRect> snapshot = new Dictionary<string, LayoutRect>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _items.Count; i++)
            {
                MainCanvasItem item = _items[i];
                snapshot[item.Key] = new LayoutRect
                {
                    X = item.Bounds.X,
                    Y = item.Bounds.Y,
                    Width = item.Bounds.Width,
                    Height = item.Bounds.Height,
                    ZOrder = i,
                    FontSize = item.IsText ? item.FontSize : 0,
                    Text = item.IsText ? item.Text : null
                };
            }

            return snapshot;
        }

        private void PushUndoSnapshot()
        {
            _undoStack.Push(CaptureLayoutSnapshot());
            _redoStack.Clear();
        }

        private void UndoLayout()
        {
            if (_undoStack.Count == 0)
            {
                return;
            }

            _redoStack.Push(CaptureLayoutSnapshot());
            ApplyLayoutSnapshot(_undoStack.Pop());
        }

        private void RedoLayout()
        {
            if (_redoStack.Count == 0)
            {
                return;
            }

            _undoStack.Push(CaptureLayoutSnapshot());
            ApplyLayoutSnapshot(_redoStack.Pop());
        }

        private void ApplyLayoutSnapshot(Dictionary<string, LayoutRect> snapshot)
        {
            foreach (MainCanvasItem item in _items)
            {
                if (!snapshot.TryGetValue(item.Key, out LayoutRect? rect))
                {
                    continue;
                }

                item.Bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);

                if (item.IsText && rect.FontSize > 0)
                {
                    item.FontSize = rect.FontSize;
                }

                if (item.IsText && rect.Text != null)
                {
                    item.Text = rect.Text;
                }
            }

            _items.Sort((a, b) =>
            {
                int za = snapshot.TryGetValue(a.Key, out LayoutRect? ra) ? ra.ZOrder : _items.IndexOf(a);
                int zb = snapshot.TryGetValue(b.Key, out LayoutRect? rb) ? rb.ZOrder : _items.IndexOf(b);
                return za.CompareTo(zb);
            });

            Invalidate();
        }

        private bool HandleAlignmentShortcut(Keys key)
        {
            if (_selectedItems.Count < 2 || _selectedItem == null)
            {
                return false;
            }

            Rectangle reference = _selectedItem.Bounds;
            List<MainCanvasItem> selected = _selectedItems.Where(i => i.Visible).ToList();
            PushUndoSnapshot();

            if (key == Keys.L)
            {
                foreach (MainCanvasItem item in selected) item.Bounds = new Rectangle(reference.Left, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
            }
            else if (key == Keys.R)
            {
                foreach (MainCanvasItem item in selected) item.Bounds = new Rectangle(reference.Right - item.Bounds.Width, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
            }
            else if (key == Keys.T)
            {
                foreach (MainCanvasItem item in selected) item.Bounds = new Rectangle(item.Bounds.X, reference.Top, item.Bounds.Width, item.Bounds.Height);
            }
            else if (key == Keys.B)
            {
                foreach (MainCanvasItem item in selected) item.Bounds = new Rectangle(item.Bounds.X, reference.Bottom - item.Bounds.Height, item.Bounds.Width, item.Bounds.Height);
            }
            else if (key == Keys.H)
            {
                int centerX = reference.Left + reference.Width / 2;
                foreach (MainCanvasItem item in selected) item.Bounds = new Rectangle(centerX - item.Bounds.Width / 2, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
            }
            else if (key == Keys.V)
            {
                int centerY = reference.Top + reference.Height / 2;
                foreach (MainCanvasItem item in selected) item.Bounds = new Rectangle(item.Bounds.X, centerY - item.Bounds.Height / 2, item.Bounds.Width, item.Bounds.Height);
            }
            else if (key == Keys.E)
            {
                DistributeHorizontally(selected);
            }
            else
            {
                return false;
            }

            Invalidate();
            return true;
        }

        private void DistributeHorizontally(List<MainCanvasItem> selected)
        {
            if (selected.Count < 3)
            {
                return;
            }

            selected.Sort((a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
            int left = selected.First().Bounds.Left;
            int right = selected.Last().Bounds.Right;
            int totalWidth = selected.Sum(item => item.Bounds.Width);
            int gap = Math.Max(0, (right - left - totalWidth) / (selected.Count - 1));
            int x = left;

            foreach (MainCanvasItem item in selected)
            {
                item.Bounds = new Rectangle(x, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
                x += item.Bounds.Width + gap;
            }
        }

        public bool HandleMouseWheelFromForm(MouseEventArgs e)
        {
            if (!LayoutEditMode || _selectedItem == null)
            {
                return false;
            }

            ApplyMouseWheel(e.Delta);
            return true;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (LayoutEditMode && _selectedItem != null)
            {
                ApplyMouseWheel(e.Delta);
            }
        }

        private void ApplyMouseWheel(int delta)
        {
            if (_selectedItem == null)
            {
                return;
            }

            int step = ModifierKeys.HasFlag(Keys.Shift) ? 20 : 8;
            int amount = eDeltaToAmount(delta, step);

            PushUndoSnapshot();

            if (ModifierKeys.HasFlag(Keys.Control))
            {
                AdjustSelectedTextScale(amount > 0 ? 2 : -2);
                return;
            }

            SetItemBounds(_selectedItem, GrowBounds(_selectedItem, amount * 3), scaleTextWithBounds: true);
        }

        private int eDeltaToAmount(int delta, int step)
        {
            return delta > 0 ? step : -step;
        }

        private Rectangle GrowBounds(MainCanvasItem item, int amount)
        {
            Rectangle bounds = item.Bounds;
            int newWidth = Math.Max(20, bounds.Width + amount);

            if (ShouldPreserveAspect(item))
            {
                float aspect = GetAspectRatio(item, bounds);
                int newHeight = Math.Max(20, (int)Math.Round(newWidth / aspect));
                return new Rectangle(bounds.X, bounds.Y, newWidth, newHeight);
            }

            return new Rectangle(bounds.X, bounds.Y, newWidth, Math.Max(20, bounds.Height + amount));
        }

        private void SetItemBounds(MainCanvasItem item, Rectangle bounds, bool scaleTextWithBounds)
        {
            if (item.IsText && scaleTextWithBounds && item.Bounds.Height > 0 && item.Bounds.Width > 0)
            {
                float widthScale = (float)bounds.Width / item.Bounds.Width;
                float heightScale = (float)bounds.Height / item.Bounds.Height;
                float scale = Math.Min(widthScale, heightScale);

                if (scale > 0)
                {
                    item.FontSize = Math.Max(8, item.FontSize * scale);
                }
            }

            item.Bounds = bounds;
            Invalidate();
        }

        private void MoveSelectedItems(int dx, int dy)
        {
            foreach (MainCanvasItem item in SelectedOrPrimary())
            {
                item.Bounds = new Rectangle(item.Bounds.X + dx, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);
            }

            Invalidate();
        }

        private IEnumerable<MainCanvasItem> SelectedOrPrimary()
        {
            if (_selectedItems.Count > 0)
            {
                return _selectedItems;
            }

            return _selectedItem != null ? new[] { _selectedItem } : Array.Empty<MainCanvasItem>();
        }

        private void AdjustSelectedTextScale(int delta)
        {
            if (_selectedItem == null || !_selectedItem.IsText)
            {
                return;
            }

            _selectedItem.FontSize = Math.Max(8, _selectedItem.FontSize + delta);
            Invalidate();
        }

        private void EditSelectedText()
        {
            if (_selectedItem == null || !_selectedItem.IsText)
            {
                return;
            }

            using Form dialog = new Form
            {
                Text = AppText.T("en", "Canvas.EditText"),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 135),
                BackColor = ThemePalette.FromArgb(20, 26, 28),
                ForeColor = _textColor
            };

            TextBox textBox = new TextBox
            {
                Left = 16,
                Top = 20,
                Width = 388,
                Height = 34,
                Text = _selectedItem.Text,
                Font = _fontFactory(18, FontStyle.Regular)
            };

            Button okButton = new ThemeButton
            {
                Text = AppText.T("en", "Common.Ok"),
                DialogResult = DialogResult.OK,
                Left = 220,
                Top = 78,
                Width = 85,
                Height = 34
            };

            Button cancelButton = new ThemeButton
            {
                Text = AppText.T("en", "Common.Cancel"),
                DialogResult = DialogResult.Cancel,
                Left = 318,
                Top = 78,
                Width = 85,
                Height = 34
            };

            dialog.Controls.Add(textBox);
            dialog.Controls.Add(okButton);
            dialog.Controls.Add(cancelButton);
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _selectedItem.Text = textBox.Text;
                SaveLayout();
                Invalidate();
            }
        }

        private void MoveSelectedLayer(int direction)
        {
            if (_selectedItem == null)
            {
                return;
            }

            int index = _items.IndexOf(_selectedItem);
            if (index < 0)
            {
                return;
            }

            int newIndex = Math.Max(0, Math.Min(_items.Count - 1, index + direction));
            if (newIndex == index)
            {
                return;
            }

            _items.RemoveAt(index);
            _items.Insert(newIndex, _selectedItem);
            Invalidate();
        }

        private void BringSelectedToFront()
        {
            foreach (MainCanvasItem item in SelectedOrPrimary().ToList())
            {
                _items.Remove(item);
                _items.Add(item);
            }

            Invalidate();
        }

        private void SendSelectedToBack()
        {
            foreach (MainCanvasItem item in SelectedOrPrimary().Reverse().ToList())
            {
                _items.Remove(item);
                _items.Insert(0, item);
            }

            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();

            if (LayoutEditMode)
            {
                return;
            }

            Point designPoint = ToDesignPoint(e.Location);
            MainCanvasItem? item = HitTest(designPoint);
            item?.ClickAction?.Invoke();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (!LayoutEditMode || e.Button != MouseButtons.Left)
            {
                return;
            }

            Point designPoint = ToDesignPoint(e.Location);
            MainCanvasItem? hitItem = HitTest(designPoint);
            bool multiSelect = ModifierKeys.HasFlag(Keys.Control);

            if (hitItem == null)
            {
                if (!multiSelect)
                {
                    _selectedItems.Clear();
                    _selectedItem = null;
                }

                Invalidate();
                return;
            }

            if (multiSelect)
            {
                if (_selectedItems.Contains(hitItem))
                {
                    _selectedItems.Remove(hitItem);
                    if (ReferenceEquals(_selectedItem, hitItem))
                    {
                        _selectedItem = _selectedItems.LastOrDefault();
                    }
                }
                else
                {
                    _selectedItems.Add(hitItem);
                    _selectedItem = hitItem;
                }
            }
            else if (!_selectedItems.Contains(hitItem))
            {
                _selectedItems.Clear();
                _selectedItems.Add(hitItem);
                _selectedItem = hitItem;
            }
            else
            {
                _selectedItem = hitItem;
            }

            if (_selectedItem == null)
            {
                Invalidate();
                return;
            }

            PushUndoSnapshot();

            _dragItem = _selectedItem;
            _dragMouseStart = designPoint;
            _dragStartBounds = _selectedItem.Bounds;
            _dragStartFontSize = _selectedItem.FontSize;
            _resizeMode = GetResizeMode(_selectedItem.Bounds, designPoint);
            _mouseDragged = false;
            _dragStartBoundsByItem.Clear();

            foreach (MainCanvasItem item in _selectedItems)
            {
                _dragStartBoundsByItem[item] = item.Bounds;
            }

            Cursor = CursorForResizeMode(_resizeMode);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Point designPoint = ToDesignPoint(e.Location);

            if (!LayoutEditMode)
            {
                MainCanvasItem? hoverItem = HitTest(designPoint);
                MainCanvasItem? newHoverZoomItem = hoverItem?.HoverZoom == true ? hoverItem : null;

                if (!ReferenceEquals(_hoverZoomItem, newHoverZoomItem))
                {
                    _hoverZoomItem = newHoverZoomItem;
                    Invalidate();
                }

                bool showHandCursor =
                    hoverItem?.ClickAction != null &&
                    !hoverItem.Key.Equals("MainLogo", StringComparison.OrdinalIgnoreCase);

                string? toolTipKey = string.IsNullOrWhiteSpace(hoverItem?.ToolTipText) ? null : hoverItem.Key;
                if (!string.Equals(_visibleToolTipKey, toolTipKey, StringComparison.Ordinal))
                {
                    _visibleToolTipKey = toolTipKey;
                    _toolTip.SetToolTip(this, toolTipKey == null ? null : hoverItem!.ToolTipText);
                }

                Cursor = showHandCursor ? Cursors.Hand : Cursors.Default;
                return;
            }

            if (_dragItem == null || e.Button != MouseButtons.Left)
            {
                MainCanvasItem? hoverItem = HitTest(designPoint);
                Cursor = hoverItem == null ? Cursors.Default : CursorForResizeMode(GetResizeMode(hoverItem.Bounds, designPoint));
                return;
            }

            int dx = designPoint.X - _dragMouseStart.X;
            int dy = designPoint.Y - _dragMouseStart.Y;
            _mouseDragged = Math.Abs(dx) > 2 || Math.Abs(dy) > 2;

            if (_resizeMode == LayoutResizeMode.Move)
            {
                Rectangle proposedPrimary = new Rectangle(
                    _dragStartBounds.X + dx,
                    _dragStartBounds.Y + dy,
                    _dragStartBounds.Width,
                    _dragStartBounds.Height
                );

                Point snapDelta = CalculateSnapDelta(proposedPrimary);
                dx += snapDelta.X;
                dy += snapDelta.Y;

                foreach ((MainCanvasItem item, Rectangle startBounds) in _dragStartBoundsByItem)
                {
                    item.Bounds = new Rectangle(startBounds.X + dx, startBounds.Y + dy, startBounds.Width, startBounds.Height);
                }
            }
            else
            {
                Rectangle resizedBounds = ResizeBoundsForItem(_dragItem, _dragStartBounds, dx, dy, _resizeMode);
                _dragItem.Bounds = resizedBounds;

                if (_dragItem.IsText && _dragStartBounds.Height > 0 && _dragStartBounds.Width > 0)
                {
                    float widthScale = (float)resizedBounds.Width / _dragStartBounds.Width;
                    float heightScale = (float)resizedBounds.Height / _dragStartBounds.Height;
                    float scale = Math.Min(widthScale, heightScale);

                    if (scale > 0)
                    {
                        _dragItem.FontSize = Math.Max(8, _dragStartFontSize * scale);
                    }
                }
            }

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoverZoomItem != null)
            {
                _hoverZoomItem = null;
                Invalidate();
            }

            _visibleToolTipKey = null;
            _toolTip.SetToolTip(this, null);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (!LayoutEditMode)
            {
                return;
            }

            Point designPoint = ToDesignPoint(e.Location);
            MainCanvasItem? item = HitTest(designPoint);
            if (item != null && item.IsText)
            {
                _selectedItems.Clear();
                _selectedItems.Add(item);
                _selectedItem = item;
                EditSelectedText();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!LayoutEditMode)
            {
                return;
            }

            _dragItem = null;
            _resizeMode = LayoutResizeMode.None;
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            UpdateDragDropEffect(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            UpdateDragDropEffect(e);
        }

        private void UpdateDragDropEffect(DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length == 0 || !files[0].EndsWith(".blk", StringComparison.OrdinalIgnoreCase))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            Point point = PointToClient(new Point(e.X, e.Y));
            MainCanvasItem? item = HitTestDroppable(ToDesignPoint(point));
            e.Effect = item?.DropAction != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);

            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length == 0 || !files[0].EndsWith(".blk", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Point point = PointToClient(new Point(e.X, e.Y));
            MainCanvasItem? item = HitTestDroppable(ToDesignPoint(point));
            item?.DropAction?.Invoke(files[0]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = GetCanvasScale();
            PointF offset = GetCanvasOffset(scale);

            GraphicsState state = e.Graphics.Save();
            e.Graphics.TranslateTransform(offset.X, offset.Y);
            e.Graphics.ScaleTransform(scale, scale);

            foreach (MainCanvasItem item in _items)
            {
                if (!item.Visible)
                {
                    continue;
                }

                if (item.FillColor.HasValue)
                {
                    using Brush fillBrush = new SolidBrush(item.FillColor.Value);
                    e.Graphics.FillRectangle(fillBrush, item.Bounds);
                    if (IllustratedTheme.Enabled &&
                        (item.Key.EndsWith("Card", StringComparison.Ordinal) ||
                         item.Key.EndsWith("ButtonBg", StringComparison.Ordinal)))
                    {
                        IllustratedTheme.DrawFrame(e.Graphics, Rectangle.Inflate(item.Bounds, -2, -2), false);
                    }
                }
                else if (item.IsText)
                {
                    if (IllustratedTheme.Enabled && item.Key is "NeckCardState" or "KeyCardState")
                    {
                        bool enabled = item.Text == "Enabled";
                        Rectangle r=item.Bounds;
                        IllustratedTheme.DrawSwitch(e.Graphics,new Rectangle(r.X+r.Width/2-92,r.Y+7,55,28),enabled);
                        using Font statusFont=_fontFactory(26,FontStyle.Bold);
                        using Brush ink=new SolidBrush(IllustratedTheme.Ivory);
                        e.Graphics.DrawString(item.Text,statusFont,ink,new Rectangle(r.X+r.Width/2-20,r.Y+4,115,r.Height));
                        continue;
                    }
                    // The graphics transform already scales the font with the canvas.
                    // A former inverse-scale minimum kept text physically large in a
                    // small window, which made it escape its design rectangle.
                    float fontSize = item.FontSize;
                    using Font font = _fontFactory(fontSize, item.FontStyle);
                    using Brush brush = new SolidBrush(_textColor);
                    using StringFormat format = new StringFormat
                    {
                        Alignment = item.TextAlignment,
                        LineAlignment = item.LineAlignment
                    };

                    e.Graphics.DrawString(item.Text, font, brush, item.Bounds, format);
                }
                else if (item.Image != null)
                {
                    Rectangle drawBounds = item.Bounds;

                    if (!LayoutEditMode && ReferenceEquals(item, _hoverZoomItem))
                    {
                        drawBounds = GetHoverZoomBounds(item.Bounds);
                    }

                    if (!IllustratedTheme.DrawAsset(e.Graphics,item.Image,drawBounds)) e.Graphics.DrawImage(item.Image, drawBounds);
                }

                if (LayoutEditMode)
                {
                    DrawEditBorder(e.Graphics, item, _selectedItems.Contains(item), ReferenceEquals(item, _selectedItem));
                }
            }

            e.Graphics.Restore(state);
        }

        private Rectangle GetHoverZoomBounds(Rectangle bounds)
        {
            const float zoom = 1.10f;

            int newWidth = Math.Max(1, (int)Math.Round(bounds.Width * zoom));
            int newHeight = Math.Max(1, (int)Math.Round(bounds.Height * zoom));
            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;

            return new Rectangle(
                centerX - newWidth / 2,
                centerY - newHeight / 2,
                newWidth,
                newHeight);
        }

        private void DrawEditBorder(Graphics graphics, MainCanvasItem item, bool selected, bool primary)
        {
            using Pen pen = new Pen(primary ? Color.Yellow : selected ? _handleColor : ThemePalette.FromArgb(70, 0, 210, 255), primary ? 2 : 1);
            graphics.DrawRectangle(pen, item.Bounds);

            if (!primary)
            {
                return;
            }

            using Brush brush = new SolidBrush(_handleColor);
            int size = HandleSize;
            graphics.FillRectangle(brush, item.Bounds.Left, item.Bounds.Top, size, size);
            graphics.FillRectangle(brush, item.Bounds.Right - size, item.Bounds.Top, size, size);
            graphics.FillRectangle(brush, item.Bounds.Left, item.Bounds.Bottom - size, size, size);
            graphics.FillRectangle(brush, item.Bounds.Right - size, item.Bounds.Bottom - size, size, size);
        }

        private MainCanvasItem? HitTest(Point point)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Visible && _items[i].Bounds.Contains(point))
                {
                    return _items[i];
                }
            }

            return null;
        }

        private MainCanvasItem? HitTestDroppable(Point point)
        {
            // Drag/drop should work on the whole visual field, even if a non-drop visual layer
            // such as text, a label, or a button is above the actual rectangle.
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                MainCanvasItem item = _items[i];
                if (item.Visible && item.DropAction != null && item.Bounds.Contains(point))
                {
                    return item;
                }
            }

            return null;
        }

        private Point CalculateSnapDelta(Rectangle movingBounds)
        {
            int bestDx = 0;
            int bestDy = 0;
            int bestXDistance = SnapDistance + 1;
            int bestYDistance = SnapDistance + 1;

            List<int> movingXs = new List<int> { movingBounds.Left, movingBounds.Left + movingBounds.Width / 2, movingBounds.Right };
            List<int> movingYs = new List<int> { movingBounds.Top, movingBounds.Top + movingBounds.Height / 2, movingBounds.Bottom };
            List<int> targetXs = new List<int> { 0, DesignCanvasWidth / 2, DesignCanvasWidth };
            List<int> targetYs = new List<int> { 0, DesignCanvasHeight / 2, DesignCanvasHeight };

            foreach (MainCanvasItem item in _items)
            {
                if (!item.Visible || _selectedItems.Contains(item))
                {
                    continue;
                }

                targetXs.Add(item.Bounds.Left);
                targetXs.Add(item.Bounds.Left + item.Bounds.Width / 2);
                targetXs.Add(item.Bounds.Right);
                targetYs.Add(item.Bounds.Top);
                targetYs.Add(item.Bounds.Top + item.Bounds.Height / 2);
                targetYs.Add(item.Bounds.Bottom);
            }

            foreach (int movingX in movingXs)
            {
                foreach (int targetX in targetXs)
                {
                    int distance = Math.Abs(targetX - movingX);
                    if (distance <= SnapDistance && distance < bestXDistance)
                    {
                        bestXDistance = distance;
                        bestDx = targetX - movingX;
                    }
                }
            }

            foreach (int movingY in movingYs)
            {
                foreach (int targetY in targetYs)
                {
                    int distance = Math.Abs(targetY - movingY);
                    if (distance <= SnapDistance && distance < bestYDistance)
                    {
                        bestYDistance = distance;
                        bestDy = targetY - movingY;
                    }
                }
            }

            return new Point(bestDx, bestDy);
        }

        private LayoutResizeMode GetResizeMode(Rectangle bounds, Point point)
        {
            int size = HandleSize + 4;
            bool left = point.X <= bounds.Left + size;
            bool right = point.X >= bounds.Right - size;
            bool top = point.Y <= bounds.Top + size;
            bool bottom = point.Y >= bounds.Bottom - size;

            if (left && top) return LayoutResizeMode.TopLeft;
            if (right && top) return LayoutResizeMode.TopRight;
            if (left && bottom) return LayoutResizeMode.BottomLeft;
            if (right && bottom) return LayoutResizeMode.BottomRight;
            return LayoutResizeMode.Move;
        }

        private Cursor CursorForResizeMode(LayoutResizeMode mode)
        {
            return mode switch
            {
                LayoutResizeMode.TopLeft => Cursors.SizeNWSE,
                LayoutResizeMode.BottomRight => Cursors.SizeNWSE,
                LayoutResizeMode.TopRight => Cursors.SizeNESW,
                LayoutResizeMode.BottomLeft => Cursors.SizeNESW,
                _ => Cursors.SizeAll
            };
        }

        private Rectangle ResizeBoundsForItem(MainCanvasItem item, Rectangle startBounds, int dx, int dy, LayoutResizeMode mode)
        {
            Rectangle bounds = ResizeBounds(startBounds, dx, dy, mode, keepAspect: false);

            if (!ShouldPreserveAspect(item))
            {
                return bounds;
            }

            float aspect = GetAspectRatio(item, startBounds);
            int width = Math.Max(20, bounds.Width);
            int height = Math.Max(20, (int)Math.Round(width / aspect));
            int x = bounds.X;
            int y = bounds.Y;

            if (mode is LayoutResizeMode.TopLeft or LayoutResizeMode.TopRight)
            {
                y = startBounds.Bottom - height;
            }

            if (mode is LayoutResizeMode.TopLeft or LayoutResizeMode.BottomLeft)
            {
                x = startBounds.Right - width;
            }

            return new Rectangle(x, y, width, height);
        }

        private bool ShouldPreserveAspect(MainCanvasItem item)
        {
            if (ModifierKeys.HasFlag(Keys.Alt))
            {
                return false;
            }

            return item.Image != null || ModifierKeys.HasFlag(Keys.Shift);
        }

        private float GetAspectRatio(MainCanvasItem item, Rectangle fallbackBounds)
        {
            if (item.Image != null && item.Image.Height > 0)
            {
                return Math.Max(0.01f, (float)item.Image.Width / item.Image.Height);
            }

            return fallbackBounds.Height > 0 ? Math.Max(0.01f, (float)fallbackBounds.Width / fallbackBounds.Height) : 1f;
        }

        private void FixImageAspect(MainCanvasItem item)
        {
            if (item.Image == null || item.Image.Height <= 0)
            {
                return;
            }

            float aspect = GetAspectRatio(item, item.Bounds);
            int height = Math.Max(20, (int)Math.Round(item.Bounds.Width / aspect));
            item.Bounds = new Rectangle(item.Bounds.X, item.Bounds.Y, item.Bounds.Width, height);
        }

        private Rectangle ResizeBounds(Rectangle startBounds, int dx, int dy, LayoutResizeMode mode, bool keepAspect)
        {
            Rectangle bounds = startBounds;

            switch (mode)
            {
                case LayoutResizeMode.TopLeft:
                    bounds.X += dx; bounds.Y += dy; bounds.Width -= dx; bounds.Height -= dy; break;
                case LayoutResizeMode.TopRight:
                    bounds.Y += dy; bounds.Width += dx; bounds.Height -= dy; break;
                case LayoutResizeMode.BottomLeft:
                    bounds.X += dx; bounds.Width -= dx; bounds.Height += dy; break;
                case LayoutResizeMode.BottomRight:
                    bounds.Width += dx; bounds.Height += dy; break;
            }

            if (keepAspect && startBounds.Height > 0)
            {
                float ratio = (float)startBounds.Width / startBounds.Height;
                bounds.Height = Math.Max(20, bounds.Height);
                bounds.Width = Math.Max(20, (int)Math.Round(bounds.Height * ratio));
            }

            bounds.Width = Math.Max(20, bounds.Width);
            bounds.Height = Math.Max(20, bounds.Height);
            return bounds;
        }

        public void ApplyLayout(Dictionary<string, LayoutRect> layout)
        {
            try
            {
                foreach (MainCanvasItem item in _items)
                {
                    if (!layout.TryGetValue(item.Key, out LayoutRect? rect))
                    {
                        continue;
                    }

                    item.Bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);

                    if (item.IsText && rect.FontSize > 0)
                    {
                        item.FontSize = rect.FontSize;
                    }

                    if (item.IsText && rect.Text != null)
                    {
                        item.Text = rect.Text;
                    }
                }

                // Capture ordering before sorting: IndexOf inside a sort comparer
                // changes as the sort moves items and can cover foreground artwork.
                var ordered = _items.Select((item,index) => new { item,index })
                    .OrderBy(x => IsIllustratedCanvas
                        ? (x.item.FillColor.HasValue ? 0 : 1)
                        : layout.TryGetValue(x.item.Key,out LayoutRect? rect) ? rect.ZOrder : x.index)
                    .ThenBy(x => x.index).Select(x => x.item).ToList();
                _items.Clear();
                _items.AddRange(ordered);

                Invalidate();
            }
            catch
            {
                // Ignore bad baked layout data and keep default layout.
            }
        }

        public void LoadLayout()
        {
            try
            {
                if (!File.Exists(_layoutFilePath))
                {
                    return;
                }

                string json = File.ReadAllText(_layoutFilePath);
                Dictionary<string, LayoutRect>? layout = JsonSerializer.Deserialize<Dictionary<string, LayoutRect>>(json);

                if (layout == null)
                {
                    return;
                }

                foreach (MainCanvasItem item in _items)
                {
                    if (!layout.TryGetValue(item.Key, out LayoutRect? rect))
                    {
                        continue;
                    }

                    item.Bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);

                    if (item.IsText && rect.FontSize > 0)
                    {
                        item.FontSize = rect.FontSize;
                    }

                    if (item.IsText && rect.Text != null)
                    {
                        item.Text = rect.Text;
                    }
                }

                _items.Sort((a, b) =>
                {
                    int za = layout.TryGetValue(a.Key, out LayoutRect? ra) ? ra.ZOrder : _items.IndexOf(a);
                    int zb = layout.TryGetValue(b.Key, out LayoutRect? rb) ? rb.ZOrder : _items.IndexOf(b);
                    return za.CompareTo(zb);
                });

                Invalidate();
            }
            catch
            {
                // Ignore bad layout data and keep default layout.
            }
        }

        public void SaveLayout()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_layoutFilePath) ?? AppContext.BaseDirectory);

                Dictionary<string, LayoutRect> layout = new Dictionary<string, LayoutRect>(StringComparer.OrdinalIgnoreCase);

                if (File.Exists(_layoutFilePath))
                {
                    try
                    {
                        string existingJson = File.ReadAllText(_layoutFilePath);
                        Dictionary<string, LayoutRect>? existingLayout = JsonSerializer.Deserialize<Dictionary<string, LayoutRect>>(existingJson);
                        if (existingLayout != null)
                        {
                            layout = new Dictionary<string, LayoutRect>(existingLayout, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                    catch
                    {
                        layout = new Dictionary<string, LayoutRect>(StringComparer.OrdinalIgnoreCase);
                    }
                }

                for (int i = 0; i < _items.Count; i++)
                {
                    MainCanvasItem item = _items[i];
                    layout[item.Key] = new LayoutRect
                    {
                        X = item.Bounds.X,
                        Y = item.Bounds.Y,
                        Width = item.Bounds.Width,
                        Height = item.Bounds.Height,
                        ZOrder = i,
                        FontSize = item.IsText ? item.FontSize : 0,
                        Text = item.IsText ? item.Text : null
                    };
                }

                string json = JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_layoutFilePath, json);
            }
            catch
            {
                // Saving the layout should never crash the app.
            }
        }
    }
    private readonly Color _backgroundColor = ThemePalette.FromArgb(5, 12, 14);
    private readonly Color _fieldColor = ThemePalette.FromArgb(0, 8, 9);
    private readonly Color _textColor = ThemePalette.FromArgb(235, 238, 240);

    private Panel _mainPanel = null!;
    private Panel _settingsPanel = null!;
    private Panel _aboutPanel = null!;
    private Panel _presetPanel = null!;
    private Panel _customVrPanel = null!;

    private MainPageCanvas _mainCanvas = null!;
    private MainPageCanvas _settingsCanvas = null!;
    private MainPageCanvas _aboutCanvas = null!;
    private MainPageCanvas _recommendedCanvas = null!;
    private Panel _recommendedPanel = null!;

    private string _recommendedSoftwarePrefix = "VD";
    private Image? _recommendedSoftwareIcon;
    private int _recommendedPage = 1;


    private readonly Dictionary<string, Control> _layoutTargets = new(StringComparer.OrdinalIgnoreCase);
    private bool _layoutEditMode = false;
    private const int LayoutHandleSize = 12;

    private PictureBox _configBrowseButton = null!;
    private PictureBox _desktopBrowseButton = null!;
    private PictureBox _customVrBrowseButton = null!;

    private PictureBox _lowButton = null!;
    private PictureBox _mediumButton = null!;
    private PictureBox _highButton = null!;
    private PictureBox _customVrToggleButton = null!;

    private TextBox _configPathBox = null!;
    private TextBox _desktopPathBox = null!;
    private TextBox _customVrPathBox = null!;

    private string _configBlkPath = "";
    private string _desktopBlkPath = "";
    private string _customVrBlkPath = "";
    private string _machineBlkPath = "";
    private string _desktopControlsBlkPath = "";
    private string _vrControlsBlkPath = "";
    private string _warThunderExePath = "";
    private GraphicsApi _desktopGraphicsApi = GraphicsApi.DX12;
    private GraphicsApi _vrGraphicsApi = GraphicsApi.DX12;
    private bool _switchControlsWithProfile;
    private readonly System.Windows.Forms.Timer _controlsReapplyTimer = new System.Windows.Forms.Timer();
    private readonly System.Windows.Forms.Timer _gameRunningStateTimer = new System.Windows.Forms.Timer();
    private bool _lastKnownGameRunning;
    private string _pendingControlsPresetPath = "";
    private string _pendingControlsProfileName = "";
    private bool _updateCheckInProgress;
    private bool _updateDeferredForGame;
    private DateTime _nextAutomaticUpdateCheckUtc = DateTime.MaxValue;
    private string _latestReleaseUrl = GitHubReleasesUrl;
    private NeckAssistForm? _neckAssistForm;
    private HiddenKeybindsForm? _hiddenKeybindsForm;
    private Image? _neckAssistImage;
    private Image? _neckAssistActiveImage;
    private Image? _hiddenKeybindsImage;
    private Image? _hiddenKeybindsActiveImage;

    private ComboBox? _desktopGraphicsApiCombo;
    private ComboBox? _vrGraphicsApiCombo;
    private Label? _desktopGraphicsApiLabel;
    private Label? _vrGraphicsApiLabel;
    private ComboBox? _desktopControlsCombo;
    private ComboBox? _vrControlsCombo;
    private Button? _desktopControlsAddButton;
    private Button? _vrControlsAddButton;
    private Button? _desktopControlsRemoveButton;
    private Button? _vrControlsRemoveButton;
    private Label? _desktopControlsLabel;
    private Label? _vrControlsLabel;
    private readonly List<ControlProfileEntry> _desktopControlProfiles = new();
    private readonly List<ControlProfileEntry> _vrControlProfiles = new();
    private string _selectedDesktopControlProfileId = "";
    private string _selectedVrControlProfileId = "";
    private bool _syncingControlProfileCombos;

    private readonly Keys[] _secretCodeSequence =
    {
        Keys.Up,
        Keys.Up,
        Keys.Down,
        Keys.Down,
        Keys.Left,
        Keys.Right,
        Keys.Left,
        Keys.Right
    };

    private readonly Keys[] _promoSecretCodeSequence =
    {
        Keys.Left,
        Keys.Left,
        Keys.Right,
        Keys.Right,
        Keys.Up,
        Keys.Down
    };

    private int _secretCodeIndex;
    private int _promoSecretCodeIndex;
    private bool _secretCodeArmed;
    private bool _promoSecretRunning;
    private Size _lastNormalClientSize;

    private bool _customVrEnabled;
    private bool _showHighWarning = true;
    private bool _useBetaBuilds;
    private bool _minimizeToTray;
    private bool _startWithWindows;
    private bool _showHomeAircraft;
    private bool _showHomeKeybinds;
    private bool _startMinimizedToTray;
    private bool _useTestServer;
    private string _languageCode = "en";
    private string _uiTheme = AppThemeAssets.DefaultTheme;
    private bool _allowExit;
    private NotifyIcon? _trayIcon;
    private AviationHomePage? _aviationHome;

    private VrPreset _selectedVrPreset = VrPreset.None;
    private AppliedMode _lastAppliedMode = AppliedMode.None;

    private Image _mainLogo = null!;
    private Image _infoImage = null!;
    private Image _settingsImage = null!;
    private Image _homeImage = null!;

    private Image _vrRed = null!;
    private Image _vrOrange = null!;
    private Image _vrGreen = null!;

    private Image _monitorRed = null!;
    private Image _monitorOrange = null!;
    private Image _monitorGreen = null!;

    private Image _playOn = null!;
    private Image _playOff = null!;
    private Image _updateGreen = null!;
    private Image _updateYellow = null!;

    private Image _browseRed = null!;
    private Image _browseGreen = null!;

    private Image _captureSettingsImage = null!;
    private Image _captureSettingsGrayImage = null!;
    private Image _removeImage = null!;
    private Image _removeGrayImage = null!;

    private Image _buttonOff = null!;
    private Image _buttonOn = null!;

    private Image _lowRed = null!;
    private Image _lowGreen = null!;
    private Image _mediumRed = null!;
    private Image _mediumGreen = null!;
    private Image _highRed = null!;
    private Image _highGreen = null!;

    private Image _nvidiaProfileInspector = null!;

    private Image? _youtubeImage;
    private Image? _beerImage;
    private Image? _discordImage;
    private Image? _recommendedSettingsImage;
    private Image? _helpImage;
    private Image? _verticalLineImage;

    private Image? _vdImage;
    private Image? _metaImage;
    private Image? _steamVrSoftwareImage;
    private Image? _backArrowImage;
    private Image? _backButtonImage;
    private Image? _backGrayButtonImage;
    private Image? _nextButtonImage;
    private Image? _nextGrayButtonImage;
    private Image? _snailImage;
    private Image? _diplomaImage;

    private static string AppFolder =>
        AppContext.BaseDirectory;

    private static string SettingsFolder =>
        Path.Combine(AppFolder, "Settings");

    private static string GraphicSettingsFolder =>
        Path.Combine(AppFolder, "GraphicSettings");

    private static string StateFilePath =>
        Path.Combine(SettingsFolder, "settings.json");

    private static string LayoutFilePath =>
        Path.Combine(SettingsFolder, "layout.json");

    private static string AboutLayoutFilePath =>
        Path.Combine(SettingsFolder, "about_layout.json");

    private static string SettingsLayoutFilePath =>
        Path.Combine(SettingsFolder, "settings_layout.json");

    private static string RecommendedLayoutFilePath =>
        Path.Combine(SettingsFolder, "recommended_layout.json");

    private static string RecommendedSelectionLayoutFilePath =>
        Path.Combine(SettingsFolder, "recommended_selection_layout_v3.json");

    private static string RecommendedViewerLayoutFilePath =>
        Path.Combine(SettingsFolder, "recommended_viewer_layout.json");

    private static Dictionary<string, LayoutRect> ParseBakedLayout(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, LayoutRect>>(json) ??
               new Dictionary<string, LayoutRect>(StringComparer.OrdinalIgnoreCase);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            if (IllustratedTheme.Enabled)
            {
                // Paint child controls through a single composited buffer. This
                // prevents the dark themed shell from flashing black when clicks
                // trigger several transparent controls to redraw at once.
                // Child containers are buffered individually by BufferedUi.
                // Whole-window composition delays native/WebView2 repainting.
            }
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!IllustratedTheme.Enabled)
        {
            base.OnPaintBackground(e);
            return;
        }

        using Brush background = new SolidBrush(_backgroundColor);
        e.Graphics.FillRectangle(background, ClientRectangle);
    }

    public MainForm()
    {
        AutoScaleMode = AutoScaleMode.None;

        Text = "War Thunder VR Assistant";
        HandleCreated += (_,_) => IllustratedTheme.ApplyWindowChrome(this);
        // 1.8.1 could start at 1300x675 and then expose scrollbars at common
        // Windows DPI settings. The illustrated dashboard is designed around a
        // wider 16:9-ish canvas and scales down itself when necessary.
        ClientSize = new Size(S(1600), S(900));
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(S(1120), S(630));
        BackColor = _backgroundColor;
        DoubleBuffered = true;
        BufferedUi.Attach(this);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;
        MouseWheel += MainForm_MouseWheel;
        Resize += MainForm_Resize;
        Move += (_, _) => PositionNeckAssistPanel();
        ClientSizeChanged += (_, _) => PositionNeckAssistPanel();
        ResizeEnd += (_, _) => SaveState();
        FormClosing += (_, e) =>
        {
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing && _minimizeToTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
        _revisionCancellation.Cancel();
            _gameRunningStateTimer.Stop();
            _vtrimForm?.Close();
        _neckAssistForm?.Close();
            _hiddenKeybindsForm?.Close();
            SaveState();
        };

        _controlsReapplyTimer.Interval = 1000;
        _controlsReapplyTimer.Tick += (_, _) => ReapplyPendingControlsAfterGameExit();
        _gameRunningStateTimer.Interval = 1000;
        _gameRunningStateTimer.Tick += (_, _) =>
        {
            RefreshGameRunningVisualState();
            if (_updateDeferredForGame && !_updateCheckInProgress && !IsWarThunderRunningOrStarting())
            {
                _updateDeferredForGame = false;
                _ = CheckForUpdatesAsync(false);
            }
            if (!_updateCheckInProgress && DateTime.UtcNow >= _nextAutomaticUpdateCheckUtc)
                _ = CheckForUpdatesAsync(false);
        };
        _gameRunningStateTimer.Start();

        AppUpgradeMigration.Run(SettingsFolder);
        LoadState();
        AppThemeAssets.SetActiveTheme(_uiTheme);
        LoadAssets();
        LoadGameInstallations();
        ConfigureTrayIcon();
        _lastNormalClientSize = ClientSize;

        BuildMainScreen();
        BuildSettingsScreen();
        BuildAboutScreen();
        BuildRecommendedScreen();

        Shown += async (_, _) =>
        {
            if (IsNeckAssistSavedEnabled())
            {
                ToggleNeckAssistPanel();
                _neckAssistForm?.Hide();
                ShowScreen(_mainPanel);
            }
            if (_startMinimizedToTray && Environment.GetCommandLineArgs().Contains("--startup", StringComparer.OrdinalIgnoreCase)) HideToTray();
            await CheckForUpdatesAsync(false);
        };

        if (LoadExternalLayoutFiles)
        {
            LoadLayout();
        }

        if (IllustratedTheme.Enabled) BuildThemeNavigation();
        InitializeRevisionServices();
        // Create the KeyBind page once at startup so Advanced Switch Bindings can
        // poll physical switches even when the user is on Home/Neck/VTrim.
        if (_hiddenKeybindsForm == null || _hiddenKeybindsForm.IsDisposed)
        {
            ToggleHiddenKeybindsPanel();
            _hiddenKeybindsForm?.Hide();
        }
        ShowScreen(_mainPanel);
        ApplyLocalization();
        UpdateVisualStates();
    }

    private sealed class ControlProfileEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";

        public override string ToString() => Name;
    }

    private sealed class ControlProfileComboItem
    {
        public ControlProfileComboItem(ControlProfileEntry? profile, string text)
        {
            Profile = profile;
            Text = text;
        }

        public ControlProfileEntry? Profile { get; }
        public string Text { get; }
        public override string ToString() => Text;
    }

    private string T(string key) => AppText.T(_languageCode, key);

    private string TF(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    private void ApplyLocalization()
    {
        _languageCode = AppText.Normalize(_languageCode);
        Text = "War Thunder VR Assistant";
        _aviationHome?.SetLanguage(_languageCode);
        if (_themeNavigation != null)
        {
            foreach (IllustratedNavButton button in _themeNavigation.Controls.OfType<IllustratedNavButton>())
            {
                button.Text = button.PageKey switch
                {
                    "home" => T("Nav.Home"),
                    "profiles" => T("Nav.Profiles"),
                    "neck" => T("Nav.Neck"),
                    "keybind" => T("Nav.Keybind"),
                    "vtrim" => T("Nav.VTrim"),
                    "vtrim-profiles" => T("Nav.Profiles"),
                    "gameupdates" => T("Nav.GameUpdates"),
                    "options" => T("Nav.Options"),
                    "info" => T("Nav.Info"),
                    "updates" => T("Nav.Updates"),
                    _ => button.Text
                };
                button.AccessibleName = button.Text;
                button.Invalidate();
            }
        }
        ApplyCanvasLocalization();
        foreach (AssistantInfoPage info in _aboutPanel?.Controls.OfType<AssistantInfoPage>() ?? Enumerable.Empty<AssistantInfoPage>())
            info.SetLanguage(_languageCode);
        _neckAssistForm?.ApplyLanguage(_languageCode);
        _hiddenKeybindsForm?.ApplyLanguage(_languageCode);
        _vtrimForm?.ApplyLanguage(_languageCode);
        Invalidate();
    }

    private void ApplyCanvasLocalization()
    {
        if (_settingsCanvas != null)
        {
            _settingsCanvas.SetText("SettingsTip", T("Profiles.Tip"));
            _settingsCanvas.SetText("ConfigTitle", T("Profiles.FolderTitle"));
            _settingsCanvas.SetText("ConfigDescription", T("Profiles.FolderDesc"));
            _settingsCanvas.SetText("DesktopTitle", T("Profiles.MonitorTitle"));
            _settingsCanvas.SetText("DesktopDescription", T("Profiles.MonitorDesc"));
            _settingsCanvas.SetText("PresetTitle", T("Profiles.VrPresets"));
            _settingsCanvas.SetText("CustomToggleTitle", T("Profiles.CustomVr"));
            _settingsCanvas.SetText("CustomVrTitle", T("Profiles.CustomVrProfile"));
            _settingsCanvas.SetText("CustomVrDescription", T("Profiles.CustomVrDesc"));
            _settingsCanvas.SetText("ControlsProfilesButton", T("Profiles.Controls"));
            _settingsCanvas.SetText("ControlsProfilesDescription", T("Profiles.ControlsDesc"));
            _settingsCanvas.SetText("MoreInfoButton", T("Profiles.Recommended"));
            _settingsCanvas.SetText("ProfilesHeading", T("Profiles.Heading"));
            _settingsCanvas.SetText("LowButton", T("Profiles.Low"));
            _settingsCanvas.SetText("MediumButton", T("Profiles.Medium"));
            _settingsCanvas.SetText("HighButton", T("Profiles.High"));
        }
        if (_aboutCanvas != null)
        {
            _aboutCanvas.SetText("AboutTitle", T("Info.Title"));
            _aboutCanvas.SetText("AboutPurpose", T("Info.Purpose"));
            _aboutCanvas.SetText("HowToTitle", T("Info.HowTitle"));
            _aboutCanvas.SetText("HowToText", T("Info.HowText"));
            _aboutCanvas.SetText("PatchNotesTitle", TF("Info.ChangesTitle", CurrentVersion));
            _aboutCanvas.SetText("PatchNotesText", T("Info.ChangesText"));
            _aboutCanvas.SetText("OpenSourceText", T("Info.OpenSource"));
        }
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (!_fullScreen && WindowState == FormWindowState.Normal)
        {
            _lastNormalClientSize = ClientSize;
        }

        PositionGraphicsApiControls();
        // Transparent illustrated assets and owner-drawn frames must be repainted
        // as one scene while the native window is being resized.  Without this,
        // WinForms can leave copies of old frame edges behind during live sizing.
        Invalidate(true);
    }

    protected override void WndProc(ref Message m)
    {
        // Manual resizing keeps the main window at the same 16:9 composition used
        // by the Home artwork. Windows is still allowed to maximize normally.
        if (!_fullScreen && m.Msg == WM_SIZING && WindowState == FormWindowState.Normal && m.LParam != IntPtr.Zero)
        {
            ResizeRect rect = Marshal.PtrToStructure<ResizeRect>(m.LParam);
            LockResizeRectangle(m.WParam.ToInt32(), ref rect);
            Marshal.StructureToPtr(rect, m.LParam, false);
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }

    private void LockResizeRectangle(int edge, ref ResizeRect rect)
    {
        int extraWidth = Math.Max(0, Width - ClientSize.Width);
        int extraHeight = Math.Max(0, Height - ClientSize.Height);

        int windowWidth = Math.Max(1, rect.Right - rect.Left);
        int windowHeight = Math.Max(1, rect.Bottom - rect.Top);

        int clientWidth = Math.Max(S(960), windowWidth - extraWidth);
        int clientHeight = Math.Max(S(540), windowHeight - extraHeight);

        bool widthDriven = edge == WMSZ_LEFT || edge == WMSZ_RIGHT;
        bool heightDriven = edge == WMSZ_TOP || edge == WMSZ_BOTTOM;

        if (widthDriven)
        {
            clientHeight = Math.Max(S(540), (int)Math.Round(clientWidth / LockedWindowAspectRatio));
        }
        else if (heightDriven)
        {
            clientWidth = Math.Max(S(960), (int)Math.Round(clientHeight * LockedWindowAspectRatio));
        }
        else
        {
            float currentRatio = clientWidth / (float)Math.Max(1, clientHeight);

            if (currentRatio > LockedWindowAspectRatio)
            {
                clientHeight = Math.Max(S(540), (int)Math.Round(clientWidth / LockedWindowAspectRatio));
            }
            else
            {
                clientWidth = Math.Max(S(960), (int)Math.Round(clientHeight * LockedWindowAspectRatio));
            }
        }

        windowWidth = clientWidth + extraWidth;
        windowHeight = clientHeight + extraHeight;

        switch (edge)
        {
            case WMSZ_LEFT:
                rect.Left = rect.Right - windowWidth;
                rect.Bottom = rect.Top + windowHeight;
                break;

            case WMSZ_RIGHT:
                rect.Right = rect.Left + windowWidth;
                rect.Bottom = rect.Top + windowHeight;
                break;

            case WMSZ_TOP:
                rect.Top = rect.Bottom - windowHeight;
                rect.Right = rect.Left + windowWidth;
                break;

            case WMSZ_BOTTOM:
                rect.Bottom = rect.Top + windowHeight;
                rect.Right = rect.Left + windowWidth;
                break;

            case WMSZ_TOPLEFT:
                rect.Left = rect.Right - windowWidth;
                rect.Top = rect.Bottom - windowHeight;
                break;

            case WMSZ_TOPRIGHT:
                rect.Right = rect.Left + windowWidth;
                rect.Top = rect.Bottom - windowHeight;
                break;

            case WMSZ_BOTTOMLEFT:
                rect.Left = rect.Right - windowWidth;
                rect.Bottom = rect.Top + windowHeight;
                break;

            case WMSZ_BOTTOMRIGHT:
            default:
                rect.Right = rect.Left + windowWidth;
                rect.Bottom = rect.Top + windowHeight;
                break;
        }
    }

    private void UpdateAspectMaximizedBounds()
    {
        // Let Windows use the entire monitor work area, including correct
        // taskbar, DPI and multi-monitor offsets. Aspect locking is windowed only.
        MaximizedBounds = Rectangle.Empty;
    }

    private Size FitClientSizeToLockedAspect(Size requested, Rectangle workingArea)
    {
        // Keep the *client* composition stable while leaving enough room for the
        // native title bar and taskbar. Maximized windows remain managed by Windows.
        int maxWidth = Math.Max(S(760), workingArea.Width - S(36));
        int maxHeight = Math.Max(S(430), workingArea.Height - S(72));
        int minWidth = Math.Min(S(960), maxWidth);
        int minHeight = Math.Min(S(540), maxHeight);

        int width = Math.Clamp(requested.Width, minWidth, maxWidth);
        int height = Math.Max(1, (int)Math.Round(width / LockedWindowAspectRatio));
        if (height > maxHeight)
        {
            height = maxHeight;
            width = Math.Max(1, (int)Math.Round(height * LockedWindowAspectRatio));
        }
        if (height < minHeight)
        {
            height = minHeight;
            width = Math.Max(1, (int)Math.Round(height * LockedWindowAspectRatio));
        }
        if (width > maxWidth)
        {
            width = maxWidth;
            height = Math.Max(1, (int)Math.Round(width / LockedWindowAspectRatio));
        }

        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        try
        {
            int useDarkMode = 1;
            DwmSetWindowAttribute(
                Handle,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDarkMode,
                sizeof(int)
            );
        }
        catch
        {
            // Ignore if Windows does not support dark title bars.
        }
    }

    private int S(int value)
    {
        return (int)Math.Round(value * AppScale);
    }

    private Font UiFont(float pixelSize, FontStyle style = FontStyle.Regular)
    {
        return new Font("Segoe UI", pixelSize, style, GraphicsUnit.Pixel);
    }

    private void LoadAssets()
    {
        try
        {
            Icon = AssetManager.LoadIcon("Icon.ico");
        }
        catch
        {
            // Keep default icon if missing.
        }

        _mainLogo = SafeLoadImage("MainScreenLogo.png");

        _infoImage = SafeLoadImage("Info.png");
        _settingsImage = SafeLoadImage("Settings.png");
        _homeImage = SafeLoadImage("Home.png");

        _vrRed = SafeLoadImage("VR_Red.png");
        _vrOrange = SafeLoadImage("VR_Orange.png");
        _vrGreen = SafeLoadImage("VR_Green.png");

        _monitorRed = SafeLoadImage("Monitor_Red.png");
        _monitorOrange = SafeLoadImage("Monitor_Orange.png");
        _monitorGreen = SafeLoadImage("Monitor_Green.png");

        _playOn = SafeLoadImage("play_on.png");
        _playOff = SafeLoadImage("play_off.png");
        _updateGreen = SafeLoadImage("Update_Green.png");
        _updateYellow = SafeLoadImage("Update_Yellow.png");

        _browseRed = SafeLoadImage("Browse_Red.png");
        _browseGreen = SafeLoadImage("Browse_Green.png");

        _captureSettingsImage = SafeLoadImage("CaptureSettings.png");
        _captureSettingsGrayImage = SafeLoadImage("CaptureSettingsGray.png");
        _removeImage = SafeLoadImage("Remove.png");
        _removeGrayImage = SafeLoadImage("RemoveGray.png");

        _buttonOff = SafeLoadImage("Button_Off.png");
        _buttonOn = SafeLoadImage("Button_On.png");

        _lowRed = SafeLoadImage("Low_Red.png");
        _lowGreen = SafeLoadImage("Low_Green.png");

        _mediumRed = SafeLoadImage("Medium_Red.png");
        _mediumGreen = SafeLoadImage("Medium_Green.png");

        _highRed = SafeLoadImage("High_Red.png");
        _highGreen = SafeLoadImage("High_Green.png");

        _nvidiaProfileInspector = SafeLoadImage("NvidiaProfileInspector.png");

        _youtubeImage =
            TryLoadImage("youtube.png") ??
            TryLoadImage("Youtube.png") ??
            TryLoadImage("YouTube.png") ??
            TryLoadImage("YoutubeButton.png") ??
            TryLoadImage("YouTubeButton.png") ??
            TryLoadImage("Subscribe.png") ??
            TryLoadImage("SubscribeButton.png");

        _beerImage =
            TryLoadImage("beer.png") ??
            TryLoadImage("Beer.png") ??
            TryLoadImage("BeerMug.png") ??
            TryLoadImage("BeerButton.png") ??
            TryLoadImage("BuyMeABeer.png") ??
            TryLoadImage("BuyMeABeerButton.png");

        if (_beerImage != null)
        {
            _beerImage = RemoveDarkBackgroundMatte(_beerImage);
        }

        _discordImage =
            TryLoadImage("discord.png") ??
            TryLoadImage("Discord.png") ??
            TryLoadImage("DiscordButton.png") ??
            TryLoadImage("DiscordLogo.png") ??
            TryLoadImage("WarThunderDiscord.png");

        _recommendedSettingsImage =
            TryLoadImage("RecommendedSettings.png") ??
            TryLoadImage("recommendedsettings.png");

        _helpImage = TryLoadImage("help.png");
        _verticalLineImage = TryLoadImage("VerticalLine.png");

        _vdImage = TryLoadImage("VD.png");
        _metaImage = TryLoadImage("Meta.png");
        _steamVrSoftwareImage = TryLoadImage("SteamVR.png");
        _backArrowImage = TryLoadImage("BackArrow.png");
        _backButtonImage = TryLoadImage("Back.png");
        _backGrayButtonImage = TryLoadImage("BackGray.png");
        _nextButtonImage = TryLoadImage("Next.png");
        _nextGrayButtonImage = TryLoadImage("NextGray.png");
        _snailImage = TryLoadImage("snail.png");
        _diplomaImage = TryLoadImage("diploma.png");
    }

    private Image SafeLoadImage(string fileName)
    {
        try
        {
            return AssetManager.LoadImage(fileName);
        }
        catch
        {
            return CreateMissingAssetImage(fileName, 500, 180);
        }
    }

    private Image? TryLoadImage(string fileName)
    {
        try
        {
            return AssetManager.LoadImage(fileName);
        }
        catch
        {
            return null;
        }
    }

    private Image RemoveDarkBackgroundMatte(Image image)
    {
        Bitmap bitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(image, 0, 0, image.Width, image.Height);
        }

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);

                // Some exported PNGs look transparent in an editor but still contain a very dark matte.
                // This removes only nearly-black opaque pixels around the beer mug.
                if (pixel.A > 0 && pixel.R < 14 && pixel.G < 18 && pixel.B < 18)
                {
                    bitmap.SetPixel(x, y, ThemePalette.FromArgb(0, pixel.R, pixel.G, pixel.B));
                }
            }
        }

        return bitmap;
    }

    private Image CreateMissingAssetImage(string fileName, int width, int height)
    {
        Bitmap bitmap = new Bitmap(width, height);

        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ThemePalette.FromArgb(45, 0, 0));

        using Pen borderPen = new Pen(Color.Red, 4);
        g.DrawRectangle(borderPen, 2, 2, width - 4, height - 4);

        using Font font = UiFont(24, FontStyle.Bold);
        using Brush brush = new SolidBrush(Color.White);

        string text = $"MISSING:\n{fileName}";

        StringFormat format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.DrawString(text, font, brush, new RectangleF(0, 0, width, height), format);

        return bitmap;
    }

    private Image CropImage(Image image, int cropLeft, int cropTop, int cropRight, int cropBottom)
    {
        int newWidth = image.Width - cropLeft - cropRight;
        int newHeight = image.Height - cropTop - cropBottom;

        if (newWidth <= 0 || newHeight <= 0)
        {
            return image;
        }

        Bitmap cropped = new Bitmap(newWidth, newHeight);

        using Graphics g = Graphics.FromImage(cropped);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Rectangle destination = new Rectangle(0, 0, newWidth, newHeight);
        Rectangle source = new Rectangle(cropLeft, cropTop, newWidth, newHeight);

        g.DrawImage(image, destination, source, GraphicsUnit.Pixel);

        return cropped;
    }

    private void BuildMainScreen()
    {
        _mainPanel = CreateScreenPanel();
        Controls.Add(_mainPanel);

        _mainCanvas = new MainPageCanvas(LayoutFilePath, _backgroundColor, _textColor, UiFont)
        {
            Dock = DockStyle.Fill
        };

        _mainPanel.Controls.Add(_mainCanvas);

        _mainCanvas.AddImage("MainLogo", _mainLogo, new Rectangle(0, -11, 874, 889), ArmSecretCode);
        string displayedVersion = string.Format(CultureInfo.CurrentCulture, T("Home.Version"), CurrentVersion) + (string.IsNullOrWhiteSpace(BuildChannelLabel) ? "" : $" {BuildChannelLabel}");
        _mainCanvas.AddText("VersionText", displayedVersion, new Rectangle(268, 675, 360, 72), 32f, FontStyle.Regular);
        _mainCanvas.AddImage("VRButton", _vrOrange, new Rectangle(920, 310, 560, 241), ApplyVrMode);
        _mainCanvas.AddImage("MonitorButton", _monitorOrange, new Rectangle(920, 55, 560, 234), ApplyMonitorMode);
        _mainCanvas.AddImage("PlayButton", _playOff, new Rectangle(830, 570, 740, 200), null);

        if (_discordImage != null)
        {
            _mainCanvas.AddImage("DiscordButton", _discordImage, new Rectangle(1415, 777, 175, 53), OpenDiscord);
        }
        else
        {
            _mainCanvas.AddText("DiscordButton", T("Home.Discord"), new Rectangle(1415, 777, 175, 53), 26f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, OpenDiscord);
        }

        if (_beerImage != null)
        {
            _mainCanvas.AddImage("BeerButton", _beerImage, new Rectangle(50, 697, 105, 120), ShowBeerMessage);
        }
        else
        {
            _mainCanvas.AddText("BeerButton", T("Home.Support"), new Rectangle(42, 727, 120, 55), 19f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, ShowBeerMessage);
        }
        _mainCanvas.SetItemToolTip("BeerButton", T("Home.Tooltip.Beer"));

        if (_youtubeImage != null)
        {
            _mainCanvas.AddImage("YoutubeButton", _youtubeImage, new Rectangle(680, 697, 125, 125), OpenYouTube);
        }
        else
        {
            _mainCanvas.AddText("YoutubeButton", T("Home.YouTube"), new Rectangle(680, 730, 125, 55), 19f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, OpenYouTube);
        }
        _mainCanvas.SetItemToolTip("YoutubeButton", T("Home.Tooltip.YouTube"));

        _neckAssistImage ??= SafeLoadImage("NeckAssist.png");
        _neckAssistActiveImage ??= SafeLoadImage("NeckAssist_Active.png");
        _hiddenKeybindsImage ??= SafeLoadImage("KeyBindAssistant_Inactive_v2.png");
        _hiddenKeybindsActiveImage ??= SafeLoadImage("HiddenKeybinds-v3.png");
        _mainCanvas.AddImage("NeckAssistButton", IsNeckAssistSavedEnabled() ? _neckAssistActiveImage : _neckAssistImage, new Rectangle(1516, 245, 81, 81), ToggleNeckAssistPanel);
        _mainCanvas.SetItemToolTip("NeckAssistButton", IsNeckAssistSavedEnabled() ? T("Home.Tooltip.Neck.On") : T("Home.Tooltip.Neck.Off"));

        _mainCanvas.AddImage("InfoIcon", _infoImage, new Rectangle(1516, 150, 81, 81), () =>
        {
            ShowScreen(_aboutPanel);
        });
        _mainCanvas.AddImage("SettingsIcon", _settingsImage, new Rectangle(1500, 25, 114, 114), () =>
        {
            ShowScreen(_settingsPanel);
        });
        bool keyBindAssistantActive = IsKeyBindAssistantSavedEnabled();
        _mainCanvas.AddImage("HiddenKeybindsButton", keyBindAssistantActive ? _hiddenKeybindsActiveImage : _hiddenKeybindsImage, new Rectangle(1516, 340, 81, 81), ToggleHiddenKeybindsPanel);
        _mainCanvas.SetItemToolTip("HiddenKeybindsButton", keyBindAssistantActive ? T("Home.Tooltip.Key.On") : T("Home.Tooltip.Key.Off"));

        _mainCanvas.ApplyLayout(ParseBakedLayout(BakedMainLayoutJson));
        if (IllustratedTheme.Enabled) BuildIllustratedHome();
    }

    private void BuildIllustratedHome()
    {
        _mainCanvas.Visible = false;
        void ToggleNeckFromHome()
        {
            EnsureNeckAssistPanel();
            _neckAssistForm!.ToggleAssistance();
        }
        void ToggleKeysFromHome()
        {
            EnsureHiddenKeybindsPanel();
            _hiddenKeybindsForm!.ToggleAssistance();
        }
        _aviationHome = new AviationHomePage(
            _mainLogo,
            _neckAssistActiveImage ?? SafeLoadImage("NeckAssist_Active.png"),
            _hiddenKeybindsActiveImage ?? SafeLoadImage("HiddenKeybinds-v3.png"),
            ApplyMonitorMode,
            ApplyVrMode,
            ToggleNeckAssistPanel,
            ToggleNeckFromHome,
            ToggleHiddenKeybindsPanel,
            ToggleKeysFromHome,
            SelectGameServerChannel,
            RefreshServerSignalsFromUi,
            HomePrimaryAction,
            ArmSecretCode,
            OpenDiscord,
            OpenYouTube,
            ShowBeerMessage,
            OpenVTrim, ToggleVTrimFromHome);
        _aviationHome.SetLanguage(_languageCode);
        _mainPanel.Controls.Add(_aviationHome);
        _aviationHome.BringToFront();
        return;

#if false // Retained only as migration reference; the legacy themed canvas is not compiled or used.
        // Separate layout: never write over a user's Classic canvas arrangement.
        _mainCanvas.Dispose();
        _mainCanvas = new MainPageCanvas(Path.Combine(SettingsFolder, "illustrated_layout.json"), _backgroundColor, _textColor, UiFont) { Dock = DockStyle.Fill };
        _mainPanel.Controls.Add(_mainCanvas);
        _mainCanvas.AddRectangle("Sidebar", new Rectangle(0, 0, 240, 844), IllustratedTheme.Panel);
        _mainCanvas.AddText("Brand", "VR ASSISTANT", new Rectangle(22, 18, 208, 55), 24, FontStyle.Bold);
        void Nav(string key, string label, Image image, int y, Action action)
        {
            _mainCanvas.AddImage(key, image, new Rectangle(22, y, 45, 45), action);
            _mainCanvas.AddText(key + "Label", label, new Rectangle(80, y, 150, 45), 20, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center, action);
        }
        Nav("HomeIcon", "Home", _homeImage, 105, () => ShowScreen(_mainPanel));
        Nav("SettingsIcon", "Profiles", _settingsImage, 183, () => ShowScreen(_settingsPanel));
        Nav("NeckAssistButton", "Neck Assistant", IsNeckAssistSavedEnabled() ? _neckAssistActiveImage! : _neckAssistImage!, 261, ToggleNeckAssistPanel);
        Nav("HiddenKeybindsButton", "KeyBind Assistant", IsKeyBindAssistantSavedEnabled() ? _hiddenKeybindsActiveImage! : _hiddenKeybindsImage!, 339, ToggleHiddenKeybindsPanel);
        Nav("OptionsIcon", "App Options", _settingsImage, 417, ShowApplicationOptionsDialog);
        Nav("InfoIcon", "Info", _infoImage, 495, () => ShowScreen(_aboutPanel));
        _mainCanvas.AddImage("MainLogo", _mainLogo, new Rectangle(265, 100, 650, 650), ArmSecretCode);
        _mainCanvas.AddText("ModeHeading", "PLAY MODE", new Rectangle(970, 90, 570, 55), 28, FontStyle.Bold);
        _mainCanvas.AddImage("MonitorButton", _monitorOrange, new Rectangle(975, 175, 570, 142), ApplyMonitorMode);
        _mainCanvas.AddImage("VRButton", _vrOrange, new Rectangle(975, 350, 570, 142), ApplyVrMode);
        _mainCanvas.AddImage("PlayButton", _playOff, new Rectangle(975, 565, 570, 142), null);
        _mainCanvas.AddText("VersionText", $"Version {CurrentVersion}", new Rectangle(280, 780, 620, 42), 20, FontStyle.Regular);
        _mainCanvas.AddText("DiscordButton", "DISCORD", new Rectangle(960, 780, 180, 42), 19, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, OpenDiscord);
        _mainCanvas.AddText("YoutubeButton", "YOUTUBE", new Rectangle(1155, 780, 170, 42), 19, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, OpenYouTube);
        _mainCanvas.AddText("BeerButton", "SUPPORT", new Rectangle(1340, 780, 200, 42), 19, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, ShowBeerMessage);
        _mainCanvas.CompactThemeHome();
        _mainCanvas.ApplyLayout(new Dictionary<string, LayoutRect>
        {
            ["MonitorButton"] = new() { X=850,Y=170,Width=320,Height=110 },
            ["VRButton"] = new() { X=1190,Y=170,Width=320,Height=110 },
            ["PlayButton"] = new() { X=850,Y=615,Width=660,Height=140 },
            ["MainLogo"] = new() { X=10,Y=75,Width=810,Height=810 },
            ["ModeHeading"] = new() { X=850,Y=60,Width=735,Height=60 },
            ["VersionText"] = new() { X=20,Y=920,Width=760,Height=44 },
            ["DiscordButton"] = new() { X=865,Y=910,Width=200,Height=50 },
            ["YoutubeButton"] = new() { X=1110,Y=910,Width=200,Height=50 },
            ["BeerButton"] = new() { X=1360,Y=910,Width=200,Height=50 },
        });
        _mainCanvas.AddRectangle("NeckCard",new Rectangle(850,320,320,250),IllustratedTheme.Panel,ToggleNeckAssistPanel);
        _mainCanvas.AddRectangle("KeyCard",new Rectangle(1190,320,320,250),IllustratedTheme.Panel,ToggleHiddenKeybindsPanel);
        _mainCanvas.AddText("NeckCardTitle","Neck Assistant",new Rectangle(860,334,300,45),23,FontStyle.Bold,StringAlignment.Center,StringAlignment.Center,ToggleNeckAssistPanel);
        _mainCanvas.AddText("KeyCardTitle","KeyBind Assistant",new Rectangle(1200,334,300,45),23,FontStyle.Bold,StringAlignment.Center,StringAlignment.Center,ToggleHiddenKeybindsPanel);
        _mainCanvas.AddImage("NeckAssistButton",IsNeckAssistSavedEnabled() ? _neckAssistActiveImage! : _neckAssistImage!,new Rectangle(940,388,140,120),ToggleNeckAssistPanel);
        _mainCanvas.AddImage("HiddenKeybindsButton",IsKeyBindAssistantSavedEnabled() ? _hiddenKeybindsActiveImage! : _hiddenKeybindsImage!,new Rectangle(1280,388,140,120),ToggleHiddenKeybindsPanel);
        _mainCanvas.AddText("NeckCardState",IsNeckAssistSavedEnabled() ? "Enabled" : "Off",new Rectangle(860,515,300,40),26,FontStyle.Bold,StringAlignment.Center,StringAlignment.Center,() =>
        {
            if(_neckAssistForm == null || _neckAssistForm.IsDisposed) ToggleNeckAssistPanel();
            _neckAssistForm!.ToggleAssistance(); _neckAssistForm.Hide(); ShowScreen(_mainPanel);
        });
        _mainCanvas.AddText("KeyCardState",IsKeyBindAssistantSavedEnabled() ? "Enabled" : "Off",new Rectangle(1200,515,300,40),26,FontStyle.Bold,StringAlignment.Center,StringAlignment.Center,() =>
        {
            if(_hiddenKeybindsForm == null || _hiddenKeybindsForm.IsDisposed) ToggleHiddenKeybindsPanel();
            _hiddenKeybindsForm!.ToggleAssistance(); _hiddenKeybindsForm.Hide(); ShowScreen(_mainPanel);
        });
        _mainCanvas.ApplyLayout(new Dictionary<string, LayoutRect>
        {
            ["MonitorButton"] = new() { X=850,Y=145,Width=355,Height=180 },
            ["VRButton"] = new() { X=1230,Y=145,Width=355,Height=180 },
            ["NeckCard"] = new() { X=850,Y=355,Width=355,Height=350 },
            ["KeyCard"] = new() { X=1230,Y=355,Width=355,Height=350 },
            ["NeckCardTitle"] = new() { X=865,Y=374,Width=325,Height=48 },
            ["KeyCardTitle"] = new() { X=1245,Y=374,Width=325,Height=48 },
            ["NeckAssistButton"] = new() { X=925,Y=427,Width=205,Height=205 },
            ["HiddenKeybindsButton"] = new() { X=1305,Y=427,Width=205,Height=205 },
            ["NeckCardState"] = new() { X=870,Y=641,Width=315,Height=44 },
            ["KeyCardState"] = new() { X=1250,Y=641,Width=315,Height=44 },
            ["PlayButton"] = new() { X=850,Y=748,Width=735,Height=135 },
        });
        _mainCanvas.LayoutIllustratedHome();
#endif
    }

    private Panel? _themeNavigation;
    private Label? _themeBrand;
    private Label? _themeSubtitle;
    private bool _layingOutThemeChrome;
    private void SelectThemePage(string page)
    {
        LayoutAircraftHomeFilter();
        if (_themeNavigation == null) return;
        string pageKey = NormalizeThemePageKey(page);
        foreach(var button in _themeNavigation.Controls.OfType<IllustratedNavButton>())
        {
            button.Selected = button.PageKey == pageKey;
            button.FeatureActive = button.PageKey == "neck" ? (_neckAssistForm?.AssistanceEnabled ?? IsNeckAssistSavedEnabled()) : button.PageKey == "keybind" ? (_hiddenKeybindsForm?.AssistantEnabled ?? IsKeyBindAssistantSavedEnabled()) : button.PageKey == "vtrim" && (_vtrimForm?.OutputConnected == true);
            button.Invalidate();
        }
    }

    private static string NormalizeThemePageKey(string page) => page switch
    {
        "Home" or "home" => "home",
        "Profiles" or "profiles" => "profiles",
        "Neck Assistant" or "neck" => "neck",
        "KeyBind Assistant" or "keybind" => "keybind",
        "VTrim Assistant" or "vtrim" => "vtrim",
        "Game Updates" or "gameupdates" => "gameupdates",
        "Settings" or "options" => "options",
        "Info" or "info" => "info",
        "Updates" or "updates" => "updates",
        _ => page
    };
    private void BuildThemeNavigation()
    {
        const int width = 280;
        Padding = new Padding(width + 20, 12, 12, 66);
        _themeNavigation = new Panel
        {
            BackColor = IllustratedTheme.Panel,
            AutoScroll = false,
            Bounds = new Rectangle(10, 12, width, ClientSize.Height - 78),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
        };
        Controls.Add(_themeNavigation);
        Paint += (_,e) => DrawThemeChrome(e.Graphics);
        Resize += (_,_) => { LayoutThemePages(); Invalidate(); };

        // v2.0.6: the large illustrated brand artwork already identifies the app.
        // Do not duplicate "VR ASSISTANT / FOR WAR THUNDER" in the sidebar.
        _themeBrand = null;
        _themeSubtitle = null;

        void Navigate(Action action)
        {
            _neckAssistForm?.Hide();
            _hiddenKeybindsForm?.Hide();
            _vtrimForm?.Hide();
            action();
            _themeNavigation.BringToFront();
        }

        (string Key, string Text, string Icon, Action Action)[] links = [
            ("home", T("Nav.Home"), "home", () => Navigate(() => ShowScreen(_mainPanel))),
            ("profiles", T("Nav.Profiles"), "folder", () => Navigate(() => ShowScreen(_settingsPanel))),
            ("neck", T("Nav.Neck"), "head", () => Navigate(ToggleNeckAssistPanel)),
            ("keybind", T("Nav.Keybind"), "keys", () => Navigate(ToggleHiddenKeybindsPanel)),
            ("vtrim", T("Nav.VTrim"), "trim", () => Navigate(OpenVTrim)),
            ("options", T("Nav.Options"), "gear", ShowApplicationOptionsDialog),
            ("info", T("Nav.Info"), "info", () => Navigate(() => ShowScreen(_aboutPanel)))];
        for (int i = 0; i < links.Length; i++)
        {
            var link = links[i];
            IllustratedNavButton button = new(link.Text, link.Icon) { PageKey = link.Key, Bounds = new Rectangle(8, 18 + i * 62, 264, 56) };
            button.Click += (_, _) => link.Action();
            if (link.Key == "vtrim") button.QuickDoubleClick += (_, _) => { _vtrimNavigationExpanded = !_vtrimNavigationExpanded; LayoutThemeChrome(); };
            _themeNavigation.Controls.Add(button);
        }
        LayoutThemePages();
    }

    private void DrawThemeChrome(Graphics graphics)
    {
        float scale = ThemeChromeScale;
        int S(float value) => Math.Max(1, (int)Math.Round(value * scale));
        int footerHeight = S(66);
        int footerLineY = ClientSize.Height - footerHeight + S(8);

        using Pen line = new(IllustratedTheme.Ivory, 1);
        graphics.DrawRectangle(line, 3, 3, ClientSize.Width - 7, ClientSize.Height - 7);
        graphics.DrawLine(line, S(10), footerLineY, ClientSize.Width - S(10), footerLineY);

        // The footer is useful status, not branding copy. Show the selected
        // circuit, installed version and update state supplied by GameServices.
        int statusY = footerLineY + S(13);
        // v2.0 intentionally has no server-availability dot. This footer text is
        // only the selected circuit + local game/update information.

        using Font versionFont = new("Segoe UI", Math.Max(S(20), S(24)), FontStyle.Bold, GraphicsUnit.Pixel);
        string version = string.IsNullOrWhiteSpace(BuildChannelLabel) ? $"v {CurrentVersion}" : $"v {CurrentVersion} {BuildChannelLabel}";
        Size versionSize = TextRenderer.MeasureText(version, versionFont, Size.Empty, TextFormatFlags.NoPadding);
        int versionX = ClientSize.Width - versionSize.Width - S(24);
        int versionY = footerLineY + Math.Max(0, (footerHeight - S(8) - versionSize.Height) / 2);
        TextRenderer.DrawText(graphics, version, versionFont, new Point(versionX, versionY), IllustratedTheme.Ivory);

        int statusRight = versionX - S(24);
        int statusWidth = Math.Max(1, statusRight - S(28));
        int statusFontSize = S(18);
        int minimumStatusFontSize = Math.Max(12, S(13));
        while (statusFontSize > minimumStatusFontSize)
        {
            using Font test = new("Segoe UI", statusFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(graphics, _gameVersionStatusText, test, Size.Empty, TextFormatFlags.NoPadding).Width <= statusWidth) break;
            statusFontSize--;
        }
        using Font statusFont = new("Segoe UI", statusFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(graphics, _gameVersionStatusText, statusFont,
            new Rectangle(S(28), statusY - S(2), statusWidth, S(34)), IllustratedTheme.Ivory,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private Rectangle ThemeContentBounds => new(
        Padding.Left,
        Padding.Top,
        Math.Max(1, ClientSize.Width - Padding.Horizontal),
        Math.Max(1, ClientSize.Height - Padding.Vertical));

    // The form uses AutoScaleMode.None. WinForms coordinates are already device
    // pixels here, so multiplying by DeviceDpi again was the source of the
    // giant sidebar and nested scrollbars seen in 1.8.1 at 125/150% scaling.
    private float ThemeChromeScale
    {
        get
        {
            // Scale the complete chrome with the window. The 860px height reference
            // compensates for the Windows title/taskbar area when a 16:9 display is
            // maximized, so typography no longer looks undersized at full screen.
            float widthScale = ClientSize.Width / 1600F;
            float heightScale = ClientSize.Height / 860F;
            return Math.Clamp(Math.Min(widthScale, heightScale), 0.72F, 1.85F);
        }
    }

    private void LayoutThemeChrome()
    {
        if (_themeNavigation == null || _layingOutThemeChrome) return;
        _layingOutThemeChrome = true;
        try
        {
            float scale = ThemeChromeScale;
            int S(float value) => Math.Max(1, (int)Math.Round(value * scale));

            // Keep enough canvas for the large VR Assistant logo at common 125% DPI.
            // Navigation remains readable but no longer consumes unnecessary width.
            int logicalClientWidth = (int)Math.Round(ClientSize.Width / scale);
            int navigationWidth = S(logicalClientWidth < 1180 ? 230 : logicalClientWidth < 1450 ? 255 : 285);
            int footerHeight = S(66);
            Padding = new Padding(navigationWidth + S(20), S(12), S(12), footerHeight);
            _themeNavigation.SetBounds(S(10), S(12), navigationWidth, Math.Max(1, ClientSize.Height - S(78)));

            if (_themeBrand != null)
            {
                _themeBrand.SetBounds(S(18), S(15), navigationWidth - S(36), S(43));
                SetOwnedFont(_themeBrand, 20 * scale, FontStyle.Bold);
            }
            if (_themeSubtitle != null)
            {
                _themeSubtitle.SetBounds(S(20), S(55), navigationWidth - S(40), S(24));
                SetOwnedFont(_themeSubtitle, 10 * scale, FontStyle.Regular);
            }

            IllustratedNavButton[] buttons = _themeNavigation.Controls.OfType<IllustratedNavButton>().ToArray();
            int navRow = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool child = buttons[i].PageKey == "vtrim-profiles";
                buttons[i].Visible = !child || _vtrimNavigationExpanded;
                if (!buttons[i].Visible) continue;
                buttons[i].SetBounds(S(child ? 38 : 8), S(16 + navRow++ * 62), navigationWidth - S(child ? 46 : 16), S(58));
                SetOwnedFont(buttons[i], 20 * scale, FontStyle.Bold);
            }
        }
        finally
        {
            _layingOutThemeChrome = false;
        }
    }

    private void LayoutThemePages()
    {
        if (!IllustratedTheme.Enabled) return;
        LayoutThemeChrome();
        LayoutAircraftHomeFilter();
        Rectangle viewport = ThemeContentBounds;
        foreach (Panel? page in new[] { _mainPanel, _settingsPanel, _aboutPanel, _recommendedPanel })
        {
            if (page == null) continue;
            page.Dock = DockStyle.None;
            page.Bounds = viewport;
        }
        if (_vtrimForm is { IsDisposed: false }) _vtrimForm.Bounds = viewport;
        if (_neckAssistForm is { IsDisposed: false })
        {
            _neckAssistForm.Dock = DockStyle.None;
            _neckAssistForm.Bounds = viewport;
        }
        if (_hiddenKeybindsForm is { IsDisposed: false })
        {
            _hiddenKeybindsForm.Dock = DockStyle.None;
            _hiddenKeybindsForm.Bounds = viewport;
        }
    }

    private void ToggleNeckAssistPanel()
    {
        if (_neckAssistForm is { IsDisposed: false, Visible: true })
        {
            _neckAssistForm.Hide();
            return;
        }

        EnsureNeckAssistPanel();
        PositionNeckAssistPanel();
        _neckAssistForm!.Show();
        _neckAssistForm.BringToFront();
        SelectThemePage("Neck Assistant");
    }

    private void EnsureNeckAssistPanel()
    {
        if (_neckAssistForm == null || _neckAssistForm.IsDisposed)
        {
            _neckAssistForm = new NeckAssistForm(AppFolder) { Icon = this.Icon };
            _neckAssistForm.ApplyLanguage(_languageCode);
            _neckAssistForm.AssistanceStateChanged += (_, _) => RefreshNeckAssistIcon();
            _neckAssistForm.ConfigureNavigation(_homeImage, _infoImage, () => { _neckAssistForm.Hide(); ShowScreen(_aboutPanel); });
            _neckAssistForm.FormClosed += (_, _) => _neckAssistForm = null;
            _neckAssistForm.CloseRequested += (_, _) => { _neckAssistForm.Hide(); ShowScreen(_mainPanel); };
            _neckAssistForm.TopLevel = false;
            _neckAssistForm.FormBorderStyle = FormBorderStyle.None;
            _neckAssistForm.Dock = IllustratedTheme.Enabled ? DockStyle.None : DockStyle.Fill;
            Controls.Add(_neckAssistForm);
            _ = _neckAssistForm.Handle;
            _neckAssistForm.StartAssistanceRuntime();
        }
    }

    private void ToggleHiddenKeybindsPanel()
    {
        if (_hiddenKeybindsForm is { IsDisposed: false, Visible: true })
        {
            _hiddenKeybindsForm.Hide();
            return;
        }

        EnsureHiddenKeybindsPanel();
        _hiddenKeybindsForm!.Bounds = IllustratedTheme.Enabled ? ThemeContentBounds : DisplayRectangle;
        _hiddenKeybindsForm.Show();
        _hiddenKeybindsForm.BringToFront();
        SelectThemePage("KeyBind Assistant");
    }

    private void EnsureHiddenKeybindsPanel()
    {
        if (_hiddenKeybindsForm == null || _hiddenKeybindsForm.IsDisposed)
        {
            _hiddenKeybindsForm = new HiddenKeybindsForm(AppFolder, _homeImage, _infoImage, () =>
            {
                _hiddenKeybindsForm?.Hide();
                ShowScreen(_aboutPanel);
            });
            _hiddenKeybindsForm.Icon = this.Icon;
            _hiddenKeybindsForm.ApplyLanguage(_languageCode);
            _hiddenKeybindsForm.AssistantStateChanged += (_, _) => RefreshKeyBindAssistantIcon();
            _hiddenKeybindsForm.CloseRequested += (_, _) =>
            {
                _hiddenKeybindsForm?.Hide();
                ShowScreen(_mainPanel);
            };
            _hiddenKeybindsForm.FormClosed += (_, _) => _hiddenKeybindsForm = null;
            _hiddenKeybindsForm.TopLevel = false;
            _hiddenKeybindsForm.FormBorderStyle = FormBorderStyle.None;
            _hiddenKeybindsForm.Dock = IllustratedTheme.Enabled ? DockStyle.None : DockStyle.Fill;
            Controls.Add(_hiddenKeybindsForm);
            _ = _hiddenKeybindsForm.Handle;
        }
    }

    private bool IsKeyBindAssistantSavedEnabled()
    {
        try
        {
            string path = Path.Combine(SettingsFolder, "hidden_keybinds.json");
            if (!File.Exists(path)) return false;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("Enabled", out JsonElement enabled) && enabled.GetBoolean();
        }
        catch { return false; }
    }

    private void RefreshKeyBindAssistantIcon()
    {
        if (_mainCanvas == null || _hiddenKeybindsImage == null || _hiddenKeybindsActiveImage == null) return;
        bool active = _hiddenKeybindsForm?.AssistantEnabled ?? IsKeyBindAssistantSavedEnabled();
        _mainCanvas.SetImage("HiddenKeybindsButton", active ? _hiddenKeybindsActiveImage : _hiddenKeybindsImage);
        _mainCanvas.SetText("KeyCardState", active ? "Enabled" : "Off");
        SelectThemePage(_themeNavigation?.Controls.OfType<IllustratedNavButton>().FirstOrDefault(b => b.Selected)?.PageKey ?? "home");
        _mainCanvas.SetItemToolTip("HiddenKeybindsButton", active ? T("Home.Tooltip.Key.On") : T("Home.Tooltip.Key.Off"));
        UpdateVisualStates();
    }

    private bool IsNeckAssistSavedEnabled()
    {
        try
        {
            string path = Path.Combine(SettingsFolder, "neck_assist.json");
            if (!File.Exists(path)) return false;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("Enabled", out JsonElement enabled) && enabled.GetBoolean();
        }
        catch { return false; }
    }

    private void RefreshNeckAssistIcon()
    {
        if (_mainCanvas == null || _neckAssistImage == null || _neckAssistActiveImage == null) return;
        bool active = _neckAssistForm?.AssistanceEnabled ?? IsNeckAssistSavedEnabled();
        _mainCanvas.SetImage("NeckAssistButton", active ? _neckAssistActiveImage : _neckAssistImage);
        _mainCanvas.SetText("NeckCardState", active ? "Enabled" : "Off");
        SelectThemePage(_themeNavigation?.Controls.OfType<IllustratedNavButton>().FirstOrDefault(b => b.Selected)?.PageKey ?? "home");
        _mainCanvas.SetItemToolTip("NeckAssistButton", active ? T("Home.Tooltip.Neck.On") : T("Home.Tooltip.Neck.Off"));
        UpdateVisualStates();
    }

    private void PositionNeckAssistPanel()
    {
        if (_neckAssistForm is not { IsDisposed: false }) return;
        _neckAssistForm.Bounds = IllustratedTheme.Enabled ? ThemeContentBounds : DisplayRectangle;
    }

    private void BuildSettingsScreen()
    {
        _settingsPanel = CreateScreenPanel();
        Controls.Add(_settingsPanel);

        _settingsCanvas = new MainPageCanvas(IllustratedTheme.Enabled ? Path.Combine(SettingsFolder,"illustrated_profiles.json") : SettingsLayoutFilePath, _backgroundColor, _textColor, UiFont)
        {
            Dock = DockStyle.Fill
        };

        _settingsPanel.Controls.Add(_settingsCanvas);

        _settingsCanvas.AddText("SettingsTip", "Select the War Thunder folder once; config and launcher files are detected automatically.", new Rectangle(360, 3, 900, 50), 22f, FontStyle.Italic);

        // Subtle section dividers keep the configuration rows visually distinct.
        Color sectionDividerColor = ThemePalette.FromArgb(62, 82, 88);
        _settingsCanvas.AddRectangle("ConfigSectionDivider", new Rectangle(75, 220, 1065, 2), sectionDividerColor);
        _settingsCanvas.AddRectangle("DesktopSectionDivider", new Rectangle(75, 405, 1065, 2), sectionDividerColor);

        _settingsCanvas.AddImage("InfoIcon", _infoImage, new Rectangle(1408, 45, 81, 81), () => ShowScreen(_aboutPanel));
        _settingsCanvas.AddImage("HomeIcon", _homeImage, new Rectangle(1498, 28, 114, 106), () => ShowScreen(_mainPanel));

        _settingsCanvas.AddRectangle(
            "AppOptionsButtonBg",
            new Rectangle(880, 230, 280, 55),
            ThemePalette.FromArgb(18, 30, 34),
            ShowApplicationOptionsDialog);
        _settingsCanvas.AddText(
            "AppOptionsButton",
            "APP OPTIONS",
            new Rectangle(880, 230, 280, 55),
            20f,
            FontStyle.Bold,
            StringAlignment.Center,
            StringAlignment.Center,
            ShowApplicationOptionsDialog);

        // Controls and launcher tools are grouped at the top-center.
        _settingsCanvas.AddRectangle(
            "ControlsProfilesButtonBg",
            new Rectangle(735, 55, 470, 85),
            ThemePalette.FromArgb(18, 30, 34),
            ShowControlsProfilesDialog);
        _settingsCanvas.AddText(
            "ControlsProfilesButton",
            "CONTROLS PROFILES",
            new Rectangle(735, 55, 470, 85),
            30f,
            FontStyle.Bold,
            StringAlignment.Center,
            StringAlignment.Center,
            ShowControlsProfilesDialog);
        _settingsCanvas.AddText(
            "ControlsProfilesDescription",
            "Select your custom controls for different presets (Optional)",
            new Rectangle(735, 145, 470, 70),
            24f,
            FontStyle.Italic,
            StringAlignment.Near,
            StringAlignment.Near);

        _settingsCanvas.AddText(
            "GameExeTitle",
            "WAR THUNDER LAUNCHER",
            new Rectangle(75, 250, 300, 42),
            21f,
            FontStyle.Regular,
            StringAlignment.Near,
            StringAlignment.Center);
        _settingsCanvas.AddRectangle(
            "GameExeField",
            new Rectangle(75, 292, 300, 28),
            _fieldColor,
            BrowseForWarThunderExe,
            path => SetWarThunderExePath(path));
        _settingsCanvas.AddText(
            "GameExePathText",
            "",
            new Rectangle(85, 293, 280, 26),
            11f,
            FontStyle.Regular,
            StringAlignment.Near,
            StringAlignment.Center,
            null,
            path => SetWarThunderExePath(path));
        _settingsCanvas.AddImage(
            "GameExeBrowse",
            _browseRed,
            new Rectangle(390, 235, 280, 86),
            BrowseForWarThunderExe,
            path => SetWarThunderExePath(path));

        _settingsCanvas.AddText("ConfigDescription", "Locate your War Thunder installation folder.", new Rectangle(75, 55, 680, 48), 24f, FontStyle.Italic, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddText("ConfigTitle", "WAR THUNDER FOLDER", new Rectangle(75, 102, 520, 48), 27f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddRectangle("ConfigField", new Rectangle(75, 155, 500, 46), _fieldColor, BrowseForWarThunderFolder);
        _settingsCanvas.AddText("ConfigPathText", "", new Rectangle(85, 158, 475, 38), 16f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddImage("ConfigBrowse", _browseRed, new Rectangle(390, 100, 280, 86), BrowseForWarThunderFolder, path => SetFilePath(FileSlot.Config, path));

        _settingsCanvas.AddText("DesktopDescription", "Your custom settings for War Thunder when playing on flat screen.", new Rectangle(75, 255, 760, 48), 24f, FontStyle.Italic, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddText("DesktopTitle", "DESCTOP .blk", new Rectangle(75, 310, 250, 48), 27f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddRectangle("DesktopField", new Rectangle(75, 320, 500, 46), _fieldColor, () => BrowseForFile(FileSlot.Desktop));
        _settingsCanvas.AddText("DesktopPathText", "", new Rectangle(85, 323, 475, 38), 16f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddImage("DesktopBrowse", _browseRed, new Rectangle(260, 300, 280, 86), () => BrowseForFile(FileSlot.Desktop), path => SetFilePath(FileSlot.Desktop, path));
        _settingsCanvas.AddImage("DesktopCaptureSettings", _captureSettingsImage, new Rectangle(690, 315, 250, 50), CaptureDesktopSettings);
        _settingsCanvas.AddImage("DesktopRemoveSettings", _removeGrayImage, new Rectangle(950, 315, 190, 50), null);

        _settingsCanvas.AddText("CustomToggleTitle", "CUSTOM VR .blk", new Rectangle(1215, 135, 330, 46), 31f, FontStyle.Regular);
        _settingsCanvas.AddImage("CustomToggle", _buttonOff, new Rectangle(1250, 185, 260, 140), ToggleCustomVr);

        _settingsCanvas.AddText("PresetTitle", "VR PRESETS", new Rectangle(75, 425, 500, 50), 39.68158f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddImage("LowButton", _lowRed, new Rectangle(45, 485, 430, 260), () => SelectVrPreset(VrPreset.Low));
        _settingsCanvas.AddImage("MediumButton", _mediumRed, new Rectangle(500, 485, 430, 260), () => SelectVrPreset(VrPreset.Medium));
        _settingsCanvas.AddImage("HighButton", _highRed, new Rectangle(955, 485, 430, 260), () => SelectVrPreset(VrPreset.High));

        if (_recommendedSettingsImage != null)
        {
            _settingsCanvas.AddImage("MoreInfoButton", _recommendedSettingsImage, new Rectangle(1165, 360, 430, 126), ShowRecommendedSettingsFlow);
        }
        else
        {
            _settingsCanvas.AddText("MoreInfoButton", "RECOMMENDED SETTINGS", new Rectangle(1165, 360, 430, 126), 28f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, ShowRecommendedSettingsFlow);
        }

        if (_helpImage != null)
        {
            _settingsCanvas.AddImage("HelpButton", _helpImage, new Rectangle(300, 430, 43, 43), ShowRecommendedGpuGraph);
        }
        else
        {
            _settingsCanvas.AddText("HelpButton", "?", new Rectangle(300, 430, 43, 43), 24f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, ShowRecommendedGpuGraph);
        }

        _settingsCanvas.AddText("CustomVrDescription", "Your custom settings for War Thunder when playing in VR.", new Rectangle(75, 420, 760, 48), 24f, FontStyle.Italic, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddText("CustomVrTitle", "VR .blk", new Rectangle(75, 485, 220, 48), 30f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddRectangle("CustomVrField", new Rectangle(75, 485, 500, 46), _fieldColor, () => BrowseForFile(FileSlot.CustomVr));
        _settingsCanvas.AddText("CustomVrPathText", "", new Rectangle(85, 488, 475, 38), 16f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddImage("CustomVrBrowse", _browseRed, new Rectangle(260, 475, 280, 86), () => BrowseForFile(FileSlot.CustomVr), path => SetFilePath(FileSlot.CustomVr, path));
        _settingsCanvas.AddImage("CustomVrCaptureSettings", _captureSettingsImage, new Rectangle(690, 490, 250, 50), CaptureCustomVrSettings);
        _settingsCanvas.AddImage("CustomVrRemoveSettings", _removeGrayImage, new Rectangle(950, 490, 190, 50), null);

        BuildGraphicsApiControls();
        BuildProfileControlSelectors();
        if (IllustratedTheme.Enabled) BuildIllustratedProfiles();
    }

    private void BuildIllustratedProfiles()
    {
        foreach(string key in new[]{"InfoIcon","HomeIcon","AppOptionsButtonBg","AppOptionsButton","ConfigSectionDivider","DesktopSectionDivider"})
            _settingsCanvas.SetItemVisible(key,false);
        _settingsCanvas.AddImage("ProfilesIcon",IllustratedTheme.IconAsset("folder",220),new Rectangle(30,2,124,124));
        _settingsCanvas.AddText("ProfilesHeading","PROFILES",new Rectangle(172,20,960,78),44,FontStyle.Bold,StringAlignment.Near);
        _settingsCanvas.AddRectangle("InstallationCard",new Rectangle(35,140,1550,190),IllustratedTheme.Panel);
        _settingsCanvas.AddRectangle("DesktopCard",new Rectangle(35,350,1550,265),IllustratedTheme.Panel);
        _settingsCanvas.AddRectangle("VrCard",new Rectangle(35,640,1550,285),IllustratedTheme.Panel);
        _settingsCanvas.ApplyLayout(new Dictionary<string,LayoutRect>
        {
            ["SettingsTip"]=new(){X=40,Y=85,Width=1520,Height=45,FontSize=25},
            ["ConfigTitle"]=new(){X=60,Y=160,Width=800,Height=45,FontSize=30},
            ["ConfigDescription"]=new(){X=60,Y=215,Width=950,Height=48,FontSize=25},
            ["ConfigBrowse"]=new(){X=1220,Y=180,Width=330,Height=88},
            ["DesktopTitle"]=new(){X=60,Y=370,Width=500,Height=48,FontSize=30,Text="MONITOR PROFILE"},
            ["DesktopDescription"]=new(){X=60,Y=420,Width=1050,Height=45,FontSize=25},
            ["DesktopBrowse"]=new(){X=60,Y=485,Width=300,Height=56},
            ["DesktopCaptureSettings"]=new(){X=600,Y=485,Width=460,Height=56},
            ["DesktopRemoveSettings"]=new(){X=1090,Y=485,Width=460,Height=56},
            ["CustomToggleTitle"]=new(){X=1140,Y=662,Width=245,Height=48,FontSize=24},
            ["CustomToggle"]=new(){X=1400,Y=660,Width=150,Height=56},
            ["PresetTitle"]=new(){X=60,Y=662,Width=350,Height=48,FontSize=30},
            ["HelpButton"]=new(){X=380,Y=665,Width=42,Height=42},
            ["LowButton"]=new(){X=60,Y=750,Width=465,Height=138},
            ["MediumButton"]=new(){X=550,Y=750,Width=480,Height=138},
            ["HighButton"]=new(){X=1055,Y=750,Width=495,Height=138},
            ["CustomVrTitle"]=new(){X=60,Y=662,Width=550,Height=48,FontSize=30,Text="CUSTOM VR PROFILE"},
            ["CustomVrDescription"]=new(){X=60,Y=716,Width=1440,Height=45,FontSize=25},
            ["CustomVrBrowse"]=new(){X=60,Y=790,Width=300,Height=56},
            ["CustomVrCaptureSettings"]=new(){X=600,Y=790,Width=460,Height=56},
            ["CustomVrRemoveSettings"]=new(){X=1090,Y=790,Width=460,Height=56},
            ["ControlsProfilesButtonBg"]=new(){X=40,Y=912,Width=480,Height=66},
            ["ControlsProfilesButton"]=new(){X=40,Y=912,Width=480,Height=66,FontSize=28},
            ["ControlsProfilesDescription"]=new(){X=550,Y=913,Width=490,Height=70,FontSize=22},
            ["MoreInfoButton"]=new(){X=1070,Y=932,Width=510,Height=60},
        });
    }

    private void ShowApplicationOptionsDialog()
    {
        using Form dialog = new()
        {
            Text = T("Nav.Options"),
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(1080, 810),
            MinimumSize = new Size(860, 700),
            FormBorderStyle = FormBorderStyle.Sizable,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = _backgroundColor,
            ForeColor = _textColor,
            AutoScaleMode = AutoScaleMode.None
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(40, 24, 40, 32),
            BackColor = _backgroundColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));

        Label heading = new()
        {
            Text = T("Options.Title"),
            Font = UiFont(32, FontStyle.Bold),
            ForeColor = ThemePalette.FromArgb(95, 225, 245),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false
        };

        var languagePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 4)
        };
        languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        languagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        Label languageLabel = new()
        {
            Text = T("Options.Language"),
            Font = UiFont(21, FontStyle.Bold),
            ForeColor = _textColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(48, 0, 12, 0)
        };
        ComboBox language = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemePalette.FromArgb(18, 30, 34),
            ForeColor = _textColor,
            Font = UiFont(19, FontStyle.Bold),
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 12, 0, 10)
        };
        foreach (LanguageOption languageOption in AppText.Languages)
            language.Items.Add(languageOption);
        language.DisplayMember = nameof(LanguageOption.Name);
        language.ValueMember = nameof(LanguageOption.Code);
        language.SelectedItem = language.Items.Cast<LanguageOption>()
            .FirstOrDefault(languageOption => languageOption.Code == _languageCode) ?? language.Items[0];
        LanguageFlagRenderer.Configure(language);
        languagePanel.Controls.Add(languageLabel, 0, 0);
        languagePanel.Controls.Add(language, 1, 0);

        var themePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 4)
        };
        themePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        themePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        Label themeLabel = new()
        {
            Text = T("Options.Theme"),
            Font = UiFont(21, FontStyle.Bold),
            ForeColor = _textColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(48, 0, 12, 0)
        };
        ComboBox theme = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemePalette.FromArgb(18, 30, 34),
            ForeColor = _textColor,
            Font = UiFont(19, FontStyle.Bold),
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 12, 0, 10)
        };
        foreach (string themeName in AppThemeAssets.AvailableThemes)
            theme.Items.Add(AppThemeAssets.DisplayName(themeName));
        int themeIndex = AppThemeAssets.AvailableThemes
            .Select((name, index) => (name, index))
            .FirstOrDefault(pair => pair.name.Equals(_uiTheme, StringComparison.OrdinalIgnoreCase)).index;
        theme.SelectedIndex = Math.Clamp(themeIndex, 0, Math.Max(0, theme.Items.Count - 1));
        themePanel.Controls.Add(themeLabel, 0, 0);
        themePanel.Controls.Add(theme, 1, 0);

        var betaPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 2, 4, 2) };
        ThemeCheckBox beta = new()
        {
            Text = T("Options.Beta"),
            Checked = _useBetaBuilds,
            Font = UiFont(20, FontStyle.Bold),
            ForeColor = _textColor,
            AutoSize = true,
            Location = new Point(8, 4)
        };
        Label betaHelp = new()
        {
            Text = T("Options.BetaHelp"),
            Font = UiFont(18, FontStyle.Regular),
            ForeColor = ThemePalette.FromArgb(190, 210, 214),
            AutoSize = false,
            Bounds = new Rectangle(48, 44, 820, 58),
            AutoEllipsis = false,
            TextAlign = ContentAlignment.TopLeft
        };
        betaPanel.Controls.AddRange(new Control[] { beta, betaHelp });
        betaPanel.Resize += (_, _) => betaHelp.Width = Math.Max(200, betaPanel.ClientSize.Width - 58);

        ThemeCheckBox minimize = new()
        {
            Text = T("Options.Minimize"),
            Checked = _minimizeToTray,
            Font = UiFont(21, FontStyle.Bold),
            ForeColor = _textColor,
            AutoSize = false,
            Dock = DockStyle.Fill
        };

        ThemeCheckBox startup = new()
        {
            Text = T("Options.Startup"),
            Checked = _startWithWindows,
            Font = UiFont(21, FontStyle.Bold),
            ForeColor = _textColor,
            AutoSize = false,
            Dock = DockStyle.Fill
        };

        Label trayHelp = new()
        {
            Text = T("Options.TrayHelp"),
            Font = UiFont(18, FontStyle.Regular),
            ForeColor = ThemePalette.FromArgb(190, 210, 214),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false,
            Padding = new Padding(48, 10, 0, 0)
        };

        var vjoyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 4)
        };
        vjoyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        vjoyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        Label vjoyHelp = new()
        {
            Text = T("Options.VJoyHelp"),
            Font = UiFont(18, FontStyle.Regular),
            ForeColor = ThemePalette.FromArgb(190, 210, 214),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            Padding = new Padding(48, 0, 12, 0)
        };
        ThemeButton uninstallVJoy = new()
        {
            Text = T("Options.UninstallVJoy"),
            Font = UiFont(18, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = ThemePalette.FromArgb(112, 36, 38),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 10, 0, 10)
        };
        uninstallVJoy.FlatAppearance.BorderColor = ThemePalette.FromArgb(190, 74, 70);
        uninstallVJoy.Click += async (_, _) =>
        {
            if (MessageBox.Show(dialog,
                    T("Options.UninstallConfirm"),
                    T("Options.UninstallTitle"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            uninstallVJoy.Enabled = false;
            string oldText = uninstallVJoy.Text;
            try
            {
                await HOTASTrimUtility.VJoySetup.UninstallAsync(message => uninstallVJoy.Text = message.Length > 28 ? "UNINSTALLING..." : message);
                MessageBox.Show(dialog,
                    T("Options.UninstallDone"),
                    T("Options.UninstallTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, ex.Message, T("Options.UninstallTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                uninstallVJoy.Text = oldText;
                uninstallVJoy.Enabled = true;
            }
        };
        vjoyPanel.Controls.Add(vjoyHelp, 0, 0);
        vjoyPanel.Controls.Add(uninstallVJoy, 1, 0);

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };
        // Percentage columns keep both framed actions fully visible at every
        // supported dialog width; no fixed-width button can spill off the edge.
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        ThemeButton save = new()
        {
            Text = T("Options.Save"),
            Font = UiFont(19, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = ThemePalette.FromArgb(25, 95, 55),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 10, 10, 10),
            DialogResult = DialogResult.OK
        };
        ThemeButton cancel = new()
        {
            Text = T("Options.Cancel"),
            Font = UiFont(19, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = ThemePalette.FromArgb(35, 55, 60),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 10, 0, 10),
            DialogResult = DialogResult.Cancel
        };
        buttons.Controls.Add(new Panel(), 0, 0);
        buttons.Controls.Add(save, 1, 0);
        buttons.Controls.Add(cancel, 2, 0);

        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        dialog.HandleCreated += (_, _) => IllustratedTheme.ApplyWindowChrome(dialog);

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(languagePanel, 0, 1);
        root.Controls.Add(themePanel, 0, 2);
        var showAircraft = new ThemeCheckBox { Text = "Show aircraft on Home", Checked = _showHomeAircraft,
            Font = UiFont(21, FontStyle.Bold), ForeColor = _textColor, Dock = DockStyle.Fill };
        var showKeybinds = new ThemeCheckBox { Text = "Show keybind profiles on Home", Checked = _showHomeKeybinds,
            Font = UiFont(21, FontStyle.Bold), ForeColor = _textColor, Dock = DockStyle.Fill };
        var homeOptions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        homeOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        homeOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        homeOptions.Controls.Add(showAircraft, 0, 0);
        homeOptions.Controls.Add(showKeybinds, 0, 1);
        root.Controls.Add(homeOptions, 0, 3);
        root.Controls.Add(minimize, 0, 4);
        root.Controls.Add(startup, 0, 5);
        root.Controls.Add(trayHelp, 0, 6);
        root.Controls.Add(vjoyPanel, 0, 7);
        root.Controls.Add(buttons, 0, 8);
        dialog.Controls.Add(root);

        // Resize typography and chrome together. This keeps the options dialog
        // readable on large displays and prevents the framed buttons from being
        // clipped against the bottom/right edge.
        var optionBaseFonts = new Dictionary<Control, float>();
        void CaptureOptionFonts(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                optionBaseFonts[child] = child.Font.Size;
                if (child.HasChildren) CaptureOptionFonts(child);
            }
        }
        CaptureOptionFonts(dialog);
        Padding baseRootPadding = root.Padding;
        Padding baseLanguagePadding = languageLabel.Padding;
        Padding baseThemePadding = themeLabel.Padding;
        Padding baseBetaPadding = betaPanel.Padding;
        Padding baseTrayPadding = trayHelp.Padding;
        Padding baseSaveMargin = save.Margin;
        Padding baseCancelMargin = cancel.Margin;
        float[] optionRows = { 58F, 70F, 70F, 112F, 72F, 72F, 0F, 96F, 116F };
        void ApplyOptionScale()
        {
            float scale = Math.Clamp(Math.Min(dialog.ClientSize.Width / 1080F, dialog.ClientSize.Height / 750F), 0.92F, 1.55F);
            Padding SP(Padding p) => new(
                (int)Math.Round(p.Left * scale), (int)Math.Round(p.Top * scale),
                (int)Math.Round(p.Right * scale), (int)Math.Round(p.Bottom * scale));
            root.Padding = SP(baseRootPadding);
            languageLabel.Padding = SP(baseLanguagePadding);
            themeLabel.Padding = SP(baseThemePadding);
            betaPanel.Padding = SP(baseBetaPadding);
            trayHelp.Padding = SP(baseTrayPadding);
            save.Margin = SP(baseSaveMargin);
            cancel.Margin = SP(baseCancelMargin);
            for (int i = 0; i < optionRows.Length; i++)
                if (root.RowStyles[i].SizeType == SizeType.Absolute) root.RowStyles[i].Height = optionRows[i] * scale;
            foreach ((Control control, float baseSize) in optionBaseFonts.ToArray())
            {
                if (control.IsDisposed) continue;
                float target = Math.Max(11F, baseSize * scale);
                if (Math.Abs(control.Font.Size - target) < .05F) continue;
                ResponsiveFonts.Set(control, target, control.Font.Style, control.Font.Unit);
            }
            betaHelp.SetBounds(
                (int)Math.Round(48 * scale), (int)Math.Round(44 * scale),
                Math.Max(220, betaPanel.ClientSize.Width - (int)Math.Round(58 * scale)),
                Math.Max(48, betaPanel.ClientSize.Height - (int)Math.Round(50 * scale)));
        }
        void FitOptionsToScreen()
        {
            Rectangle area = Screen.FromControl(dialog).WorkingArea;
            int frameWidth = Math.Max(0, dialog.Width - dialog.ClientSize.Width);
            int frameHeight = Math.Max(0, dialog.Height - dialog.ClientSize.Height);
            int maxClientWidth = Math.Max(760, area.Width - frameWidth - 24);
            int maxClientHeight = Math.Max(520, area.Height - frameHeight - 32);
            if (dialog.ClientSize.Width > maxClientWidth || dialog.ClientSize.Height > maxClientHeight)
                dialog.ClientSize = new Size(Math.Min(dialog.ClientSize.Width, maxClientWidth), Math.Min(dialog.ClientSize.Height, maxClientHeight));
        }
        dialog.Resize += (_, _) => ApplyOptionScale();
        dialog.Shown += (_, _) => { FitOptionsToScreen(); ApplyOptionScale(); };
        ApplyOptionScale();

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _useBetaBuilds = false;
        _minimizeToTray = minimize.Checked;
        _startWithWindows = startup.Checked;
        _showHomeAircraft = showAircraft.Checked;
        _showHomeKeybinds = showKeybinds.Checked;
        LayoutAircraftHomeFilter();
        _startMinimizedToTray = startup.Checked;
        _languageCode = language.SelectedItem is LanguageOption option ? option.Code : "en";
        string previousTheme = _uiTheme;
        int selectedThemeIndex = Math.Clamp(theme.SelectedIndex, 0, AppThemeAssets.AvailableThemes.Count - 1);
        _uiTheme = AppThemeAssets.AvailableThemes[selectedThemeIndex];
        ApplyLocalization();
        if (!SetWindowsStartup(_startWithWindows))
        {
            MessageBox.Show(this,
                T("Options.StartupFailed"),
                T("Options.DialogTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        SaveState();
        if (!string.Equals(previousTheme, _uiTheme, StringComparison.OrdinalIgnoreCase))
        {
            _allowExit = true;
            Application.Restart();
        }
    }

    private void ConfigureTrayIcon()
    {
        ContextMenuStrip menu = new();
        menu.Items.Add(T("Tray.Restore"), null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(T("Tray.Exit"), null, (_, _) =>
        {
            _allowExit = true;
            _trayIcon?.Dispose();
            _trayIcon = null;
            Close();
        });

        _trayIcon = new NotifyIcon
        {
            Text = T("Tray.AppName"),
            Icon = Icon ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void HideToTray()
    {
        _neckAssistForm?.Hide();
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Hide();
        _trayIcon?.ShowBalloonTip(1500, T("Tray.AppName"), T("Tray.Balloon"), ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    internal void RestoreFromExternalLaunch() => RestoreFromTray();

    private static bool SetWindowsStartup(bool enabled)
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (runKey == null) return false;
            const string valueName = "WTVRSettingsAssistant";
            if (enabled)
            {
                runKey.SetValue(valueName, "\"" + Application.ExecutablePath + "\" --startup");
            }
            else
            {
                runKey.DeleteValue(valueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch { return false; }
    }

    private void BuildGraphicsApiControls()
    {
        _desktopGraphicsApiLabel = new Label
        {
            Text = "",
            BackColor = Color.Transparent,
            Visible = false,
            AutoSize = false
        };

        _vrGraphicsApiLabel = new Label
        {
            Text = "",
            BackColor = Color.Transparent,
            Visible = false,
            AutoSize = false
        };

        _desktopGraphicsApiCombo = CreateGraphicsApiComboBox(_desktopGraphicsApi);
        _vrGraphicsApiCombo = CreateGraphicsApiComboBox(_vrGraphicsApi);

        _desktopGraphicsApiCombo.SelectedIndexChanged += (_, _) =>
        {
            _desktopGraphicsApi = _desktopGraphicsApiCombo.SelectedIndex == 0
                ? GraphicsApi.DX11
                : GraphicsApi.DX12;
            SaveState();

            if (_lastAppliedMode == AppliedMode.Monitor)
            {
                ApplyGraphicsApiToConfig(AppliedMode.Monitor);
            }
        };

        _vrGraphicsApiCombo.SelectedIndexChanged += (_, _) =>
        {
            _vrGraphicsApi = _vrGraphicsApiCombo.SelectedIndex == 0
                ? GraphicsApi.DX11
                : GraphicsApi.DX12;
            SaveState();

            if (_lastAppliedMode == AppliedMode.VR)
            {
                ApplyGraphicsApiToConfig(AppliedMode.VR);
            }
        };

        _settingsPanel.Controls.Add(_desktopGraphicsApiLabel);
        _settingsPanel.Controls.Add(_desktopGraphicsApiCombo);
        _settingsPanel.Controls.Add(_vrGraphicsApiLabel);
        _settingsPanel.Controls.Add(_vrGraphicsApiCombo);

        SyncGraphicsApiComboSelections();

        _desktopGraphicsApiLabel.BringToFront();
        _desktopGraphicsApiCombo.BringToFront();
        _vrGraphicsApiLabel.BringToFront();
        _vrGraphicsApiCombo.BringToFront();

        PositionGraphicsApiControls();
    }

    private ComboBox CreateGraphicsApiComboBox(GraphicsApi selectedApi)
    {
        ComboBox combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemePalette.FromArgb(18, 30, 34),
            ForeColor = _textColor,
            Font = UiFont(15, FontStyle.Bold),
            IntegralHeight = false,
            DropDownHeight = 90,
            DropDownWidth = 118
        };

        combo.Items.Add("DX11");
        combo.Items.Add("DX12");
        combo.SelectedIndex = selectedApi == GraphicsApi.DX11 ? 0 : 1;
        return combo;
    }

    private void PositionGraphicsApiControls()
    {
        if (_settingsPanel == null ||
            _desktopGraphicsApiLabel == null ||
            _desktopGraphicsApiCombo == null ||
            _vrGraphicsApiLabel == null ||
            _vrGraphicsApiCombo == null)
        {
            return;
        }

        float scaleX = _settingsPanel.ClientSize.Width / (float)BaseClientWidth;
        int designHeight = IllustratedTheme.Enabled ? 1000 : BaseClientHeight;
        float scaleY = _settingsPanel.ClientSize.Height / (float)designHeight;
        float scale = Math.Max(0.1f, Math.Min(scaleX, scaleY));

        float drawnWidth = BaseClientWidth * scale;
        float drawnHeight = designHeight * scale;
        float offsetX = (_settingsPanel.ClientSize.Width - drawnWidth) / 2f;
        float offsetY = (_settingsPanel.ClientSize.Height - drawnHeight) / 2f;

        Rectangle ScaleRect(Rectangle r)
        {
            return new Rectangle(
                (int)Math.Round(offsetX + r.X * scale),
                (int)Math.Round(offsetY + r.Y * scale),
                Math.Max(1, (int)Math.Round(r.Width * scale)),
                Math.Max(1, (int)Math.Round(r.Height * scale)));
        }

        // Compact renderer selectors only. No separate "API" text labels.
        // Desktop: between Browse and Capture Settings.
        Rectangle desktopLabelBounds = Rectangle.Empty;
        Rectangle desktopComboBounds = ScaleRect(new Rectangle(555, 315, 118, 40));

        Rectangle vrLabelBounds = Rectangle.Empty;
        Rectangle vrComboBounds;

        if (_customVrEnabled)
        {
            // Custom VR: between Browse and Capture Settings.
            vrComboBounds = ScaleRect(new Rectangle(555, 490, 118, 40));
        }
        else
        {
            // Preset mode: keep the selector in the same DX column used by custom VR.
            vrComboBounds = ScaleRect(new Rectangle(555, 435, 118, 40));
        }

        _desktopGraphicsApiLabel.Bounds = desktopLabelBounds;
        if(IllustratedTheme.Enabled)
        {
            desktopComboBounds=ScaleRect(new Rectangle(390,490,150,46));
            vrComboBounds=ScaleRect(_customVrEnabled ? new Rectangle(390,795,150,46) : new Rectangle(650,665,150,46));
        }
        _desktopGraphicsApiCombo.Bounds = desktopComboBounds;
        _vrGraphicsApiLabel.Bounds = vrLabelBounds;
        _vrGraphicsApiCombo.Bounds = vrComboBounds;

        float fontScale = Math.Max(0.7f, scale);
        _desktopGraphicsApiLabel.Font = UiFont(14 * fontScale, FontStyle.Bold);
        _desktopGraphicsApiCombo.Font = UiFont(16 * fontScale, FontStyle.Bold);
        _vrGraphicsApiLabel.Font = UiFont(14 * fontScale, FontStyle.Bold);
        _vrGraphicsApiCombo.Font = UiFont(16 * fontScale, FontStyle.Bold);
        PositionProfileControlSelectors();
    }

    private void BuildProfileControlSelectors()
    {
        _desktopControlsLabel = CreateProfileControlLabel("CONTROLS PROFILE");
        _vrControlsLabel = CreateProfileControlLabel("CONTROLS PROFILE");
        _desktopControlsCombo = CreateProfileControlCombo();
        _vrControlsCombo = CreateProfileControlCombo();
        _desktopControlsAddButton = CreateProfileControlButton("ADD");
        _vrControlsAddButton = CreateProfileControlButton("ADD");
        _desktopControlsRemoveButton = CreateProfileControlButton("CLEAR");
        _vrControlsRemoveButton = CreateProfileControlButton("CLEAR");

        _desktopControlsCombo.SelectedIndexChanged += (_, _) => SelectControlProfileFromCombo(AppliedMode.Monitor);
        _vrControlsCombo.SelectedIndexChanged += (_, _) => SelectControlProfileFromCombo(AppliedMode.VR);
        _desktopControlsAddButton.Click += (_, _) => AddControlProfiles(AppliedMode.Monitor);
        _vrControlsAddButton.Click += (_, _) => AddControlProfiles(AppliedMode.VR);
        _desktopControlsRemoveButton.Click += (_, _) => RemoveSelectedControlProfile(AppliedMode.Monitor);
        _vrControlsRemoveButton.Click += (_, _) => RemoveSelectedControlProfile(AppliedMode.VR);

        foreach (Control control in new Control[]
        {
            _desktopControlsLabel, _desktopControlsCombo, _desktopControlsAddButton, _desktopControlsRemoveButton,
            _vrControlsLabel, _vrControlsCombo, _vrControlsAddButton, _vrControlsRemoveButton
        })
        {
            _settingsPanel.Controls.Add(control);
            control.BringToFront();
        }

        RefreshControlProfileCombos();
        PositionProfileControlSelectors();
    }

    private Label CreateProfileControlLabel(string text) => new()
    {
        Text = text,
        BackColor = Color.Transparent,
        ForeColor = IllustratedTheme.Enabled ? ThemePalette.FromArgb(238, 196, 88) : _textColor,
        Font = UiFont(15, FontStyle.Bold),
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private ComboBox CreateProfileControlCombo()
    {
        ComboBox combo = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 40,
            BackColor = ThemePalette.FromArgb(18, 30, 34),
            ForeColor = _textColor,
            Font = UiFont(16, FontStyle.Bold),
            IntegralHeight = false,
            DropDownHeight = 220,
            DropDownWidth = 360
        };
        combo.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selected ? ThemePalette.FromArgb(68, 56, 28) : ThemePalette.FromArgb(18, 30, 34);
            Color border = selected ? ThemePalette.FromArgb(232, 184, 76) : ThemePalette.FromArgb(48, 63, 68);
            using SolidBrush backgroundBrush = new(background);
            e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
            using Pen borderPen = new(border);
            e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 1), Math.Max(0, e.Bounds.Height - 1));
            string text = combo.GetItemText(combo.Items[e.Index]) ?? "";
            Rectangle textBounds = new(e.Bounds.X + 10, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 14), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, combo.Font, textBounds, _textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };
        return combo;
    }

    private Button CreateProfileControlButton(string text) => new ThemeButton
    {
        Text = text,
        FlatStyle = FlatStyle.Flat,
        BackColor = ThemePalette.FromArgb(18, 30, 34),
        ForeColor = ThemePalette.FromArgb(244, 201, 96),
        Font = UiFont(15, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private void PositionProfileControlSelectors()
    {
        if (_settingsPanel == null ||
            _desktopControlsLabel == null || _desktopControlsCombo == null ||
            _desktopControlsAddButton == null || _desktopControlsRemoveButton == null ||
            _vrControlsLabel == null || _vrControlsCombo == null ||
            _vrControlsAddButton == null || _vrControlsRemoveButton == null)
        {
            return;
        }

        float scaleX = _settingsPanel.ClientSize.Width / (float)BaseClientWidth;
        int designHeight = IllustratedTheme.Enabled ? 1000 : BaseClientHeight;
        float scaleY = _settingsPanel.ClientSize.Height / (float)designHeight;
        float scale = Math.Max(0.1f, Math.Min(scaleX, scaleY));
        float drawnWidth = BaseClientWidth * scale;
        float drawnHeight = designHeight * scale;
        float offsetX = (_settingsPanel.ClientSize.Width - drawnWidth) / 2f;
        float offsetY = (_settingsPanel.ClientSize.Height - drawnHeight) / 2f;

        Rectangle ScaleRect(Rectangle r) => new(
            (int)Math.Round(offsetX + r.X * scale),
            (int)Math.Round(offsetY + r.Y * scale),
            Math.Max(1, (int)Math.Round(r.Width * scale)),
            Math.Max(1, (int)Math.Round(r.Height * scale)));

        Rectangle desktopLabel = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(60, 555, 430, 48))
            : ScaleRect(new Rectangle(75, 370, 250, 28));
        Rectangle desktopCombo = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(520, 552, 560, 54))
            : ScaleRect(new Rectangle(690, 370, 250, 40));
        Rectangle desktopAdd = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(1110, 552, 120, 54))
            : ScaleRect(new Rectangle(950, 370, 62, 40));
        Rectangle desktopRemove = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(1245, 552, 135, 54))
            : ScaleRect(new Rectangle(1020, 370, 62, 40));

        Rectangle vrLabel = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(60, 858, 430, 48))
            : ScaleRect(new Rectangle(75, 545, 250, 28));
        Rectangle vrCombo = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(520, 855, 560, 54))
            : ScaleRect(new Rectangle(690, 545, 250, 40));
        Rectangle vrAdd = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(1110, 855, 120, 54))
            : ScaleRect(new Rectangle(950, 545, 62, 40));
        Rectangle vrRemove = IllustratedTheme.Enabled
            ? ScaleRect(new Rectangle(1245, 855, 135, 54))
            : ScaleRect(new Rectangle(1020, 545, 62, 40));

        _desktopControlsLabel.Bounds = desktopLabel;
        _desktopControlsCombo.Bounds = desktopCombo;
        _desktopControlsAddButton.Bounds = desktopAdd;
        _desktopControlsRemoveButton.Bounds = desktopRemove;
        _vrControlsLabel.Bounds = vrLabel;
        _vrControlsCombo.Bounds = vrCombo;
        _vrControlsAddButton.Bounds = vrAdd;
        _vrControlsRemoveButton.Bounds = vrRemove;

        float fontScale = Math.Max(0.95f, scale);
        foreach (Control control in new Control[] { _desktopControlsLabel, _vrControlsLabel })
            control.Font = UiFont(15 * fontScale, FontStyle.Bold);
        foreach (Control control in new Control[] { _desktopControlsCombo, _vrControlsCombo })
            control.Font = UiFont(16 * fontScale, FontStyle.Bold);
        foreach (Control control in new Control[] { _desktopControlsAddButton, _desktopControlsRemoveButton, _vrControlsAddButton, _vrControlsRemoveButton })
            control.Font = UiFont(15 * fontScale, FontStyle.Bold);
    }

    private void UpdateControlProfileSelectorStates()
    {
        bool settingsVisible = _settingsPanel?.Visible == true;
        bool showDesktop = settingsVisible;
        bool showVr = settingsVisible && _customVrEnabled;

        SetControlProfileSelectorVisible(_desktopControlsLabel, _desktopControlsCombo, _desktopControlsAddButton, _desktopControlsRemoveButton, showDesktop, HasSelectedControlsProfile(AppliedMode.Monitor));
        SetControlProfileSelectorVisible(_vrControlsLabel, _vrControlsCombo, _vrControlsAddButton, _vrControlsRemoveButton, showVr, HasSelectedControlsProfile(AppliedMode.VR));
    }

    private static void SetControlProfileSelectorVisible(Label? label, ComboBox? combo, Button? addButton, Button? removeButton, bool visible, bool hasSelection)
    {
        if (label != null) label.Visible = visible;
        if (combo != null) combo.Visible = visible;
        if (addButton != null) addButton.Visible = visible;
        if (removeButton != null)
        {
            removeButton.Visible = visible;
            removeButton.Enabled = visible && hasSelection;
        }

        if (!visible) return;

        label?.BringToFront();
        combo?.BringToFront();
        addButton?.BringToFront();
        removeButton?.BringToFront();
    }

    private void RefreshControlProfileCombos()
    {
        _syncingControlProfileCombos = true;
        try
        {
            RefreshControlProfileCombo(_desktopControlsCombo, _desktopControlProfiles, _selectedDesktopControlProfileId);
            RefreshControlProfileCombo(_vrControlsCombo, _vrControlProfiles, _selectedVrControlProfileId);
        }
        finally
        {
            _syncingControlProfileCombos = false;
        }
        RefreshControlProfileFooter();
    }

    private static void RefreshControlProfileCombo(ComboBox? combo, List<ControlProfileEntry> profiles, string selectedId)
    {
        if (combo == null) return;
        combo.Items.Clear();
        combo.Items.Add(new ControlProfileComboItem(null, "No controls profile"));
        foreach (ControlProfileEntry profile in profiles.Where(p => !string.IsNullOrWhiteSpace(p.Path)).ToArray())
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
                profile.Name = Path.GetFileNameWithoutExtension(profile.Path);
            combo.Items.Add(new ControlProfileComboItem(profile, profile.Name));
        }

        int selectedIndex = 0;
        for (int i = 1; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ControlProfileComboItem item &&
                item.Profile?.Id == selectedId)
            {
                selectedIndex = i;
                break;
            }
        }
        combo.SelectedIndex = selectedIndex;
    }

    private void SelectControlProfileFromCombo(AppliedMode mode)
    {
        if (_syncingControlProfileCombos) return;
        ComboBox? combo = mode == AppliedMode.Monitor ? _desktopControlsCombo : _vrControlsCombo;
        ControlProfileEntry? selected = (combo?.SelectedItem as ControlProfileComboItem)?.Profile;
        SelectControlProfile(mode, selected);
    }

    private void SelectControlProfile(AppliedMode mode, ControlProfileEntry? selected)
    {
        if (mode == AppliedMode.Monitor)
        {
            _selectedDesktopControlProfileId = selected?.Id ?? "";
            _desktopControlsBlkPath = selected?.Path ?? "";
        }
        else
        {
            _selectedVrControlProfileId = selected?.Id ?? "";
            _vrControlsBlkPath = selected?.Path ?? "";
        }
        SaveState();
        UpdateVisualStates();
    }

    private void AddControlProfiles(AppliedMode mode)
    {
        using OpenFileDialog open = new()
        {
            Title = mode == AppliedMode.Monitor ? "Add Monitor controls presets" : "Add Custom VR controls presets",
            Filter = "War Thunder controls BLK files (*.blk)|*.blk|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (open.ShowDialog(this) != DialogResult.OK) return;

        List<ControlProfileEntry> list = mode == AppliedMode.Monitor ? _desktopControlProfiles : _vrControlProfiles;
        foreach (string path in open.FileNames.Where(path => Path.GetExtension(path).Equals(".blk", StringComparison.OrdinalIgnoreCase)))
            AddOrSelectControlProfile(list, path, mode);

        EnsureMachineBlkDetected();
        SaveState();
        RefreshControlProfileCombos();
        UpdateVisualStates();
    }

    private void EnsureMachineBlkDetected()
    {
        if (File.Exists(_machineBlkPath)) return;
        string? detected = TryAutoDetectMachineBlk();
        if (!string.IsNullOrWhiteSpace(detected))
            _machineBlkPath = detected;
    }

    private void RemoveSelectedControlProfile(AppliedMode mode)
    {
        List<ControlProfileEntry> list = mode == AppliedMode.Monitor ? _desktopControlProfiles : _vrControlProfiles;
        string selectedId = mode == AppliedMode.Monitor ? _selectedDesktopControlProfileId : _selectedVrControlProfileId;
        if (string.IsNullOrWhiteSpace(selectedId)) return;
        list.RemoveAll(profile => profile.Id == selectedId);
        if (mode == AppliedMode.Monitor)
        {
            _selectedDesktopControlProfileId = "";
            _desktopControlsBlkPath = "";
        }
        else
        {
            _selectedVrControlProfileId = "";
            _vrControlsBlkPath = "";
        }
        SaveState();
        RefreshControlProfileCombos();
        UpdateVisualStates();
    }

    private void AddOrSelectControlProfile(List<ControlProfileEntry> list, string path, AppliedMode mode)
    {
        string fullPath = Path.GetFullPath(path);
        ControlProfileEntry? existing = list.FirstOrDefault(profile => PathsEqual(profile.Path, fullPath));
        if (existing == null)
        {
            existing = new ControlProfileEntry
            {
                Name = Path.GetFileNameWithoutExtension(fullPath),
                Path = fullPath
            };
            list.Add(existing);
        }
        if (mode == AppliedMode.Monitor)
        {
            _selectedDesktopControlProfileId = existing.Id;
            _desktopControlsBlkPath = existing.Path;
        }
        else
        {
            _selectedVrControlProfileId = existing.Id;
            _vrControlsBlkPath = existing.Path;
        }
    }

    private string GetSelectedControlsPath(AppliedMode mode)
    {
        string selectedId = mode == AppliedMode.Monitor ? _selectedDesktopControlProfileId : _selectedVrControlProfileId;
        List<ControlProfileEntry> list = mode == AppliedMode.Monitor ? _desktopControlProfiles : _vrControlProfiles;
        string fallback = mode == AppliedMode.Monitor ? _desktopControlsBlkPath : _vrControlsBlkPath;
        ControlProfileEntry? profile = list.FirstOrDefault(p => p.Id == selectedId);
        return profile?.Path ?? fallback ?? "";
    }

    private bool HasSelectedControlsProfile(AppliedMode mode) =>
        !string.IsNullOrWhiteSpace(GetSelectedControlsPath(mode));

    private static void MigrateControlProfileList(List<ControlProfileEntry> profiles, string legacyPath, ref string selectedId)
    {
        foreach (ControlProfileEntry profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
                profile.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(profile.Name) && !string.IsNullOrWhiteSpace(profile.Path))
                profile.Name = Path.GetFileNameWithoutExtension(profile.Path);
        }

        if (!string.IsNullOrWhiteSpace(legacyPath) && profiles.All(profile => !PathsEqual(profile.Path, legacyPath)))
        {
            profiles.Add(new ControlProfileEntry
            {
                Name = Path.GetFileNameWithoutExtension(legacyPath),
                Path = legacyPath
            });
        }

        string currentSelectedId = selectedId;
        if (!profiles.Any(profile => profile.Id == currentSelectedId))
            selectedId = profiles.FirstOrDefault(profile => PathsEqual(profile.Path, legacyPath))?.Id ?? "";
    }

    private void BuildRecommendedScreen()
    {
        _recommendedPanel = CreateScreenPanel();
        Controls.Add(_recommendedPanel);

        _recommendedCanvas = new MainPageCanvas(RecommendedSelectionLayoutFilePath, _backgroundColor, _textColor, UiFont)
        {
            Dock = DockStyle.Fill
        };

        _recommendedPanel.Controls.Add(_recommendedCanvas);
        BuildRecommendedSelectionCanvas();
    }

    private void ShowRecommendedSettingsFlow()
    {
        BuildRecommendedSelectionCanvas();
        ShowScreen(_recommendedPanel);
    }

    private void ShowRecommendedGpuGraph()
    {
        using RecommendedVrSettingsForm popup = new RecommendedVrSettingsForm(
            _verticalLineImage,
            _backgroundColor,
            _textColor,
            BakedRecommendedGpuLayoutJson,
            _languageCode);

        try
        {
            popup.Icon = Icon;
        }
        catch
        {
            // Ignore icon issues for this helper window.
        }

        popup.StartPosition = FormStartPosition.CenterParent;
        popup.ShowDialog(this);
    }

    private void BuildRecommendedSelectionCanvas()
    {
        _recommendedCanvas.SetLayoutFilePath(RecommendedSelectionLayoutFilePath);
        _recommendedCanvas.ClearItems();

        if (_backArrowImage != null)
        {
            _recommendedCanvas.AddImage("RecommendedBackArrow", _backArrowImage, new Rectangle(45, 72, 98, 80), () => ShowScreen(_settingsPanel));
        }
        else
        {
            _recommendedCanvas.AddText("RecommendedBackArrow", "<", new Rectangle(45, 72, 98, 80), 52f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, () => ShowScreen(_settingsPanel));
        }

        _recommendedCanvas.AddImage("RecommendedInfoIcon", _infoImage, new Rectangle(1408, 45, 81, 81), () => ShowScreen(_aboutPanel));
        _recommendedCanvas.AddImage("RecommendedSettingsIcon", _settingsImage, new Rectangle(1498, 28, 114, 114), () => ShowScreen(_settingsPanel));

        _recommendedCanvas.AddText(
            "RecommendedSelectTitle",
            "Click on the software you use\nto connect your vr headset to your PC.",
            new Rectangle(150, 150, 1325, 210),
            56f,
            FontStyle.Regular,
            StringAlignment.Center,
            StringAlignment.Center);

        AddRecommendedSoftwareChoice("RecommendedVDChoice", _vdImage, "VD", new Rectangle(330, 407, 235, 235));
        AddRecommendedSoftwareChoice("RecommendedMetaChoice", _metaImage, "Meta", new Rectangle(647, 410, 330, 231));
        AddRecommendedSoftwareChoice("RecommendedSteamVRChoice", _steamVrSoftwareImage, "SteamVR", new Rectangle(1010, 424, 360, 203));

        _recommendedCanvas.ApplyLayout(ParseBakedLayout(BakedRecommendedSelectionLayoutJson));
        _recommendedCanvas.SetText("RecommendedSelectTitle", T("Recommended.SelectSoftware"));
    }

    private void AddRecommendedSoftwareChoice(string key, Image? image, string prefix, Rectangle bounds)
    {
        Action clickAction = () => ShowRecommendedPageViewer(prefix, 1);

        if (image != null)
        {
            _recommendedCanvas.AddImage(key, image, bounds, clickAction, hoverZoom: true);
        }
        else
        {
            _recommendedCanvas.AddText(key, prefix, bounds, 42f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, clickAction);
        }
    }

    private void ShowRecommendedPageViewer(string prefix, int page)
    {
        _recommendedSoftwarePrefix = prefix;
        _recommendedPage = Math.Clamp(page, 1, prefix == "VD" ? 6 : 5);
        _recommendedSoftwareIcon = GetRecommendedSoftwareIcon(prefix);

        BuildRecommendedPageCanvas();
        ShowScreen(_recommendedPanel);
    }

    private void BuildRecommendedPageCanvas()
    {
        _recommendedCanvas.SetLayoutFilePath(RecommendedViewerLayoutFilePath);
        _recommendedCanvas.ClearItems();

        if (_backArrowImage != null)
        {
            _recommendedCanvas.AddImage("RecommendedBackArrow", _backArrowImage, new Rectangle(45, 72, 98, 80), BuildRecommendedSelectionCanvasAndShow);
        }
        else
        {
            _recommendedCanvas.AddText("RecommendedBackArrow", "<", new Rectangle(45, 72, 98, 80), 52f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, BuildRecommendedSelectionCanvasAndShow);
        }

        _recommendedCanvas.AddImage("RecommendedInfoIcon", _infoImage, new Rectangle(1408, 45, 81, 81), () => ShowScreen(_aboutPanel));
        _recommendedCanvas.AddImage("RecommendedSettingsIcon", _settingsImage, new Rectangle(1498, 28, 114, 114), () => ShowScreen(_settingsPanel));

        string selectedIconLayoutKey = $"RecommendedSelectedSoftwareIcon_{_recommendedSoftwarePrefix}";
        Rectangle selectedIconBounds = GetRecommendedSoftwareIconBounds(_recommendedSoftwarePrefix);
        if (_recommendedSoftwareIcon != null)
        {
            _recommendedCanvas.AddImage(selectedIconLayoutKey, _recommendedSoftwareIcon, selectedIconBounds);
        }
        else
        {
            _recommendedCanvas.AddText(selectedIconLayoutKey, _recommendedSoftwarePrefix, selectedIconBounds, 36f, FontStyle.Bold);
        }

        Image? pageImage = TryLoadImage($"{_recommendedSoftwarePrefix}_Page_{_recommendedPage}.png");
        if (pageImage != null)
        {
            _recommendedCanvas.AddImage("RecommendedPageImage", pageImage, new Rectangle(0, 230, 1625, 517));
        }
        else
        {
            _recommendedCanvas.AddRectangle("RecommendedPageImageBackground", new Rectangle(0, 230, 1625, 517), ThemePalette.FromArgb(28, 36, 38));
            _recommendedCanvas.AddText(
                "RecommendedPageImageMissingText",
                TF("Recommended.MissingImage", $"{_recommendedSoftwarePrefix}_Page_{_recommendedPage}.png"),
                new Rectangle(0, 230, 1625, 517),
                30f,
                FontStyle.Italic,
                StringAlignment.Center,
                StringAlignment.Center);
        }

        bool canGoBack = _recommendedPage > 1;
        bool canGoNext = _recommendedPage < (_recommendedSoftwarePrefix == "VD" ? 6 : 5);
        Action? backClickAction = null;
        if (canGoBack)
        {
            backClickAction = () => ShowRecommendedPageViewer(_recommendedSoftwarePrefix, _recommendedPage - 1);
        }

        Action? nextClickAction = null;
        if (canGoNext)
        {
            nextClickAction = () => ShowRecommendedPageViewer(_recommendedSoftwarePrefix, _recommendedPage + 1);
        }

        AddRecommendedNavButton(
            "RecommendedBottomBack",
            canGoBack ? _backButtonImage : _backGrayButtonImage,
            T("Recommended.Back"),
            new Rectangle(333, 754, 340, 81),
            backClickAction);

        _recommendedCanvas.AddImage("RecommendedBottomHome", _homeImage, new Rectangle(773, 758, 78, 73), () => ShowScreen(_mainPanel));

        AddRecommendedNavButton(
            "RecommendedBottomNext",
            canGoNext ? _nextButtonImage : _nextGrayButtonImage,
            T("Recommended.Next"),
            new Rectangle(955, 754, 340, 81),
            nextClickAction);

        _recommendedCanvas.ApplyLayout(ParseBakedLayout(BakedRecommendedViewerLayoutJson));
        _recommendedCanvas.SetText("RecommendedBottomBack", T("Recommended.Back"));
        _recommendedCanvas.SetText("RecommendedBottomNext", T("Recommended.Next"));
    }

    private void BuildRecommendedSelectionCanvasAndShow()
    {
        BuildRecommendedSelectionCanvas();
        ShowScreen(_recommendedPanel);
    }

    private void AddRecommendedNavButton(string key, Image? image, string fallbackText, Rectangle bounds, Action? clickAction)
    {
        if (image != null)
        {
            _recommendedCanvas.AddImage(key, image, bounds, clickAction, hoverZoom: true);
        }
        else
        {
            _recommendedCanvas.AddText(key, fallbackText, bounds, 34f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, clickAction);
        }
    }

    private Rectangle GetRecommendedSoftwareIconBounds(string prefix)
    {
        if (prefix.Equals("VD", StringComparison.OrdinalIgnoreCase)) return new Rectangle(704, 4, 216, 216);
        if (prefix.Equals("Meta", StringComparison.OrdinalIgnoreCase)) return new Rectangle(687, 28, 251, 175);
        if (prefix.Equals("SteamVR", StringComparison.OrdinalIgnoreCase)) return new Rectangle(642, 16, 341, 192);
        return new Rectangle(693, 28, 238, 166);
    }

    private Image? GetRecommendedSoftwareIcon(string prefix)
    {
        if (prefix.Equals("VD", StringComparison.OrdinalIgnoreCase)) return _vdImage;
        if (prefix.Equals("Meta", StringComparison.OrdinalIgnoreCase)) return _metaImage;
        if (prefix.Equals("SteamVR", StringComparison.OrdinalIgnoreCase)) return _steamVrSoftwareImage;
        return null;
    }

    private void BuildPresetPanel()
    {
        _presetPanel = new Panel
        {
            Left = S(48),
            Top = S(275),
            Width = S(1235),
            Height = S(330),
            BackColor = _backgroundColor
        };

        _settingsPanel.Controls.Add(_presetPanel);

        CreateLabel(
            _presetPanel,
            "RECOMMENDED VR SETTINGS",
            0,
            0,
            760,
            35,
            28,
            FontStyle.Regular
        );

        _lowButton = CreateImageButton(_presetPanel, _lowRed, 0, 50, 330, 170, () =>
        {
            SelectVrPreset(VrPreset.Low);
        });

        _mediumButton = CreateImageButton(_presetPanel, _mediumRed, 420, 50, 330, 170, () =>
        {
            SelectVrPreset(VrPreset.Medium);
        });

        _highButton = CreateImageButton(_presetPanel, _highRed, 840, 50, 330, 170, () =>
        {
            SelectVrPreset(VrPreset.High);
        });

        string noteText =
            "Choose the preset based on the examples provided bellow.\n" +
            "Note: You can change those at any time, it will replace your current VR settings with one of the following presets, if you tweak and customize the settings I recommend using\n" +
            "CUSTOM VR .cfg Toggle ON, and loading your perfected VR Settings as a cfg file).";

        CreateLabel(
            _presetPanel,
            noteText,
            0,
            235,
            1240,
            80,
            14,
            FontStyle.Italic
        );

    }

    private void BuildCustomVrArea()
    {
        CreateLabel(
            _settingsPanel,
            "CUSTOM VR .blk",
            705,
            105,
            430,
            42,
            30,
            FontStyle.Regular
        );

        _customVrToggleButton = CreateImageButton(
            _settingsPanel,
            _buttonOff,
            690,
            155,
            300,
            120,
            ToggleCustomVr
        );

        _customVrPanel = new Panel
        {
            Left = S(48),
            Top = S(315),
            Width = S(650),
            Height = S(210),
            BackColor = _backgroundColor,
            Visible = false
        };

        _settingsPanel.Controls.Add(_customVrPanel);

        AddFilePicker(
            _customVrPanel,
            "VR .blk",
            "Your custom settings for War Thunder when playing in VR.",
            0,
            0,
            FileSlot.CustomVr,
            out _customVrPathBox,
            out _customVrBrowseButton
        );
    }

    private void BuildAboutScreen()
    {
        _aboutPanel = CreateScreenPanel();
        Controls.Add(_aboutPanel);

        _aboutCanvas = new MainPageCanvas(IllustratedTheme.Enabled ? Path.Combine(SettingsFolder,"illustrated_info.json") : AboutLayoutFilePath, _backgroundColor, _textColor, UiFont)
        {
            Dock = DockStyle.Fill
        };

        _aboutPanel.Controls.Add(_aboutCanvas);

        _aboutCanvas.AddText(
            "AboutTitle",
            "WAR THUNDER VR ASSISTANT",
            new Rectangle(40, 35, 1280, 65),
            40f,
            FontStyle.Bold,
            StringAlignment.Near,
            StringAlignment.Center);

        _aboutCanvas.AddText(
            "AboutPurpose",
            "A Windows utility for switching War Thunder between Desktop and VR profiles and providing configurable neck-rotation assistance. It supports OpenXR runtimes including SteamVR OpenXR and VDXR, manages graphics, renderer and control profiles, launches the selected setup, and can extend comfortable head movement for rear visibility in VR.",
            new Rectangle(40, 115, 1480, 120),
            27f,
            FontStyle.Regular,
            StringAlignment.Near,
            StringAlignment.Near);

        _aboutCanvas.AddRectangle("AboutTopDivider", new Rectangle(40, 250, 1480, 2), ThemePalette.FromArgb(62, 82, 88));

        _aboutCanvas.AddText(
            "HowToTitle",
            "HOW TO USE",
            new Rectangle(40, 275, 700, 55),
            32f,
            FontStyle.Bold,
            StringAlignment.Near,
            StringAlignment.Center);

        _aboutCanvas.AddText(
            "HowToText",
            "1. Select the War Thunder installation folder; the app finds config.blk and the launcher.\n\n" +
            "2. Capture Desktop/VR graphics and optional control profiles, then press MONITOR or VR to apply them.\n\n" +
            "3. Open Neck Assistant and switch it ON before starting VR. It works with OpenXR runtimes including SteamVR OpenXR and VDXR; the green icon confirms it remains active.\n\n" +
            "4. ADVANCED uses adjustable rear-view curves and can work as Toggle or Hold. SIMPLE adds a fixed rear rotation while its assigned input is held.\n\n" +
            "5. Bind keyboard, mouse or HOTAS inputs. Simple uses War Thunder's in-game recenter and its deadzone chooses the viewing direction.",
            new Rectangle(40, 340, 700, 365),
            25f,
            FontStyle.Regular,
            StringAlignment.Near,
            StringAlignment.Near);

        _aboutCanvas.AddText(
            "PatchNotesTitle",
            $"PATCH NOTES  ·  v1.0 → v{CurrentVersion}",
            new Rectangle(40, 275, 1480, 55),
            32f,
            FontStyle.Bold,
            StringAlignment.Near,
            StringAlignment.Center);

        _aboutCanvas.AddText(
            "PatchNotesText",
            "• New KeyBind Assistant maps keyboard, mouse or HOTAS inputs to hidden War Thunder commands\n\n" +
            "• VR head position Up/Down and Switch map to battlefield shortcuts\n\n" +
            "• Persistent blue OFF / green ACTIVE icons for both assistants\n\n" +
            "• Corrected game input injection and administrator-level error reporting\n\n" +
            "• App options for beta updates, minimize to tray and start with Windows\n\n" +
            "• Single-instance protection: reopening restores the existing window\n\n" +
            "• Safer updates preserve settings, graphics profiles and control profiles",
            new Rectangle(40, 340, 1480, 470),
            24f,
            FontStyle.Regular,
            StringAlignment.Near,
            StringAlignment.Near);

        _aboutCanvas.AddText(
            "OpenSourceText",
            "Free and open source community software. This unofficial tool is not affiliated with, endorsed by, or sponsored by Gaijin Entertainment.",
            new Rectangle(40, 735, 1160, 80),
            22f,
            FontStyle.Italic,
            StringAlignment.Near,
            StringAlignment.Center);

        _aboutCanvas.AddImage("InfoIcon", _infoImage, new Rectangle(1408, 45, 81, 81), () =>
        {
            ShowScreen(_aboutPanel);
        });

        _aboutCanvas.AddImage("HomeIcon", _homeImage, new Rectangle(1498, 28, 114, 115), () =>
        {
            ShowScreen(_mainPanel);
        });

        _aboutCanvas.ApplyLayout(ParseBakedLayout(BakedAboutLayoutJson));
        _aboutCanvas.SetItemVisible("HowToTitle", false);
        _aboutCanvas.SetItemVisible("HowToText", false);
        _aboutCanvas.SetItemVisible("AboutColumnDivider", false);
        _aboutCanvas.ApplyLayout(new Dictionary<string,LayoutRect>
        {
            ["PatchNotesTitle"]=new(){X=40,Y=275,Width=1480,Height=55,FontSize=32},
            ["PatchNotesText"]=new(){X=40,Y=340,Width=1480,Height=470,FontSize=24},
            ["OpenSourceText"]=new(){X=40,Y=835,Width=1480,Height=80,FontSize=22},
        });
        if(IllustratedTheme.Enabled)
        {
            foreach(string key in new[]{"InfoIcon","HomeIcon","AboutTopDivider","AboutColumnDivider"}) _aboutCanvas.SetItemVisible(key,false);
            _aboutCanvas.AddRectangle("ChangesCard",new Rectangle(30,255,1560,635),IllustratedTheme.Panel);
            _aboutCanvas.ApplyLayout(new Dictionary<string,LayoutRect>
            {
                ["AboutTitle"]=new(){X=40,Y=20,Width=1520,Height=60,FontSize=38},
                ["AboutPurpose"]=new(){X=40,Y=100,Width=1510,Height=130,FontSize=25},
                ["PatchNotesTitle"]=new(){X=55,Y=280,Width=1500,Height=50,FontSize=30},
                ["PatchNotesText"]=new(){X=55,Y=345,Width=1500,Height=520,FontSize=24,Text="• KeyBind Assistant maps keyboard, mouse and HOTAS inputs to hidden game commands.\n\n• Physical ON/OFF switch support and persistent assistant states.\n\n• Embedded VTrim with aircraft profiles, physical-axis routing and response curves.\n\n• Automatic aircraft detection and per-aircraft Custom Profiles.\n\n• Full-page profile editing, copy/paste tools and custom curve points.\n\n• Improved launcher/update handling, localization, rendering and responsive layouts."},
                ["OpenSourceText"]=new(){X=40,Y=913,Width=1510,Height=66,FontSize=22},
            });
            _aboutCanvas.Visible=false;
            _aboutPanel.Controls.Add(new AssistantInfoPage(CurrentVersion));
            _aboutPanel.Controls[_aboutPanel.Controls.Count-1].BringToFront();
        }
    }

    private void CreateTopNavigation(Panel panel)
    {
        // These values match the main-page Info/Gear icon scale and position after AppScale.
        CreateImageButton(panel, _infoImage, 1126, 36, 65, 65, () =>
        {
            ShowScreen(_aboutPanel);
        });

        CreateImageButton(panel, _homeImage, 1198, 22, 91, 92, () =>
        {
            ShowScreen(_mainPanel);
        });
    }

    private Panel CreateScreenPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _backgroundColor,
            Visible = false
        };
    }

    private Label CreateLabel(
        Control parent,
        string text,
        int left,
        int top,
        int width,
        int height,
        int fontSize,
        FontStyle style)
    {
        Label label = new Label
        {
            Text = text,
            Left = S(left),
            Top = S(top),
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = UiFont(S(fontSize), style),
            AutoSize = true,
            MaximumSize = new Size(S(width), 0),
            MinimumSize = Size.Empty
        };

        parent.Controls.Add(label);
        return label;
    }

    private PictureBox CreateImage(
        Control parent,
        Image image,
        int left,
        int top,
        int width,
        int height)
    {
        PictureBox picture = new PictureBox
        {
            Image = image,
            Left = S(left),
            Top = S(top),
            Width = S(width),
            Height = S(height),
            BackColor = Color.Transparent,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        parent.Controls.Add(picture);
        return picture;
    }

    private PictureBox CreateImageStretch(
        Control parent,
        Image image,
        int left,
        int top,
        int width,
        int height)
    {
        PictureBox picture = new PictureBox
        {
            Image = image,
            Left = S(left),
            Top = S(top),
            Width = S(width),
            Height = S(height),
            BackColor = Color.Transparent,
            SizeMode = PictureBoxSizeMode.StretchImage
        };

        parent.Controls.Add(picture);
        return picture;
    }

    private PictureBox CreateImageButton(
        Control parent,
        Image image,
        int left,
        int top,
        int width,
        int height,
        Action onClick)
    {
        PictureBox picture = CreateImage(parent, image, left, top, width, height);
        picture.Cursor = Cursors.Hand;
        picture.Click += (_, _) =>
        {
            if (!_layoutEditMode)
            {
                onClick();
            }
        };
        return picture;
    }

    private Button CreateTextButton(string text, int left, int top, int width, int height)
    {
        Button button = new ThemeButton
        {
            Text = text,
            Left = S(left),
            Top = S(top),
            Width = S(width),
            Height = S(height),
            BackColor = ThemePalette.FromArgb(20, 26, 28),
            ForeColor = _textColor,
            FlatStyle = FlatStyle.Flat,
            Font = UiFont(S(22), FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor = ThemePalette.FromArgb(180, 20, 20);
        button.FlatAppearance.BorderSize = S(2);

        return button;
    }

    private void AddFilePicker(
        Control parent,
        string title,
        string description,
        int left,
        int top,
        FileSlot slot,
        out TextBox pathBox,
        out PictureBox browseButton)
    {
        CreateLabel(
            parent,
            title,
            left,
            top,
            580,
            36,
            22,
            FontStyle.Regular
        );

        Panel fieldPanel = new Panel
        {
            Left = S(left),
            Top = S(top + 42),
            Width = S(320),
            Height = S(42),
            BackColor = _fieldColor,
            AllowDrop = true
        };

        parent.Controls.Add(fieldPanel);

        TextBox createdPathBox = new TextBox
        {
            Left = S(8),
            Top = S(10),
            Width = S(304),
            Height = S(25),
            BorderStyle = BorderStyle.None,
            BackColor = _fieldColor,
            ForeColor = _textColor,
            Font = UiFont(S(16), FontStyle.Regular),
            ReadOnly = true,
            AllowDrop = true,
            TabStop = false,
            ShortcutsEnabled = false,
            HideSelection = true
        };

        createdPathBox.GotFocus += (_, _) =>
        {
            createdPathBox.SelectionStart = createdPathBox.TextLength;
            createdPathBox.SelectionLength = 0;
        };

        createdPathBox.MouseUp += (_, _) =>
        {
            createdPathBox.SelectionStart = createdPathBox.TextLength;
            createdPathBox.SelectionLength = 0;
        };

        fieldPanel.Controls.Add(createdPathBox);

        HookFileDrop(fieldPanel, slot);
        HookFileDrop(createdPathBox, slot);

        PictureBox createdBrowseButton = CreateImageButton(
            parent,
            _browseRed,
            left + 325,
            top + 32,
            225,
            60,
            () => BrowseForFile(slot)
        );

        CreateLabel(
            parent,
            description,
            left,
            top + 78,
            720,
            30,
            14,
            FontStyle.Italic
        );

        pathBox = createdPathBox;
        browseButton = createdBrowseButton;
    }

    private void HookFileDrop(Control control, FileSlot slot)
    {
        control.DragEnter += (_, e) =>
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        };

        control.DragDrop += (_, e) =>
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

            if (files.Length == 0)
                return;

            SetFilePath(slot, files[0]);
        };
    }

    private void CaptureDesktopSettings()
    {
        CaptureSettingsFromConfig(FileSlot.Desktop);
    }

    private void CaptureCustomVrSettings()
    {
        CaptureSettingsFromConfig(FileSlot.CustomVr);
    }

    private void RemoveDesktopSettings()
    {
        _desktopBlkPath = "";
        _lastAppliedMode = AppliedMode.None;
        SaveState();
        UpdateVisualStates();
    }

    private void RemoveCustomVrSettings()
    {
        _customVrBlkPath = "";
        _lastAppliedMode = AppliedMode.None;
        SaveState();
        UpdateVisualStates();
    }

    private void CaptureSettingsFromConfig(FileSlot targetSlot)
    {
        if (!IsConfigSelected())
        {
            ShowWarning("Please select your War Thunder config.blk file first.");
            return;
        }

        try
        {
            Directory.CreateDirectory(GraphicSettingsFolder);

            string targetFileName = targetSlot == FileSlot.Desktop
                ? "DesktopSettings.blk"
                : "VRSettings.blk";

            string configuredTargetPath = targetSlot == FileSlot.Desktop ? _desktopBlkPath : _customVrBlkPath;
            string targetPath = !string.IsNullOrWhiteSpace(configuredTargetPath) &&
                                !PathsEqual(configuredTargetPath, _configBlkPath)
                ? configuredTargetPath
                : Path.Combine(GraphicSettingsFolder, targetFileName);

            string? targetFolder = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            File.Copy(_configBlkPath, targetPath, true);

            if (targetSlot == FileSlot.Desktop)
            {
                _desktopBlkPath = targetPath;
                if (TryDetectGraphicsApiFromBlk(targetPath, out GraphicsApi detectedDesktopApi))
                {
                    _desktopGraphicsApi = detectedDesktopApi;
                }
            }
            else if (targetSlot == FileSlot.CustomVr)
            {
                _customVrBlkPath = targetPath;
                if (TryDetectGraphicsApiFromBlk(targetPath, out GraphicsApi detectedVrApi))
                {
                    _vrGraphicsApi = detectedVrApi;
                }
            }

            _lastAppliedMode = AppliedMode.None;
            SaveState();
            UpdateVisualStates();
            SyncGraphicsApiComboSelections();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                T("Dialog.CaptureSettingsFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private bool IsWarThunderExeSelected()
    {
        return !string.IsNullOrWhiteSpace(_warThunderExePath) &&
               File.Exists(_warThunderExePath) &&
               Path.GetFileName(_warThunderExePath).Equals("launcher.exe", StringComparison.OrdinalIgnoreCase);
    }

    private void BrowseForWarThunderExe()
    {
        using OpenFileDialog dialog = new OpenFileDialog
        {
            Title = T("Dialog.LauncherFileTitle"),
            Filter = T("Dialog.LauncherFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(_warThunderExePath) && File.Exists(_warThunderExePath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(_warThunderExePath);
            dialog.FileName = "launcher.exe";
        }
        else if (IsConfigSelected())
        {
            string? configFolder = Path.GetDirectoryName(_configBlkPath);
            if (!string.IsNullOrWhiteSpace(configFolder) && Directory.Exists(configFolder))
            {
                dialog.InitialDirectory = configFolder;
            }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetWarThunderExePath(dialog.FileName);
        }
    }

    private void SetWarThunderExePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        path = path.Trim().Trim('"');

        if (!File.Exists(path))
        {
            ShowWarning(T("Warning.SelectedExeMissing"));
            return;
        }

        if (!Path.GetFileName(path).Equals("launcher.exe", StringComparison.OrdinalIgnoreCase))
        {
            ShowWarning(T("Warning.SelectLauncherExe"));
            return;
        }

        _warThunderExePath = Path.GetFullPath(path);
        SaveState();
        UpdateVisualStates();
    }

    private bool TryDetectGraphicsApiFromBlk(string path, out GraphicsApi api)
    {
        api = GraphicsApi.DX12;

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            return TryDetectGraphicsApiFromText(File.ReadAllText(path), out api);
        }
        catch
        {
            return false;
        }
    }

    private bool TryDetectGraphicsApiFromText(string blkText, out GraphicsApi api)
    {
        api = GraphicsApi.DX12;

        Match match = Regex.Match(
            blkText,
            @"driver\s*:\s*t\s*=\s*""(?<driver>dx11|dx12)""",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return false;
        }

        api = match.Groups["driver"].Value.Equals("dx11", StringComparison.OrdinalIgnoreCase)
            ? GraphicsApi.DX11
            : GraphicsApi.DX12;

        return true;
    }

    private void SyncGraphicsApiComboSelections()
    {
        if (_desktopGraphicsApiCombo != null)
        {
            int desired = _desktopGraphicsApi == GraphicsApi.DX11 ? 0 : 1;
            if (_desktopGraphicsApiCombo.SelectedIndex != desired)
            {
                _desktopGraphicsApiCombo.SelectedIndex = desired;
            }
        }

        if (_vrGraphicsApiCombo != null)
        {
            int desired = _vrGraphicsApi == GraphicsApi.DX11 ? 0 : 1;
            if (_vrGraphicsApiCombo.SelectedIndex != desired)
            {
                _vrGraphicsApiCombo.SelectedIndex = desired;
            }
        }
    }

    private GraphicsApi GetGraphicsApiForMode(AppliedMode mode)
    {
        return mode == AppliedMode.Monitor
            ? _desktopGraphicsApi
            : _vrGraphicsApi;
    }


    internal void SetHomeServerVersions(Version? live, Version? test) => _aviationHome?.SetServerVersions(live, test);
    private GameServerChannel SelectedGameServerChannel => _useTestServer ? GameServerChannel.Test : GameServerChannel.Live;

    private async void SelectGameServerChannel(GameServerChannel channel)
    {
        if (_launchBusy || channel == SelectedGameServerChannel) return;
        GameServerChannel previous = SelectedGameServerChannel;
        if (!SwitchGameInstallation(channel)) return;
        await HandleServerSwitchAsync(previous, channel);
    }

    private bool ApplySelectedGameServer(bool showError)
    {
        try
        {
            if (!IsConfigSelected()) throw new IOException(T("Warning.SelectWarThunderInstall"));
            if (IsWarThunderRunningOrStarting()) throw new IOException(T("Warning.CloseGameBeforeServer"));

            string root = Path.GetDirectoryName(_configBlkPath)
                ?? throw new IOException(T("Warning.InvalidConfigPath"));
            if (LauncherRunningIn(root)) throw new IOException(T("Warning.CloseLauncherBeforeServer"));
            if (SelectedGameServerChannel == GameServerChannel.Test)
                PrepareDevServerWorkspace(root, _configBlkPath, enteringTest: true);

            // Preserve the user's complete custom config. Only yunetwork{} is
            // replaced; WarThunderServerConfig writes atomically.
            WarThunderServerConfig.ApplyToFile(_configBlkPath, SelectedGameServerChannel);
            if (SelectedGameServerChannel == GameServerChannel.Test)
                File.Copy(_configBlkPath, Path.Combine(DevServerWorkspace(root), "config.test.blk"), true);
            return true;
        }
        catch (Exception ex)
        {
            if (showError) MessageBox.Show(this, ex.Message, T("Dialog.GameServerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private bool ApplyGraphicsApiToConfig(AppliedMode mode, bool duringLaunch = false)
    {
        if (mode == AppliedMode.None ||
            string.IsNullOrWhiteSpace(_configBlkPath) ||
            !File.Exists(_configBlkPath))
        {
            return true;
        }

        string? writeWarning = GetGameConfigWriteWarning(duringLaunch);
        if (writeWarning != null) { ShowWarning(writeWarning); return false; }
        try
        {
            string configText = WarThunderServerConfig.Apply(
                File.ReadAllText(_configBlkPath), SelectedGameServerChannel);
            string driverValue = GetGraphicsApiForMode(mode) == GraphicsApi.DX11 ? "dx11" : "dx12";
            string enableVrValue = mode == AppliedMode.VR ? "yes" : "no";

            // War Thunder stores the renderer as driver:t="dx11" / driver:t="dx12".
            Match driverMatch = Regex.Match(
                configText,
                @"driver\s*:\s*t\s*=\s*""[^""]*""",
                RegexOptions.IgnoreCase);

            if (driverMatch.Success)
            {
                configText = configText.Remove(driverMatch.Index, driverMatch.Length)
                    .Insert(driverMatch.Index, $"driver:t=\"{driverValue}\"");
            }
            else
            {
                Match videoMatch = Regex.Match(
                    configText,
                    @"video\s*\{",
                    RegexOptions.IgnoreCase);

                if (videoMatch.Success)
                {
                    int insertAt = videoMatch.Index + videoMatch.Length;
                    configText = configText.Insert(
                        insertAt,
                        Environment.NewLine + $"  driver:t=\"{driverValue}\"");
                }
                else
                {
                    configText += Environment.NewLine +
                                  "video{" + Environment.NewLine +
                                  $"  driver:t=\"{driverValue}\"" + Environment.NewLine +
                                  "}" + Environment.NewLine;
                }
            }

            Match enableVrMatch = Regex.Match(
                configText,
                @"enableVR\s*:\s*b\s*=\s*(?:yes|no)",
                RegexOptions.IgnoreCase);

            if (enableVrMatch.Success)
            {
                configText = configText.Remove(enableVrMatch.Index, enableVrMatch.Length)
                    .Insert(enableVrMatch.Index, $"enableVR:b={enableVrValue}");
            }
            else
            {
                Match gameplayMatch = Regex.Match(configText, @"gameplay\s*\{", RegexOptions.IgnoreCase);
                if (gameplayMatch.Success)
                {
                    int insertAt = gameplayMatch.Index + gameplayMatch.Length;
                    configText = configText.Insert(insertAt, Environment.NewLine + $"  enableVR:b={enableVrValue}");
                }
                else
                {
                    configText += Environment.NewLine +
                                  "gameplay{" + Environment.NewLine +
                                  $"  enableVR:b={enableVrValue}" + Environment.NewLine +
                                  "}" + Environment.NewLine;
                }
            }

            AtomicFile.WriteText(_configBlkPath, configText);

            string verifiedText = File.ReadAllText(_configBlkPath);
            if (!string.Equals(verifiedText, configText, StringComparison.Ordinal))
            {
                throw new IOException(T("Warning.GraphicsVerifyFailed"));
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                T("Dialog.GraphicsApiFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return false;
        }
    }

    private void LaunchWarThunderDirect()
    {
        if (!IsWarThunderExeSelected())
        {
            return;
        }

        try
        {
            if (!ApplySelectedGameServer(true)) return;
            // Re-apply the renderer right before launch in case another tool or the game changed config.blk.
            if (_lastAppliedMode != AppliedMode.None)
            {
                if (!ApplyGraphicsApiToConfig(_lastAppliedMode, duringLaunch: true)) return;
            }

            string gameFolder = Path.GetDirectoryName(_warThunderExePath) ?? AppContext.BaseDirectory;
            string launchArguments = "-forcestart";
            bool isSteamInstallation = gameFolder.Contains(
                Path.DirectorySeparatorChar + "steamapps" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
            string miniLauncherPath = Path.Combine(gameFolder, "beac_wt_mlauncher.exe");
            string executableToLaunch = File.Exists(miniLauncherPath) ? miniLauncherPath : _warThunderExePath;

            if (!isSteamInstallation && File.Exists(miniLauncherPath))
            {
                launchArguments += " -nosteam";
            }

            if (_lastAppliedMode == AppliedMode.VR)
            {
                launchArguments += " -config:gameplay/enableVR:b=yes";
            }
            else if (_lastAppliedMode == AppliedMode.Monitor)
            {
                launchArguments += " -config:gameplay/enableVR:b=no";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executableToLaunch,
                Arguments = launchArguments,
                WorkingDirectory = gameFolder,
                UseShellExecute = true
            });
            // The mini-launcher/anti-cheat bootstrap can take several seconds
            // before aces.exe or aces_BE.exe appears. Keep Home in RUNNING
            // during that hand-off so the user cannot double-launch the game.
            _gameLaunchPendingUntilUtc = DateTime.UtcNow.AddSeconds(45);
            UpdateVisualStates();
        }
        catch (Exception ex)
        {
            _gameLaunchPendingUntilUtc = DateTime.MinValue;
            UpdateVisualStates();
            MessageBox.Show(
                ex.Message,
                T("Dialog.StartGameFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void BrowseForFile(FileSlot slot)
    {
        if (slot == FileSlot.Config)
        {
            BrowseForWarThunderFolder();
            return;
        }

        using OpenFileDialog dialog = new OpenFileDialog();

        dialog.Title = T("Dialog.SelectBlkTitle");
        dialog.Filter = T("Dialog.BlkFilter");

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetFilePath(slot, dialog.FileName);
        }
    }

    private void SetFilePath(FileSlot slot, string path)
    {
        if (slot == FileSlot.Config && Directory.Exists(path))
        {
            SetWarThunderFolder(path);
            return;
        }

        if (!File.Exists(path))
        {
            ShowWarning("The selected file does not exist.");
            return;
        }

        if (!Path.GetExtension(path).Equals(".blk", StringComparison.OrdinalIgnoreCase))
        {
            ShowWarning("Only .blk files are supported.");
            return;
        }

        if (slot == FileSlot.Config &&
            !Path.GetFileName(path).Equals("config.blk", StringComparison.OrdinalIgnoreCase))
        {
            ShowWarning("Please select the War Thunder config.blk file.");
            return;
        }

        switch (slot)
        {
            case FileSlot.Config:
                _configBlkPath = path;
                break;

            case FileSlot.Desktop:
                _desktopBlkPath = path;
                _lastAppliedMode = AppliedMode.None;

                if (TryDetectGraphicsApiFromBlk(path, out GraphicsApi detectedDesktopApi))
                {
                    _desktopGraphicsApi = detectedDesktopApi;
                }
                break;

            case FileSlot.CustomVr:
                _customVrBlkPath = path;
                _lastAppliedMode = AppliedMode.None;

                if (TryDetectGraphicsApiFromBlk(path, out GraphicsApi detectedVrApi))
                {
                    _vrGraphicsApi = detectedVrApi;
                }
                break;
        }

        if (slot == FileSlot.Config) ApplySelectedGameServer(false);

        SaveState();
        UpdateVisualStates();
        SyncGraphicsApiComboSelections();
    }

    private void ToggleCustomVr()
    {
        _customVrEnabled = !_customVrEnabled;
        _lastAppliedMode = AppliedMode.None;

        SaveState();
        UpdateVisualStates();
        PositionGraphicsApiControls();
    }

    private void SelectVrPreset(VrPreset preset)
    {
        bool presetChanged = _selectedVrPreset != preset;
        _selectedVrPreset = preset;
        _lastAppliedMode = AppliedMode.None;

        // When a preset is selected for the first time, initialize the dropdown from
        // the renderer already stored inside that preset. After that the user may change it.
        if (presetChanged &&
            TryDetectGraphicsApiFromText(GetSelectedVrPresetContent(), out GraphicsApi detectedVrApi))
        {
            _vrGraphicsApi = detectedVrApi;
        }

        SaveState();
        UpdateVisualStates();
        SyncGraphicsApiComboSelections();
        PositionGraphicsApiControls();
    }

    private void ApplyVrMode()
    {
        string? writeWarning = GetGameConfigWriteWarning();
        if (writeWarning != null) { ShowWarning(writeWarning); return; }
        string? warning = GetVrWarning();

        if (warning != null)
        {
            ShowWarning(warning);
            return;
        }

        string vrControlsPath = GetSelectedControlsPath(AppliedMode.VR);
        if (!ValidateProfilePaths(_customVrEnabled ? _customVrBlkPath : null, vrControlsPath, "VR"))
        {
            return;
        }

        bool applyControls = HasSelectedControlsProfile(AppliedMode.VR);
        if (!TryReadProfileTargets(applyControls, out string originalConfig, out string? originalMachine)) return;

        bool graphicsApplied;
        if (_customVrEnabled)
        {
            graphicsApplied = ApplyBlkFile(_customVrBlkPath, AppliedMode.VR);
        }
        else
        {
            string presetContent = GetSelectedVrPresetContent();
            if (string.IsNullOrWhiteSpace(presetContent))
            {
                ShowWarning("Please go to Settings first and choose a VR preset.");
                return;
            }

            graphicsApplied = ApplyBlkContent(presetContent, AppliedMode.VR);
        }

        if (!graphicsApplied ||
            (applyControls && !ApplyControlsProfile(vrControlsPath, "VR")))
        {
            RestoreProfileTargets(originalConfig, originalMachine);
        }
    }

    private void BrowseForWarThunderFolder()
    {
        using FolderBrowserDialog dialog = new FolderBrowserDialog
        {
            Description = "Select the main War Thunder installation folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        string? currentFolder = !string.IsNullOrWhiteSpace(_configBlkPath)
            ? Path.GetDirectoryName(_configBlkPath)
            : null;
        if (!string.IsNullOrWhiteSpace(currentFolder) && Directory.Exists(currentFolder))
        {
            dialog.SelectedPath = currentFolder;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetWarThunderFolder(dialog.SelectedPath);
        }
    }

    private void SetWarThunderFolder(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath)) return;

        string folder = Path.GetFullPath(selectedPath.Trim().Trim('"'));
        if (Path.GetFileName(folder).Equals("win64", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(folder).Equals("win32", StringComparison.OrdinalIgnoreCase))
        {
            folder = Directory.GetParent(folder)?.FullName ?? folder;
        }

        string configPath = Path.Combine(folder, "config.blk");
        string launcherPath = Path.Combine(folder, "launcher.exe");
        if (!File.Exists(configPath) || !File.Exists(launcherPath))
        {
            ShowWarning("Select the main War Thunder folder containing both config.blk and launcher.exe.");
            return;
        }

        if (_launchBusy || IsWarThunderRunningOrStarting()) { ShowWarning("Close War Thunder and finish any update before selecting another installation."); return; }
        GameServerChannel channel;
        try
        {
            channel = WarThunderServerConfig.Detect(File.ReadAllText(configPath));
            folder = ValidateGameFolder(folder, channel);
        }
        catch (Exception ex) { ShowWarning(ex.Message); return; }
        _installations.SetBoth(folder);
        _useTestServer = channel == GameServerChannel.Test;
        _configBlkPath = Path.Combine(folder, "config.blk");
        _warThunderExePath = Path.Combine(folder, "launcher.exe");
        SaveGameInstallations();
        SaveState();
        UpdateVisualStates();
    }

    private void ApplyMonitorMode()
    {
        string? writeWarning = GetGameConfigWriteWarning();
        if (writeWarning != null) { ShowWarning(writeWarning); return; }
        string? warning = GetMonitorWarning();

        if (warning != null)
        {
            ShowWarning(warning);
            return;
        }

        string desktopControlsPath = GetSelectedControlsPath(AppliedMode.Monitor);
        if (!ValidateProfilePaths(_desktopBlkPath, desktopControlsPath, "Desktop"))
        {
            return;
        }

        bool applyControls = HasSelectedControlsProfile(AppliedMode.Monitor);
        if (!TryReadProfileTargets(applyControls, out string originalConfig, out string? originalMachine)) return;

        if (!ApplyBlkFile(_desktopBlkPath, AppliedMode.Monitor) ||
            (applyControls && !ApplyControlsProfile(desktopControlsPath, "Desktop")))
        {
            RestoreProfileTargets(originalConfig, originalMachine);
        }
    }

    private bool ValidateProfilePaths(string? graphicsSourcePath, string controlsSourcePath, string profileName)
    {
        if (!string.IsNullOrWhiteSpace(controlsSourcePath))
        {
            EnsureMachineBlkDetected();

            if (!Path.GetFileName(_machineBlkPath).Equals("machine.blk", StringComparison.OrdinalIgnoreCase))
            {
                ShowWarning("The app could not find War Thunder's live machine.blk controls file automatically. Open Controls Profiles from Settings and select machine.blk manually once.");
                return false;
            }

            if (PathsEqual(_configBlkPath, _machineBlkPath))
            {
                ShowWarning("config.blk and machine.blk cannot be the same file. Select the real machine.blk in Controls Profiles.");
                return false;
            }

            if (PathsEqual(controlsSourcePath, _machineBlkPath) || PathsEqual(controlsSourcePath, _configBlkPath))
            {
                ShowWarning($"The {profileName} controls preset must be an exported controls file. It cannot be config.blk or the live machine.blk.");
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(graphicsSourcePath) &&
            (PathsEqual(graphicsSourcePath, _configBlkPath) ||
             (!string.IsNullOrWhiteSpace(controlsSourcePath) && PathsEqual(graphicsSourcePath, _machineBlkPath))))
        {
            ShowWarning($"The {profileName} graphics profile must be a separate saved .blk file. It cannot be the live config.blk or machine.blk.");
            return false;
        }

        return true;
    }

    private static bool PathsEqual(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            return Path.GetFullPath(first).Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return first.Equals(second, StringComparison.OrdinalIgnoreCase);
        }
    }

    private string? GetGameConfigWriteWarning(bool duringLaunch = false)
    {
        if (_launchBusy && !duringLaunch) return T("Game.UpdateInProgress");
        if (IsWarThunderRunningOrStarting()) return T("Warning.CloseGameBeforeServer");
        string? root = Path.GetDirectoryName(_configBlkPath);
        if (!string.IsNullOrWhiteSpace(root) && LauncherRunningIn(root))
            return T("Warning.CloseLauncherBeforeServer");
        return null;
    }

    private bool TryReadProfileTargets(bool applyControls, out string config, out string? machine)
    {
        config = string.Empty;
        machine = null;
        try
        {
            config = File.ReadAllText(_configBlkPath);
            if (applyControls) machine = File.ReadAllText(_machineBlkPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowWarning(ex.Message);
            return false;
        }
    }

    private void RestoreProfileTargets(string originalConfig, string? originalMachine)
    {
        try
        {
            AtomicFile.WriteText(_configBlkPath, originalConfig);
            if (originalMachine != null)
            {
                AtomicFile.WriteText(_machineBlkPath, originalMachine);
            }

            _lastAppliedMode = AppliedMode.None;
            SaveState();
            UpdateVisualStates();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Profiles.RestoreFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string? GetVrWarning()
    {
        bool missingConfig = !IsConfigSelected();
        bool missingVr = !IsVrConfigured();

        if (missingConfig && missingVr)
        {
            if (_customVrEnabled)
            {
                return "Please go to Settings first and select your War Thunder config.blk file and your custom VR .blk file.";
            }

            return "Please go to Settings first and select your War Thunder config.blk file and choose a VR preset.";
        }

        if (missingConfig)
        {
            return "Please go to Settings first and select your War Thunder config.blk file. The software needs to know where the game is installed before it can apply changes.";
        }

        if (missingVr)
        {
            if (_customVrEnabled)
            {
                return "No custom VR settings are loaded. Please go to Settings and select your VR .blk file first.";
            }

            return "Please go to Settings first and choose a VR preset.";
        }

        if (HasSelectedControlsProfile(AppliedMode.VR))
        {
            if (!File.Exists(_machineBlkPath))
            {
                return "Control profile switching is enabled, but machine.blk is not configured. Open Controls Profiles in Settings first.";
            }

            if (!File.Exists(GetSelectedControlsPath(AppliedMode.VR)))
            {
                return "Control profile switching is enabled, but the VR controls .blk is not configured.";
            }
        }

        return null;
    }

    private string? GetMonitorWarning()
    {
        bool missingConfig = !IsConfigSelected();
        bool missingDesktop = !IsDesktopConfigured();

        if (missingConfig && missingDesktop)
        {
            return "Please go to Settings first and select your War Thunder config.blk file and your Desktop .blk file.";
        }

        if (missingConfig)
        {
            return "Please go to Settings first and select your War Thunder config.blk file. The software needs to know where the game is installed before it can apply changes.";
        }

        if (missingDesktop)
        {
            return "Please go to Settings first and select your Desktop .blk file.";
        }

        if (HasSelectedControlsProfile(AppliedMode.Monitor))
        {
            if (!File.Exists(_machineBlkPath))
            {
                return "Control profile switching is enabled, but machine.blk is not configured. Open Controls Profiles in Settings first.";
            }

            if (!File.Exists(GetSelectedControlsPath(AppliedMode.Monitor)))
            {
                return "Control profile switching is enabled, but the Desktop controls .blk is not configured.";
            }
        }

        return null;
    }

    private bool ApplyBlkFile(string sourcePath, AppliedMode mode)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                ShowWarning($"Source .blk file was not found:\n\n{sourcePath}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_configBlkPath))
            {
                ShowWarning("Please go to Settings first and select your War Thunder config.blk file.");
                return false;
            }

            string? configFolder = Path.GetDirectoryName(_configBlkPath);

            if (string.IsNullOrWhiteSpace(configFolder) || !Directory.Exists(configFolder))
            {
                ShowWarning("The War Thunder config.blk folder does not exist.");
                return false;
            }

            AtomicFile.WriteText(_configBlkPath, File.ReadAllText(sourcePath));
            if (!ApplyGraphicsApiToConfig(mode))
            {
                return false;
            }

            _lastAppliedMode = mode;

            SaveState();
            UpdateVisualStates();

            // Success popups are intentionally disabled so switching presets is instant and quiet.
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return false;
        }
    }

    private bool ApplyBlkContent(string blkContent, AppliedMode mode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_configBlkPath))
            {
                ShowWarning("Please go to Settings first and select your War Thunder config.blk file.");
                return false;
            }

            string? configFolder = Path.GetDirectoryName(_configBlkPath);

            if (string.IsNullOrWhiteSpace(configFolder) || !Directory.Exists(configFolder))
            {
                ShowWarning("The War Thunder config.blk folder does not exist.");
                return false;
            }

            AtomicFile.WriteText(_configBlkPath, blkContent);
            if (!ApplyGraphicsApiToConfig(mode))
            {
                return false;
            }

            _lastAppliedMode = mode;

            SaveState();
            UpdateVisualStates();

            // Success popups are intentionally disabled so switching presets is instant and quiet.
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return false;
        }
    }

    private string GetSelectedVrPresetContent()
    {
        return _selectedVrPreset switch
        {
            VrPreset.Low => EmbeddedVrLowBlk,
            VrPreset.Medium => EmbeddedVrMediumBlk,
            VrPreset.High => EmbeddedVrHighBlk,
            _ => ""
        };
    }

    private string GetSelectedVrSourcePath()
    {
        // Custom VR still uses the user's selected .blk file.
        // Built-in LOW/MEDIUM/HIGH presets are embedded directly in this .cs file now,
        // so they are written through ApplyBlkContent instead of copied from disk.
        return _customVrEnabled ? _customVrBlkPath : "";
    }

    private bool IsConfigSelected()
    {
        return !string.IsNullOrWhiteSpace(_configBlkPath) &&
               File.Exists(_configBlkPath) &&
               Path.GetFileName(_configBlkPath).Equals("config.blk", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDesktopConfigured()
    {
        return !string.IsNullOrWhiteSpace(_desktopBlkPath) &&
               File.Exists(_desktopBlkPath) &&
               Path.GetExtension(_desktopBlkPath).Equals(".blk", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsVrConfigured()
    {
        if (_customVrEnabled)
        {
            return !string.IsNullOrWhiteSpace(_customVrBlkPath) &&
                   File.Exists(_customVrBlkPath) &&
                   Path.GetExtension(_customVrBlkPath).Equals(".blk", StringComparison.OrdinalIgnoreCase);
        }

        return _selectedVrPreset != VrPreset.None;
    }

    private void UpdateVisualStates()
    {
        RefreshControlProfileFooter();
        _lastKnownGameRunning = IsWarThunderRunningOrStarting();
        _aviationHome?.SetState(
            _lastAppliedMode == AppliedMode.Monitor,
            _lastAppliedMode == AppliedMode.VR,
            _neckAssistForm?.AssistanceEnabled ?? IsNeckAssistSavedEnabled(),
            _hiddenKeybindsForm?.AssistantEnabled ?? IsKeyBindAssistantSavedEnabled(),
            SelectedGameServerChannel,
            IsWarThunderExeSelected(),
            _lastKnownGameRunning,
            _launchBusy);
        _aviationHome?.SetVTrimState(_vtrimForm?.OutputEnabled == true);
        _aviationHome?.SetGameUpdateAvailable(_selectedGameUpdateAvailable);

        if (_configPathBox != null)
        {
            _configPathBox.Text = _configBlkPath;
            _configPathBox.SelectionStart = _configPathBox.TextLength;
            _configPathBox.SelectionLength = 0;
        }

        if (_desktopPathBox != null)
        {
            _desktopPathBox.Text = _desktopBlkPath;
            _desktopPathBox.SelectionStart = _desktopPathBox.TextLength;
            _desktopPathBox.SelectionLength = 0;
        }

        if (_customVrPathBox != null)
        {
            _customVrPathBox.Text = _customVrBlkPath;
            _customVrPathBox.SelectionStart = _customVrPathBox.TextLength;
            _customVrPathBox.SelectionLength = 0;
        }

        if (_configBrowseButton != null)
        {
            _configBrowseButton.Image = IsConfigSelected() && IsWarThunderExeSelected() ? _browseGreen : _browseRed;
        }

        if (_desktopBrowseButton != null)
        {
            _desktopBrowseButton.Image = IsDesktopConfigured() ? _browseGreen : _browseRed;
        }

        if (_customVrBrowseButton != null)
        {
            _customVrBrowseButton.Image =
                !string.IsNullOrWhiteSpace(_customVrBlkPath) && File.Exists(_customVrBlkPath)
                    ? _browseGreen
                    : _browseRed;
        }

        if (_customVrToggleButton != null)
        {
            _customVrToggleButton.Image = _customVrEnabled ? _buttonOn : _buttonOff;
        }

        if (_presetPanel != null)
        {
            _presetPanel.Visible = !_customVrEnabled;
        }

        if (_customVrPanel != null)
        {
            _customVrPanel.Visible = _customVrEnabled;
        }

        if (_lowButton != null)
        {
            _lowButton.Image = !_customVrEnabled && _selectedVrPreset == VrPreset.Low ? _lowGreen : _lowRed;
        }

        if (_mediumButton != null)
        {
            _mediumButton.Image = !_customVrEnabled && _selectedVrPreset == VrPreset.Medium ? _mediumGreen : _mediumRed;
        }

        if (_highButton != null)
        {
            _highButton.Image = !_customVrEnabled && _selectedVrPreset == VrPreset.High ? _highGreen : _highRed;
        }

        if (_mainCanvas != null)
        {
            Image vrImage;
            if (_lastAppliedMode == AppliedMode.VR)
            {
                vrImage = _vrGreen;
            }
            else if (IsVrConfigured())
            {
                // VR source .blk/preset is linked and ready, but not currently applied.
                vrImage = _vrRed;
            }
            else
            {
                // Not connected/configured yet.
                vrImage = _vrOrange;
            }

            Image monitorImage;
            if (_lastAppliedMode == AppliedMode.Monitor)
            {
                monitorImage = _monitorGreen;
            }
            else if (IsDesktopConfigured())
            {
                // Desktop source .blk is linked and ready, but not currently applied.
                monitorImage = _monitorRed;
            }
            else
            {
                // Not connected/configured yet.
                monitorImage = _monitorOrange;
            }

            _mainCanvas.SetImage("VRButton", vrImage);
            _mainCanvas.SetImage("MonitorButton", monitorImage);

            bool canPlay = IsWarThunderExeSelected();
            _mainCanvas.SetImage("PlayButton", canPlay ? _playOn : _playOff);
            _mainCanvas.SetItemClickAction("PlayButton", canPlay ? HomePrimaryAction : null);
        }

        if (_settingsCanvas != null)
        {
            _settingsCanvas.SetText("ConfigPathText", _configBlkPath);
            _settingsCanvas.SetText("DesktopPathText", _desktopBlkPath);
            _settingsCanvas.SetText("CustomVrPathText", _customVrBlkPath);
            _settingsCanvas.SetText("GameExePathText", _warThunderExePath);

            // New compact layout: file paths remain stored internally, but are not shown.
            _settingsCanvas.SetItemVisible("ConfigField", false);
            _settingsCanvas.SetItemVisible("ConfigPathText", false);
            _settingsCanvas.SetItemVisible("DesktopField", false);
            _settingsCanvas.SetItemVisible("DesktopPathText", false);
            _settingsCanvas.SetItemVisible("GameExeField", false);
            _settingsCanvas.SetItemVisible("GameExePathText", false);
            _settingsCanvas.SetItemVisible("GameExeTitle", false);
            _settingsCanvas.SetItemVisible("GameExeBrowse", false);

            bool canCaptureDesktopSettings = IsConfigSelected();
            bool canRemoveDesktopSettings = !string.IsNullOrWhiteSpace(_desktopBlkPath);
            bool customVrFileLoaded = !string.IsNullOrWhiteSpace(_customVrBlkPath) && File.Exists(_customVrBlkPath);
            bool canCaptureCustomVrSettings = IsConfigSelected();
            bool canRemoveCustomVrSettings = !string.IsNullOrWhiteSpace(_customVrBlkPath);

            _settingsCanvas.SetImage("ConfigBrowse", IsConfigSelected() && IsWarThunderExeSelected() ? _browseGreen : _browseRed);
            _settingsCanvas.SetImage("DesktopBrowse", IsDesktopConfigured() ? _browseGreen : _browseRed);
            _settingsCanvas.SetImage("CustomVrBrowse", customVrFileLoaded ? _browseGreen : _browseRed);

            _settingsCanvas.SetImage("DesktopCaptureSettings", canCaptureDesktopSettings ? _captureSettingsImage : _captureSettingsGrayImage);
            _settingsCanvas.SetItemClickAction("DesktopCaptureSettings", canCaptureDesktopSettings ? CaptureDesktopSettings : null);
            _settingsCanvas.SetImage("DesktopRemoveSettings", canRemoveDesktopSettings ? _removeImage : _removeGrayImage);
            _settingsCanvas.SetItemClickAction("DesktopRemoveSettings", canRemoveDesktopSettings ? RemoveDesktopSettings : null);

            _settingsCanvas.SetImage("CustomVrCaptureSettings", canCaptureCustomVrSettings ? _captureSettingsImage : _captureSettingsGrayImage);
            _settingsCanvas.SetItemClickAction("CustomVrCaptureSettings", canCaptureCustomVrSettings ? CaptureCustomVrSettings : null);
            _settingsCanvas.SetImage("CustomVrRemoveSettings", canRemoveCustomVrSettings ? _removeImage : _removeGrayImage);
            _settingsCanvas.SetItemClickAction("CustomVrRemoveSettings", canRemoveCustomVrSettings ? RemoveCustomVrSettings : null);

            _settingsCanvas.SetItemVisible("ControlsProfilesButtonBg", false);
            _settingsCanvas.SetItemVisible("ControlsProfilesButton", false);
            _settingsCanvas.SetItemVisible("ControlsProfilesDescription", false);
            _settingsCanvas.SetImage("CustomToggle", _customVrEnabled ? _buttonOn : _buttonOff);

            _settingsCanvas.SetImage("LowButton", !_customVrEnabled && _selectedVrPreset == VrPreset.Low ? _lowGreen : _lowRed);
            _settingsCanvas.SetImage("MediumButton", !_customVrEnabled && _selectedVrPreset == VrPreset.Medium ? _mediumGreen : _mediumRed);
            _settingsCanvas.SetImage("HighButton", !_customVrEnabled && _selectedVrPreset == VrPreset.High ? _highGreen : _highRed);

            bool showPresets = !_customVrEnabled;
            _settingsCanvas.SetItemVisible("PresetTitle", showPresets);
            _settingsCanvas.SetItemVisible("LowButton", showPresets);
            _settingsCanvas.SetItemVisible("MediumButton", showPresets);
            _settingsCanvas.SetItemVisible("HighButton", showPresets);
            _settingsCanvas.SetItemVisible("MoreInfoButton", true);
            _settingsCanvas.SetItemVisible("HelpButton", showPresets);

            _settingsCanvas.SetItemVisible("CustomVrTitle", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrField", false);
            _settingsCanvas.SetItemVisible("CustomVrPathText", false);
            _settingsCanvas.SetItemVisible("CustomVrBrowse", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrCaptureSettings", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrRemoveSettings", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrDescription", _customVrEnabled);

            PositionGraphicsApiControls();
            UpdateControlProfileSelectorStates();
        }

        // Footer readiness depends on the selected War Thunder executable.
        if (IllustratedTheme.Enabled)
            Invalidate(new Rectangle(0, Math.Max(0, ClientSize.Height - 100), ClientSize.Width, Math.Min(100, ClientSize.Height)));
    }

    private void ShowScreen(Panel panel)
    {
        _vtrimForm?.Hide();
        _neckAssistForm?.Hide();
        _hiddenKeybindsForm?.Hide();
        _mainPanel.Visible = false;
        _settingsPanel.Visible = false;
        _aboutPanel.Visible = false;
        if (_recommendedPanel != null)
        {
            _recommendedPanel.Visible = false;
        }

        // Panels built while hidden do not always receive a fresh Dock layout after
        // the illustrated sidebar adds form padding. Reapply the padded viewport so
        // pages never remain wider than the visible area after a resize/navigation.
        if (IllustratedTheme.Enabled)
        {
            panel.Dock = DockStyle.None;
            panel.Bounds = ThemeContentBounds;
        }
        else
        {
            panel.Bounds = DisplayRectangle;
        }
        panel.Visible = true;
        panel.PerformLayout();
        panel.BringToFront();
        SelectThemePage(ReferenceEquals(panel,_mainPanel) ? "Home" : ReferenceEquals(panel,_settingsPanel) ? "Profiles" : ReferenceEquals(panel,_aboutPanel) ? "Info" : "Profiles");

        if (ReferenceEquals(panel, _settingsPanel))
        {
            _desktopGraphicsApiLabel?.BringToFront();
            _desktopGraphicsApiCombo?.BringToFront();
            _vrGraphicsApiLabel?.BringToFront();
            _vrGraphicsApiCombo?.BringToFront();
            PositionGraphicsApiControls();
            UpdateControlProfileSelectorStates();
        }

        UpdateVisualStates();
    }

    private void RegisterLayoutTarget(string key, Control control)
    {
        // The old PictureBox-based layout editor is no longer used on the main page.
        // The main page is drawn by MainPageCanvas so PNG transparency and overlapping layers work properly.
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || (_fullScreen && keyData == Keys.Escape))
        {
            ToggleFullScreen();
            return true;
        }
        Keys keyCode = keyData & Keys.KeyCode;

        if (HandleSecretCode(keyCode))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool _fullScreen;
    private Rectangle _beforeFullScreenBounds;
    private FormWindowState _beforeFullScreenState;

    private void ToggleFullScreen()
    {
        if (!_fullScreen)
        {
            _beforeFullScreenState = WindowState;
            _beforeFullScreenBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            Rectangle screen = Screen.FromControl(this).Bounds;
            _fullScreen = true;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = screen;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _beforeFullScreenBounds;
            WindowState = _beforeFullScreenState;
            _fullScreen = false;
        }
        LayoutThemePages();
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (LayoutEditorEnabled &&
            (_mainPanel.Visible || (_mainCanvas?.LayoutEditMode ?? false)) &&
            _mainCanvas != null &&
            _mainCanvas.HandleKeyDown(e, () => ShowScreen(_mainPanel), title => Text = title))
        {
            e.Handled = true;
            return;
        }

        if (LayoutEditorEnabled &&
            (_settingsPanel.Visible || (_settingsCanvas?.LayoutEditMode ?? false)) &&
            _settingsCanvas != null &&
            _settingsCanvas.HandleKeyDown(e, () => ShowScreen(_settingsPanel), title => Text = title))
        {
            e.Handled = true;
            return;
        }

        if (LayoutEditorEnabled &&
            (_aboutPanel.Visible || (_aboutCanvas?.LayoutEditMode ?? false)) &&
            _aboutCanvas != null &&
            _aboutCanvas.HandleKeyDown(e, () => ShowScreen(_aboutPanel), title => Text = title))
        {
            e.Handled = true;
            return;
        }

        if (LayoutEditorEnabled &&
            (_recommendedPanel.Visible || (_recommendedCanvas?.LayoutEditMode ?? false)) &&
            _recommendedCanvas != null &&
            _recommendedCanvas.HandleKeyDown(e, () => ShowScreen(_recommendedPanel), title => Text = title))
        {
            e.Handled = true;
            return;
        }

    }

    private void ArmSecretCode()
    {
        _secretCodeArmed = true;
        _secretCodeIndex = 0;
        _promoSecretCodeIndex = 0;
        // The redesigned home no longer uses the legacy canvas. Keep keyboard
        // focus inside the main window after the logo click so ProcessCmdKey can
        // receive the arrow sequence reliably.
        Activate();
        if (_aviationHome != null)
        {
            _aviationHome.Select();
        }
        else
        {
            _mainCanvas?.Focus();
        }
    }

    private bool HandleSecretCode(Keys keyCode)
    {
        // The secrets only listen after clicking the War Thunder logo on the main page.
        // ProcessCmdKey catches arrow keys more reliably than normal KeyDown.
        if (!_secretCodeArmed ||
            _promoSecretRunning ||
            !ContainsFocus ||
            (_mainCanvas?.LayoutEditMode ?? false) ||
            (_settingsCanvas?.LayoutEditMode ?? false) ||
            (_aboutCanvas?.LayoutEditMode ?? false) ||
            (_recommendedCanvas?.LayoutEditMode ?? false))
        {
            return false;
        }

        bool isSecretKey =
            keyCode == Keys.Up ||
            keyCode == Keys.Down ||
            keyCode == Keys.Left ||
            keyCode == Keys.Right;

        if (!isSecretKey)
        {
            return false;
        }

        bool originalSecretComplete = AdvanceSecretSequence(keyCode, _secretCodeSequence, ref _secretCodeIndex);
        bool promoSecretComplete = AdvanceSecretSequence(keyCode, _promoSecretCodeSequence, ref _promoSecretCodeIndex);

        if (originalSecretComplete)
        {
            _secretCodeIndex = 0;
            _promoSecretCodeIndex = 0;
            _secretCodeArmed = false;
            ShowSecretPopup();
        }
        else if (promoSecretComplete)
        {
            _secretCodeIndex = 0;
            _promoSecretCodeIndex = 0;
            _secretCodeArmed = false;
            _ = RunPromoSecretSequenceAsync();
        }

        // Consume arrow keys once the secret is armed so focus/navigation does not break the sequence.
        return true;
    }

    private bool AdvanceSecretSequence(Keys keyCode, Keys[] sequence, ref int index)
    {
        if (keyCode == sequence[index])
        {
            index++;
            return index >= sequence.Length;
        }

        index = keyCode == sequence[0] ? 1 : 0;
        return false;
    }

    private async Task RunPromoSecretSequenceAsync()
    {
        if (_promoSecretRunning)
        {
            return;
        }

        _promoSecretRunning = true;

        try
        {
            Rectangle secretScreenBounds = GetSecretScreenBounds();
            Point? triggerPivot = await ShowPromoCertificationPromptAsync(secretScreenBounds);

            if (!triggerPivot.HasValue)
            {
                return;
            }

            using SnailJumpForm snailForm = new SnailJumpForm(_snailImage, secretScreenBounds, triggerPivot.Value);
            Task soundTask = Task.Run(PlaySnailSoundSafely);
            snailForm.Show();
            snailForm.BringToFront();
            await snailForm.PlayZoomAsync();
            await soundTask;
            await Task.Delay(800);
            snailForm.Close();

            ShowDiplomaPopup(secretScreenBounds);
        }
        finally
        {
            _promoSecretRunning = false;
        }
    }

    private Task<Point?> ShowPromoCertificationPromptAsync(Rectangle screenBounds)
    {
        TaskCompletionSource<Point?> tcs = new TaskCompletionSource<Point?>();
        CertificationPromptForm promptForm = new CertificationPromptForm(_backgroundColor, _textColor, UiFont);
        CenterFormOnScreen(promptForm, screenBounds);

        bool triggered = false;

        void TriggerFromCursor()
        {
            if (triggered || promptForm.IsDisposed)
            {
                return;
            }

            triggered = true;
            tcs.TrySetResult(Cursor.Position);
            promptForm.Close();
        }

        bool IsCursorNearButton(Button button)
        {
            const int triggerPadding = 42;
            Point clientPoint = promptForm.PointToClient(Cursor.Position);
            Rectangle expanded = button.Bounds;
            expanded.Inflate(triggerPadding, triggerPadding);
            return expanded.Contains(clientPoint);
        }

        void CheckHoverTrap()
        {
            if (IsCursorNearButton(promptForm.YesButton) || IsCursorNearButton(promptForm.NoButton))
            {
                TriggerFromCursor();
            }
        }

        promptForm.YesButton.MouseEnter += (_, _) => TriggerFromCursor();
        promptForm.NoButton.MouseEnter += (_, _) => TriggerFromCursor();
        promptForm.MouseMove += (_, _) => CheckHoverTrap();
        promptForm.FormClosed += (_, _) =>
        {
            if (!triggered)
            {
                tcs.TrySetResult(null);
            }
            promptForm.Dispose();
        };

        promptForm.Show(this);
        promptForm.BringToFront();
        SystemSounds.Question.Play();
        return tcs.Task;
    }

    private void ShowDiplomaPopup(Rectangle screenBounds)
    {
        SystemSounds.Asterisk.Play();
        using DiplomaForm popup = new DiplomaForm(_diplomaImage, _backgroundColor, _textColor, UiFont, screenBounds);
        try
        {
            popup.Icon = Icon;
        }
        catch
        {
            // Ignore icon issues for the diploma popup.
        }

        CenterFormOnScreen(popup, screenBounds);
        popup.ShowDialog(this);
    }

    private void PlaySnailSoundSafely()
    {
        string? tempFilePath = null;

        try
        {
            string? soundPath = FindSnailSoundFilePath();

            if (soundPath == null)
            {
                using Stream embeddedStream = AssetManager.LoadResourceStream("snail.wav");
                tempFilePath = Path.Combine(Path.GetTempPath(), $"wtvr_snail_{Guid.NewGuid():N}.wav");

                using FileStream tempFile = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                embeddedStream.CopyTo(tempFile);
                soundPath = tempFilePath;
            }

            bool played = PlaySound(soundPath, IntPtr.Zero, SND_FILENAME | SND_SYNC | SND_NODEFAULT);

            if (!played)
            {
                using SoundPlayer fallbackPlayer = new SoundPlayer(soundPath);
                fallbackPlayer.Load();
                fallbackPlayer.PlaySync();
            }
        }
        catch
        {
            // If the sound is missing or Windows cannot decode it, keep the easter egg sequence going.
        }
        finally
        {
            if (tempFilePath != null)
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore temp file cleanup issues.
                }
            }
        }
    }

    private string? FindSnailSoundFilePath()
    {
        string[] soundPaths =
        {
            Path.Combine(AppFolder, "snail.wav"),
            Path.Combine(AppFolder, "Assets", "snail.wav"),
            Path.Combine(AppFolder, "Resources", "snail.wav"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snail.wav"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "snail.wav"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "snail.wav")
        };

        return soundPaths.FirstOrDefault(File.Exists);
    }

    private Rectangle GetSecretScreenBounds()
    {
        try
        {
            return Screen.FromControl(this).WorkingArea;
        }
        catch
        {
            return Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        }
    }

    private void CenterFormOnScreen(Form form, Rectangle screenBounds)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(
            screenBounds.Left + (screenBounds.Width - form.Width) / 2,
            screenBounds.Top + (screenBounds.Height - form.Height) / 2);
    }

    private void ShowPromoSecretFinalPopup(Rectangle screenBounds)
    {
        using Form popup = new Form
        {
            Text = "Easter egg unlocked",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(S(720), S(260)),
            BackColor = _backgroundColor,
            ForeColor = _textColor,
            ShowInTaskbar = false
        };

        try
        {
            popup.Icon = Icon;
        }
        catch
        {
            // Ignore icon issues for the secret popup.
        }

        Label label = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Congratulations! You passed the War Thunder's simulator battles training for VR pilots!\n\nGo now and show them what you're made of!",
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UiFont(S(24), FontStyle.Bold),
            Padding = new Padding(S(28))
        };

        Label hint = new Label
        {
            Dock = DockStyle.Bottom,
            Height = S(36),
            Text = "click anywhere to close",
            ForeColor = ThemePalette.FromArgb(170, 190, 195),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UiFont(S(14), FontStyle.Italic)
        };

        label.Click += (_, _) => popup.Close();
        hint.Click += (_, _) => popup.Close();
        popup.Click += (_, _) => popup.Close();

        popup.Controls.Add(label);
        popup.Controls.Add(hint);
        CenterFormOnScreen(popup, screenBounds);
        popup.ShowDialog(this);
    }

    private void ShowSecretPopup()
    {
        using Form popup = new Form
        {
            Text = "Secret unlocked",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(S(430), S(180)),
            BackColor = _backgroundColor,
            ForeColor = _textColor,
            ShowInTaskbar = false
        };

        try
        {
            popup.Icon = Icon;
        }
        catch
        {
            // Ignore icon issues for the secret popup.
        }

        Label label = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Squier RLZ!",
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UiFont(S(42), FontStyle.Bold)
        };

        Label hint = new Label
        {
            Dock = DockStyle.Bottom,
            Height = S(36),
            Text = "click anywhere to close",
            ForeColor = ThemePalette.FromArgb(170, 190, 195),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UiFont(S(14), FontStyle.Italic)
        };

        label.Click += (_, _) => popup.Close();
        hint.Click += (_, _) => popup.Close();
        popup.Click += (_, _) => popup.Close();

        popup.Controls.Add(label);
        popup.Controls.Add(hint);
        popup.ShowDialog(this);
    }

    private void MainForm_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (!LayoutEditorEnabled)
        {
            return;
        }

        if (_mainCanvas != null && _mainCanvas.HandleMouseWheelFromForm(e))
        {
            return;
        }

        if (_settingsCanvas != null && _settingsCanvas.HandleMouseWheelFromForm(e))
        {
            return;
        }

        if (_aboutCanvas != null && _aboutCanvas.HandleMouseWheelFromForm(e))
        {
            return;
        }

        if (_recommendedCanvas != null && _recommendedCanvas.HandleMouseWheelFromForm(e))
        {
            return;
        }
    }

    private void LoadLayout()
    {
        _mainCanvas?.LoadLayout();
        _settingsCanvas?.LoadLayout();
        _aboutCanvas?.LoadLayout();
        _recommendedCanvas?.LoadLayout();
    }

    private void SaveLayout()
    {
        _mainCanvas?.SaveLayout();
        _settingsCanvas?.SaveLayout();
        _aboutCanvas?.SaveLayout();
    }

    private void ShowHighWarningDialog()
    {
        const float scale = 1.25f;

        int S(int value)
        {
            return (int)Math.Round(value * scale);
        }

        using Form dialog = new Form();

        dialog.AutoScaleMode = AutoScaleMode.None;
        dialog.Text = T("HighWarning.Title");
        dialog.ClientSize = new Size(S(1120), S(700));
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
        dialog.MaximizeBox = false;
        dialog.MinimizeBox = false;
        dialog.BackColor = _backgroundColor;
        dialog.ForeColor = _textColor;
        dialog.Icon = Icon;

        Label bodyTop = new Label
        {
            Text = T("HighWarning.BodyTop"),
            Left = S(35),
            Top = S(18),
            Width = S(1045),
            Height = S(150),
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = UiFont(S(18), FontStyle.Regular)
        };

        dialog.Controls.Add(bodyTop);

        PictureBox nvidiaImage = new PictureBox
        {
            Image = _nvidiaProfileInspector,
            Left = S(35),
            Top = S(175),
            Width = S(830),
            Height = S(405),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        Label zoomHint = new Label
        {
            Text = T("HighWarning.Zoom"),
            Left = nvidiaImage.Left + nvidiaImage.Width - S(125),
            Top = nvidiaImage.Top + S(12),
            Width = S(105),
            Height = S(32),
            BackColor = ThemePalette.FromArgb(20, 26, 28),
            ForeColor = Color.White,
            Font = UiFont(S(13), FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
            Cursor = Cursors.Hand
        };

        nvidiaImage.MouseEnter += (_, _) => zoomHint.Visible = true;
        nvidiaImage.MouseLeave += (_, _) => zoomHint.Visible = false;
        nvidiaImage.Click += (_, _) => ShowNvidiaImageZoomDialog(dialog);
        zoomHint.MouseEnter += (_, _) => zoomHint.Visible = true;
        zoomHint.Click += (_, _) => ShowNvidiaImageZoomDialog(dialog);

        dialog.Controls.Add(nvidiaImage);
        dialog.Controls.Add(zoomHint);
        zoomHint.BringToFront();

        LinkLabel nvidiaDownloadLink = new LinkLabel
        {
            Text = T("HighWarning.DownloadInspector"),
            Left = S(35),
            Top = S(600),
            Width = S(830),
            Height = S(34),
            LinkColor = ThemePalette.FromArgb(120, 210, 255),
            ActiveLinkColor = Color.White,
            VisitedLinkColor = ThemePalette.FromArgb(120, 210, 255),
            BackColor = Color.Transparent,
            Font = UiFont(S(18), FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        nvidiaDownloadLink.LinkClicked += (_, _) =>
        {
            OpenNvidiaProfileInspector();
        };

        dialog.Controls.Add(nvidiaDownloadLink);

        Label bodyRight = new Label
        {
            Text = T("HighWarning.BodyRight"),
            Left = S(880),
            Top = S(145),
            Width = S(235),
            Height = S(500),
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = UiFont(S(17), FontStyle.Regular)
        };

        dialog.Controls.Add(bodyRight);
        bodyRight.BringToFront();

        CheckBox showAgainCheckBox = new ThemeCheckBox
        {
            Text = T("HighWarning.ShowAgain"),
            Left = S(55),
            Top = S(660),
            Width = S(560),
            Height = S(35),
            Checked = _showHighWarning,
            ForeColor = _textColor,
            BackColor = _backgroundColor,
            Font = UiFont(S(18), FontStyle.Regular)
        };

        dialog.Controls.Add(showAgainCheckBox);

        Button okButton = new ThemeButton
        {
            Text = T("Common.Ok"),
            Left = S(950),
            Top = S(650),
            Width = S(120),
            Height = S(40),
            BackColor = ThemePalette.FromArgb(20, 26, 28),
            ForeColor = _textColor,
            FlatStyle = FlatStyle.Flat,
            Font = UiFont(S(20), FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        okButton.FlatAppearance.BorderColor = ThemePalette.FromArgb(180, 20, 20);
        okButton.FlatAppearance.BorderSize = S(1);

        okButton.Click += (_, _) =>
        {
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        dialog.Controls.Add(okButton);
        okButton.BringToFront();

        dialog.ShowDialog(this);

        _showHighWarning = showAgainCheckBox.Checked;
        SaveState();
    }

    private void ShowNvidiaImageZoomDialog(Form owner)
    {
        const float scale = 1.25f;

        int S(int value)
        {
            return (int)Math.Round(value * scale);
        }

        using Form zoomDialog = new Form
        {
            Text = T("HighWarning.ZoomTitle"),
            ClientSize = new Size(S(1180), S(720)),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = _backgroundColor,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimizeBox = false,
            Icon = Icon
        };

        PictureBox zoomedImage = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = _nvidiaProfileInspector,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = _backgroundColor,
            Cursor = Cursors.Hand
        };

        Label closeHint = new Label
        {
            Text = T("HighWarning.CloseImage"),
            Dock = DockStyle.Bottom,
            Height = S(34),
            ForeColor = _textColor,
            BackColor = ThemePalette.FromArgb(20, 26, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UiFont(S(13), FontStyle.Italic)
        };

        zoomedImage.Click += (_, _) => zoomDialog.Close();

        zoomDialog.Controls.Add(zoomedImage);
        zoomDialog.Controls.Add(closeHint);
        zoomDialog.ShowDialog(owner);
    }

    private void OpenNvidiaProfileInspector()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Orbmu2k/nvidiaProfileInspector/releases",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                T("HighWarning.OpenInspectorFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private async void CheckForUpdatesFromButton()
    {
        await CheckForUpdatesAsync(true);
    }

    private async Task CheckForUpdatesAsync(bool interactive)
    {
        if (_updateCheckInProgress) return;
        if (IsWarThunderRunningOrStarting())
        {
            _updateDeferredForGame = true;
            _nextAutomaticUpdateCheckUtc = DateTime.MaxValue;
            if (interactive)
                MessageBox.Show(this, "The app will check for updates after War Thunder closes.",
                    T("Update.Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _updateCheckInProgress = true;
        _updateDeferredForGame = false;
        _nextAutomaticUpdateCheckUtc = DateTime.UtcNow.AddHours(6);
        try
        {
            using HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WT-VR-Settings-Assistant");

            string json = await client.GetStringAsync(GitHubLatestReleaseApi);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement release = document.RootElement;

            string tag = release.GetProperty("tag_name").GetString() ?? "";
            string releaseUrl = release.TryGetProperty("html_url", out JsonElement urlElement)
                ? urlElement.GetString() ?? GitHubReleasesUrl
                : GitHubReleasesUrl;

            string? updatePackageUrl = null;
            string? updatePackageDigest = null;
            string? updatePackageName = null;
            if (release.TryGetProperty("assets", out JsonElement assetsElement) &&
                assetsElement.ValueKind == JsonValueKind.Array)
            {
                var updatePackage = assetsElement
                    .EnumerateArray()
                    .Select(asset => new
                    {
                        Name = asset.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? "" : "",
                        Url = asset.TryGetProperty("browser_download_url", out JsonElement downloadElement) ? downloadElement.GetString() : null,
                        Digest = asset.TryGetProperty("digest", out JsonElement digestElement) ? digestElement.GetString() : null
                    })
                    .Where(asset => asset.Name.StartsWith("WTVRSettingsAssistant", StringComparison.OrdinalIgnoreCase) &&
                        asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(asset.Url))
                    .OrderByDescending(asset => asset.Name.Equals($"WTVRSettingsAssistant-{tag}.zip", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                updatePackageUrl = updatePackage?.Url;
                updatePackageDigest = updatePackage?.Digest;
                updatePackageName = updatePackage?.Name;
            }

            if (!Version.TryParse(CurrentVersion, out Version? current) ||
                AppUpdateVersion.ReleaseAppVersion(tag, updatePackageName) is not Version latest)
            {
                throw new InvalidOperationException("GitHub returned an unrecognized version number.");
            }

            bool updateAvailable = AppUpdateVersion.IsNewer(current, latest);
            _latestReleaseUrl = releaseUrl;

            if (updateAvailable)
            {
                if (string.IsNullOrWhiteSpace(updatePackageUrl))
                {
                    _nextAutomaticUpdateCheckUtc = DateTime.UtcNow.AddMinutes(30);
                    if (interactive)
                    {
                        MessageBox.Show(
                            this,
                            T("Update.PackageUnavailable"),
                            T("Update.PackageUnavailableTitle"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        OpenLatestReleasePage();
                    }
                }
                else if (IsWarThunderRunningOrStarting())
                {
                    _updateDeferredForGame = true;
                    _nextAutomaticUpdateCheckUtc = DateTime.MaxValue;
                }
                else if (!await DownloadAndInstallUpdateAsync(updatePackageUrl, updatePackageDigest, latest, interactive) && !_updateDeferredForGame)
                {
                    _nextAutomaticUpdateCheckUtc = DateTime.UtcNow.AddMinutes(30);
                }
            }
            else if (interactive)
            {
                MessageBox.Show(
                    this,
                    string.Format(CultureInfo.CurrentCulture, T("Update.Latest"), CurrentVersion),
                    T("Update.Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _nextAutomaticUpdateCheckUtc = DateTime.UtcNow.AddMinutes(30);
            if (interactive)
            {
                MessageBox.Show(
                    this,
                    string.Format(CultureInfo.CurrentCulture, T("Update.CheckFailed"), ex.Message),
                    T("Update.Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _updateCheckInProgress = false;
        }
    }

    private async Task<bool> DownloadAndInstallUpdateAsync(string packageUrl, string? expectedDigest,
        Version latestVersion, bool interactive)
    {
        string? temporaryRoot = null;
        Form? updateProgress = null;
        Label? updateStatus = null;

        try
        {
            if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out Uri? packageUri) ||
                packageUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(packageUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !packageUri.AbsolutePath.StartsWith(
                    "/theartofvalio-cmyk/WTVRSettingsAssistant/releases/download/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("GitHub returned an invalid update-package address.");
            }

            if (IsWarThunderRunningOrStarting())
            {
                _updateDeferredForGame = true;
                _nextAutomaticUpdateCheckUtc = DateTime.MaxValue;
                return false;
            }

            SaveState();
            updateProgress = new Form
            {
                Text = "War Thunder VR Assistant update",
                ClientSize = new Size(460, 130),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false,
                ShowInTaskbar = true,
                TopMost = true,
                BackColor = Color.FromArgb(31, 33, 36),
                ForeColor = Color.White
            };
            updateStatus = new Label
            {
                Text = $"Automatically updating to v{latestVersion}. Please wait; the app will restart.",
                Bounds = new Rectangle(20, 22, 420, 55),
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White
            };
            updateProgress.Controls.Add(updateStatus);
            updateProgress.Controls.Add(new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Bounds = new Rectangle(20, 91, 420, 18)
            });
            updateProgress.Show();
            updateProgress.Activate();
            UseWaitCursor = true;
            Enabled = false;

            temporaryRoot = Path.Combine(Path.GetTempPath(), "WTVRSettingsAssistantUpdate_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(temporaryRoot, "update.zip");
            string extractPath = Path.Combine(temporaryRoot, "package");
            Directory.CreateDirectory(extractPath);

            using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("WT-VR-Settings-Assistant");
                using HttpResponseMessage response = await client.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using Stream source = await response.Content.ReadAsStreamAsync();
                await using FileStream destination = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination);
            }

            if (!string.IsNullOrWhiteSpace(expectedDigest))
            {
                if (!expectedDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                    expectedDigest.Length != 71)
                    throw new InvalidDataException("GitHub returned an invalid update checksum.");
                updateStatus.Text = "Verifying the update package…";
                string actualDigest = await Task.Run(() =>
                {
                    using FileStream packageStream = File.OpenRead(zipPath);
                    return Convert.ToHexString(SHA256.HashData(packageStream));
                });
                if (!string.Equals(actualDigest, expectedDigest[7..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded update does not match GitHub's checksum.");
            }

            updateStatus.Text = "Preparing the update for installation…";
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true));

            string executableName = UpdateExecutableName;
            string? packagedExecutable = Directory
                .EnumerateFiles(extractPath, executableName, SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault();

            if (packagedExecutable == null)
            {
                throw new InvalidDataException($"The update package does not contain {executableName}.");
            }

            string? packagedVersionText = FileVersionInfo.GetVersionInfo(packagedExecutable).FileVersion;
            if (packagedVersionText is null ||
                AppUpdateVersion.ParseReleaseVersion(packagedVersionText) is not Version packagedVersion ||
                !AppUpdateVersion.IsSameRelease(packagedVersion, latestVersion))
            {
                throw new InvalidDataException("The update package version does not match the GitHub release.");
            }

            if (IsWarThunderRunningOrStarting())
            {
                _updateDeferredForGame = true;
                _nextAutomaticUpdateCheckUtc = DateTime.MaxValue;
                return false;
            }

            string payloadRoot = Path.GetDirectoryName(packagedExecutable)!;
            string updaterScriptPath = Path.Combine(Path.GetTempPath(), "WTVRSettingsAssistantUpdater_" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(updaterScriptPath, BuildUpdaterScript(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            ProcessStartInfo updater = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            updater.ArgumentList.Add("-NoProfile");
            updater.ArgumentList.Add("-ExecutionPolicy");
            updater.ArgumentList.Add("Bypass");
            updater.ArgumentList.Add("-File");
            updater.ArgumentList.Add(updaterScriptPath);
            updater.ArgumentList.Add("-ProcessId");
            updater.ArgumentList.Add(Environment.ProcessId.ToString());
            updater.ArgumentList.Add("-PayloadRoot");
            updater.ArgumentList.Add(payloadRoot);
            updater.ArgumentList.Add("-InstallRoot");
            updater.ArgumentList.Add(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            updater.ArgumentList.Add("-ExecutableName");
            updater.ArgumentList.Add(executableName);
            updater.ArgumentList.Add("-TemporaryRoot");
            updater.ArgumentList.Add(temporaryRoot);

            if (Process.Start(updater) == null)
            {
                throw new InvalidOperationException("The update helper could not be started.");
            }

            temporaryRoot = null; // The updater owns cleanup after this point.
            updateStatus.Text = "Installing update and restarting…";
            Application.Exit();
            return true;
        }
        catch (Exception ex)
        {
            updateProgress?.Close();
            Enabled = true;
            if (interactive)
                MessageBox.Show(this, "The update could not be installed. No application files were changed.\n\n" + ex.Message,
                    "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                _trayIcon?.ShowBalloonTip(5000, T("Update.Title"),
                    string.Format(CultureInfo.CurrentCulture, T("Update.CheckFailed"), ex.Message), ToolTipIcon.Warning);
            return false;
        }
        finally
        {
            updateProgress?.Dispose();
            Enabled = true;
            UseWaitCursor = false;

            if (!string.IsNullOrWhiteSpace(temporaryRoot))
            {
                try
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
                catch
                {
                    // Temporary files can be removed by Windows later if they are still in use.
                }
            }
        }
    }

    private static string BuildUpdaterScript()
    {
        return """
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$PayloadRoot,
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [Parameter(Mandatory = $true)][string]$ExecutableName,
    [Parameter(Mandatory = $true)][string]$TemporaryRoot
)

$ErrorActionPreference = 'Stop'
Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue

# These locations contain user-owned state and survive every application update.
$preservedFolders = @('Settings', 'GraphicSettings', 'ControlSettings', 'Diagnostics', 'CustomThemes')
$preservedRootFiles = @('installed.flag')
$legacyFiles = @(
    'WTVRSettingsAssistant.deps.json',
    'WTVRSettingsAssistant.dll',
    'WTVRSettingsAssistant.pdb',
    'WTVRSettingsAssistant.runtimeconfig.json',
    'Microsoft.Web.WebView2.Wpf.dll',
    'Microsoft.Web.WebView2.Core.xml',
    'Microsoft.Web.WebView2.WinForms.xml',
    'Microsoft.Web.WebView2.Wpf.xml',
    'NeckAssist/OpenXR/LICENSE-XRNeckSafer.txt',
    'NeckAssist/OpenXR/XR_APILAYER_NOVENDOR_XRNeckSafer.dll',
    'NeckAssist/OpenXR/XR_APILAYER_NOVENDOR_XRNeckSafer.json'
)
$backupRoot = Join-Path $TemporaryRoot 'rollback\old-app-files'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$packageFiles = @()
$backedUp = @()

function Is-UserData([string]$relative) {
    $top = ($relative -split '[\\/]', 2)[0]
    return ($preservedFolders -contains $top) -or ($preservedRootFiles -contains $relative)
}

function Check-RelativePath([string]$relative) {
    if ([IO.Path]::IsPathRooted($relative) -or
        (($relative -split '[\\/]') | Where-Object { $_ -eq '..' -or $_ -eq '.' }).Count -gt 0) {
        throw "Unsafe update file path: $relative"
    }
}

function Backup-AppFile([string]$relative) {
    $source = Join-Path $InstallRoot $relative
    if (Test-Path -LiteralPath $source -PathType Container) {
        throw "A user folder conflicts with an application file: $relative"
    }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { return }
    $destination = Join-Path $backupRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force -ErrorAction Stop
    $script:backedUp += $relative
}

try {
    # The package declares the files it owns. Only those files and exact known
    # 1.5/2.0 leftovers may be replaced or removed. Unknown user files survive.
    $packageFiles = @(Get-ChildItem -LiteralPath $PayloadRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($PayloadRoot.Length).TrimStart('\', '/')
        Check-RelativePath $relative
        if (-not (Is-UserData $relative)) {
            [pscustomobject]@{ Source = $_.FullName; Relative = $relative }
        }
    })
    $ownedPaths = @($packageFiles | ForEach-Object Relative) + $legacyFiles
    foreach ($relative in ($ownedPaths | Select-Object -Unique)) {
        Check-RelativePath $relative
        Backup-AppFile $relative
    }

    foreach ($relative in $legacyFiles) {
        $oldPath = Join-Path $InstallRoot $relative
        if (Test-Path -LiteralPath $oldPath -PathType Leaf) {
            Remove-Item -LiteralPath $oldPath -Force -ErrorAction Stop
        }
    }
    foreach ($relative in @('NeckAssist/OpenXR', 'NeckAssist')) {
        $oldDirectory = Join-Path $InstallRoot $relative
        if ((Test-Path -LiteralPath $oldDirectory -PathType Container) -and
            -not @(Get-ChildItem -LiteralPath $oldDirectory -Force).Count) {
            Remove-Item -LiteralPath $oldDirectory -Force -ErrorAction Stop
        }
    }

    foreach ($file in $packageFiles) {
        $destination = Join-Path $InstallRoot $file.Relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        $copied = $false
        for ($attempt = 0; $attempt -lt 20 -and -not $copied; $attempt++) {
            try {
                Copy-Item -LiteralPath $file.Source -Destination $destination -Force -ErrorAction Stop
                $copied = $true
            }
            catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $copied) { throw "Could not install $($file.Relative)" }
    }

    $executablePath = Join-Path $InstallRoot $ExecutableName
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        throw 'The updated executable was not installed.'
    }

    Start-Process -FilePath $executablePath -WorkingDirectory $InstallRoot
    Remove-Item -LiteralPath $TemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
}
catch {
    # Restore only files the updater owned. Unrecognized user files were never touched.
    foreach ($file in $packageFiles) {
        $destination = Join-Path $InstallRoot $file.Relative
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
        }
    }
    foreach ($relative in $backedUp) {
        $source = Join-Path $backupRoot $relative
        $destination = Join-Path $InstallRoot $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force -ErrorAction SilentlyContinue
    }
    $executablePath = Join-Path $InstallRoot $ExecutableName
    if (Test-Path -LiteralPath $executablePath -PathType Leaf) {
        Start-Process -FilePath $executablePath -WorkingDirectory $InstallRoot
    }
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        "The update could not be completed and the previous application files were restored.`n`n$($_.Exception.Message)",
        'Update failed',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null

}
""";
    }

    private void OpenLatestReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _latestReleaseUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Dialog.OpenReleaseFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenDiscord()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.com/invite/eXWbS3WExB",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                T("Dialog.OpenDiscordFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void OpenYouTube()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.youtube.com/@SPYBGWTVR?sub_confirmation=1",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                T("Dialog.OpenLinkFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private sealed class RecommendedVrSettingsForm : Form
    {
        private readonly MainPageCanvas _canvas;

        private readonly string _languageCode;

        public RecommendedVrSettingsForm(Image? verticalLineImage, Color bgColor, Color textColor, string bakedLayoutJson, string languageCode)
        {
            AutoScaleMode = AutoScaleMode.None;
            _languageCode = AppText.Normalize(languageCode);
            Text = RT("Recommended.Title");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(1625, 844);
            MinimumSize = new Size(1000, 520);
            BackColor = bgColor;
            KeyPreview = true;

            _canvas = new MainPageCanvas(RecommendedLayoutFilePath, bgColor, textColor, UiFont)
            {
                Dock = DockStyle.Top,
                Height = Math.Max(650, ClientSize.Height)
            };

            Panel viewport = new() { Dock = DockStyle.Fill, AutoScroll = true };
            viewport.Controls.Add(_canvas);
            Controls.Add(viewport);
            viewport.ClientSizeChanged += (_, _) => _canvas.Height = Math.Max(650, viewport.ClientSize.Height);

            AddCanvasItems(verticalLineImage, bgColor);
            _canvas.ApplyLayout(ParseBakedLayout(bakedLayoutJson));
            _canvas.SetText("IntroText", RT("Recommended.Intro"));
            _canvas.SetText("GpuLabel", RT("Recommended.GpuLabel"));
            _canvas.SetText("LowTitle", RT("Profiles.Low"));
            _canvas.SetText("MediumTitle", RT("Profiles.Medium"));
            _canvas.SetText("HighTitle", RT("Profiles.High"));
            _canvas.SetText("BottomInfo", RT("Recommended.Info"));
        }

        private Font UiFont(float pixelSize, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", pixelSize, style, GraphicsUnit.Pixel);
        }

        private string RT(string key) => AppText.T(_languageCode, key);

        private void RecommendedVrSettingsForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_canvas.HandleKeyDown(e, () => { }, title => Text = title))
            {
                e.Handled = true;
            }
        }

        private void AddCanvasItems(Image? verticalLineImage, Color bgColor)
        {
            _canvas.AddText(
                "IntroText",
                RT("Recommended.Intro"),
                new Rectangle(13, 0, 1565, 230),
                27.0002f,
                FontStyle.Italic,
                StringAlignment.Near,
                StringAlignment.Near
            );

            _canvas.AddText(
                "GpuLabel",
                RT("Recommended.GpuLabel"),
                new Rectangle(13, 461, 162, 79),
                39.5f,
                FontStyle.Regular,
                StringAlignment.Near,
                StringAlignment.Near
            );

            AddColumn(
                "Low",
                verticalLineImage,
                152,
                RT("Profiles.Low"),
                "NVIDIA:\n" +
                "GTX 1080 / 1080 Ti\n" +
                "RTX 20 series\n" +
                "RTX 30 series up to RTX 3070\n\n" +
                "AMD:\n" +
                "RX 5700 XT\n" +
                "RX 6600 XT / 6650 XT\n" +
                "RX 6700 / 6700 XT / 6750 XT\n" +
                "RX 7600 XT"
            );

            AddColumn(
                "Medium",
                verticalLineImage,
                613,
                RT("Profiles.Medium"),
                "NVIDIA:\n" +
                "RTX 30 series: 3080 / 3090 Ti\n" +
                "RTX 40 series up to RTX 4070\n" +
                "RTX 50 series up to RTX 5060\n\n" +
                "AMD:\n" +
                "RX 6800 / 6800 XT\n" +
                "RX 6900 XT / 6950 XT\n" +
                "RX 7700 XT\n" +
                "RX 7800 XT\n" +
                "RX 7900 GRE"
            );

            AddColumn(
                "High",
                verticalLineImage,
                1063,
                RT("Profiles.High"),
                "NVIDIA:\n" +
                "RTX 40 series: 4080 / 4080 Super / 4090\n" +
                "RTX 50 series: 5070 / 5080 / 5090\n\n" +
                "AMD:\n" +
                "RX 7900 XT\n" +
                "RX 7900 XTX\n" +
                "RX 9070\n" +
                "RX 9070 XT"
            );

            _canvas.AddText(
                "BottomInfo",
                RT("Recommended.Info"),
                new Rectangle(20, 759, 1605, 102),
                25.012989f,
                FontStyle.Regular,
                StringAlignment.Near,
                StringAlignment.Near
            );
        }

        private void AddColumn(string keyPrefix, Image? verticalLineImage, int x, string title, string body)
        {
            int titleY = keyPrefix.Equals("Low", StringComparison.OrdinalIgnoreCase) ? 238 : 230;
            int bodyX = keyPrefix.Equals("Low", StringComparison.OrdinalIgnoreCase) ? 193 : x + 47;

            if (verticalLineImage != null)
            {
                _canvas.AddImage($"{keyPrefix}Line", verticalLineImage, new Rectangle(x, 226, 47, 529));
            }
            else
            {
                _canvas.AddRectangle($"{keyPrefix}Line", new Rectangle(x, 226, 47, 529), ThemePalette.FromArgb(228, 76, 96));
            }

            _canvas.AddText(
                $"{keyPrefix}Title",
                title,
                new Rectangle(x + 47, titleY, 330, 75),
                56f,
                FontStyle.Bold,
                StringAlignment.Near,
                StringAlignment.Near
            );

            _canvas.AddText(
                $"{keyPrefix}Body",
                body,
                new Rectangle(bodyX, 325, 420, 430),
                28f,
                FontStyle.Regular,
                StringAlignment.Near,
                StringAlignment.Near
            );
        }
    }

    private sealed class CertificationPromptForm : Form
    {
        public Button YesButton { get; }
        public Button NoButton { get; }

        public CertificationPromptForm(Color bgColor, Color textColor, Func<float, FontStyle, Font> fontFactory)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "War Thunder VR Assistant";
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(680, 210);
            BackColor = bgColor;
            ForeColor = textColor;

            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor,
                Padding = new Padding(24, 22, 24, 18)
            };

            PictureBox iconBox = new PictureBox
            {
                Left = 32,
                Top = 34,
                Width = 48,
                Height = 48,
                Image = SystemIcons.Question.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            Label questionLabel = new Label
            {
                Left = 102,
                Top = 30,
                Width = 535,
                Height = 70,
                Text = "Do you want to become a certified War Thunder VR Pilot?",
                ForeColor = textColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = fontFactory(21, FontStyle.Regular)
            };


            YesButton = CreateLegitButton("Yes", 385, 142, textColor, fontFactory);
            NoButton = CreateLegitButton("No", 515, 142, textColor, fontFactory);

            AcceptButton = YesButton;
            CancelButton = NoButton;

            contentPanel.Controls.Add(iconBox);
            contentPanel.Controls.Add(questionLabel);
            contentPanel.Controls.Add(YesButton);
            contentPanel.Controls.Add(NoButton);
            Controls.Add(contentPanel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            try
            {
                int useDarkMode = 1;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
            }
            catch
            {
                // Ignore if dark title bars are not supported.
            }
        }

        private static Button CreateLegitButton(string text, int left, int top, Color textColor, Func<float, FontStyle, Font> fontFactory)
        {
            Button button = new ThemeButton
            {
                Text = text,
                Width = 110,
                Height = 38,
                Left = left,
                Top = top,
                Font = fontFactory(14, FontStyle.Regular),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemePalette.FromArgb(20, 33, 38),
                ForeColor = textColor,
                TabStop = true
            };

            button.FlatAppearance.BorderColor = ThemePalette.FromArgb(76, 100, 106);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = ThemePalette.FromArgb(30, 48, 55);
            button.FlatAppearance.MouseDownBackColor = ThemePalette.FromArgb(42, 62, 70);
            return button;
        }
    }

    private sealed class DiplomaForm : Form
    {
        private readonly Image? _diplomaImage;

        public DiplomaForm(Image? diplomaImage, Color bgColor, Color textColor, Func<float, FontStyle, Font> fontFactory, Rectangle screenBounds)
        {
            _diplomaImage = diplomaImage;

            AutoScaleMode = AutoScaleMode.None;
            Text = "War Thunder VR Pilot Diploma";
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = bgColor;
            ForeColor = textColor;

            if (diplomaImage != null)
            {
                const int buttonAreaHeight = 64;
                const int outerMargin = 80;

                int maxClientWidth = Math.Max(420, screenBounds.Width - outerMargin);
                int maxClientHeight = Math.Max(320, screenBounds.Height - outerMargin);
                int maxImageHeight = Math.Max(180, maxClientHeight - buttonAreaHeight);

                double scale = Math.Min(
                    1.0,
                    Math.Min(
                        maxClientWidth / (double)diplomaImage.Width,
                        maxImageHeight / (double)diplomaImage.Height));

                int displayWidth = Math.Max(1, (int)Math.Round(diplomaImage.Width * scale));
                int displayHeight = Math.Max(1, (int)Math.Round(diplomaImage.Height * scale));

                ClientSize = new Size(displayWidth, displayHeight + buttonAreaHeight);

                PictureBox picture = new PictureBox
                {
                    Left = 0,
                    Top = 0,
                    Width = displayWidth,
                    Height = displayHeight,
                    Image = diplomaImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = bgColor
                };

                Button saveButton = new ThemeButton
                {
                    Text = "Save Diploma Image",
                    Left = Math.Max(12, (displayWidth - 230) / 2),
                    Top = displayHeight + 12,
                    Width = 230,
                    Height = 40,
                    Font = fontFactory(15, FontStyle.Regular),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ThemePalette.FromArgb(18, 30, 34),
                    ForeColor = textColor
                };
                saveButton.FlatAppearance.BorderColor = ThemePalette.FromArgb(85, 110, 116);
                saveButton.FlatAppearance.BorderSize = 1;
                saveButton.Click += (_, _) => SaveDiplomaImage();

                Controls.Add(picture);
                Controls.Add(saveButton);
            }
            else
            {
                ClientSize = new Size(680, 240);

                Label fallback = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "diploma.png was not found.",
                    ForeColor = textColor,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = fontFactory(28, FontStyle.Bold)
                };
                Controls.Add(fallback);
            }
        }

        private void SaveDiplomaImage()
        {
            if (_diplomaImage == null)
            {
                return;
            }

            using SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save Diploma Image",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp",
                FileName = "War_Thunder_VR_Pilot_Diploma.png",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            ImageFormat format = extension switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".bmp" => ImageFormat.Bmp,
                _ => ImageFormat.Png
            };

            try
            {
                _diplomaImage.Save(dialog.FileName, format);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not save diploma", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private sealed class SecretPromoCountdownForm : Form
    {
        private readonly Label _counterLabel;

        public SecretPromoCountdownForm(Color bgColor, Color textColor, Func<float, FontStyle, Font> fontFactory)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Secret Promo Code Unlocked";
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(210, 88);
            BackColor = bgColor;
            ForeColor = textColor;

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Text = "Secret Promo Code Unlocked",
                ForeColor = textColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = fontFactory(8, FontStyle.Bold)
            };

            _counterLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "5",
                ForeColor = textColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = fontFactory(18, FontStyle.Bold)
            };

            Controls.Add(_counterLabel);
            Controls.Add(titleLabel);
        }

        public void SetCounter(string text)
        {
            if (IsDisposed)
            {
                return;
            }

            _counterLabel.Text = text;
            _counterLabel.Refresh();
        }
    }

    private sealed class SnailJumpForm : Form
    {
        private const int WS_EX_LAYERED = 0x00080000;
        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref Point pptDst,
            ref Size psize,
            IntPtr hdcSrc,
            ref Point pptSrc,
            int crKey,
            ref BlendFunction pblend,
            int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private readonly Image? _snailImage;
        private readonly Point _pivotClientPoint;
        private Rectangle _snailBounds = Rectangle.Empty;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED;
                return cp;
            }
        }

        public SnailJumpForm(Image? snailImage, Rectangle screenBounds, Point pivotScreenPoint)
        {
            _snailImage = snailImage;

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Bounds = screenBounds;
            BackColor = Color.Black;
            DoubleBuffered = true;

            _pivotClientPoint = new Point(
                pivotScreenPoint.X - screenBounds.Left,
                pivotScreenPoint.Y - screenBounds.Top);
        }

        public async Task PlayZoomAsync()
        {
            const int steps = 7;
            const int delayMs = 8;

            int maxSize = Math.Max(900, Math.Min(ClientSize.Width, ClientSize.Height) + 260);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int size = (int)Math.Round(70 + ((maxSize - 70) * t));
                _snailBounds = new Rectangle(
                    _pivotClientPoint.X - size / 2,
                    _pivotClientPoint.Y - size / 2,
                    size,
                    size);
                RenderLayeredFrame();
                await Task.Delay(delayMs);
            }
        }

        private void RenderLayeredFrame()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            using Bitmap frame = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(frame))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                if (_snailImage != null && !_snailBounds.IsEmpty)
                {
                    graphics.DrawImage(_snailImage, _snailBounds);
                }
            }

            AlphaDib.Paint(Handle, Location, frame);

        }
    }

    private void ShowBeerMessage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.paypal.com/donate/?business=SLS9FP9VALFV4&no_recurring=1&item_name=Thank+you+for+supporting+what+I+do%21&currency_code=EUR",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                T("Dialog.OpenDonationFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ShowControlsProfilesDialog()
    {
        using Form dialog = new Form
        {
            Text = T("Controls.Title"),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(1150, 610),
            BackColor = _backgroundColor,
            ForeColor = _textColor
        };

        try { dialog.Icon = Icon; } catch { }

        Label title = new Label
        {
            Left = 25,
            Top = 20,
            Width = 1100,
            Height = 38,
            Text = T("Controls.Description"),
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = UiFont(23, FontStyle.Bold)
        };

        CheckBox enabled = new ThemeCheckBox
        {
            Left = 28,
            Top = 66,
            Width = 430,
            Height = 32,
            Text = T("Controls.Enable"),
            Checked = _switchControlsWithProfile,
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = UiFont(18, FontStyle.Regular)
        };

        using ToolTip helpToolTip = new ToolTip
        {
            AutoPopDelay = 12000,
            InitialDelay = 250,
            ReshowDelay = 100,
            ShowAlways = true
        };

        string machineHelp = T("Controls.MachineHelp");
        string desktopControlsHelp = T("Controls.DesktopHelp");
        string vrControlsHelp = T("Controls.VrHelp");

        TextBox machineBox = CreateControlsPathBox(dialog, "machine.blk", _machineBlkPath, 35, 155, helpToolTip, machineHelp, out Button machineBrowse);
        Button autoDetect = CreateControlsDialogButton(T("Controls.AutoDetect"), 945, 155, 165, 45);
        dialog.Controls.Add(autoDetect);

        TextBox desktopBox = CreateControlsPathBox(dialog, T("Controls.DesktopBlk"), _desktopControlsBlkPath, 35, 285, helpToolTip, desktopControlsHelp, out Button desktopBrowse);
        TextBox vrBox = CreateControlsPathBox(dialog, T("Controls.VrBlk"), _vrControlsBlkPath, 35, 415, helpToolTip, vrControlsHelp, out Button vrBrowse);

        void BrowseInto(TextBox box, string titleText)
        {
            using OpenFileDialog open = new OpenFileDialog
            {
                Title = titleText,
                Filter = "War Thunder BLK files (*.blk)|*.blk|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (open.ShowDialog(dialog) == DialogResult.OK)
            {
                box.Text = open.FileName;
            }
        }

        machineBrowse.Click += (_, _) => BrowseInto(machineBox, T("Controls.SelectMachine"));
        desktopBrowse.Click += (_, _) => BrowseInto(desktopBox, T("Controls.SelectDesktop"));
        vrBrowse.Click += (_, _) => BrowseInto(vrBox, T("Controls.SelectVr"));

        autoDetect.Click += (_, _) =>
        {
            string? detected = TryAutoDetectMachineBlk();
            if (detected != null)
            {
                machineBox.Text = detected;
            }
            else
            {
                MessageBox.Show(dialog, T("Controls.MachineNotFound"), T("Controls.Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        Button save = CreateControlsDialogButton(T("Options.Save"), 855, 545, 120, 45);
        Button cancel = CreateControlsDialogButton(T("Options.Cancel"), 990, 545, 120, 45);
        dialog.Controls.Add(save);
        dialog.Controls.Add(cancel);

        save.Click += (_, _) =>
        {
            _switchControlsWithProfile = enabled.Checked;
            _machineBlkPath = machineBox.Text.Trim();
            _desktopControlsBlkPath = desktopBox.Text.Trim();
            _vrControlsBlkPath = vrBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(_desktopControlsBlkPath))
                AddOrSelectControlProfile(_desktopControlProfiles, _desktopControlsBlkPath, AppliedMode.Monitor);
            if (!string.IsNullOrWhiteSpace(_vrControlsBlkPath))
                AddOrSelectControlProfile(_vrControlProfiles, _vrControlsBlkPath, AppliedMode.VR);
            SaveState();
            RefreshControlProfileCombos();
            UpdateVisualStates();
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        dialog.Controls.Add(title);
        dialog.Controls.Add(enabled);
        dialog.ShowDialog(this);
    }

    private TextBox CreateControlsPathBox(
        Form parent,
        string labelText,
        string initialValue,
        int left,
        int top,
        ToolTip helpToolTip,
        string helpText,
        out Button browseButton)
    {
        Label label = new Label
        {
            Left = left,
            Top = top - 34,
            Width = 600,
            Height = 30,
            Text = labelText,
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = UiFont(18, FontStyle.Regular)
        };

        TextBox box = new TextBox
        {
            Left = left,
            Top = top,
            Width = 720,
            Height = 45,
            Text = initialValue,
            BackColor = ThemePalette.FromArgb(8, 16, 18),
            ForeColor = _textColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = UiFont(16, FontStyle.Regular),
            AllowDrop = true
        };

        box.DragEnter += (_, e) =>
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                e.Effect = files.Length > 0 && files[0].EndsWith(".blk", StringComparison.OrdinalIgnoreCase)
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            }
        };
        box.DragDrop += (_, e) =>
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0 && files[0].EndsWith(".blk", StringComparison.OrdinalIgnoreCase))
            {
                box.Text = files[0];
            }
        };

        int helpLeft = left + TextRenderer.MeasureText(labelText, label.Font).Width + 8;
        Control helpControl;
        if (_helpImage != null)
        {
            helpControl = new PictureBox
            {
                Left = helpLeft,
                Top = top - 47,
                Width = 43,
                Height = 43,
                Image = _helpImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Cursor = Cursors.Help,
                TabStop = false
            };
        }
        else
        {
            helpControl = CreateControlsDialogButton("?", helpLeft, top - 34, 32, 30);
            helpControl.Font = UiFont(14, FontStyle.Bold);
            helpControl.Cursor = Cursors.Help;
        }

        helpToolTip.SetToolTip(helpControl, helpText);
        helpControl.Click += (_, _) => MessageBox.Show(
            parent,
            helpText,
            string.Format(CultureInfo.CurrentCulture, T("Controls.HelpTitle"), labelText),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        browseButton = CreateControlsDialogButton(T("Controls.Browse"), 775, top, 155, 45);
        parent.Controls.Add(label);
        parent.Controls.Add(helpControl);
        parent.Controls.Add(box);
        parent.Controls.Add(browseButton);
        helpControl.BringToFront();
        return box;
    }

    private Button CreateControlsDialogButton(string text, int left, int top, int width, int height)
    {
        Button button = new ThemeButton
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemePalette.FromArgb(18, 30, 34),
            ForeColor = _textColor,
            Font = UiFont(16, FontStyle.Regular)
        };
        button.FlatAppearance.BorderColor = ThemePalette.FromArgb(85, 110, 116);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private string? TryAutoDetectMachineBlk()
    {
        try
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string[] savesRoots =
            {
                Path.Combine(documents, "My Games", "WarThunder", "Saves"),
                Path.Combine(documents, "WarThunder", "Saves")
            };

            foreach (string savesRoot in savesRoots)
            {
                string? activeAccountMachine = TryGetActiveAccountMachineBlk(savesRoot);
                if (activeAccountMachine != null)
                {
                    return activeAccountMachine;
                }
            }

            string[] candidates =
            {
                Path.Combine(documents, "My Games", "WarThunder", "Saves", "last", "production", "machine.blk"),
                Path.Combine(documents, "WarThunder", "Saves", "last", "production", "machine.blk")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            string fallbackSavesRoot = Path.Combine(documents, "My Games", "WarThunder", "Saves");
            if (Directory.Exists(fallbackSavesRoot))
            {
                return Directory.EnumerateFiles(fallbackSavesRoot, "machine.blk", SearchOption.AllDirectories)
                    .FirstOrDefault(path => path.Contains(Path.DirectorySeparatorChar + "production" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch { }

        return null;
    }

    private static string? TryGetActiveAccountMachineBlk(string savesRoot)
    {
        try
        {
            string lastLoginPath = Path.Combine(savesRoot, "lastlogin.blk");
            if (!File.Exists(lastLoginPath))
            {
                return null;
            }

            string lastLoginText = File.ReadAllText(lastLoginPath);
            Match uidMatch = Regex.Match(lastLoginText, @"uid\s*:\s*i64\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (!uidMatch.Success)
            {
                return null;
            }

            string activeMachinePath = Path.Combine(savesRoot, uidMatch.Groups[1].Value, "production", "machine.blk");
            return File.Exists(activeMachinePath) ? activeMachinePath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveSavedMachineBlkPath(string savedPath)
    {
        if (string.IsNullOrWhiteSpace(savedPath) ||
            !savedPath.Contains(Path.DirectorySeparatorChar + "last" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return savedPath;
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] savesRoots =
        {
            Path.Combine(documents, "My Games", "WarThunder", "Saves"),
            Path.Combine(documents, "WarThunder", "Saves")
        };

        foreach (string savesRoot in savesRoots)
        {
            string? activeMachinePath = TryGetActiveAccountMachineBlk(savesRoot);
            if (activeMachinePath != null)
            {
                return activeMachinePath;
            }
        }

        return savedPath;
    }

    private static string ResolveSavedWarThunderLauncherPath(string savedPath)
    {
        if (string.IsNullOrWhiteSpace(savedPath)) return savedPath;
        if (Path.GetFileName(savedPath).Equals("launcher.exe", StringComparison.OrdinalIgnoreCase)) return savedPath;

        if (Path.GetFileName(savedPath).Equals("aces.exe", StringComparison.OrdinalIgnoreCase))
        {
            string? binaryFolder = Path.GetDirectoryName(savedPath);
            string? gameFolder = binaryFolder == null ? null : Directory.GetParent(binaryFolder)?.FullName;
            if (gameFolder != null)
            {
                string launcherPath = Path.Combine(gameFolder, "launcher.exe");
                if (File.Exists(launcherPath)) return launcherPath;
            }
        }

        return savedPath;
    }

    private bool ApplyControlsProfile(string presetPath, string profileName)
    {
        try
        {
            if (!File.Exists(_machineBlkPath))
            {
                ShowWarning(T("Controls.MachineMissing"));
                return false;
            }

            if (!File.Exists(presetPath))
            {
                ShowWarning(TF("Controls.PresetMissing", profileName));
                return false;
            }

            string presetText = File.ReadAllText(presetPath);

            if (!TryExtractNamedBlkBlock(presetText, "controls", out string controlsBlock))
            {
                MessageBox.Show(this, T("Controls.MissingControlsBlock"), T("Controls.MissingControlsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!TryExtractNamedBlkBlock(presetText, "settings", out string controlsSettingsBlock))
            {
                MessageBox.Show(
                    this,
                    T("Controls.MissingSettingsBlock"),
                    T("Controls.IncompleteTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            string backupFolder = Path.Combine(AppFolder, "ControlSettings");
            Directory.CreateDirectory(backupFolder);

            // War Thunder can use both the active account machine.blk and its "last"
            // mirror. Keep every live mirror in sync so a mode click takes effect
            // immediately and the game cannot restore the previously loaded controls.
            List<string> machineTargets = GetLiveMachineBlkTargets();
            foreach (string machinePath in machineTargets)
            {
                string machineText = File.ReadAllText(machinePath);
                string safeBackupName = machinePath
                    .Replace(':', '_')
                    .Replace('\\', '_')
                    .Replace('/', '_');
                string backupPath = Path.Combine(backupFolder, safeBackupName + ".backup");
                if (!File.Exists(backupPath))
                {
                    File.Copy(machinePath, backupPath, false);
                }

                if (!TryExtractNamedBlkBlock(machineText, "settings", out string liveSettingsBlock))
                {
                    throw new InvalidDataException(TF("Controls.MachineMissingSettings", machinePath));
                }

                string mergedControlSettings = MergeControlSettingsBlock(liveSettingsBlock, controlsSettingsBlock);
                string merged = ReplaceRequiredBlkBlock(machineText, "controls", controlsBlock, machinePath);
                merged = ReplaceRequiredBlkBlock(merged, "settings", mergedControlSettings, machinePath);
                File.WriteAllText(machinePath, merged);

                // Read the file back to catch a real truncated/invalid write. Do not
                // reject a successful write because War Thunder normalizes whitespace,
                // numeric formatting, or duplicated scalar entries in machine.blk.
                string appliedMachineText = File.ReadAllText(machinePath);
                if (!TryExtractNamedBlkBlock(appliedMachineText, "controls", out _) ||
                    !TryExtractNamedBlkBlock(appliedMachineText, "settings", out _))
                {
                    throw new IOException(TF("Controls.InvalidWrite", profileName, machinePath));
                }
            }

            if (IsWarThunderRunning())
            {
                _pendingControlsPresetPath = presetPath;
                _pendingControlsProfileName = profileName;
                _controlsReapplyTimer.Start();
                MessageBox.Show(
                    this,
                    TF("Controls.RestartRequired", profileName),
                    T("Controls.RestartTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Controls.ApplyFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private string ReplaceRequiredBlkBlock(string targetText, string blockName, string replacementBlock, string targetPath)
    {
        if (!TryFindNamedBlkBlock(targetText, blockName, out int start, out int length))
        {
            throw new InvalidDataException(TF("Controls.MachineMissingBlock", blockName, targetPath));
        }

        return targetText.Substring(0, start) + replacementBlock + targetText.Substring(start + length);
    }

    private static bool BlkBlocksEqual(string first, string second)
    {
        static string Normalize(string value) => value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        return string.Equals(Normalize(first), Normalize(second), StringComparison.Ordinal);
    }

    private string MergeControlSettingsBlock(string liveBlock, string presetBlock)
    {
        Dictionary<string, string> presetValues = ExtractControlSettingValues(presetBlock);
        string merged = liveBlock;

        foreach ((string key, string typedValue) in presetValues)
        {
            string pattern = $@"(?m)^(?<indent>[ \t]*){Regex.Escape(key)}\s*:[A-Za-z0-9_]+\s*=.*$";
            MatchCollection existing = Regex.Matches(merged, pattern);
            if (existing.Count > 0)
            {
                // A few War Thunder builds can retain duplicate scalar entries. Update
                // every occurrence so the last value read by the game cannot remain stale.
                merged = Regex.Replace(
                    merged,
                    pattern,
                    match => match.Groups["indent"].Value + key + ":" + typedValue);
            }
            else
            {
                int closeBrace = merged.LastIndexOf('}');
                if (closeBrace < 0) throw new InvalidDataException(T("Controls.LiveSettingsMalformed"));
                string newline = merged.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                merged = merged.Insert(closeBrace, "  " + key + ":" + typedValue + newline);
            }
        }

        return merged;
    }

    private static bool ControlSettingsMatch(string appliedBlock, string presetBlock)
    {
        Dictionary<string, string> expected = ExtractControlSettingValues(presetBlock);
        Dictionary<string, string> actual = ExtractControlSettingValues(appliedBlock);
        return expected.Count > 0 && expected.All(pair =>
            actual.TryGetValue(pair.Key, out string? value) &&
            string.Equals(value, pair.Value, StringComparison.Ordinal));
    }

    private static Dictionary<string, string> ExtractControlSettingValues(string settingsBlock)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(
                     settingsBlock,
                     @"(?m)^[ \t]*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:(?<type>[A-Za-z0-9_]+)\s*=\s*(?<value>[^\r\n]*)$"))
        {
            string key = match.Groups["key"].Value;
            if (IsControlSettingName(key))
            {
                values[key] = match.Groups["type"].Value + "=" + match.Groups["value"].Value.Trim();
            }
        }

        return values;
    }

    private static bool IsControlSettingName(string key)
    {
        return key.EndsWith("Multiplier", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Sens", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Nonlinearity", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("DeadZone", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("aimAccelerationDelay", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("mouseZMult", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("mouseJoystick", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("helicopterMouseJoystick", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("mouseAileron", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("helicopterMouseAileron", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("invert", StringComparison.OrdinalIgnoreCase) ||
               key is "freeCameraInertia" or "replayCameraWiggle" or "mouseSmooth" or
                   "mouseJoyMode" or "helicopterMouseJoyMode" or "cameraSmooth" or
                   "cameraSpeed" or "cameraMouseSpeed" or "cameraFpsPhysMult" or
                   "vrCameraFpsPhysMult" or "cameraInvertY" or "isXChgSticks" or
                   "secondaryInvert" or "secondaryXChgSticks" or "zoomForTurret" or
                   "isVibration" or "forceGain" or "cameraShakeMultiplier" or
                   "vrCameraShakeMultiplier" or "holdBtnToGrabHotas" or "gamepadMinVibration";
    }


    private void RefreshGameRunningVisualState()
    {
        bool wasRunning = _lastKnownGameRunning;
        bool running = IsWarThunderRunningOrStarting();
        if (running == wasRunning) return;

        UpdateVisualStates();

        // Game/anti-cheat exit can update the local manifest. Refresh the selected
        // branch immediately so RUNNING never falls back to a stale UPDATE/LAUNCH
        // state after War Thunder closes.
        if (wasRunning && !running && !IsDisposed && !_revisionCancellation.IsCancellationRequested)
            _ = RefreshGameVersionStatusAsync();
    }

    private bool IsWarThunderRunningOrStarting()
    {
        bool running = IsWarThunderRunning();
        if (running)
        {
            _gameLaunchPendingUntilUtc = DateTime.MinValue;
            return true;
        }
        return GameLaunchPending;
    }

    private static bool IsWarThunderRunning()
    {
        Process[] aces = Process.GetProcessesByName("aces");
        Process[] acesBe = Process.GetProcessesByName("aces_BE");
        try { return aces.Length > 0 || acesBe.Length > 0; }
        finally
        {
            foreach (Process process in aces) process.Dispose();
            foreach (Process process in acesBe) process.Dispose();
        }
    }

    private void ReapplyPendingControlsAfterGameExit()
    {
        if (IsWarThunderRunning() || string.IsNullOrWhiteSpace(_pendingControlsPresetPath)) return;

        _controlsReapplyTimer.Stop();
        string presetPath = _pendingControlsPresetPath;
        string profileName = _pendingControlsProfileName;
        _pendingControlsPresetPath = "";
        _pendingControlsProfileName = "";
        ApplyControlsProfile(presetPath, profileName);
    }

    private List<string> GetLiveMachineBlkTargets()
    {
        List<string> targets = new List<string>();

        void AddIfPresent(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            string fullPath = Path.GetFullPath(path);
            if (!targets.Any(existing => PathsEqual(existing, fullPath)))
            {
                targets.Add(fullPath);
            }
        }

        AddIfPresent(_machineBlkPath);
        AddIfPresent(ResolveSavedMachineBlkPath(_machineBlkPath));
        AddIfPresent(TryAutoDetectMachineBlk());

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        AddIfPresent(Path.Combine(documents, "My Games", "WarThunder", "Saves", "last", "production", "machine.blk"));
        AddIfPresent(Path.Combine(documents, "WarThunder", "Saves", "last", "production", "machine.blk"));

        return targets;
    }

    private static bool TryExtractNamedBlkBlock(string text, string blockName, out string block)
    {
        block = "";
        if (!TryFindNamedBlkBlock(text, blockName, out int start, out int length)) return false;
        block = text.Substring(start, length);
        return true;
    }

    private static bool TryFindNamedBlkBlock(string text, string blockName, out int start, out int length)
    {
        start = -1;
        length = 0;

        for (int i = 0; i <= text.Length - blockName.Length; i++)
        {
            if (!text.AsSpan(i).StartsWith(blockName, StringComparison.OrdinalIgnoreCase)) continue;

            bool leftOk = i == 0 || !(char.IsLetterOrDigit(text[i - 1]) || text[i - 1] == '_');
            int nameEnd = i + blockName.Length;
            bool rightOk = nameEnd >= text.Length || !(char.IsLetterOrDigit(text[nameEnd]) || text[nameEnd] == '_');
            if (!leftOk || !rightOk) continue;

            int brace = nameEnd;
            while (brace < text.Length && char.IsWhiteSpace(text[brace])) brace++;
            if (brace >= text.Length || text[brace] != '{') continue;

            bool inString = false;
            bool escape = false;
            int depth = 0;
            for (int j = brace; j < text.Length; j++)
            {
                char c = text[j];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        start = i;
                        length = j - i + 1;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(
            message,
            T("Profiles.MissingSettingsTitle"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning
        );
    }

    private void LoadState()
    {
        try
        {
            SavedState? state = null;
            foreach (string candidate in new[] { StateFilePath, StateFilePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    state = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(candidate));
                    if (state is not null) break;
                }
                catch (JsonException) { }
                catch (IOException) { }
            }

            if (state == null) return;

            _configBlkPath = state.ConfigBlkPath ?? "";
            _desktopBlkPath = state.DesktopBlkPath ?? "";
            _customVrBlkPath = state.CustomVrBlkPath ?? "";
            _machineBlkPath = ResolveSavedMachineBlkPath(state.MachineBlkPath ?? "");
            _desktopControlsBlkPath = state.DesktopControlsBlkPath ?? "";
            _vrControlsBlkPath = state.VrControlsBlkPath ?? "";
            _desktopControlProfiles.Clear();
            _desktopControlProfiles.AddRange((state.DesktopControlProfiles ?? new()).Where(profile => !string.IsNullOrWhiteSpace(profile.Path)));
            _vrControlProfiles.Clear();
            _vrControlProfiles.AddRange((state.VrControlProfiles ?? new()).Where(profile => !string.IsNullOrWhiteSpace(profile.Path)));
            _selectedDesktopControlProfileId = state.SelectedDesktopControlProfileId ?? "";
            _selectedVrControlProfileId = state.SelectedVrControlProfileId ?? "";
            MigrateControlProfileList(_desktopControlProfiles, _desktopControlsBlkPath, ref _selectedDesktopControlProfileId);
            MigrateControlProfileList(_vrControlProfiles, _vrControlsBlkPath, ref _selectedVrControlProfileId);
            _desktopControlsBlkPath = GetSelectedControlsPath(AppliedMode.Monitor);
            _vrControlsBlkPath = GetSelectedControlsPath(AppliedMode.VR);
            _warThunderExePath = ResolveSavedWarThunderLauncherPath(state.WarThunderExePath ?? "");
            _desktopGraphicsApi = state.DesktopGraphicsApi;
            _vrGraphicsApi = state.VrGraphicsApi;
            _switchControlsWithProfile = state.SwitchControlsWithProfile;
            _customVrEnabled = state.CustomVrEnabled;
            _showHighWarning = state.ShowHighWarning;
            _selectedVrPreset = state.SelectedVrPreset;
            _lastAppliedMode = state.LastAppliedMode;
            _useBetaBuilds = state.UseBetaBuilds;
            _minimizeToTray = state.MinimizeToTray;
            _startWithWindows = state.StartWithWindows;
            _showHomeAircraft = state.ShowHomeAircraft;
            _showHomeKeybinds = state.ShowHomeKeybinds;
            _startMinimizedToTray = state.StartMinimizedToTray;
            _useTestServer = state.UseTestServer;
            _languageCode = AppText.Normalize(state.LanguageCode);
            _uiTheme = AppThemeAssets.NormalizeThemeName(state.UiTheme);

            Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            Size requested = state.WindowLayoutRevision >= 183 &&
                             state.WindowWidth >= S(900) && state.WindowHeight >= S(470)
                ? new Size(state.WindowWidth, state.WindowHeight)
                : new Size(S(1600), S(900));

            // v2.0.6 migration: older revisions could save arbitrary aspect ratios.
            // Normalize them on load so Home, Neck, KeyBind and embedded VTrim all
            // receive the same predictable responsive viewport.
            ClientSize = FitClientSizeToLockedAspect(requested, work);
            _lastNormalClientSize = ClientSize;
        }
        catch
        {
            // Start fresh if settings cannot be loaded.
        }
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);

            SavedState state = new SavedState
            {
                ConfigBlkPath = _configBlkPath,
                DesktopBlkPath = _desktopBlkPath,
                CustomVrBlkPath = _customVrBlkPath,
                MachineBlkPath = _machineBlkPath,
                DesktopControlsBlkPath = _desktopControlsBlkPath,
                VrControlsBlkPath = _vrControlsBlkPath,
                DesktopControlProfiles = _desktopControlProfiles,
                VrControlProfiles = _vrControlProfiles,
                SelectedDesktopControlProfileId = _selectedDesktopControlProfileId,
                SelectedVrControlProfileId = _selectedVrControlProfileId,
                WarThunderExePath = _warThunderExePath,
                DesktopGraphicsApi = _desktopGraphicsApi,
                VrGraphicsApi = _vrGraphicsApi,
                SwitchControlsWithProfile = _switchControlsWithProfile,
                CustomVrEnabled = _customVrEnabled,
                ShowHighWarning = _showHighWarning,
                SelectedVrPreset = _selectedVrPreset,
                LastAppliedMode = _lastAppliedMode,
                UseBetaBuilds = _useBetaBuilds,
                MinimizeToTray = _minimizeToTray,
                StartWithWindows = _startWithWindows,
                ShowHomeAircraft = _showHomeAircraft,
                ShowHomeKeybinds = _showHomeKeybinds,
                StartMinimizedToTray = _startMinimizedToTray,
                UseTestServer = _useTestServer,
                LanguageCode = _languageCode,
                UiTheme = _uiTheme,
                WindowWidth = ClientSize.Width,
                WindowHeight = ClientSize.Height,
                WindowLayoutRevision = 205
            };

            string json = JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            AtomicFile.WriteTextWithBackup(StateFilePath, json);
        }
        catch
        {
            // Avoid crashing if settings cannot be saved.
        }
    }
}


