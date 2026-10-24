@echo off
REM Connect Codex to the Docmon MCP server by writing an [mcp_servers.docmon] table to %USERPROFILE%\.codex\config.toml.
REM Authenticates with the Docmon bearer token (Authorization: Bearer <token>).
REM Usage: install-codex.bat [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
REM Override with DOCMON_MCP_URL / DOCMON_CODEX_CONFIG.
setlocal
if "%DOCMON_MCP_URL%"=="" set "DOCMON_MCP_URL=http://127.0.0.1:8080/mcp"
if "%DOCMON_BEARER_TOKEN%"=="" set "DOCMON_BEARER_TOKEN=docmonadmin"
if not "%~1"=="" set "DOCMON_BEARER_TOKEN=%~1"
if "%DOCMON_CODEX_CONFIG%"=="" set "DOCMON_CODEX_CONFIG=%USERPROFILE%\.codex\config.toml"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:DOCMON_CODEX_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $lines=@(); if(Test-Path $p){ $lines=@(Get-Content -Path $p) }; $out=New-Object System.Collections.Generic.List[string]; $skip=$false; $found=$false; foreach($l in $lines){ if($l -match '^\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$'){ $n=$Matches[1].Replace([string][char]34,''); $skip=($n -eq 'mcp_servers.docmon' -or $n.StartsWith('mcp_servers.docmon.')); if($skip){ $found=$true } }; if(-not $skip){ $out.Add($l) } }; while($out.Count -gt 0 -and [string]::IsNullOrWhiteSpace($out[$out.Count-1])){ $out.RemoveAt($out.Count-1) }; if($out.Count -gt 0){ $out.Add('') }; $out.Add('[mcp_servers.docmon]'); $out.Add('url = ' + (ConvertTo-Json $env:DOCMON_MCP_URL)); $out.Add('http_headers = { Authorization = ' + (ConvertTo-Json ('Bearer ' + $env:DOCMON_BEARER_TOKEN)) + ' }'); [IO.File]::WriteAllText($p, (($out -join [Environment]::NewLine) + [Environment]::NewLine)); Write-Host ('Added docmon to ' + $p)"
if errorlevel 1 exit /b 1
echo Restart Codex to pick up the change.
endlocal
