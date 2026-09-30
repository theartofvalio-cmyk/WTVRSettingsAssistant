@echo off
setlocal
cd /d "%~dp0"
where dotnet.exe >nul 2>&1
if errorlevel 1 (
 echo .NET SDK was not found. Install the .NET 10 SDK. Visual Studio is optional.
 if not defined WTA_CI pause
 exit /b 1
)

rem Use a short absolute build root so Windows MAX_PATH is never tied to
rem the folder where the source ZIP was extracted. Directory.Build.props reads
rem this environment variable for every project (main app + both VTrim projects).
set "WTA_BUILD_TEMP=%TEMP%\WTA21"
if exist "%WTA_BUILD_TEMP%" rmdir /s /q "%WTA_BUILD_TEMP%"

set "OUT=%~dp0Builds\WTAssistant"
rem Publish over the existing portable folder. User profiles and custom files
rem can live beside the executable, so never clear the whole output folder.

rem Remove ALL stale developer/intermediate output before restore/build.
rem VTrim.csproj and VTrim.Embedded.csproj share a source folder, so stale module
rem intermediates from either project must never survive into a clean publish.
if exist "%~dp0WTAssistant\bin" rmdir /s /q "%~dp0WTAssistant\bin"
if exist "%~dp0WTAssistant\obj" rmdir /s /q "%~dp0WTAssistant\obj"
if exist "%~dp0Modules\VTrim\Integration\VTrim\bin" rmdir /s /q "%~dp0Modules\VTrim\Integration\VTrim\bin"
if exist "%~dp0Modules\VTrim\Integration\VTrim\obj" rmdir /s /q "%~dp0Modules\VTrim\Integration\VTrim\obj"

echo Restoring WT Assistant v2.0.8...
dotnet restore ".\WTAssistant\WTVRSettingsAssistant.csproj" -p:Configuration=Release -p:Platform=x86 -r win-x86
if errorlevel 1 (
 echo.
 echo WT Assistant v2.0.8 restore failed.
 if not defined WTA_CI pause
 exit /b 1
)

echo Building WT Assistant v2.0.8 clean portable app...
dotnet publish ".\WTAssistant\WTVRSettingsAssistant.csproj" -c Release -p:Platform=x86 -r win-x86 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en -p:DebugType=None -p:DebugSymbols=false -o "%OUT%"
if errorlevel 1 (
 echo.
 echo WT Assistant v2.0.8 publish failed.
 if not defined WTA_CI pause
 exit /b 1
)

if not exist "%OUT%\Settings" mkdir "%OUT%\Settings"

rem WT Assistant is WinForms-only. The WebView2 NuGet package can emit the WPF
rem wrapper even though it is never used; keep the release root clean and avoid
rem the WindowsBase version warning/copy artifact.
if exist "%OUT%\Microsoft.Web.WebView2.Wpf.dll" del /q "%OUT%\Microsoft.Web.WebView2.Wpf.dll"
if exist "%OUT%\Microsoft.Web.WebView2.Wpf.xml" del /q "%OUT%\Microsoft.Web.WebView2.Wpf.xml"
if exist "%OUT%\Microsoft.Web.WebView2.Core.xml" del /q "%OUT%\Microsoft.Web.WebView2.Core.xml"
if exist "%OUT%\Microsoft.Web.WebView2.WinForms.xml" del /q "%OUT%\Microsoft.Web.WebView2.WinForms.xml"

rem Keep the release root intentionally simple: the single-file app plus the
rem two native runtime DLLs users actually need to see beside it.  The OpenXR
rem manifest lives under Drivers with a short WT Assistant name.
if not exist "%OUT%\Drivers" mkdir "%OUT%\Drivers"
if not exist "%OUT%\vJoyInterface.dll" if exist "%OUT%\Drivers\vJoy\vJoyInterface.dll" move /y "%OUT%\Drivers\vJoy\vJoyInterface.dll" "%OUT%\vJoyInterface.dll" >nul
if exist "%OUT%\XR_APILAYER_NOVENDOR_XRNeckSafer.dll" if not exist "%OUT%\NeckAssistant.dll" move /y "%OUT%\XR_APILAYER_NOVENDOR_XRNeckSafer.dll" "%OUT%\NeckAssistant.dll" >nul
if exist "%OUT%\NeckAssist\OpenXR\XR_APILAYER_NOVENDOR_XRNeckSafer.dll" if not exist "%OUT%\NeckAssistant.dll" move /y "%OUT%\NeckAssist\OpenXR\XR_APILAYER_NOVENDOR_XRNeckSafer.dll" "%OUT%\NeckAssistant.dll" >nul
if exist "%OUT%\XR_APILAYER_NOVENDOR_XRNeckSafer.json" move /y "%OUT%\XR_APILAYER_NOVENDOR_XRNeckSafer.json" "%OUT%\Drivers\NeckAssistant.json" >nul
if exist "%OUT%\NeckAssist\OpenXR\XR_APILAYER_NOVENDOR_XRNeckSafer.json" move /y "%OUT%\NeckAssist\OpenXR\XR_APILAYER_NOVENDOR_XRNeckSafer.json" "%OUT%\Drivers\NeckAssistant.json" >nul
if exist "%OUT%\NeckAssistant.json" move /y "%OUT%\NeckAssistant.json" "%OUT%\Drivers\NeckAssistant.json" >nul
if exist "%OUT%\vJoySetup.exe" (
  if not exist "%OUT%\Drivers\vJoy" mkdir "%OUT%\Drivers\vJoy"
  move /y "%OUT%\vJoySetup.exe" "%OUT%\Drivers\vJoy\vJoySetup.exe" >nul
)

rem .NET runtime and managed dependencies remain bundled into the single-file EXE.
rem Only the two native runtime DLLs above stay external.

echo Running non-interactive Windows self-test...
if exist "%OUT%\self-test-report.txt" del /q "%OUT%\self-test-report.txt"
"%OUT%\WTVRSettingsAssistant.exe" --self-test
set "SELFTEST_EXIT=%ERRORLEVEL%"
if exist "%OUT%\self-test-report.txt" (
  echo.
  type "%OUT%\self-test-report.txt"
)
if not "%SELFTEST_EXIT%"=="0" (
  echo.
  echo WT Assistant self-test failed with exit code %SELFTEST_EXIT%.
  if not defined WTA_CI pause
  exit /b %SELFTEST_EXIT%
)
if exist "%OUT%\self-test-report.txt" del /q "%OUT%\self-test-report.txt"

rem Keep the source tree tidy too. The runnable copy is already in Builds\WTAssistant.
if exist "%~dp0WTAssistant\bin" rmdir /s /q "%~dp0WTAssistant\bin"
if exist "%~dp0WTAssistant\obj" rmdir /s /q "%~dp0WTAssistant\obj"
if exist "%~dp0Modules\VTrim\Integration\VTrim\bin" rmdir /s /q "%~dp0Modules\VTrim\Integration\VTrim\bin"
if exist "%~dp0Modules\VTrim\Integration\VTrim\obj" rmdir /s /q "%~dp0Modules\VTrim\Integration\VTrim\obj"
if exist "%WTA_BUILD_TEMP%" rmdir /s /q "%WTA_BUILD_TEMP%"

echo.
echo WT Assistant v2.0.8 completed.
echo CLEAN APP FOLDER:
echo   %OUT%
echo.
echo Expected root:
echo   WTVRSettingsAssistant.exe
echo   vJoyInterface.dll
echo   NeckAssistant.dll
echo   Drivers\NeckAssistant.json
echo   Drivers\
echo   Licenses\
echo   Settings\
echo.
echo Do NOT distribute or run the old WTAssistant\bin\... folder as the release build.
echo.
if not defined WTA_CI pause
