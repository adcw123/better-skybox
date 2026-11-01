@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0tools\Manage-SKYBOX.ps1" -Action Launch
set "SKYBOX_EXIT=%ERRORLEVEL%"
if not "%SKYBOX_EXIT%"=="0" pause
exit /b %SKYBOX_EXIT%
