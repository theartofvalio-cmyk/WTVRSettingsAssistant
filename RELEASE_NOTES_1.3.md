# War Thunder VR Settings Assistant 1.3

Version 1.3 introduces configurable neck-rotation assistance while retaining the Desktop/VR graphics and controls-profile workflow.

## New in 1.3

- Added an embedded Neck Rotation Assistance page.
- Added an OpenXR API layer for supported OpenXR runtimes, including SteamVR OpenXR and VDXR.
- Added live horizontal and vertical headset/view graphs.
- Added separate left/right and up/down assistance controls with an optional Link Axes toggle.
- Added draggable graph markers for boost activation, return/release, natural-motion resume, and maximum view.
- Added Rear-view Boost mode: normal 1:1 head movement, a configurable assisted transition, then natural 1:1 movement with the added rear offset.
- Added transition-softness controls and motion stabilization to reduce abrupt movement around thresholds.
- Added optional HOTAS bindings for recenter and Neck Assist activation, supporting single buttons and button combinations.
- Added explicit Assign, Try Again, Clear, and Cancel binding controls.
- Improved backend status so registration is not confused with live headset telemetry.
- Expanded the in-app information page and usage instructions.

## Default Neck Assist profile

- Horizontal: boost starts at 21°, releases at 12°, natural movement resumes at 28°, maximum view 180°, softness 98%.
- Vertical: boost starts at 15°, releases at 9°, natural movement resumes at 20°, maximum view 115°, softness 98%.
- Rear-view Boost and Link Axes are enabled by default; vertical assistance and Neck Assist itself start disabled.
- HOTAS bindings are unassigned on a clean installation.

## Important Neck Assist note

Enable Neck Assist before starting the VR game so OpenXR can load the API layer. After the headset is connected, changes to angles and softness apply live without restarting the game.

Existing Settings and GraphicSettings folders are preserved by the automatic updater.
