# WT Assistant 2.3.0 - theme engine, enhanced Home UI and safe-update migration

## Visual themes

- F-16 is now the default visual theme.
- Added MiG-29 theme using the supplied Russian/MiG artwork.
- Theme selection is available under App Options > Visual theme.
- MiG-29 only overrides artwork that changes; unchanged assets inherit from F-16.
- Embedded VTrim follows the selected theme palette.
- Theme selection is stored in normal user settings and survives updates.

## Enhanced Home UI

- Added a WebView2-backed Home presentation layer while keeping all telemetry, profile, VTrim, Neck, KeyBind, update and game-launch logic in C#.
- The layout and positions follow the existing Home design rather than introducing a new launcher layout.
- Added restrained hover elevation, smooth card/image transitions and responsive scaling.
- Existing aircraft-profile and control-profile components remain connected to the same C# data and actions.
- If WebView2 cannot initialize or its renderer fails, the app automatically falls back to the existing WinForms Home screen.
- Compatibility escape hatch: start with `--classic-ui`, or create `Settings\classic_ui.flag`, to force the native Home UI.

## Full assistant CSS presentation

- Extended the WebView2/CSS presentation layer to Neck Assistant, KeyBind Assistant and embedded VTrim.
- Existing WinForms controls remain alive underneath and continue to own settings, events, telemetry and hardware state; web controls mirror changes into the native controls rather than reimplementing business logic.
- Added smooth page scrolling, responsive scale-to-width behavior, consistent CSS switches, sliders, buttons, fields and dropdowns, and F-16/MiG-29 theme-aware styling.
- Dynamic native previews (neck curves, VTrim curves/axes/trim bars) are mirrored into the CSS page.
- Added **PRECISE / NATIVE VIEW** for pages with drag-based native editors, with one-click return to the enhanced surface.
- Larger secondary WinForms dialogs are progressively CSS-mirrored automatically when opened.
- The original native pages remain the fallback if WebView2 initialization or rendering fails.

## Update safety / migration

- The updater now distinguishes user-owned data from app-owned files.
- Preserved during an update: `Settings`, `GraphicSettings`, `ControlSettings`, `Diagnostics`, `CustomThemes` and `installed.flag`.
- Old app-owned DLLs/assets/files are backed up, removed, and replaced by the clean new payload.
- If installation fails, the previous app-owned files are restored; user data is never removed by the updater.
- Retired old UI-layout JSON files are cleaned by an idempotent migration, without touching profiles/bindings/game paths.
- Main settings, hidden keybinds and advanced-switch settings use atomic writes plus `.bak` recovery.
- VTrim profiles/default setup now keep a current backup and recover from it if the primary JSON is damaged.
- VTrim application preferences now use the same atomic-save and `.bak` recovery path.
- Fixed profile restore edge cases: Horizontal Rudder Assist is restored, null/legacy binding collections are sanitized, and legacy null device names no longer abort profile loading.

## Retained from 2.2.x

- Cache-first War Thunder aircraft artwork/database behavior.
- Default vs Custom aircraft controls.
- Aircraft-specific trim bindings and response curves.
- Flight Assistant eligibility rules.
- Profile Copy/Paste compatibility rules.
- Telemetry aircraft auto-selection/auto-creation.
- Neck Assistant Simple Hold fixes.
- Aircraft controls editor repaint/scroll performance fixes.
## Build fix 1

- Isolated `VTrim.csproj` and `VTrim.Embedded.csproj` into separate `obj` / `bin` trees through `Directory.Build.props`.
- `Build.cmd` and `Publish-Clean.cmd` now remove VTrim intermediates before restore and perform an explicit RID-aware restore before publish.
- Fixes `MSB3030` where MSBuild could not find `obj\x86\Release\...\VTrim.Embedded.dll`.


## Build fix 2

- Moved all project `obj` / `bin` intermediates to a short `%TEMP%\WTA230` build root through the repository-level `Directory.Build.props`.
- Each project uses `$(MSBuildProjectName)` below that root, so `VTrim`, `VTrim.Embedded`, and the main app remain isolated without long source-dependent paths.
- Clean restore now passes `Configuration=Release` together with `Platform=x86` and `win-x86`, preventing restore-time evaluation of long Debug publish paths.
- `Build.cmd` and `Publish-Clean.cmd` clean the short temporary build tree before and after a successful build.
- The local-test ZIP now uses a short internal root folder (`WTA230`) to avoid doubling long extraction paths.
- Fixes Windows/MSBuild `Path ... exceeds the OS max path limit` errors during restore.
### Build Fix 3
- Fixed `CS9007` in `CssFormSurface.cs` caused by nested JavaScript closing braces inside a double-dollar interpolated raw C# string.
- The CSS assistant surface now uses triple-dollar raw interpolation, so ordinary JavaScript `}}` sequences are treated as content while C# substitutions use `{{{...}}}`.
- Added a static regression check for this raw-string delimiter rule.
- The existing WebView2 `WindowsBase` version message remains a non-fatal MSBuild warning; it is not the cause of the failed build.

