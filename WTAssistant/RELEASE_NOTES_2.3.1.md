# WT Assistant 2.3.1 - stabilization, functional restoration and theme polish

## Functional stabilization

- Removed the experimental generic `CssFormSurface` mirroring layer from Neck Assistant, KeyBind Assistant, VTrim Assistant, Advanced Switches and secondary dialogs.
- Restored the original native C# controls as the direct authority for hardware capture, sliders, DataGridViews, custom drag editors, save/load events and assistant behavior.
- Advanced Switch ADD/EDIT/save flows remain directly wired to the existing `AdvancedSwitchService` and native dialogs.
- Retained atomic settings writes and `.bak` recovery for main settings, hidden keybinds, Advanced Switches and VTrim persistence.
- Retained VTrim recovery/sanitization fixes, including restoring Horizontal Rudder Assist from saved profiles.

## Home UI

- Home keeps the dedicated WebView2/HTML/CSS dashboard because it has explicit C# action wiring rather than generic control mirroring.
- Added a loading cover so the user does not see native Home first and then a CSS surface.
- CSS Home is only revealed after WebView2 navigation completes; if initialization fails, the native fallback is shown directly.
- Home uses uniform viewport scaling and does not expose browser horizontal/vertical scrollbars.
- Added restrained activation feedback for enabled/toggled Home cards.
- Fixed the F-16 hero artwork to use real PNG transparency rather than an opaque black background.

## Resolution and DPI

- Main app remains Per-Monitor V2 DPI aware.
- Advanced Switch dialogs use DPI autoscaling.
- VTrim page containers explicitly disable horizontal scrolling while retaining vertical scrolling where required.
- Added a responsive-layout simulation covering 1920x1080, 2560x1440 and 4K/high-DPI scenarios.

## Themes and branding

- F-16 remains the default theme.
- MiG-29 remains the alternate theme using the supplied artwork.
- Theme selection persists in normal user settings.
- A theme change restarts the app so the selected theme's runtime window/taskbar/tray icon is loaded consistently.
- Theme asset lookup still falls back to F-16/shared assets when an alternate theme intentionally does not override a file.

## Languages

- Language names remain in their native-language form.
- Added an owner-drawn country flag beside each supported language instead of relying on regional-indicator emoji support.

## Build and clean publish

- Version bumped to 2.3.1.
- Short isolated `%TEMP%\WTA231` build/intermediate paths remain in place to prevent MAX_PATH and VTrim project collisions.
- Main project explicitly disables WPF and removes the unused WebView2 WPF reference before assembly resolution.
- Clean publish removes WebView2 WPF/XML documentation artifacts that are not required by the WinForms application.
- vJoy keeps its required `vJoyInterface.dll` filename under `Drivers\vJoy\` rather than being renamed.

## Safe update behavior

The updater continues to preserve user-owned data:

- `Settings\`
- `GraphicSettings\`
- `ControlSettings\`
- `Diagnostics\`
- `CustomThemes\`
- `installed.flag`

Application-owned files are backed up, removed and replaced with the new payload. A failed update restores the previous application-owned files without deleting user data.

The 2.3.1 migration also removes the retired generic-CSS cache from experimental 2.3.0 builds.

## Release gate

Included validation covers source invariants, update policy and responsive layout scenarios. A real **Release | x86** Windows build plus functional testing with actual input hardware remains required before public release.
