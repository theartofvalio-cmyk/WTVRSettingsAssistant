@echo off
setlocal
cd /d "%~dp0"
python Tests\verify_package.py
if errorlevel 1 exit /b 1
dotnet run --project Tests\GameServices.Tests\GameServices.Tests.csproj -c Release
if errorlevel 1 exit /b 1
dotnet run --project Modules\VTrim\Integration\VTrim\Tests\VTrim.Core.Tests.csproj -c Release
if errorlevel 1 exit /b 1
echo Static and managed tests completed.
pause
