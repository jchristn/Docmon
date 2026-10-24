@echo off
REM Connect Mux to the Docmon MCP server by adding a 'docmon' entry to Mux's mcp-servers.json.
REM Authenticates with the Docmon bearer token (Authorization: Bearer <token>).
REM Usage: install-mux.bat [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
REM Override the endpoint with DOCMON_MCP_BASE_URL (the path is /mcp) and the config path with DOCMON_MUX_CONFIG.
setlocal
if "%DOCMON_MCP_BASE_URL%"=="" set "DOCMON_MCP_BASE_URL=http://127.0.0.1:8080"
if "%DOCMON_BEARER_TOKEN%"=="" set "DOCMON_BEARER_TOKEN=docmonadmin"
if not "%~1"=="" set "DOCMON_BEARER_TOKEN=%~1"
if "%DOCMON_MUX_CONFIG%"=="" set "DOCMON_MUX_CONFIG=%USERPROFILE%\.mux\mcp-servers.json"
set "DOCMON_CONFIG=%DOCMON_MUX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:DOCMON_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $raw=''; if(Test-Path $p){ $raw=Get-Content -Raw -Path $p }; if([string]::IsNullOrWhiteSpace($raw)){ $root=[PSCustomObject]@{} } else { $root=$raw | ConvertFrom-Json }; if($null -eq $root.servers){ $root | Add-Member -NotePropertyName servers -NotePropertyValue @() -Force }; $others=@($root.servers | Where-Object { $_.name -ne 'docmon' }); $auth=[PSCustomObject]@{ type='bearer'; bearerToken=$env:DOCMON_BEARER_TOKEN; apiKeyHeader='X-API-Key'; apiKeyValue='' }; $entry=[PSCustomObject]@{ name='docmon'; transport='http'; command=''; args=@(); env=[PSCustomObject]@{}; url=$env:DOCMON_MCP_BASE_URL; mcpPath='/mcp'; auth=$auth }; $root.servers=@($others + $entry); [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Added docmon to ' + $p + ' (bearer auth)')"
if errorlevel 1 exit /b 1
echo Restart Mux to pick up the change.
endlocal
