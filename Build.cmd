@echo off
setlocal
cd /d "%~dp0"
where dotnet.exe >nul 2>&1
if errorlevel 1 (
 echo .NET SDK was not found. Install the .NET 10 SDK / Visual Studio .NET desktop development workload.
 pause
 exit /b 1
)
dotnet build ".\WTAssistant\WTVRSettingsAssistant.csproj" -c Release -p:Platform=x86
if errorlevel 1 (
 echo.
 echo WT Assistant application build failed.
 pause
 exit /b 1
)
echo.
echo WT Assistant v2.0.5 build completed.
echo Output: WTAssistant\bin\x86\Release\net10.0-windows10.0.19041.0\
echo.
echo VTrim no longer includes or manages the custom VTrim kernel driver.
echo VTrim uses vJoy Device 1 for the virtual Roll/Pitch/Rudder axes.
pause
