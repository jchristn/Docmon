@echo off
REM Disconnect Claude Code from the Docmon MCP server (via the claude CLI).
setlocal
where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH.
  exit /b 1
)

call claude mcp remove --scope user docmon
echo Removed 'docmon' MCP server from Claude Code.
endlocal
