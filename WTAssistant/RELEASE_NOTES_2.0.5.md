# WT Assistant 2.0.5

Responsive 16:9 / clipping QA revision.

## Main window / Home
- Main window now keeps a 16:9 client composition while manually resizing and maximizes into the largest centered 16:9 rectangle that fits the Windows working area.
- Home now scales as one design surface: logo, Live/Test cards, Monitor/VR cards, assistant cards, Launch Game and community buttons grow or shrink together.
- Home follows the supplied reference layout: Live/Test at the top, large Monitor/VR cards, three assistants below, large Launch Game below the assistants and social buttons at the bottom.
- Navigation and footer scale with the host window. Owner-drawn navigation, server, launch, social and footer text now shrink-to-fit rather than being replaced with ellipses.
- Duplicate `VR ASSISTANT / FOR WAR THUNDER` sidebar branding remains removed.

## VTrim
- Dashboard, Devices & Output, Axis Curves and Profiles use responsive scroll/design surfaces so scaled child controls are not clipped by an unscaled page.
- Absolute table metrics, margins, padding, minimum sizes, fonts and custom controls scale from the current host size.
- All normal fixed-size labels/buttons participate in a post-layout text-fit pass; dynamic status text is rechecked when its text changes.
- Embedded VTrim no longer re-enables `AutoEllipsis`; critical labels wrap or shrink-to-fit instead of showing `...`.
- Responsive font objects are owned/disposed safely by the scaler rather than disposing ambient/inherited fonts.
- Trim bars and repeat-speed sliders reserve enough edge padding for their marker/thumb, preventing half-clipped endpoints.
- Rudder Assist, Trim Precision, Trim Bindings, Physical Input Devices, Axis Routing, Game Setup, Profiles and Curves use proportional/flexible allocations where narrow fixed columns previously caused cropping.
- Trim Bindings has clearer vertical grouping and full Rudder Left/Right controls.
- Profiles support `Prop Plane`, `Jet Plane` and `Helicopter` categories with aircraft icons, reserved for later category-specific functionality.

## KeyBind / Neck / dialogs
- Neck Assistant typography scales with the embedded host and Advanced help text has additional room.
- Custom Switch Builder initial button bounds stay inside the window, behavior text is not ellipsized, and the EXAMPLES guide explains common state/trigger/output scenarios.
- App Options uses a flexible layout with a dedicated bottom action row so SAVE OPTIONS and CANCEL remain inside the dialog at supported sizes.
- Update War Thunder uses larger version text and a taller bottom-aligned UPDATE GAME action.

## QA
- `Tests/layout_audit.py` validates responsive geometry over multiple Home, App Options and VTrim viewport/scale combinations.
- `Tests/verify_package.py` validates revision/project wiring, assets, responsive source markers, absence of deliberate ellipsis in critical UI, and performs a lexical/delimiter scan of every C# source file including the multiline-string regression that caused the previous `CS1010` build error.
- Final Windows compilation and runtime rendering still require Visual Studio/.NET Windows Desktop on Windows; those tools are not available in the Linux packaging environment.
