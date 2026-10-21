# Validation for War Thunder VR Assistant v2.0.10

On Windows with the .NET 10 SDK, run `Run-Tests.cmd` for the layout audit and managed GameServices, aircraft data, and VTrim tests. Run `dotnet run --project WTAssistant/verification/ThemePreview/ThemePreview.csproj -c Release -p:Platform=x86 -- --startup-smoke` to check Windows startup, WebView2 deferral, repeated tray restore, and saved window size.

Run `Build.cmd` for the self-contained x86 publish and packaged executable self-test. The release ZIP must contain the executable, native DLLs, Drivers, and Licenses at its root, with no personal Settings folder. Confirm that the executable file and product versions are both 2.0.10 and that its `--self-test` report passes.

The automated tests simulate input and game state. Physical HOTAS, vJoy, VR runtime, and War Thunder behavior still require a real-device acceptance test.

The controls regression suite is `Tests/ControlsInput.Tests`. It checks each of the 128 input bits, legacy/extended gesture persistence, repeat and fixed-count timing, cancellation, device identity persistence, and the sequence editor in every supported language. It also measures the production Windows polling timer with mocked output. The optional `--native-timing` switch sends keys only while its own test window has foreground focus and aborts if it cannot acquire or retain that focus.

Local v2.0.10 verification (21 October 2026): 225 controls checks, 412 layout checks, 41 game-service checks, the aircraft-data checks and 62 VTrim core checks passed. The 100 ms scheduling test averaged 100.1 ms using the production 25 ms Windows UI timer with mocked output. The self-contained x86 package passed its self-test with zero failures and zero warnings. The optional native input test could not acquire foreground focus in this automated session and aborted; actual key delivery, PTO2 hardware and in-game countermeasure rates remain acceptance checks.
