@echo off
call "%~dp0scripts\run-dotnet.bat" tui %*
exit /b %errorlevel%
