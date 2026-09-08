# War Thunder VR Settings Assistant 1.5

Version 1.5 adds KeyBind Assistant, completes the Neck Assistant interface, improves background operation, and makes updating safer for existing installations.

## New in 1.5

- Added **KeyBind Assistant** for useful War Thunder commands that are not exposed in the normal Controls menu.
- Included mappings for **VR Head Position Up** (`Page Up`), **VR Head Position Down** (`Page Down`), and **Switch map to battlefield** (`N`).
- KeyBind Assistant accepts keyboard keys, mouse buttons, HOTAS buttons, and multi-input combinations.
- Added an independent KeyBind Assistant ON/OFF switch. It defaults to off and remembers the user's choice.
- Added dedicated blue inactive and green active KeyBind Assistant icons.
- Prevented simulated KeyBind Assistant commands from being captured as part of Neck Assistant bindings.
- Corrected the 64-bit Windows input packet and added scan-code injection for better in-game command recognition.
- Added a clear status message when Windows blocks input because the app and War Thunder run at different administrator levels.
- Renamed Neck Assist to **Neck Assistant** throughout the interface.
- Refined Advanced and Simple layouts so controls, graphs, binding rows, and help text remain visible at supported window sizes.
- Added larger, clearer binding dialogs with explicit Assign, Try Again, Clear, and Cancel actions.
- Added app options to receive beta builds, minimize to the system tray, and start with Windows minimized.
- Added single-instance protection: opening the app again restores the existing window instead of launching another copy.
- Automatic updates can follow either the stable channel or stable plus beta/pre-release builds.

## Update and data safety

- Automatic updates preserve `Settings`, `GraphicSettings`, and `ControlSettings`.
- Release packages do not contain personal settings, saved profiles, or input bindings.
- Existing Neck Assistant and KeyBind Assistant states persist after exiting and reopening the application.

## Runtime support

- Neck Assistant supports OpenXR runtimes including **SteamVR OpenXR** and **VDXR**.
- Enable Neck Assistant before starting the VR game so its OpenXR API layer can load.
- Keep the app running while using KeyBind Assistant mappings.
- If War Thunder runs as administrator, run the assistant as administrator as well.
