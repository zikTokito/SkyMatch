@echo off
set "APP=%~dp0dist\SkyMatch.exe"
if not exist "%APP%" (
    echo SkyMatch.exe not found.
    echo Please keep the dist folder next to this launcher.
    pause
    exit /b 1
)
start "" "%APP%"
exit /b 0
