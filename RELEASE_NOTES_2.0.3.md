# War Thunder VR Assistant v2.0.3

This patch repairs existing aircraft profiles whose saved names or IDs did not match the War Thunder Wiki catalog format, and fixes the Home footer layout.

## Patch notes

- Existing profiles are checked against the aircraft catalog when it loads. A unique match restores the catalog ID used for the aircraft's proper name, data, and picture. Matching ignores letter case and tolerates underscore/hyphen differences such as `f_84f` and `f-84f`.
- Missing or damaged aircraft pictures in each user's local cache are fetched again automatically when the catalog and network are available. Saved profile names, controls, modes, and detection keys are preserved.
- The Home footer's Profiles search and favorite filter now sits beside the app version and leaves space for the game status text.
- War Thunder's `dummy_plane` telemetry placeholder no longer creates a new aircraft profile. Existing `dummy_plane` profiles remain untouched because there is no matching aircraft artwork to assign safely.

Download `WTVRSettingsAssistant-v2.0.3.zip` for a manual installation, or use **Update App** from an earlier version. If War Thunder is running, installation waits until the game closes.
