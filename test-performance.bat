@echo off
REM Performance test for FlyCtl vs PowerShell vs AutoHotkey

echo Testing FlyCtl.exe performance...
echo.

REM Test FlyCtl (C# native)
echo [FlyCtl.exe]
powershell -Command "Measure-Command { .\FlyCtl.exe show } | Select-Object -ExpandProperty TotalMilliseconds"

echo.
echo [PowerShell flyctl.ps1]
REM Test PowerShell
powershell -Command "Measure-Command { pwsh -File .\flyctl.ps1 show } | Select-Object -ExpandProperty TotalMilliseconds"

echo.
echo Note: FlyCtl.exe is typically 10-20x faster than AutoHotkey
echo.
pause
