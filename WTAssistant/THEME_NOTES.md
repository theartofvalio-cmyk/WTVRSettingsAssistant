# Illustrated aviation theme

This is the version 1.6 interface and replaces the retired Classic screen layout. Existing preference files are left untouched so updating cannot disturb user data.

## Enable

No activation is required. Version 1.6 opens directly in Ivory & Gold. The app displays only its name and version, without edition branding.

## Included

- Approved illustrated F-16 / VR Assistant emblem, embedded in the executable.
- Charcoal, ivory and muted-gold palette across the home, profile/settings, information and assistant screens.
- Resolution-independent drawn button frames and navigation icons; gold indicates selected navigation or enabled features.
- Persistent sidebar and Home feature cards, using existing navigation/actions and saved feature states.
- Live/Test Server selection that safely replaces only the `yunetwork` block in `config.blk`, creates a first-change backup, and persists the selection.
- Styled input buttons, checkboxes, mode selectors and slider/graph colors.
- Reflowed, scrollable Neck Assistant controls and wrapped KeyBind Assistant help for narrower content areas.
- Native Windows title-bar colors where supported. Windows-owned file pickers and third-party instructional screenshots retain their own appearance.

The theme does not change movement curves, defaults, bindings, or update channels. The only new game-file behavior is the explicit Live/Test Server selector. The mockup's decorative details are implemented as real controls; this is not a screenshot stretched over the application.

## Settings safety

Appearance is stored separately in `Settings/appearance.json` using an atomic replacement. No existing profile or binding file is rewritten by the theme selector. Build payloads exclude `Settings`, `GraphicSettings`, and `ControlSettings`; never copy old development settings over an installed app.

## Verification

`dotnet run --project verification/BezierChecks.csproj -c Release`

`dotnet run --project verification/ThemePreview/ThemePreview.csproj -c Release -- artifacts/theme-preview`

`dotnet run --project verification/ThemePreview/ThemePreview.csproj -c Release -- artifacts/classic-preview classic`

The rendering harness runs with its own settings directory and removes Shown handlers before opening off-screen forms, avoiding OpenXR registration and input polling. It is a layout check, not an in-game VR test.

## Artwork

`Assets/VRA.png` is the approved transparent 2D F-16 logo supplied for version 1.6. It is embedded unchanged and displayed through an alpha-shaped control region so transparent pixels never cover interactive UI beneath it. Other theme graphics are drawn in `IllustratedTheme.cs` so labels and controls remain scalable and interactive.
