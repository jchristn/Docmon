@echo off
REM Disconnect the Gemini CLI from Docmon by removing the 'docmon' entry from %USERPROFILE%\.gemini\settings.json.
setlocal
if "%DOCMON_GEMINI_CONFIG%"=="" set "DOCMON_GEMINI_CONFIG=%USERPROFILE%\.gemini\settings.json"
set "DOCMON_CONFIG=%DOCMON_GEMINI_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:DOCMON_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.mcpServers){ $root.mcpServers.PSObject.Properties.Remove('docmon') }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed docmon from ' + $p)"
endlocal
