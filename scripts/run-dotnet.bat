@echo off
setlocal
set "DOCMON_ROOT=%~dp0.."
set "ROLE=%~1"
shift
set "FRAMEWORK="
if /I "%~1"=="--framework" goto explicit_framework
if /I "%~1"=="-f" goto explicit_framework
if /I "%~1"=="net8" goto first_framework
if /I "%~1"=="net8.0" goto first_framework
if /I "%~1"=="net10" goto first_framework
if /I "%~1"=="net10.0" goto first_framework
goto default_framework
:explicit_framework
shift
if "%~1"=="" (echo Specify net8.0 or net10.0.& exit /b 1)
:first_framework
set "FRAMEWORK=%~1"
shift
if /I "%FRAMEWORK%"=="net8" set "FRAMEWORK=net8.0"
if /I "%FRAMEWORK%"=="net10" set "FRAMEWORK=net10.0"
if /I "%FRAMEWORK%"=="net8.0" goto project
if /I "%FRAMEWORK%"=="net10.0" goto project
echo Unsupported framework: %FRAMEWORK%
exit /b 1
:default_framework
dotnet --list-sdks | findstr /B /C:"10." >nul
if errorlevel 1 (set "FRAMEWORK=net8.0") else (set "FRAMEWORK=net10.0")
:project
if /I "%ROLE%"=="tui" (set "PROJECT=Docmon.App") else if /I "%ROLE%"=="server" (set "PROJECT=Docmon.Server") else (echo Unknown application: %ROLE%& exit /b 1)
if /I "%ROLE%"=="server" if not defined DOCMON_BEARER_TOKEN set "DOCMON_BEARER_TOKEN=docmonadmin"
set "APP_ARGUMENTS="
:arguments
if "%~1"=="" goto run
set "APP_ARGUMENTS=%APP_ARGUMENTS% %1"
shift
goto arguments
:run
cd /d "%DOCMON_ROOT%"
dotnet run --project "%DOCMON_ROOT%\src\%PROJECT%\%PROJECT%.csproj" --framework %FRAMEWORK% -- %APP_ARGUMENTS%
exit /b %errorlevel%
