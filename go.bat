@echo off
REM Builds and runs Docmon interactively from source (Debug).
REM Optional argument selects the target framework: net8.0 or net10.0.
setlocal
cd /d "%~dp0"

set "FRAMEWORK_ARGUMENT=%~1"
if /I "%FRAMEWORK_ARGUMENT%"=="--framework" set "FRAMEWORK_ARGUMENT=%~2"
if /I "%FRAMEWORK_ARGUMENT%"=="-f" set "FRAMEWORK_ARGUMENT=%~2"

call :resolve_framework "%FRAMEWORK_ARGUMENT%"
if %errorlevel% equ 2 exit /b 0
if %errorlevel% neq 0 exit /b %errorlevel%

REM Release any file lock from a previous run before rebuilding.
taskkill /IM docmon.exe /F >nul 2>&1

echo Building docmon for %FRAMEWORK%...
dotnet build "%~dp0src\Docmon.App\Docmon.App.csproj" -f %FRAMEWORK%
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)

set "EXE=%~dp0src\Docmon.App\bin\Debug\%FRAMEWORK%\docmon.exe"
if not exist "%EXE%" (
    echo Could not find "%EXE%".
    exit /b 1
)

"%EXE%"
exit /b %errorlevel%

:resolve_framework
set "FRAMEWORK=%~1"
if /I "%FRAMEWORK%"=="/?" (
    call :usage
    exit /b 2
)
if /I "%FRAMEWORK%"=="-h" (
    call :usage
    exit /b 2
)
if /I "%FRAMEWORK%"=="--help" (
    call :usage
    exit /b 2
)

if "%FRAMEWORK%"=="" (
    dotnet --list-sdks | findstr /B /C:"10." >nul
    if errorlevel 1 (
        set "FRAMEWORK=net8.0"
    ) else (
        set "FRAMEWORK=net10.0"
    )
    exit /b 0
)

if /I "%FRAMEWORK%"=="net8" set "FRAMEWORK=net8.0"
if /I "%FRAMEWORK%"=="net8.0" exit /b 0
if /I "%FRAMEWORK%"=="net10" set "FRAMEWORK=net10.0"
if /I "%FRAMEWORK%"=="net10.0" exit /b 0

echo Unsupported framework "%~1".
echo Supported frameworks: net8.0, net10.0.
echo Use net8.0 on systems without a .NET 10 SDK.
exit /b 1

:usage
echo Usage: %~nx0 [net8.0^|net10.0]
echo        %~nx0 --framework ^<net8.0^|net10.0^>
echo Builds and runs docmon from source. Defaults to net10.0 when a .NET 10 SDK
echo is installed, otherwise net8.0.
exit /b 0
