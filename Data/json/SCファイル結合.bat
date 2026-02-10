
@echo off
REM concat-files.bat
REM Run this to merge all .cs and .json under this .bat/.ps1 folder recursively.

setlocal
set "PS=powershell.exe"

REM Move to the folder where this .bat (and .ps1) resides
cd /d "%~dp0"

REM Call PowerShell with no path args (ps1 uses its own folder as root)
"%PS%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0concat-files.ps1"

RENAME code_bundle.txt data_bundle.txt

if errorlevel 1 (
  echo.
  echo PowerShell script returned an error. Check execution policy and inputs.
  echo Try: powershell -NoProfile -ExecutionPolicy Bypass -File .\concat-files.ps1 -Root "." -Output code_bundle.txt
  echo.
  pause
)

endlocal
