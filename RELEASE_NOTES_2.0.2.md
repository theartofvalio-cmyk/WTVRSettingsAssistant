# War Thunder VR Assistant 2.2 — app version 2.0.2

This patch repairs automatic updates for users on app version 2.0.1. The previous GitHub release used tag `v2.1` for an executable whose actual version was `2.0.1`. The updater downloaded the ZIP but correctly stopped installation because those versions did not match.

## Patch notes

- Release tag and executable version now both use `v2.0.2` / `2.0.2`, allowing existing installations to update automatically.
- The updater reads the app version from a versioned release ZIP name. It can handle future app releases such as `v2.0.3` and checks that the executable inside the ZIP has the same version before installing.
- Existing settings, graphics profiles, control profiles, and aircraft profiles are preserved during the update.

Download `WTVRSettingsAssistant-v2.0.2.zip`, extract it to a writable folder, and run `WTVRSettingsAssistant.exe` if you are installing manually. Existing 2.0.1 users can use **Update App** or wait for the automatic check. If War Thunder is running, installation waits until the game closes.
