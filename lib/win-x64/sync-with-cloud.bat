@echo off
set SCRIPT_DIR=%~dp0
set SCRIPT_NAME=%~n0
"%SCRIPT_DIR%\TTSCloudSync.exe" "%SCRIPT_NAME%" %*
