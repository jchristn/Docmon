@echo off
REM Packs Docmon and installs it as the global "docmon" .NET tool.
setlocal
cd /d "%~dp0"

echo Packing Docmon...
dotnet pack src\Docmon.App\Docmon.App.csproj -c Release -o "%~dp0nupkg"
if errorlevel 1 (
    echo Pack failed.
    exit /b 1
)

echo Installing the docmon global tool...
dotnet tool install --global --add-source "%~dp0nupkg" Docmon
if errorlevel 1 (
    echo.
    echo Install failed. If docmon is already installed, run reinstall-tool.bat instead.
    exit /b 1
)

echo.
echo Done. Run "docmon" from any directory.
endlocal
