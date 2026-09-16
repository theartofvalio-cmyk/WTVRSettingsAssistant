# WT Assistant / VR Assistant v2.0.5 - Visual Studio Source

Open `VR_Assistant_v2.0.5.sln` in Visual Studio.

Build configuration: **Release | x86**.

The x86 target is intentional because the integrated VTrim/vJoy path remains x86. Extract the entire archive before opening the solution.

## v2.0.5 UI / usability revision
- Uses the supplied WT VR Assistant artwork as the Home hero image and application icon.
- Reworked Home composition: Live/Test at the top, larger Monitor/VR and Launch controls, and assistant/community controls toward the bottom.
- Rebuilt Monitor, VR, Live and Test vector icons.
- Removed duplicate sidebar `VR ASSISTANT / FOR WAR THUNDER` branding.
- Neck Assistant has additional room for Advanced help text and scales typography with the host window.
- Custom Switch Builder has wider/readable behavior columns plus an Examples guide.
- VTrim Dashboard, Trim Precision, Trim Bindings and Devices layouts were reflowed to prevent clipping.
- VTrim profiles now include an aircraft category: Prop Plane, Jet Plane or Helicopter, with visual icons reserved for future category-specific functionality.
- Update War Thunder dialog now emphasizes the version information and places a larger Update Game button at the bottom.

See `WTAssistant/RELEASE_NOTES_2.0.5.md` and `Docs/REV_2_0_5_CHANGES.md`.
