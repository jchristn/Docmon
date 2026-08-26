@echo off
REM Packs Docmon and installs it as the global "docmon" .NET tool.
REM Optional argument selects the target framework: net8.0 or net10.0.
setlocal

set "ROOT_DIR=%~dp0"
set "PACKAGE_SOURCE=%ROOT_DIR%artifacts\tool-packages"

set "FRAMEWORK_ARGUMENT=%~1"
if /I "%FRAMEWORK_ARGUMENT%"=="--framework" (
    if "%~2"=="" (
        echo Missing framework value after --framework.
        call :usage
        exit /b 1
    )
    set "FRAMEWORK_ARGUMENT=%~2"
)
if /I "%FRAMEWORK_ARGUMENT%"=="-f" (
    if "%~2"=="" (
        echo Missing framework value after -f.
        call :usage
        exit /b 1
    )
    set "FRAMEWORK_ARGUMENT=%~2"
)

call :resolve_framework "%FRAMEWORK_ARGUMENT%"
if %errorlevel% equ 2 exit /b 0
if %errorlevel% neq 0 exit /b %errorlevel%

if exist "%PACKAGE_SOURCE%" rmdir /s /q "%PACKAGE_SOURCE%"
mkdir "%PACKAGE_SOURCE%"
if %errorlevel% neq 0 exit /b %errorlevel%

echo Building docmon for %FRAMEWORK%...
dotnet pack "%ROOT_DIR%src\Docmon.App\Docmon.App.csproj" --configuration Release -p:TargetFrameworks=%FRAMEWORK% --output "%PACKAGE_SOURCE%"
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)

echo Installing docmon...
dotnet tool install -g --source "%PACKAGE_SOURCE%" --framework %FRAMEWORK% --disable-parallel Docmon
if %errorlevel% neq 0 (
    echo.
    echo Install failed. If docmon is already installed, run reinstall-tool.bat instead.
    exit /b %errorlevel%
)

docmon --version
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
echo Defaults to net10.0 when a .NET 10 SDK is installed, otherwise net8.0.
exit /b 0
