@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "SETUP=%~dp0akvirtualcamera-setup.exe"
if exist "%SETUP%" (
  "%SETUP%" /S
)

set "PF64=%ProgramFiles%\akvirtualcamera\x64"
if exist "%PF64%\AkVCamManager.exe" copy /Y "%PF64%\AkVCamManager.exe" "%~dp0" >nul
if exist "%PF64%\AkVCamAssistant.exe" copy /Y "%PF64%\AkVCamAssistant.exe" "%~dp0" >nul

set "ASSISTANT=%~dp0AkVCamAssistant.exe"
if not exist "%ASSISTANT%" if exist "%PF64%\AkVCamAssistant.exe" set "ASSISTANT=%PF64%\AkVCamAssistant.exe"

if exist "%ASSISTANT%" (
  "%ASSISTANT%" --install
)

sc start AkVCamAssistant >nul 2>&1
net start AkVCamAssistant >nul 2>&1
timeout /t 3 /nobreak >nul

set "MANAGER=%~dp0AkVCamManager.exe"
if not exist "%MANAGER%" if exist "%PF64%\AkVCamManager.exe" set "MANAGER=%PF64%\AkVCamManager.exe"

set "INI=%~dp0vcam.ini"
if not exist "%INI%" set "INI=%~dp0..\..\vcam.ini"

if exist "%MANAGER%" (
  if exist "%INI%" "%MANAGER%" load "%INI%"
  "%MANAGER%" set-page-size 128000000
)

exit /b 0
