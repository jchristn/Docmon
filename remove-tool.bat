@echo off
REM Uninstalls the global "docmon" .NET tool.
setlocal
cd /d "%~dp0"

echo Uninstalling the docmon global tool...
dotnet tool uninstall --global Docmon
if errorlevel 1 (
    echo docmon was not installed, or uninstall failed.
    exit /b 1
)

echo.
echo Done.
endlocal
