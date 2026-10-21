@echo off
call "%~dp0scripts\run-dotnet.bat" server %*
exit /b %errorlevel%
