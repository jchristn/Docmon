@echo off
setlocal EnableExtensions
rem Usage: build-all.bat [tag] [namespace]
set "DOCMON_TAG=%~1"
if "%DOCMON_TAG%"=="" set "DOCMON_TAG=v0.1.0"
set "DOCMON_NAMESPACE=%~2"
if "%DOCMON_NAMESPACE%"=="" set "DOCMON_NAMESPACE=jchristn77"

pushd "%~dp0"
if errorlevel 1 exit /b 1
call build-server.bat "%DOCMON_TAG%" "%DOCMON_NAMESPACE%"
if errorlevel 1 goto :error
call build-dashboard.bat "%DOCMON_TAG%" "%DOCMON_NAMESPACE%"
if errorlevel 1 goto :error
popd
exit /b 0

:error
popd
exit /b 1
