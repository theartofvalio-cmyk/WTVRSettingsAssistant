# Validation for WT VR Assistant v2.0.6

On Windows with the .NET 10 SDK, run `Run-Tests.cmd` for the layout audit and managed GameServices, aircraft data, and VTrim tests. Run `Build.cmd` for the self-contained x86 publish and its packaged executable self-test.

For a release payload, run `python Tests/verify_release_2_0_6.py <release-folder>` to check the executable version and bundled vJoy files. Confirm the ZIP contains the executable, native DLLs, Drivers, and Licenses at its root, with no user Settings folder.

The automated tests simulate input and game state. Physical HOTAS, vJoy, VR runtime, and War Thunder behavior still require a real-device acceptance test.
