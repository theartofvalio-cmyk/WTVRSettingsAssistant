# VR Assistant v2.0

## Home and navigation
- Home assistant cards are icon-only with ON/OFF controls; hover tooltips identify Neck Assistant, KeyBind Assistant and VTrim Assistant.
- Enlarged Neck Assistant artwork on the Home page.
- Sidebar labels are larger and bold for readability.
- Footer branding is simplified to `v 2.0`.
- Live/Test server selection no longer presents server-availability status; it is only a circuit selector.

## Neck Assistant
- Every application launch starts Neck Assistant in Simple mode.
- Legacy Home/Info overlay controls are disabled in the illustrated UI.
- Camera Transition now sits directly below the Simple Hold binding instead of at the bottom of the scrolling page.
- Advanced mode remains user-selectable.

## KeyBind Assistant: Advanced Switch Bindings
- Adds DCS-style maintained physical switch mapping without game injection or memory access.
- Supports 2-state, 3-state and custom multi-state definitions made from multiple ON/OFF button conditions.
- Per-state Enter, Leave, While Active and Enter+Leave triggers.
- Pulse, Hold, same-command pulse/toggle and None output behavior using the existing vJoy output path.
- Learn Switch, Learn Position, live raw input display, switch test mode, state reordering, reverse positions for 2-state switches, persistence, debounce and startup behavior.
- Held outputs are released when the feature/mapping is disabled, a controller disconnects, the service resets, or the app closes.

## VTrim
- Keeps the existing vJoy-based implementation; no custom kernel driver/WDK code is included.
- Embedded VTrim text is larger and panels gain aviation-style angular framing to better match the rest of VR Assistant.
