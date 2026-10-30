# War Thunder VR Assistant v2.0.14

This release fixes the VTrim stick freezes and delayed jumps reported in v2.0.13. The user tested the local build with their HOTAS and confirmed the movement is smooth.

- VTrim samples each selected HOTAS flight device once per cycle and uses that same sample for vJoy output and the live stick preview. Other controllers are read separately, so they cannot hold up flight-axis output.
- Device discovery releases its temporary DirectInput handle before VTrim starts the flight reader. This removes competing reads of the selected controller.
- Advanced Switch button reads and hidden UI status updates no longer block VTrim's UI thread.
- Switching the app's visual theme applies immediately without restarting. Home artwork and the VTrim visuals update in the open window.
- Home WebView2 startup and restore are more resilient to a temporary Windows window-state error.

**Cause:** VTrim and other UI paths had separate DirectInput readers for the same HOTAS. A transient failed read could reuse the previous position for up to 100 ms, making the stick appear to freeze and then jump. The v2.0.13 dedicated flight worker removed UI scheduling delay but did not remove this input contention.

The updater preserves existing settings and profiles. For a manual install, extract the entire ZIP and keep the executable, DLLs, Drivers, and Licenses together. The ZIP contains no personal settings, profiles, or local diagnostic marker.
