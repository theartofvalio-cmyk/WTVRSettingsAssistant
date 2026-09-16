# REV 2.0.5 changes

This revision is a focused responsive-layout, 16:9 and crop/overlap hardening pass based on the supplied Windows screenshots.

## Main window
- Default client design is 1600x900.
- Manual resize is locked to 16:9.
- Maximize uses the largest centered 16:9 rectangle inside the current monitor working area.
- Navigation/footer sizes and fonts scale with the window.
- Home uses one uniform design-space scale so artwork, cards, icons and text change size together.
- Home composition is server cards -> Monitor/VR -> assistant cards -> Launch -> community links.
- Owner-drawn Home/navigation/footer text now shrinks to fit instead of using `EndEllipsis`.

## VTrim
- Responsive scroll surfaces added to Dashboard, Devices & Output, Profiles and Axis Curves.
- Scaled absolute rows/columns, margins, padding, minimum sizes and free-positioned bounds.
- Post-layout text fitting covers every fixed-size label and button and reruns when dynamic text changes.
- Removed deliberate ellipsis behavior from embedded VTrim labels.
- Responsive font ownership is tracked so scaler-created fonts are the only fonts disposed/replaced by the scaler.
- TrimBar and RepeatSpeedSlider dynamically reserve marker/thumb radius at both track edges.
- Rebalanced Rudder Assist, Trim Precision, bindings, input-device, axis-routing, setup, profile and curve allocations.
- Prop Plane / Jet Plane / Helicopter profile categories with icons retained.

## KeyBind / Neck / dialogs
- Custom Switch Builder right-side LEARN/SAVE/CANCEL controls are inside the initial 1220px client area before responsive layout runs.
- KeyBind/Custom Switch controls no longer use `AutoEllipsis=true`.
- Neck Assistant host-relative typography and Advanced help spacing retained.
- App Options action row and bottom padding prevent SAVE/CANCEL from touching or crossing the dialog edge.
- Update War Thunder retains enlarged status text and tall bottom update button.

## Regression protection
- Static package audit scans all C# source files for malformed normal string literals, including source newlines inside ordinary strings.
- Geometry audit covers common 16:9 working areas, Home card bands, assistant icon/toggle separation, App Options action buttons, VTrim surface heights, horizontal control minimums and custom slider marker containment.
