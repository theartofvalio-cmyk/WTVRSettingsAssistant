# WT Assistant v2.2.5

## Aircraft Controls editor
- Replaced the aircraft-specific Trim / Axis Curves / Flight Assistant tabs with one vertically scrollable aircraft-control page.
- The full aircraft editor now uses the War Thunder Wiki nation background and aircraft artwork behind the controls.
- The background is dimmed for readability and the aircraft artwork uses contained scaling so different aircraft source images do not crop inconsistently.
- Default/Custom Controls, copy/paste class restrictions, telemetry profile switching/creation and Flight Assistant eligibility rules remain intact.

## Portable release layout
- Clean publish root is now intentionally minimal.
- vJoy native/installer files are grouped under `Components/Drivers/vJoy/`.
- XR Neck Safer runtime files are grouped under `Components/NeckAssist/OpenXR/`.
- Added a native-library resolver so VTrim can load `vJoyInterface.dll` from its organized Components folder instead of requiring the DLL beside the main EXE.
- `Publish-Clean.cmd` remains the recommended distributable build; Visual Studio `bin` is a developer build directory and is not the release layout.

## Neck Assistant
- Retains v2.2.1 Simple Hold debounce/reacquire protection and smoother rate-limited rear-view transitions.
