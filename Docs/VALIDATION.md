# Validation for War Thunder VR Assistant v2.0.9

On Windows with the .NET 10 SDK, run `Run-Tests.cmd` for the layout audit and managed GameServices, aircraft data, and VTrim tests. Run `dotnet run --project WTAssistant/verification/ThemePreview/ThemePreview.csproj -c Release -p:Platform=x86 -- --startup-smoke` to check Windows startup, WebView2 deferral, repeated tray restore, and saved window size.

Run `Build.cmd` for the self-contained x86 publish and packaged executable self-test. The release ZIP must contain the executable, native DLLs, Drivers, and Licenses at its root, with no personal Settings folder. Confirm that the executable file and product versions are both 2.0.9 and that its `--self-test` report passes.

The automated tests simulate input and game state. Physical HOTAS, vJoy, VR runtime, and War Thunder behavior still require a real-device acceptance test.
