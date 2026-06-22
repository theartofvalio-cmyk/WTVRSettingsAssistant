using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows.Forms;

[assembly: SupportedOSPlatform("windows")]

namespace WTVRSettingsAssistant;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public static class AssetManager
{
    public static Image LoadImage(string fileName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name =>
                name.EndsWith($".Assets.{fileName}", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase));

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

        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name =>
                name.EndsWith($".Assets.{fileName}", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase));

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

        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name =>
                name.EndsWith($".Assets.{fileName}", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase));

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

public class MainForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string? pszSound, IntPtr hmod, int fdwSound);

    private const int SND_SYNC = 0x0000;
    private const int SND_NODEFAULT = 0x0002;
    private const int SND_FILENAME = 0x00020000;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const float AppScale = 1.25f;

    // Layout editor code is still inside the app, but disabled for release/customized builds.
    // Change these to true later if you want to re-enable F12 layout editing and external layout files.
    private const bool LayoutEditorEnabled = false;
    private const bool LoadExternalLayoutFiles = false;

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
    "X": 328,
    "Y": 681,
    "Width": 207,
    "Height": 66,
    "ZOrder": 1,
    "FontSize": 38
  },
  "VRButton": {
    "X": 882,
    "Y": 169,
    "Width": 698,
    "Height": 263,
    "ZOrder": 2,
    "FontSize": 0
  },
  "MonitorButton": {
    "X": 870,
    "Y": 448,
    "Width": 713,
    "Height": 260,
    "ZOrder": 3,
    "FontSize": 0
  },
  "InfoIcon": {
    "X": 1408,
    "Y": 45,
    "Width": 81,
    "Height": 81,
    "ZOrder": 4,
    "FontSize": 0
  },
  "SettingsIcon": {
    "X": 1498,
    "Y": 28,
    "Width": 114,
    "Height": 115,
    "ZOrder": 5,
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
    "Text": "Tip: You can drag and drop .blk files into the empty fields."
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
    "Y": 67,
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
    "Y": 195,
    "Width": 806,
    "Height": 51,
    "ZOrder": 7,
    "FontSize": 21.08,
    "Text": "Locate the Config.blk file in your War Thunder directory."
  },
  "DesktopTitle": {
    "X": 75,
    "Y": 243,
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
    "Y": 296,
    "Width": 280,
    "Height": 86,
    "ZOrder": 11,
    "FontSize": 0,
    "Text": null
  },
  "DesktopCaptureSettings": {
    "X": 309,
    "Y": 244,
    "Width": 292,
    "Height": 52,
    "ZOrder": 12,
    "FontSize": 0,
    "Text": null
  },
  "DesktopRemoveSettings": {
    "X": 876,
    "Y": 315,
    "Width": 200,
    "Height": 50,
    "ZOrder": 13,
    "FontSize": 0,
    "Text": null
  },
  "DesktopDescription": {
    "X": 75,
    "Y": 374,
    "Width": 901,
    "Height": 48,
    "ZOrder": 14,
    "FontSize": 21.273611,
    "Text": "Your custom settings for War Thunder when playing on flat screen."
  },
  "CustomToggleTitle": {
    "X": 1018,
    "Y": 138,
    "Width": 430,
    "Height": 50,
    "ZOrder": 15,
    "FontSize": 34,
    "Text": "CUSTOM VR .blk"
  },
  "CustomToggle": {
    "X": 1106,
    "Y": 195,
    "Width": 284,
    "Height": 154,
    "ZOrder": 16,
    "FontSize": 0,
    "Text": null
  },
  "PresetTitle": {
    "X": 72,
    "Y": 474,
    "Width": 887,
    "Height": 91,
    "ZOrder": 17,
    "FontSize": 39.68158,
    "Text": "VR PRESETS"
  },
  "LowButton": {
    "X": 72,
    "Y": 550,
    "Width": 427,
    "Height": 283,
    "ZOrder": 18,
    "FontSize": 0,
    "Text": null
  },
  "MediumButton": {
    "X": 525,
    "Y": 550,
    "Width": 424,
    "Height": 281,
    "ZOrder": 19,
    "FontSize": 0,
    "Text": null
  },
  "HighButton": {
    "X": 976,
    "Y": 550,
    "Width": 425,
    "Height": 282,
    "ZOrder": 20,
    "FontSize": 0,
    "Text": null
  },
  "MoreInfoButton": {
    "X": 1106,
    "Y": 374,
    "Width": 465,
    "Height": 136,
    "ZOrder": 21,
    "FontSize": 0,
    "Text": null
  },
  "HelpButton": {
    "X": 292,
    "Y": 498,
    "Width": 43,
    "Height": 43,
    "ZOrder": 22,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrTitle": {
    "X": 75,
    "Y": 431,
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
    "Y": 488,
    "Width": 280,
    "Height": 86,
    "ZOrder": 26,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrCaptureSettings": {
    "X": 211,
    "Y": 434,
    "Width": 300,
    "Height": 54,
    "ZOrder": 27,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrRemoveSettings": {
    "X": 876,
    "Y": 505,
    "Width": 200,
    "Height": 50,
    "ZOrder": 28,
    "FontSize": 0,
    "Text": null
  },
  "CustomVrDescription": {
    "X": 75,
    "Y": 570,
    "Width": 911,
    "Height": 39,
    "ZOrder": 29,
    "FontSize": 19.5,
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
    "Height": 104,
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
    private const float LockedWindowAspectRatio = BaseClientWidth / (float)BaseClientHeight;
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
        public bool CustomVrEnabled { get; set; }
        public bool ShowHighWarning { get; set; } = true;
        public VrPreset SelectedVrPreset { get; set; } = VrPreset.None;
        public AppliedMode LastAppliedMode { get; set; } = AppliedMode.None;
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
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
        private readonly Color _handleColor = Color.FromArgb(0, 210, 255);

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
        private const int DesignCanvasHeight = 844;

        public bool LayoutEditMode { get; private set; }

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
                    setTitle("WT VR Settings Assistant  -  CANVAS EDIT MODE | Ctrl+Click multi-select | Drag | Snap | Ctrl+L/R/T/B align | Ctrl+E distribute | Alt resize = stretch | Ctrl+S save");
                }
                else
                {
                    SaveLayout();
                    setTitle("WT VR Settings Assistant");
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
                setTitle("WT VR Settings Assistant");
                Invalidate();
                return true;
            }

            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveLayout();
                setTitle("WT VR Settings Assistant  -  LAYOUT SAVED");
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
                Text = "Edit text",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 135),
                BackColor = Color.FromArgb(20, 26, 28),
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

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Left = 220,
                Top = 78,
                Width = 85,
                Height = 34
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
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
                }
                else if (item.IsText)
                {
                    using Font font = _fontFactory(item.FontSize, item.FontStyle);
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

                    e.Graphics.DrawImage(item.Image, drawBounds);
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
            using Pen pen = new Pen(primary ? Color.Yellow : selected ? _handleColor : Color.FromArgb(70, 0, 210, 255), primary ? 2 : 1);
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
    private readonly Color _backgroundColor = Color.FromArgb(5, 12, 14);
    private readonly Color _fieldColor = Color.FromArgb(0, 8, 9);
    private readonly Color _textColor = Color.FromArgb(235, 238, 240);

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

    private PictureBox _mainVrButton = null!;
    private PictureBox _mainMonitorButton = null!;
    private PictureBox _mainLogoPicture = null!;
    private Label _mainVersionLabel = null!;
    private PictureBox _mainInfoButton = null!;
    private PictureBox _mainSettingsButton = null!;

    private readonly Dictionary<string, Control> _layoutTargets = new(StringComparer.OrdinalIgnoreCase);
    private bool _layoutEditMode;
    private Control? _selectedLayoutTarget;
    private Control? _layoutDragTarget;
    private Point _layoutDragMouseStart;
    private Point _layoutDragControlStart;
    private LayoutResizeMode _layoutResizeMode = LayoutResizeMode.None;
    private Rectangle _layoutStartBounds;
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
    private bool _lockingWindowAspect;
    private Size _lastNormalClientSize;

    private bool _customVrEnabled;
    private bool _showHighWarning = true;

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

    public MainForm()
    {
        AutoScaleMode = AutoScaleMode.None;

        Text = "WT VR Settings Assistant";
        ClientSize = new Size(S(1300), S(675));
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(S(900), S(470));
        BackColor = _backgroundColor;
        DoubleBuffered = true;
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;
        MouseWheel += MainForm_MouseWheel;
        Resize += MainForm_Resize;
        ResizeEnd += (_, _) => SaveState();
        FormClosing += (_, _) => SaveState();

        LoadAssets();
        LoadState();
        _lastNormalClientSize = ClientSize;

        BuildMainScreen();
        BuildSettingsScreen();
        BuildAboutScreen();
        BuildRecommendedScreen();

        if (LoadExternalLayoutFiles)
        {
            LoadLayout();
        }

        ShowScreen(_mainPanel);
        UpdateVisualStates();
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Normal)
        {
            _lastNormalClientSize = ClientSize;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SIZING && WindowState == FormWindowState.Normal)
        {
            ResizeRect rect = Marshal.PtrToStructure<ResizeRect>(m.LParam);
            LockResizeRectangle((int)m.WParam, ref rect);
            Marshal.StructureToPtr(rect, m.LParam, true);
        }

        base.WndProc(ref m);
    }

    private void LockResizeRectangle(int edge, ref ResizeRect rect)
    {
        int extraWidth = Math.Max(0, Width - ClientSize.Width);
        int extraHeight = Math.Max(0, Height - ClientSize.Height);

        int windowWidth = Math.Max(1, rect.Right - rect.Left);
        int windowHeight = Math.Max(1, rect.Bottom - rect.Top);

        int clientWidth = Math.Max(S(900), windowWidth - extraWidth);
        int clientHeight = Math.Max(S(470), windowHeight - extraHeight);

        bool widthDriven = edge == WMSZ_LEFT || edge == WMSZ_RIGHT;
        bool heightDriven = edge == WMSZ_TOP || edge == WMSZ_BOTTOM;

        if (widthDriven)
        {
            clientHeight = Math.Max(S(470), (int)Math.Round(clientWidth / LockedWindowAspectRatio));
        }
        else if (heightDriven)
        {
            clientWidth = Math.Max(S(900), (int)Math.Round(clientHeight * LockedWindowAspectRatio));
        }
        else
        {
            float currentRatio = clientWidth / (float)Math.Max(1, clientHeight);

            if (currentRatio > LockedWindowAspectRatio)
            {
                clientHeight = Math.Max(S(470), (int)Math.Round(clientWidth / LockedWindowAspectRatio));
            }
            else
            {
                clientWidth = Math.Max(S(900), (int)Math.Round(clientHeight * LockedWindowAspectRatio));
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
                    bitmap.SetPixel(x, y, Color.FromArgb(0, pixel.R, pixel.G, pixel.B));
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
        g.Clear(Color.FromArgb(45, 0, 0));

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
        _mainCanvas.AddText("VersionText", "Version 1.0", new Rectangle(300, 685, 275, 119), 50.48309f, FontStyle.Regular);
        _mainCanvas.AddImage("VRButton", _vrOrange, new Rectangle(889, 153, 676, 291), ApplyVrMode);
        _mainCanvas.AddImage("MonitorButton", _monitorOrange, new Rectangle(883, 456, 689, 288), ApplyMonitorMode);

        if (_discordImage != null)
        {
            _mainCanvas.AddImage("DiscordButton", _discordImage, new Rectangle(1375, 762, 228, 69), OpenDiscord);
        }
        else
        {
            _mainCanvas.AddText("DiscordButton", "DISCORD", new Rectangle(1375, 762, 228, 69), 34f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, OpenDiscord);
        }

        _mainCanvas.AddImage("InfoIcon", _infoImage, new Rectangle(1408, 45, 81, 81), () =>
        {
            ShowScreen(_aboutPanel);
        });
        _mainCanvas.AddImage("SettingsIcon", _settingsImage, new Rectangle(1498, 28, 114, 114), () =>
        {
            ShowScreen(_settingsPanel);
        });

        _mainCanvas.ApplyLayout(ParseBakedLayout(BakedMainLayoutJson));
    }

    private void BuildSettingsScreen()
    {
        _settingsPanel = CreateScreenPanel();
        Controls.Add(_settingsPanel);

        _settingsCanvas = new MainPageCanvas(SettingsLayoutFilePath, _backgroundColor, _textColor, UiFont)
        {
            Dock = DockStyle.Fill
        };

        _settingsPanel.Controls.Add(_settingsCanvas);

        _settingsCanvas.AddText("SettingsTip", "Tip: You can drag and drop .blk files into the empty fields.", new Rectangle(425, 3, 772, 50), 23.753845f, FontStyle.Italic);

        _settingsCanvas.AddImage("InfoIcon", _infoImage, new Rectangle(1408, 45, 81, 81), () => ShowScreen(_aboutPanel));
        _settingsCanvas.AddImage("HomeIcon", _homeImage, new Rectangle(1498, 28, 114, 106), () => ShowScreen(_mainPanel));

        _settingsCanvas.AddText("ConfigTitle", "WarThunder/Config.blk", new Rectangle(75, 85, 520, 42), 27f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddRectangle("ConfigField", new Rectangle(72, 138, 500, 50), _fieldColor, () => BrowseForFile(FileSlot.Config), path => SetFilePath(FileSlot.Config, path));
        _settingsCanvas.AddText("ConfigPathText", "", new Rectangle(84, 141, 475, 40), 16f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center, null, path => SetFilePath(FileSlot.Config, path));
        _settingsCanvas.AddImage("ConfigBrowse", _browseRed, new Rectangle(592, 128, 280, 86), () => BrowseForFile(FileSlot.Config), path => SetFilePath(FileSlot.Config, path));
        _settingsCanvas.AddText("ConfigDescription", "Locate the Config.blk file in your War Thunder directory.", new Rectangle(75, 195, 806, 51), 21.08f, FontStyle.Italic, StringAlignment.Near, StringAlignment.Center);

        _settingsCanvas.AddText("DesktopTitle", "DESCTOP .blk", new Rectangle(75, 265, 520, 42), 27f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddRectangle("DesktopField", new Rectangle(75, 310, 500, 50), _fieldColor, () => BrowseForFile(FileSlot.Desktop), path => SetFilePath(FileSlot.Desktop, path));
        _settingsCanvas.AddText("DesktopPathText", "", new Rectangle(85, 315, 475, 40), 16f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center, null, path => SetFilePath(FileSlot.Desktop, path));
        _settingsCanvas.AddImage("DesktopBrowse", _browseRed, new Rectangle(592, 296, 280, 86), () => BrowseForFile(FileSlot.Desktop), path => SetFilePath(FileSlot.Desktop, path));
        _settingsCanvas.AddImage("DesktopCaptureSettings", _captureSettingsImage, new Rectangle(274, 260, 250, 60), CaptureDesktopSettings);
        _settingsCanvas.AddImage("DesktopRemoveSettings", _removeGrayImage, new Rectangle(884, 310, 200, 60), null);
        _settingsCanvas.AddText("DesktopDescription", "Your custom settings for War Thunder when playing on flat screen.", new Rectangle(75, 374, 901, 48), 21.273611f, FontStyle.Italic, StringAlignment.Near, StringAlignment.Center);

        _settingsCanvas.AddText("CustomToggleTitle", "CUSTOM VR .blk", new Rectangle(951, 76, 430, 50), 34f, FontStyle.Regular);
        _settingsCanvas.AddImage("CustomToggle", _buttonOff, new Rectangle(976, 132, 380, 203), ToggleCustomVr);

        _settingsCanvas.AddText("PresetTitle", "VR PRESETS", new Rectangle(72, 474, 887, 91), 39.68158f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddImage("LowButton", _lowRed, new Rectangle(72, 550, 427, 285), () => SelectVrPreset(VrPreset.Low));
        _settingsCanvas.AddImage("MediumButton", _mediumRed, new Rectangle(525, 550, 424, 283), () => SelectVrPreset(VrPreset.Medium));
        _settingsCanvas.AddImage("HighButton", _highRed, new Rectangle(976, 550, 425, 283), () => SelectVrPreset(VrPreset.High));

        if (_recommendedSettingsImage != null)
        {
            _settingsCanvas.AddImage("MoreInfoButton", _recommendedSettingsImage, new Rectangle(976, 355, 609, 178), ShowRecommendedSettingsFlow);
        }
        else
        {
            _settingsCanvas.AddText("MoreInfoButton", "RECOMMENDED SETTINGS", new Rectangle(976, 355, 609, 178), 28f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, ShowRecommendedSettingsFlow);
        }

        if (_helpImage != null)
        {
            _settingsCanvas.AddImage("HelpButton", _helpImage, new Rectangle(292, 498, 43, 43), ShowRecommendedGpuGraph);
        }
        else
        {
            _settingsCanvas.AddText("HelpButton", "?", new Rectangle(292, 498, 43, 43), 24f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, ShowRecommendedGpuGraph);
        }

        _settingsCanvas.AddText("CustomVrTitle", "VR .blk", new Rectangle(75, 430, 520, 42), 30f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center);
        _settingsCanvas.AddRectangle("CustomVrField", new Rectangle(72, 500, 500, 50), _fieldColor, () => BrowseForFile(FileSlot.CustomVr), path => SetFilePath(FileSlot.CustomVr, path));
        _settingsCanvas.AddText("CustomVrPathText", "", new Rectangle(84, 505, 475, 40), 16f, FontStyle.Regular, StringAlignment.Near, StringAlignment.Center, null, path => SetFilePath(FileSlot.CustomVr, path));
        _settingsCanvas.AddImage("CustomVrBrowse", _browseRed, new Rectangle(592, 488, 280, 86), () => BrowseForFile(FileSlot.CustomVr), path => SetFilePath(FileSlot.CustomVr, path));
        _settingsCanvas.AddImage("CustomVrCaptureSettings", _captureSettingsImage, new Rectangle(274, 425, 250, 60), CaptureCustomVrSettings);
        _settingsCanvas.AddImage("CustomVrRemoveSettings", _removeGrayImage, new Rectangle(884, 500, 200, 60), null);
        _settingsCanvas.AddText("CustomVrDescription", "Your custom settings for War Thunder when playing in VR.", new Rectangle(75, 570, 911, 39), 19.5f, FontStyle.Italic, StringAlignment.Near, StringAlignment.Center);

        _settingsCanvas.ApplyLayout(ParseBakedLayout(BakedSettingsLayoutJson));
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
            BakedRecommendedGpuLayoutJson);

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
        _recommendedPage = Math.Max(1, Math.Min(5, page));
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
            _recommendedCanvas.AddRectangle("RecommendedPageImageBackground", new Rectangle(0, 230, 1625, 517), Color.FromArgb(28, 36, 38));
            _recommendedCanvas.AddText(
                "RecommendedPageImageMissingText",
                $"Missing image: {_recommendedSoftwarePrefix}_Page_{_recommendedPage}.png",
                new Rectangle(0, 230, 1625, 517),
                30f,
                FontStyle.Italic,
                StringAlignment.Center,
                StringAlignment.Center);
        }

        bool canGoBack = _recommendedPage > 1;
        bool canGoNext = _recommendedPage < 5;
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
            "BACK",
            new Rectangle(333, 754, 340, 81),
            backClickAction);

        _recommendedCanvas.AddImage("RecommendedBottomHome", _homeImage, new Rectangle(773, 758, 78, 73), () => ShowScreen(_mainPanel));

        AddRecommendedNavButton(
            "RecommendedBottomNext",
            canGoNext ? _nextButtonImage : _nextGrayButtonImage,
            "NEXT",
            new Rectangle(955, 754, 340, 81),
            nextClickAction);

        _recommendedCanvas.ApplyLayout(ParseBakedLayout(BakedRecommendedViewerLayoutJson));
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

        _aboutCanvas = new MainPageCanvas(AboutLayoutFilePath, _backgroundColor, _textColor, UiFont)
        {
            Dock = DockStyle.Fill
        };

        _aboutPanel.Controls.Add(_aboutCanvas);

        string aboutText =
            "Hi, you probably haven’t heard of me, and that’s completely fine. :)\n\n" +
            "My name is Valentin, and I’m a War Thunder VR player and content creator. I enjoy making guides\n" +
            "and helping people optimize their game for a smoother and more enjoyable VR experience.\n\n" +
            "I created this tool to help the War Thunder VR community switch more easily between VR and flat-screen play,\n" +
            "without having to manually change settings every time. The goal is simple: make the process smoother, faster, and less frustrating.\n\n" +
            "I’ll also be making an updated VR video guide that covers additional settings outside the game. For now, the included LOW, MEDIUM,\n" +
            "and HIGH presets are based on real settings I’ve tested with people from our Discord community. I’ve worked with players one-on-one,\n" +
            "optimizing their games across different types of hardware and testing what works best in practice.\n\n" +
            "If you’re new to VR, these presets should help you get started much more easily and give you a solid foundation for your first War Thunder VR experience.\n\n" +
            "Cheers,\n" +
            "Val";

        string supportText =
            "This tool is completely free and open source.\n" +
            "If you like what I do and you want to help me out\n" +
            "to make more stuff like this, feel free to click Subscribe\n" +
            "or buy me a Beer! :)";

        _aboutCanvas.AddText(
            "AboutText",
            aboutText,
            new Rectangle(11, 10, 1511, 570),
            23.744286f,
            FontStyle.Regular,
            StringAlignment.Near,
            StringAlignment.Near
        );

        _aboutCanvas.AddText(
            "SupportText",
            supportText,
            new Rectangle(9, 583, 809, 235),
            27.381538f,
            FontStyle.Italic,
            StringAlignment.Near,
            StringAlignment.Near
        );

        if (_youtubeImage != null)
        {
            _aboutCanvas.AddImage("YoutubeButton", _youtubeImage, new Rectangle(781, 505, 345, 282), OpenYouTube);
        }
        else
        {
            _aboutCanvas.AddText("YoutubeButton", "SUBSCRIBE", new Rectangle(781, 505, 345, 282), 28f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, OpenYouTube);
        }

        if (_beerImage != null)
        {
            _aboutCanvas.AddImage("BeerButton", _beerImage, new Rectangle(1180, 457, 301, 359), ShowBeerMessage);
        }
        else
        {
            _aboutCanvas.AddText("BeerButton", "BEER", new Rectangle(1180, 457, 301, 359), 28f, FontStyle.Bold, StringAlignment.Center, StringAlignment.Center, ShowBeerMessage);
        }

        _aboutCanvas.AddImage("InfoIcon", _infoImage, new Rectangle(1408, 45, 81, 81), () =>
        {
            ShowScreen(_aboutPanel);
        });

        _aboutCanvas.AddImage("HomeIcon", _homeImage, new Rectangle(1498, 28, 114, 115), () =>
        {
            ShowScreen(_mainPanel);
        });

        _aboutCanvas.ApplyLayout(ParseBakedLayout(BakedAboutLayoutJson));
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
        Button button = new Button
        {
            Text = text,
            Left = S(left),
            Top = S(top),
            Width = S(width),
            Height = S(height),
            BackColor = Color.FromArgb(20, 26, 28),
            ForeColor = _textColor,
            FlatStyle = FlatStyle.Flat,
            Font = UiFont(S(22), FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(180, 20, 20);
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

        if (targetSlot == FileSlot.Desktop && IsDesktopConfigured())
        {
            return;
        }

        if (targetSlot == FileSlot.CustomVr &&
            !string.IsNullOrWhiteSpace(_customVrBlkPath) &&
            File.Exists(_customVrBlkPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(GraphicSettingsFolder);

            string targetFileName = targetSlot == FileSlot.Desktop
                ? "DesktopSettings.blk"
                : "VRSettings.blk";

            string targetPath = Path.Combine(GraphicSettingsFolder, targetFileName);
            File.Copy(_configBlkPath, targetPath, true);

            if (targetSlot == FileSlot.Desktop)
            {
                _desktopBlkPath = targetPath;
            }
            else if (targetSlot == FileSlot.CustomVr)
            {
                _customVrBlkPath = targetPath;
            }

            _lastAppliedMode = AppliedMode.None;
            SaveState();
            UpdateVisualStates();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Capture settings failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void BrowseForFile(FileSlot slot)
    {
        using OpenFileDialog dialog = new OpenFileDialog();

        dialog.Title = "Select .blk file";
        dialog.Filter = "BLK files (*.blk)|*.blk|All files (*.*)|*.*";

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetFilePath(slot, dialog.FileName);
        }
    }

    private void SetFilePath(FileSlot slot, string path)
    {
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
                break;

            case FileSlot.CustomVr:
                _customVrBlkPath = path;
                _lastAppliedMode = AppliedMode.None;
                break;
        }

        SaveState();
        UpdateVisualStates();
    }

    private void ToggleCustomVr()
    {
        _customVrEnabled = !_customVrEnabled;
        _lastAppliedMode = AppliedMode.None;

        SaveState();
        UpdateVisualStates();
    }

    private void SelectVrPreset(VrPreset preset)
    {
        if (preset == VrPreset.High && _showHighWarning)
        {
            ShowHighWarningDialog();
        }

        _selectedVrPreset = preset;
        _lastAppliedMode = AppliedMode.None;

        SaveState();
        UpdateVisualStates();
    }

    private void ApplyVrMode()
    {
        string? warning = GetVrWarning();

        if (warning != null)
        {
            ShowWarning(warning);
            return;
        }

        if (_customVrEnabled)
        {
            ApplyBlkFile(_customVrBlkPath, AppliedMode.VR);
            return;
        }

        string presetContent = GetSelectedVrPresetContent();

        if (string.IsNullOrWhiteSpace(presetContent))
        {
            ShowWarning("Please go to Settings first and choose a VR preset.");
            return;
        }

        ApplyBlkContent(presetContent, AppliedMode.VR);
    }

    private void ApplyMonitorMode()
    {
        string? warning = GetMonitorWarning();

        if (warning != null)
        {
            ShowWarning(warning);
            return;
        }

        ApplyBlkFile(_desktopBlkPath, AppliedMode.Monitor);
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

        return null;
    }

    private void ApplyBlkFile(string sourcePath, AppliedMode mode)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                ShowWarning($"Source .blk file was not found:\n\n{sourcePath}");
                return;
            }

            if (string.IsNullOrWhiteSpace(_configBlkPath))
            {
                ShowWarning("Please go to Settings first and select your War Thunder config.blk file.");
                return;
            }

            string? configFolder = Path.GetDirectoryName(_configBlkPath);

            if (string.IsNullOrWhiteSpace(configFolder) || !Directory.Exists(configFolder))
            {
                ShowWarning("The War Thunder config.blk folder does not exist.");
                return;
            }

            File.Copy(sourcePath, _configBlkPath, true);

            _lastAppliedMode = mode;

            SaveState();
            UpdateVisualStates();

            // Success popups are intentionally disabled so switching presets is instant and quiet.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ApplyBlkContent(string blkContent, AppliedMode mode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_configBlkPath))
            {
                ShowWarning("Please go to Settings first and select your War Thunder config.blk file.");
                return;
            }

            string? configFolder = Path.GetDirectoryName(_configBlkPath);

            if (string.IsNullOrWhiteSpace(configFolder) || !Directory.Exists(configFolder))
            {
                ShowWarning("The War Thunder config.blk folder does not exist.");
                return;
            }

            File.WriteAllText(_configBlkPath, blkContent);

            _lastAppliedMode = mode;

            SaveState();
            UpdateVisualStates();

            // Success popups are intentionally disabled so switching presets is instant and quiet.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
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
            _configBrowseButton.Image = IsConfigSelected() ? _browseGreen : _browseRed;
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
        }

        if (_settingsCanvas != null)
        {
            _settingsCanvas.SetText("ConfigPathText", _configBlkPath);
            _settingsCanvas.SetText("DesktopPathText", _desktopBlkPath);
            _settingsCanvas.SetText("CustomVrPathText", _customVrBlkPath);

            bool canCaptureDesktopSettings = IsConfigSelected() && !IsDesktopConfigured();
            bool canRemoveDesktopSettings = !string.IsNullOrWhiteSpace(_desktopBlkPath);
            bool customVrFileLoaded = !string.IsNullOrWhiteSpace(_customVrBlkPath) && File.Exists(_customVrBlkPath);
            bool canCaptureCustomVrSettings = IsConfigSelected() && !customVrFileLoaded;
            bool canRemoveCustomVrSettings = !string.IsNullOrWhiteSpace(_customVrBlkPath);

            _settingsCanvas.SetImage("ConfigBrowse", IsConfigSelected() ? _browseGreen : _browseRed);
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
            _settingsCanvas.SetItemVisible("CustomVrField", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrPathText", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrBrowse", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrCaptureSettings", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrRemoveSettings", _customVrEnabled);
            _settingsCanvas.SetItemVisible("CustomVrDescription", _customVrEnabled);
        }
    }

    private void ShowScreen(Panel panel)
    {
        _mainPanel.Visible = false;
        _settingsPanel.Visible = false;
        _aboutPanel.Visible = false;
        if (_recommendedPanel != null)
        {
            _recommendedPanel.Visible = false;
        }

        panel.Visible = true;
        panel.BringToFront();

        UpdateVisualStates();
    }

    private void RegisterLayoutTarget(string key, Control control)
    {
        // The old PictureBox-based layout editor is no longer used on the main page.
        // The main page is drawn by MainPageCanvas so PNG transparency and overlapping layers work properly.
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys keyCode = keyData & Keys.KeyCode;

        if (HandleSecretCode(keyCode))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
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
        _mainCanvas?.Focus();
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
            ForeColor = Color.FromArgb(170, 190, 195),
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
            ForeColor = Color.FromArgb(170, 190, 195),
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
        dialog.Text = "IMPORTANT";
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
            Text =
                "Hi, congrats, you’ve chosen preset HIGH, also known by me as the \"I’ll burn your house down\" preset!\n\n" +
                "The HIGH preset uses DLAA, while War Thunder normally defaults to DLSS. NVIDIA users should use NVIDIA Profile Inspector and apply the settings shown below.\n" +
                "Once done, press Apply Changes inside NVIDIA Profile Inspector.",
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
            Text = "🔍 Zoom",
            Left = nvidiaImage.Left + nvidiaImage.Width - S(125),
            Top = nvidiaImage.Top + S(12),
            Width = S(105),
            Height = S(32),
            BackColor = Color.FromArgb(20, 26, 28),
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
            Text = "Download NVIDIA Profile Inspector here",
            Left = S(35),
            Top = S(600),
            Width = S(830),
            Height = S(34),
            LinkColor = Color.FromArgb(120, 210, 255),
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Color.FromArgb(120, 210, 255),
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
            Text =
                "This only applies if you’re using an NVIDIA GPU.\n\n" +
                "If you’re using another type of GPU, switch Anti-Aliasing to TSR.\n\n" +
                "If you’re not happy with the result, turn anti-aliasing OFF completely for much smoother performance.\n\n" +
                "After changing settings, create a custom profile so your tuned settings are not lost when switching presets.",
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

        CheckBox showAgainCheckBox = new CheckBox
        {
            Text = "Show this window every time I switch to HIGH",
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

        Button okButton = new Button
        {
            Text = "OK",
            Left = S(950),
            Top = S(650),
            Width = S(120),
            Height = S(40),
            BackColor = Color.FromArgb(20, 26, 28),
            ForeColor = _textColor,
            FlatStyle = FlatStyle.Flat,
            Font = UiFont(S(20), FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        okButton.FlatAppearance.BorderColor = Color.FromArgb(180, 20, 20);
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
            Text = "NVIDIA Profile Inspector - Zoom",
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
            Text = "Click image to close",
            Dock = DockStyle.Bottom,
            Height = S(34),
            ForeColor = _textColor,
            BackColor = Color.FromArgb(20, 26, 28),
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
                "Could not open NVIDIA Profile Inspector link",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
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
                "Could not open Discord invite",
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
                "Could not open link",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private sealed class RecommendedVrSettingsForm : Form
    {
        private readonly MainPageCanvas _canvas;

        public RecommendedVrSettingsForm(Image? verticalLineImage, Color bgColor, Color textColor, string bakedLayoutJson)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "RECOMMENDED VR SETTINGS";
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
                Dock = DockStyle.Fill
            };

            Controls.Add(_canvas);

            AddCanvasItems(verticalLineImage, bgColor);
            _canvas.ApplyLayout(ParseBakedLayout(bakedLayoutJson));
        }

        private Font UiFont(float pixelSize, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", pixelSize, style, GraphicsUnit.Pixel);
        }

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
                "The RECOMMENDED VR SETTINGS presets were created with different PC hardware limitations in mind. These presets are based on settings that have worked well for different players in the War Thunder VR community after manual testing, tweaking, and optimization.\n\nIf you are unsure which preset to choose, check the SPEC recommendations below to help you decide which preset best matches your system.",
                new Rectangle(13, 0, 1565, 230),
                27.0002f,
                FontStyle.Italic,
                StringAlignment.Near,
                StringAlignment.Near
            );

            _canvas.AddText(
                "GpuLabel",
                "GPU'S:",
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
                "LOW",
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
                "MEDIUM",
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
                "HIGH",
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
                "Info: Your GPU plays the biggest role in VR performance in War Thunder, but overall the difference between the Medium and High presets is usually not very noticeable. Even if you have an RTX 5090, you can still use Medium settings for better stability without a problem.",
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
                _canvas.AddRectangle($"{keyPrefix}Line", new Rectangle(x, 226, 47, 529), Color.FromArgb(228, 76, 96));
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
            Text = "WT VR Settings Assistant";
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
            Button button = new Button
            {
                Text = text,
                Width = 110,
                Height = 38,
                Left = left,
                Top = top,
                Font = fontFactory(14, FontStyle.Regular),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 33, 38),
                ForeColor = textColor,
                TabStop = true
            };

            button.FlatAppearance.BorderColor = Color.FromArgb(76, 100, 106);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 48, 55);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 62, 70);
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

                Button saveButton = new Button
                {
                    Text = "Save Diploma Image",
                    Left = Math.Max(12, (displayWidth - 230) / 2),
                    Top = displayHeight + 12,
                    Width = 230,
                    Height = 40,
                    Font = fontFactory(15, FontStyle.Regular),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(18, 30, 34),
                    ForeColor = textColor
                };
                saveButton.FlatAppearance.BorderColor = Color.FromArgb(85, 110, 116);
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

            using Bitmap frame = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppArgb);
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

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = frame.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = SelectObject(memDc, hBitmap);

            try
            {
                Point topLeft = Location;
                Size size = Size;
                Point sourcePoint = Point.Empty;
                BlendFunction blend = new BlendFunction
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screenDc, ref topLeft, ref size, memDc, ref sourcePoint, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
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
                "Could not open donation link",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(
            message,
            "Missing settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning
        );
    }

    private void LoadState()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                return;
            }

            string json = File.ReadAllText(StateFilePath);
            SavedState? state = JsonSerializer.Deserialize<SavedState>(json);

            if (state == null)
            {
                return;
            }

            _configBlkPath = state.ConfigBlkPath ?? "";
            _desktopBlkPath = state.DesktopBlkPath ?? "";
            _customVrBlkPath = state.CustomVrBlkPath ?? "";
            _customVrEnabled = state.CustomVrEnabled;
            _showHighWarning = state.ShowHighWarning;
            _selectedVrPreset = state.SelectedVrPreset;
            _lastAppliedMode = state.LastAppliedMode;

            if (state.WindowWidth >= S(900) && state.WindowHeight >= S(470))
            {
                ClientSize = new Size(state.WindowWidth, state.WindowHeight);
                _lastNormalClientSize = ClientSize;
            }
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
                CustomVrEnabled = _customVrEnabled,
                ShowHighWarning = _showHighWarning,
                SelectedVrPreset = _selectedVrPreset,
                LastAppliedMode = _lastAppliedMode,
                WindowWidth = ClientSize.Width,
                WindowHeight = ClientSize.Height
            };

            string json = JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(StateFilePath, json);
        }
        catch
        {
            // Avoid crashing if settings cannot be saved.
        }
    }
}
