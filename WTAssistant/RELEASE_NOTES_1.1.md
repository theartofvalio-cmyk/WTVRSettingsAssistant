# War Thunder VR Settings Assistant 1.1

Version 1.1 is a major usability and presentation update focused on easier profile switching, clearer setup, safer controls handling, and a more polished interface.

## What changed since 1.0

- Added separate Desktop and VR graphics API selectors.
- Added custom Desktop and VR `.blk` profile capture, replacement, and removal.
- Added War Thunder `launcher.exe` selection and installation-aware game launching through `beac_wt_mlauncher.exe` when available.
- Added optional Desktop/VR controls-profile switching through `machine.blk`.
- Added automatic `machine.blk` detection plus contextual help for all controls files.
- Added safer controls-section replacement with backups.
- Fixed Desktop/VR graphics switching so the selected profile and VR flag are applied correctly.
- Fixed controls switching to target both the active account and `last` `machine.blk` mirrors.
- Added controls reapplication after War Thunder exits, preventing the running game from overwriting a newly selected profile.
- Added Steam and standalone launch detection, forced-start arguments, and explicit VR/Monitor launch flags.
- Fixed profile capture buttons so existing Desktop and VR profiles can be overwritten.
- Added built-in Low, Medium, and High VR presets and recommendation pages.
- Added recommended settings information for Meta Quest, SteamVR, and Virtual Desktop.
- Redesigned and reorganized the Settings and Control Profiles screens for better spacing and readability.
- Added section dividers, larger descriptions, aligned controls, and improved help buttons.
- Redesigned the main screen with updated application branding and icon.
- Added Discord, YouTube subscribe, and project-support buttons with hover descriptions.
- Added an update-status indicator that turns yellow when a newer GitHub release is available.
- Added automatic in-place updates: download, replace application files, preserve user settings and captured profiles, and relaunch automatically.
- Added update rollback protection if file replacement fails.
- Replaced the Play artwork with a larger `LAUNCH GAME` button.
- Updated the About page with app instructions, purpose, version highlights, and disclaimer information.
- Numerous layout, alignment, scaling, wording, and visual-polish improvements.

## Easter eggs ;)

On the main screen, click the large War Thunder logo first, then enter either sequence using the arrow keys:

- `↑ ↑ ↓ ↓ ← → ← →`
- `← ← → → ↑ ↓`

## Updating

Future releases containing a ZIP package can be downloaded and installed directly by the app. The updater preserves the `Settings` and `GraphicSettings` folders and automatically relaunches the updated version.
