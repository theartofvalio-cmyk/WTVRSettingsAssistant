# Validation for War Thunder VR Assistant v2.0.14

On Windows with the .NET 10 SDK, run `Run-Tests.cmd` for the layout audit and managed GameServices, aircraft data, and VTrim tests. Run `dotnet run --project WTAssistant/verification/ThemePreview/ThemePreview.csproj -c Release -p:Platform=x86 -- --startup-smoke` to check Windows startup, WebView2 deferral, repeated tray restore, and saved window size.

Run `Build.cmd` for the self-contained x86 publish and packaged executable self-test. The release ZIP must contain the executable, native DLLs, Drivers, and Licenses at its root, with no personal Settings folder. Confirm file version 2.0.14.0, product version 2.0.14, and a passing `--self-test` report.

The automated tests simulate input and game state. Physical HOTAS, vJoy, VR runtime, and War Thunder behavior still require a real-device acceptance test.

The controls regression suite is `Tests/ControlsInput.Tests`. It checks each of the 128 input bits, legacy/extended gesture persistence, repeat and fixed-count timing, cancellation, device identity persistence, and the sequence editor in every supported language. It also measures the production Windows polling timer with mocked output. The optional `--native-timing` switch sends keys only while its own test window has foreground focus and aborts if it cannot acquire or retain that focus.

Local v2.0.10 verification (21 October 2026): 225 controls checks, 412 layout checks, 41 game-service checks, the aircraft-data checks and 62 VTrim core checks passed. The 100 ms scheduling test averaged 100.1 ms using the production 25 ms Windows UI timer with mocked output. The self-contained x86 package passed its self-test with zero failures and zero warnings. The optional native input test could not acquire foreground focus in this automated session and aborted; actual key delivery, PTO2 hardware and in-game countermeasure rates remain acceptance checks.

Local v2.0.11 verification (25 October 2026):

- `Tests/NeckSpeed.Tests` passed 4,863 assertions covering default/legacy settings, persisted limits, unchanged 1–100% parameters, faster 300% motion, rate ceilings, no overshoot, settling on release, mode switching, and layouts in all 14 languages at 850, 1500 and 3100 pixels. Simple/Advanced screenshots were rendered and inspected.
- The 225 controls checks, 412 layout checks, 41 game-service checks, aircraft-data checks and 62 VTrim core checks passed. The existing 100 ms scheduling test averaged 100.2 ms with mocked output.
- `Build.cmd` produced the self-contained x86 executable; its self-test passed with zero failures and zero warnings. File/product versions were checked against the release version.
- The release ZIP contains exactly the seven required runtime/driver/license files, with no settings, profiles or diagnostics. SHA-256: `cd38ddaad93e9f344ab52c99f1738834fa255c50d474bfa72752cbaea7d31b77`.

For VR acceptance, compare 100%, 200% and 300% during activation and release; confirm that turning off the Simple unlock immediately caps speed at 100%. Actual headset feel has not been verified in the automated checks.

Local v2.0.12 verification (29 October 2026):

- The packaged x86 executable passed its built-in self-test with zero failures and zero warnings.
- `Tests/ControlsInput.Tests` passed 227 checks, including a regression check that two switch mappings sharing one HOTAS use one controller sample per cycle; the mocked 100 ms repeat interval averaged 101.0 ms.
- `VTrim.Core.Tests` passed 62 checks; `NeckSpeed.Tests` passed 4,863 checks; the layout audit passed 412 checks; `GameServices.Tests` passed 41 checks; `AircraftData.Tests` passed.
- No physical HOTAS is connected to this machine. In-game axis latency and feel require a physical controller acceptance test.
- The v2.0.12 release ZIP contains exactly seven required runtime, driver, and license files, with no settings or profiles. SHA-256: `1354eb1e73918c794dfde87a4f5d093b04b2a2714e259a982d90fa422558f6ce`.

Local v2.0.13 verification (29 October 2026):

- `Build.cmd` produced the self-contained x86 app with file version 2.0.13.0 and product version 2.0.13. Its packaged self-test passed with zero failures and zero warnings, including a simulated 180 ms UI stall during continued flight-axis output.
- `VTrim.Core.Tests` passed 62 checks; `ControlsInput.Tests` passed 227 checks; `NeckSpeed.Tests` passed 4,863 checks; the layout audit passed 412 checks; `GameServices.Tests` passed 41 checks; `AircraftData.Tests` passed.
- The release ZIP contains exactly seven runtime, driver, and license files, with no settings or profiles. SHA-256: `2f3dda858d50fdf8add94f902451d2133aef3768c56643c962d82752780ea1e0`.
- Physical HOTAS and War Thunder gameplay were unavailable for local acceptance testing.
- The optional Home-page startup smoke check failed because its WebView2 field was still null immediately after tray restore; the VTrim and packaged app checks above passed. The Home-page path was not changed in this release.

Local v2.0.14 verification (30 October 2026):

- The user tested the local build with their HOTAS and reported that the stick movement is now smooth in the preview and game.
- Layout audit, NeckSpeed, ControlsInput, GameServices, AircraftData, and VTrim.Core all passed. VTrim.Core reported 62 passed and 0 failed.
- Theme switching in both directions passed without restarting the window. Windows startup, WebView2 initialization after tray restore, and window-size preservation passed.
- The clean self-contained x86 executable reports file version 2.0.14.0 and product version 2.0.14. Its packaged `--self-test` passed with zero failures and zero warnings.
- The release ZIP contains exactly seven runtime, driver, and license files, with no settings, profiles, diagnostics, or local-test marker. SHA-256: `0fc3931c7c2e113a78cf7281a0879eba84352f27b167df66ca3cd9fb2737071d`.
