# Source notes - WT Assistant 1.8.8

WT Assistant is a .NET 10 WinForms application. Neck Assistant includes the existing XRNeckSafer OpenXR API-layer files under `WTAssistant/ThirdParty/XRNeckSafer` with their license.

The integrated VTrim source is under `Modules/VTrim/Integration/VTrim`. Revision 1.8.8 does **not** contain the experimental custom VTrim kernel driver or its WDK/build/setup tooling. VTrim uses the established `vJoy.Wrapper` NuGet package and vJoy Device 1 for its game-facing X/Y/Rz axes.

War Thunder Live/Test switching, DevServer handling and update/version checks remain implemented in `GameServices_REV_1_8_8.cs`, `WarThunderServerConfig.cs`, and `MainForm_REV_1_8_8.cs`.
