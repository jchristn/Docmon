@echo off
REM Disconnect Cursor from Docmon by removing the 'docmon' entry from %USERPROFILE%\.cursor\mcp.json.
setlocal
if "%DOCMON_CURSOR_CONFIG%"=="" set "DOCMON_CURSOR_CONFIG=%USERPROFILE%\.cursor\mcp.json"
set "DOCMON_CONFIG=%DOCMON_CURSOR_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:DOCMON_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.mcpServers){ $root.mcpServers.PSObject.Properties.Remove('docmon') }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed docmon from ' + $p)"
endlocal
