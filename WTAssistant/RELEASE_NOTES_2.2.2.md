# WT VR Assistant 2.2.2 - Embedded Aircraft Controls + cleaner build output

## Aircraft Controls
- Double-clicking an aircraft profile now opens its aircraft-specific editor directly inside the VTrim **Profiles** page.
- The separate Aircraft Controls top-level window has been removed.
- The editor uses the cached War Thunder Wiki nation background and aircraft artwork in a wide hero header.
- Default / Custom Controls, Trim & Keybinds, Axis Curves, Flight Assistant, and compatible Copy/Paste remain available in the embedded editor.
- **Back to Profiles** returns to the aircraft-card browser without leaving VTrim.
- **Open Default Controls** still jumps to the shared Trim Dashboard / Default Profile controls.

## Build/output cleanup
- Normal Visual Studio / `dotnet build` is now framework-dependent, removing the large set of copied .NET runtime DLLs from the developer output folder.
- Added `Publish-Clean.cmd` for a self-contained, compressed single-file release build.
- Clean publish keeps only the main executable plus the native/support files that must remain external (`vJoyInterface.dll`, `Drivers`, and `NeckAssist`).

## Preserved from 2.2.1
- Neck Assistant hold/reacquire fix and smoother head movement.
- Aircraft Default/Custom Controls profiles, telemetry auto-selection/creation, Flight Assistant eligibility logic, and class-safe Copy/Paste.
