# WT VR Assistant 2.2.1 - Neck Assistant hold and smooth-motion fix

## Neck Assistant
- Fixed Simple Hold dropping out while a physical HOTAS button is still held.
- DirectInput now preserves the last recent successful physical-device state across transient poll/reacquire failures, while a genuine successful release still clears immediately.
- Simple Hold requires a short continuous release before it disengages, preventing one-frame input misses from cancelling rear view.
- Increased the neck motion command heartbeat tolerance so a short UI stall no longer looks like a released Hold input.
- Added an angular-rate ceiling to the critically damped camera transition. Large 150-180 degree Simple-mode rotations now move in smaller per-frame increments instead of feeling like 5-10 degree steps.
- Existing Transition Speed remains functional; it now controls a smoother bounded transition.

All v2.2.0 custom-aircraft-control/profile changes are retained.
