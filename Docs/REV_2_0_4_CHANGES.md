# Revision 2.0.4 changes

This revision is primarily a UI scaling, clipping and profile-metadata pass based on the supplied reference screenshots.

Key implementation points:
- `Assets/VRA.png` and `Icon.ico` now use the supplied WT VR Assistant artwork.
- `AviationHomePage.cs` uses a top server row, enlarged play-mode cards/Launch button and bottom-anchored assistants/social controls.
- `IllustratedTheme.cs` contains revised vector icons for Monitor, VR, Live and Test.
- `NeckAssistForm.cs` expands binding/help regions and applies responsive text scaling.
- `AdvancedSwitchBindings.cs` adds a responsive editor layout, wider combo drop-downs and an Examples guide.
- `Modules/VTrim/Integration/VTrim/Form1.cs` reflows Dashboard/Bindings/Precision/Devices and adds persistent aircraft-type profile metadata.
- `MainForm_REV_2_0_4.cs` contains the larger bottom-anchored game update action.
