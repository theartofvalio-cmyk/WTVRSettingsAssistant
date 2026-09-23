# WT Assistant 2.2.6 - aircraft controls cleanup and scroll performance

- Removed the user-facing Jet damping / SAS capability selector. Flight Assistant availability is now controlled internally by WT Assistant.
- Flight Assistant is currently shown/allowed only for custom propeller-aircraft profiles; jet and helicopter aircraft editors do not show the Flight Assistant section or Flight Assistant binding.
- Existing saved Flight Assistant state is automatically ignored for unsupported aircraft.
- Reduced aircraft-editor scroll flicker by stopping full mode refreshes on the 200 ms binding timer, only updating changed binding text, caching the expensive aircraft backdrop render, and making editor section surfaces opaque/double-buffered.
- Moved DEFAULT / CUSTOM / BASE profile badges to the top-left corner of aircraft cards.
- Retains the cache-first War Thunder Wiki aircraft icon logic and the 2.2.5 clean Build.cmd output layout.
