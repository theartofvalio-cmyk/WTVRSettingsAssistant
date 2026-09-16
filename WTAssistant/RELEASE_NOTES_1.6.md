# War Thunder VR Settings Assistant 1.6

Version 1.6 introduces the complete Ivory & Gold interface, adds KeyBind Assistant and Live/Test Server selection, completes the Neck Assistant interface, improves background operation, and makes updating safer for existing installations.

## New in 1.6

- Rebuilt the complete application interface around the approved charcoal, ivory, gold, and illustrated F-16 design.
- Added the supplied transparent `VRA.png` artwork with alpha-shaped UI overlap.
- Added responsive Home, Profiles, Neck Assistant, KeyBind Assistant, Settings, and Info layouts with larger type and safe scrolling at small window sizes.
- Added a persistent **Live Server / Test Server** selector. Live writes `curCircuit:t="production"`; Test writes the required `dev`, expert-mode, and web-status values.
- Server switching replaces only the balanced `yunetwork` block and creates a first-change backup beside `config.blk`.
- Added **KeyBind Assistant** for useful War Thunder commands that are not exposed in the normal Controls menu.
- Included mappings for **VR Head Position Up** (`Page Up`), **VR Head Position Down** (`Page Down`), and **Switch map to battlefield** (`N`).
- KeyBind Assistant accepts keyboard keys, mouse buttons, HOTAS buttons, and multi-input combinations.
- Added an independent KeyBind Assistant ON/OFF switch. It defaults to off and remembers the user's choice.
- Corrected 64-bit Windows input handling and added scan-code injection for better in-game command recognition.
- Renamed Neck Assist to **Neck Assistant** throughout the interface.
- Refined Advanced and Simple layouts so controls, graphs, binding rows, and help text remain visible at supported window sizes.
- Added app options to receive beta builds, minimize to the system tray, and start with Windows minimized.
- Added single-instance protection: opening the app again restores the existing window instead of launching another copy.

## Update and data safety

- Automatic updates preserve `Settings`, `GraphicSettings`, and `ControlSettings`.
- Release packages do not contain personal settings, saved profiles, or input bindings.
- The version 1.6 interface leaves existing preference files untouched.
- Existing Neck Assistant and KeyBind Assistant states persist after exiting and reopening the application.

## Runtime support

- Neck Assistant supports OpenXR runtimes including **SteamVR OpenXR** and **VDXR**.
- Enable Neck Assistant before starting the VR game so its OpenXR API layer can load.
- Keep the app running while using KeyBind Assistant mappings.
- If War Thunder runs as administrator, run the assistant as administrator as well.
