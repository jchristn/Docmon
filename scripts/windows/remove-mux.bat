@echo off
REM Disconnect Mux from Docmon by removing the 'docmon' entry from Mux's mcp-servers.json.
setlocal
if "%DOCMON_MUX_CONFIG%"=="" set "DOCMON_MUX_CONFIG=%USERPROFILE%\.mux\mcp-servers.json"
set "DOCMON_CONFIG=%DOCMON_MUX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:DOCMON_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.servers){ $root.servers=@($root.servers | Where-Object { $_.name -ne 'docmon' }) }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed docmon from ' + $p)"
endlocal
