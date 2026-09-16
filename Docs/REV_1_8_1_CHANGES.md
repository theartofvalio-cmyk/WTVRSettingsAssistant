# WT Assistant 1.8.1 source revision

This revision corrects the home-screen integration from 1.8.0. The 1.8.0 source contained the new services but the high-DPI layout could hide the entire VR Assistant hero artwork and stripped several visible pieces from the intended dashboard.

## Home/dashboard fixes

- Restored the large transparent War Thunder VR Assistant emblem on the Home page at normal desktop sizes, including common Windows 125% DPI.
- Reworked the responsive breakpoint so DPI is not effectively counted twice when deciding whether the hero column can be shown.
- Restored the `FOR WAR THUNDER` brand subtitle.
- Rebuilt Live/Test status as two separate status cards with their own icons, red/green/amber state lamp, exact last-check time and manual refresh control (F5 also refreshes).
- Kept the status wording accurate: the Live check verifies reachable Gaijin services; it does not pretend a public matchmaking-health API exists. Test status follows the latest supported official staff opening window.
- Reduced the oversized Launch Game panel that appeared on tall windows.
- Restored boxed Discord, YouTube and support controls with matching illustrated icons.
- Restored the Home-page slogan beneath the emblem.
- Added footer readiness state (`War Thunder detected` / `Select War Thunder folder`) and the `Making War Thunder Better in VR` footer text.
- Adjusted navigation width/font scaling so `KeyBind Assistant` stays on one line at common DPI settings.

## Existing 1.8 functionality retained

- Embedded VTrim page and Home toggle.
- Separate Live/Test War Thunder installation paths.
- Game version checks and handoff to Gaijin's official launcher/updater.
- Neck Assistant camera-transition-speed layout fix.
- Free window resize (no forced aspect ratio).
- Alpha/transparency revision for the Easter egg overlay.
- Cleaned current asset set.

## Build validation limitation

The source/package reference audit can be run on any machine with Python. A real WinForms/.NET build and runtime validation still needs Windows with the .NET 10 SDK and Visual Studio desktop workload.
