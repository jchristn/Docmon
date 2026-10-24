@echo off
REM Connect the Gemini CLI to the Docmon MCP server by adding a 'docmon' entry to %USERPROFILE%\.gemini\settings.json.
REM Authenticates with the Docmon bearer token (Authorization: Bearer <token>).
REM Usage: install-gemini.bat [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
REM Override with DOCMON_MCP_URL / DOCMON_GEMINI_CONFIG.
setlocal
if "%DOCMON_MCP_URL%"=="" set "DOCMON_MCP_URL=http://127.0.0.1:8080/mcp"
if "%DOCMON_BEARER_TOKEN%"=="" set "DOCMON_BEARER_TOKEN=docmonadmin"
if not "%~1"=="" set "DOCMON_BEARER_TOKEN=%~1"
if "%DOCMON_GEMINI_CONFIG%"=="" set "DOCMON_GEMINI_CONFIG=%USERPROFILE%\.gemini\settings.json"
set "DOCMON_CONFIG=%DOCMON_GEMINI_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:DOCMON_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $raw=''; if(Test-Path $p){ $raw=Get-Content -Raw -Path $p }; if([string]::IsNullOrWhiteSpace($raw)){ $root=[PSCustomObject]@{} } else { $root=$raw | ConvertFrom-Json }; if($null -eq $root.mcpServers){ $root | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([PSCustomObject]@{}) -Force }; $entry=[PSCustomObject]@{ httpUrl=$env:DOCMON_MCP_URL; headers=[PSCustomObject]@{ Authorization=('Bearer ' + $env:DOCMON_BEARER_TOKEN) } }; $root.mcpServers | Add-Member -NotePropertyName docmon -NotePropertyValue $entry -Force; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Added docmon to ' + $p)"
if errorlevel 1 exit /b 1
echo Restart the Gemini CLI to pick up the change (run /mcp to verify).
endlocal
