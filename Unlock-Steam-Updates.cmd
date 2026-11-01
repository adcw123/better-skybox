@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0tools\Manage-SKYBOX.ps1" -Action UnlockUpdates
set "SKYBOX_EXIT=%ERRORLEVEL%"
pause
exit /b %SKYBOX_EXIT%
