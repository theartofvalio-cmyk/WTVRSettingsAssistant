# War Thunder VR Assistant v2.0.13

This release fixes VTrim flight-axis stutter left unresolved in v2.0.12.

- HOTAS roll, pitch, and rudder are sampled and sent to vJoy on a dedicated 8 ms worker. Window painting, profile work, and other UI activity no longer gate flight-axis output.
- Live stick and rudder markers update on each UI poll again. The 20 Hz preview limit introduced in v2.0.12 made movement visibly stepped.
- The flight worker preserves custom response curves, independent trim, Store Trim, XInput routes, and safe zero output when an axis becomes unavailable.

**Cause:** VTrim previously read the physical flight axes and wrote vJoy output from a 15 ms WinForms timer on the shared UI thread. Any slow control repaint or other work on that thread postponed the next axis sample. v2.0.12 reduced some UI work, but retained this architecture and additionally limited the preview to 20 Hz.

The updater preserves local settings and profiles. For a manual install, extract the entire ZIP and keep the executable, DLLs, Drivers, and Licenses together. The ZIP contains no personal settings or profiles.

Automated checks include a simulated 180 ms UI stall while the flight worker continues to send axis samples. A physical HOTAS and in-game acceptance check remain necessary to confirm feel on each user's setup.
