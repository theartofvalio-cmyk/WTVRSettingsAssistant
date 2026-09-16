# War Thunder VR Settings Assistant 1.4

Version 1.4 expands Neck Rotation Assistance with two easier-to-understand movement modes, broader input support, persistent activation, and clearer controls.

## New in 1.4

- Added **Advanced** and **Simple** Neck Assist modes.
- Supports OpenXR runtimes including **SteamVR OpenXR** and **VDXR**.
- Advanced mode retains the configurable horizontal and vertical rear-view curves.
- Advanced activation can be configured as **Toggle** or **Hold**.
- Simple mode applies a fixed rear-camera rotation only while its assigned input is held.
- Added a Simple-mode head-direction deadzone to prevent accidental activation and select the intended rear-view direction.
- Added an adjustable camera transition-speed slider for smoother activation and return movement.
- Added keyboard and mouse bindings alongside HOTAS single-button and combination bindings.
- Simple mode now relies on War Thunder's in-game recenter and no longer displays a separate recenter binding.
- Neck Assist ON/OFF state is saved and restored after restarting the application.
- Added a dedicated transparent green Neck Assist icon that indicates when assistance is active.
- Added **Restore Defaults** for the recommended Advanced values without removing the user's bindings.
- Improved spacing, text size, instructions, tooltips, and binding-dialog readability.
- Updated the in-app Info page with the v1.4 workflow and feature highlights.

## Included recommended Advanced defaults

- Horizontal: boost starts at 21°, releases at 12°, natural movement resumes at 28°, maximum view 180°, softness 98%.
- Vertical: boost starts at 15°, releases at 9°, natural movement resumes at 20°, maximum view 115°, softness 98%.
- Rear-view Boost and Link Axes are enabled by default.
- Vertical assistance and Neck Assist itself start disabled on a clean installation.
- Input bindings remain unassigned on a clean installation.

## Updating

The automatic updater preserves the existing `Settings` and `GraphicSettings` folders. Release archives intentionally exclude repository documentation files.
