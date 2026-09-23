# War Thunder VR Assistant

Windows companion app for War Thunder pilots using VR, HOTAS, and rudder pedals. Version **2.0.1**.

**[Website and setup guide](https://wtvrassistant.com/)** · **[Download the latest Windows release](https://github.com/theartofvalio-cmyk/WTVRSettingsAssistant/releases/latest)**

## What it does

- **Neck Assistant** adjusts VR head movement through OpenXR, VDXR, or SteamVR for more comfortable rearward viewing.
- **KeyBind Assistant** maps keyboard, mouse, and HOTAS inputs, including physical two-position switches, to game button actions.
- **VTrim Assistant** routes HOTAS axes through vJoy, provides virtual trim, and lets you edit pitch, roll, and yaw response curves.
- **Aircraft profiles** are created as you fly using local War Thunder telemetry. The app switches to the detected aircraft's profile automatically. You can also add and customize profiles before flying them. A profile left on **Default** uses the Trim Dashboard and default axis settings; **Custom** stores aircraft-specific controls and curves.
- **Monitor and VR profiles** let you manage separate graphics and control setups.

For VTrim, select the vJoy axes in War Thunder and restore the game's axis tuning to default so the app's curves and trim are applied accurately. The [website](https://wtvrassistant.com/) has examples and setup details.

**Flight Assistant is disabled** while the project awaits clarification from Gaijin's legal team. The project aims to support fair play and to revise or disable features if concerns are identified. It is an independent community project, unaffiliated with Gaijin Entertainment.

## Install

1. Download the ZIP from the [latest release](https://github.com/theartofvalio-cmyk/WTVRSettingsAssistant/releases/latest).
2. Extract the whole ZIP to a writable folder. Keep the executable, DLLs, `Drivers`, and `Licenses` together.
3. Run `WTVRSettingsAssistant.exe`. The app checks for and installs newer releases automatically while preserving local settings and profiles.

The app targets Windows x86 and uses the [vJoy driver](https://github.com/shauleiz/vJoy) for virtual axis output. The release includes the vJoy setup program in `Drivers/vJoy`.

## Build from source

Install the .NET 10 SDK on Windows, then run `Build.cmd` from this repository root. The publish output is written to `Builds/WTAssistant`. The source includes third-party license notices under `WTAssistant/ThirdParty`.

## License and attribution

See the license files in this repository and the third-party notices in the release. War Thunder names and imagery belong to their respective owners.
