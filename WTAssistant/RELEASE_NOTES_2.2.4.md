# WT Assistant v2.2.5

## Aircraft editor layout
- Rebuilt the single-page aircraft editor as a true vertically scrolling flow.
- Added DPI-scaled minimum heights for Trim & Keybinds, Axis Curves and Flight Assistant.
- Increased curve graph/field spacing and Flight Assistant text space to prevent overlap or clipping.
- Kept aircraft/country artwork as a dimmed full-editor background with slightly more visible artwork behind the controls.

## Aircraft artwork
- Removed full-roster artwork preloading; visible aircraft now load first.
- Added parallel lazy loading for visible cards.
- Added slot image -> large Wiki unit image -> official detail-page image -> generated class silhouette fallback.
- Aircraft cards no longer remain blank when a specific Wiki slot image is missing.

## Cleaner build output
- Normal Visual Studio builds are framework-dependent and no longer force a RID/self-contained .NET runtime copy into the developer output folder.
- Clean release publishing remains self-contained and single-file.
- `Build.cmd` / `Publish-Clean.cmd` create `Publish\WTAssistant-v2.2.5` with the application EXE and organized `Components` folder.
- Publish output limits satellite runtime resources to English; WT Assistant's own UI localization remains internal to the app.

All v2.2.1 Neck Assistant hold/smoothing fixes and v2.2.x aircraft-profile features are preserved.
