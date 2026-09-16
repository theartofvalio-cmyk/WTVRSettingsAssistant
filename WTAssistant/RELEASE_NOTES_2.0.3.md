# VR Assistant 2.0.3

- Live/Test launching now requires an exact installed-version match for the selected branch. A mismatched build automatically opens the official launcher, waits for the selected branch version, closes the launcher after a stable exact match, then launches War Thunder.
- Home layout now uses a uniform design-space scale so the logo, server cards, play-mode cards, assistant icons, launch button and social buttons grow together when the window is enlarged. The main logo is slightly smaller for better breathing room.
- Neck Assistant and KeyBind Assistant Home icons use larger draw boxes to match the VTrim icon visually.
- VTrim embedded UI now scales fonts, fixed table metrics, padding and loose controls together. Trim Precision, Horizontal Assist, Trim Bindings and Devices & Output layouts were enlarged for readability.
- VTrim Profiles management area is compact instead of reserving a large unused upper region.
- App Options has a larger DPI-safe client area and a protected bottom action row so Save/Cancel are not clipped.
- Advanced Switch Bindings includes clearer instructions and human-readable trigger/output behavior choices.
- Advanced switch vJoy outputs are constrained to buttons 1-32 for the current vJoy.Wrapper backend. Legacy >32 mappings are migrated to valid buttons while preserving shared-command groups.
