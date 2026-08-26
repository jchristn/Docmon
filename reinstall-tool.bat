@echo off
REM Rebuilds Docmon and replaces any existing global "docmon" tool install.
setlocal
cd /d "%~dp0"

echo Packing Docmon...
dotnet pack src\Docmon.App\Docmon.App.csproj -c Release -o "%~dp0nupkg"
if errorlevel 1 (
    echo Pack failed.
    exit /b 1
)

echo Removing any existing docmon install...
dotnet tool uninstall --global Docmon >nul 2>&1

echo Installing the docmon global tool...
dotnet tool install --global --add-source "%~dp0nupkg" Docmon
if errorlevel 1 (
    echo Install failed.
    exit /b 1
)

echo.
echo Done. Run "docmon" from any directory.
endlocal
