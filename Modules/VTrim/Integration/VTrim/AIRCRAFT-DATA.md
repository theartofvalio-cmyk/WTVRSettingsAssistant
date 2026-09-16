# Aircraft Data and User Profiles

The embedded app uses its existing portable `Settings/VTrim` root.

- `Aircraft/aircraft-index.json`: normalized provider metadata, never user settings.
- `Aircraft/aircraft-index.meta.json`: schema, provider, successful refresh time and content hash.
- `Aircraft/Icons`, `Aircraft/Backgrounds`, `Aircraft/Frames`: validated, on-demand PNG cache.
- `Profiles/*.json`: existing authoritative user profiles, extended with `Id` and optional `AircraftId`.
- `Profiles/_active-profile.txt`: existing active selection, unchanged; no second pointer.
- `VTrim.default-setup.json`: existing profileless setup, unchanged.

Source verified September 15, 2026: https://wiki.warthunder.com/aviation and
https://wiki.warthunder.com/helicopters expose `window.WT_UnitList`, an embedded JSON
roster. The provider parses that data, not the visible UI. Wiki common.js converts
BR indices with `(index / 3 + 1).toFixed(1)`. Mode-specific BR values are retained.
Classification uses the provided role; a Fighter is not guessed to be a jet.
Only icon URLs present in the roster are accepted. No background/frame source is
currently exposed by this adapter, so those remain null.

The source currently returns no ETag or Last-Modified, and declares no-cache.
Two roster requests run in the background at startup. The normalized content hash
avoids rewriting unchanged indexes; no individual vehicle pages are crawled.
Search uses only the in-memory local index. Failure keeps the last valid cache;
empty or drastically truncated updates are rejected. Temporary files are parsed
before atomic replacement. Asset downloads are bounded and decoded before storage.

New profiles can be created without an aircraft while offline. Search selection
stores only the source ID, never typed text or absolute image paths. Editing an
association retains all controls. Existing profiles without an aircraft remain
usable. Aircraft roles do not silently change the user's existing VTrim flight type.

Run `dotnet run --project Tests/AircraftData.Tests -- --live` from the repository
root for parser, normalized search, offline/cache preservation and live-source checks.
