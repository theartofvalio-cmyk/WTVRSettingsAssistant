# War Thunder VR Assistant v2.0.12

This update addresses HOTAS lag and stutter in VTrim.

- VTrim sends each flight-axis sample to vJoy before refreshing dashboard controls and graphs. Its visual previews now refresh at about 20 Hz while flight output keeps the 15 ms polling cadence.
- Advanced Switch bindings that share a controller use one DirectInput sample per 25 ms cycle instead of polling the same HOTAS repeatedly.
- Neck Assistant now copies motion settings only when they change. Its 16 ms motion activity update remains in place, while telemetry graphs refresh less often and only while visible.

The VTrim axis loop, KeyBind Assistant, and Neck Assistant share the Windows UI thread. Repeated controller polling, settings serialization, and visual invalidation on that thread could delay VTrim's next input sample. These changes remove that unnecessary work and prioritize vJoy output.

The updater preserves local settings and profiles. For a manual install, extract the entire ZIP and keep the executable, DLLs, Drivers and Licenses together. The ZIP contains no personal settings or profiles.

Automated tests cover the packaged executable, shared-device controller reads, 128-button input behavior, Neck Assistant motion, and VTrim control logic. Physical HOTAS and in-game feel still require testing on a user's hardware.
