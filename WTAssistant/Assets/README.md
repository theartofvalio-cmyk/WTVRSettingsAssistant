# Theme-ready asset layout

WT VR Assistant 2.3 resolves artwork by logical filename so the interface can switch themes without changing every form.

Resolution order:

1. `Assets/Themes/<ActiveTheme>/...`
2. `Assets/Themes/F16/...` (default/fallback theme)
3. `Assets/Shared/...`
4. legacy/module fallback for compatibility with older source packages

## Themes/F16

The F-16 artwork is the default theme. It contains the complete visual set: branding, Monitor/VR/VTrim home artwork, Neck/KeyBind artwork, launch states, footer artwork and decorative assets.

## Themes/MiG29

The MiG-29 theme contains only the artwork that intentionally differs from F-16. Missing files automatically inherit from `Themes/F16`, so a theme does not need to duplicate unchanged artwork.

Current MiG-29 overrides:

- `Branding/VRA.png`
- `Branding/WTAssistantBrand.png`
- `Assistants/IllustratedHead.png`
- `Assistants/IllustratedKeys.png`
- `Home/Home_VTrim_New.png`

## Shared

`Shared` is for files that must not change with a theme: setup-guide screenshots, audio, rewards/easter-egg assets and app-level icons.

When adding another theme, reuse the same logical filenames. Do not rename an existing logical asset just to create a new skin.
