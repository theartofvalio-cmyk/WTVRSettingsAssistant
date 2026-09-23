# WT Assistant 2.2.5 - cache-first aircraft art and clean build layout

- Restored the stable local Wiki aircraft-icon workflow used before 2.2.4.
- Aircraft profile cards load `Settings/Aircraft/Icons/*.png` immediately when cached.
- Wiki icon sync is incremental: only new/missing/corrupt assets or aircraft whose source artwork URL changed are downloaded.
- Existing 2.2.x icon caches are reused; `.asset.json` sidecars are added without forcing a redownload.
- Profile cards use the roster/slot aircraft art again instead of the 2.2.4 generic/best-artwork pipeline.
- Fixed the 2.2.4 blank aircraft-controls page by replacing the problematic FlowLayoutPanel with a stable DPI-scaled scroll stack.
- Aircraft editor still keeps Trim & Keybinds, all Axis Curves and Flight Assistant on one scrollable page.
- Clean release output now targets `Builds/WTAssistant/` with `Drivers`, `NeckAssist`, `Settings`, and optional `System` folders instead of a crowded EXE directory.
- `Build.cmd` deletes stale `bin/obj` output before publishing so an older self-contained runtime folder cannot be confused with the release build.
