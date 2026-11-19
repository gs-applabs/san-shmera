@echo off
rem Builds and starts Chronikologio (stand-alone app). Needs the .NET SDK (11, or 10 with CHRON_NET=10.0); set CHRON_NET=10.0 to run on .NET 10.
setlocal
cd /d "%~dp0"
if "%CHRON_NET%"=="" set "CHRON_NET=11.0"
set "ChronNet=%CHRON_NET%"
dotnet build src\Chronikologio.Windows\Chronikologio.Windows.csproj -c Release -nologo -v q
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)
start "" "src\Chronikologio.Windows\bin\Release\net%CHRON_NET%-windows\Chronikologio.exe"
endlocal
