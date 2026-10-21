@echo off
setlocal EnableExtensions
rem Usage: build-dashboard.bat [tag] [namespace]
set "DOCMON_TAG=%~1"
if "%DOCMON_TAG%"=="" set "DOCMON_TAG=v0.1.0"
set "DOCMON_NAMESPACE=%~2"
if "%DOCMON_NAMESPACE%"=="" set "DOCMON_NAMESPACE=jchristn77"
if not defined DOCMON_BUILDER set "DOCMON_BUILDER=cloud-jchristn77-jchristn77"
set "DOCMON_IMAGE=%DOCMON_NAMESPACE%/docmon-dashboard"

pushd "%~dp0"
if errorlevel 1 exit /b 1

echo Building and pushing %DOCMON_IMAGE%:%DOCMON_TAG% for linux/amd64 and linux/arm64/v8...
if "%DOCMON_TAG%"=="latest" (
    docker buildx build --builder "%DOCMON_BUILDER%" --platform linux/amd64,linux/arm64/v8 --file docker/Dockerfile.dashboard --tag "%DOCMON_IMAGE%:%DOCMON_TAG%" --push .
) else (
    docker buildx build --builder "%DOCMON_BUILDER%" --platform linux/amd64,linux/arm64/v8 --file docker/Dockerfile.dashboard --tag "%DOCMON_IMAGE%:%DOCMON_TAG%" --tag "%DOCMON_IMAGE%:latest" --push .
)
if errorlevel 1 goto :error

echo Refreshing local images for the Docker daemon architecture...
docker pull "%DOCMON_IMAGE%:%DOCMON_TAG%"
if errorlevel 1 goto :error
if not "%DOCMON_TAG%"=="latest" (
    docker pull "%DOCMON_IMAGE%:latest"
    if errorlevel 1 goto :error
)
popd
exit /b 0

:error
popd
exit /b 1
