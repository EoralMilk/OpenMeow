@echo off
title OpenRA
for /F "delims==\ " %%x in ("%*") do (
  if "%%~x" EQU "Game.Mod" (goto launch)
)

@REM :choosemod
@REM set /P mod="Select mod (ra, cnc, d2k, ts) or --exit: "
@REM if /I "%mod%" EQU "--exit" (exit /b)
@REM if "%mod%" EQU "ra" (goto launchmod)
@REM if "%mod%" EQU "cnc" (goto launchmod)
@REM if "%mod%" EQU "ts" (goto launchmod)
@REM if "%mod%" EQU "d2k" (goto launchmod)
@REM echo.
@REM echo Unknown mod: %mod%
@REM echo.
@REM goto choosemod

:launchmod
cd %~dp0%
bin\OpenRA.exe Engine.EngineDir=".." Engine.LaunchPath="%~dpf0" Game.Mod="ts" %*
goto end
:launch
cd %~dp0%
bin\OpenRA.exe Engine.EngineDir=".." Engine.LaunchPath="%~dpf0" %*

:end
if %errorlevel% neq 0 goto crashdialog
exit /b

:crashdialog
set logs=%AppData%\OpenRA\Logs
if exist %USERPROFILE%\Documents\OpenRA\Logs (set logs=%USERPROFILE%\Documents\OpenRA\Logs)
if exist Support\Logs (set logs=%cd%\Support\Logs)

echo ----------------------------------------
echo OpenRA has encountered a fatal error.
echo   * Log Files are available in %logs%
echo   * FAQ is available at https://github.com/OpenRA/OpenRA/wiki/FAQ
echo ----------------------------------------
pause
