# WT Assistant 2.3.2 - Home recovery, Xbox support, profile dashboard unification

## Reliability
- Fixed the blank Home screen failure mode. WebView2 startup now has a timeout, failed navigation/process recovery, and a native-dashboard fallback.
- The first WebView navigation is no longer cancelled by the navigation guard.
- The build continues to run the non-interactive Windows self-test after publish and fails if a regression is detected.

## VTrim / controller input
- Fixed the Auto Store Trim release-capture regression by allowing normal polling gaps before spring-return capture.
- Added first-class XInput support for Xbox-compatible controllers while retaining DirectInput HOTAS/yoke/pedal support.
- Xbox sticks, buttons, triggers and D-pad are exposed to VTrim; buttons/D-pad are also available to Neck Assist, KeyBind Assist and Advanced Switches.
- Duplicate Xbox compatibility devices are filtered when a real XInput device is available.

## Aircraft profiles
- Custom aircraft profiles now edit through the same live Trim Dashboard used by the default setup, rather than a second reduced controls editor.
- Default/Custom mode remains aircraft-specific and the previous custom snapshot is preserved when an aircraft follows Default Controls.
- Aircraft artwork remains visible under light transparent/glass profile controls.

## Display / themes / persistence
- 1080p remains the minimum layout target; VTrim horizontal scrolling stays disabled and the responsive/DPI checks cover 1080p through 4K.
- F-16 remains the default theme and MiG-29 remains the alternate theme, including theme-specific app/window/taskbar branding.
- Existing user settings, profiles, bindings and caches that contain user data are preserved by update migration.
- Language names continue to display with country flags.

## Build
- Version bumped to 2.3.2.
- Short `%TEMP%\WTA232` intermediate paths prevent MAX_PATH and shared VTrim project collisions.
- Visual Studio remains optional; Build.cmd requires only the .NET 10 SDK.
