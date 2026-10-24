@echo off
REM Connect Claude Code to the Docmon MCP server at user scope (every project sees it), via the claude CLI.
REM Authenticates with the Docmon bearer token (Authorization: Bearer <token>). The token grants full control of
REM the Docker machine, so use HTTPS for remote servers.
REM Usage: install-claude.bat [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
REM Override the endpoint with DOCMON_MCP_URL.
setlocal
if "%DOCMON_MCP_URL%"=="" set "DOCMON_MCP_URL=http://127.0.0.1:8080/mcp"
if "%DOCMON_BEARER_TOKEN%"=="" set "DOCMON_BEARER_TOKEN=docmonadmin"
if not "%~1"=="" set "DOCMON_BEARER_TOKEN=%~1"

where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/claude-code
  exit /b 1
)

REM Re-running replaces the user-scope entry instead of failing on a duplicate. claude is itself a batch file
REM (claude.cmd), so it must be run with "call" or this script would end when it does.
call claude mcp remove --scope user docmon >nul 2>nul
call claude mcp add --scope user --transport http docmon "%DOCMON_MCP_URL%" --header "Authorization: Bearer %DOCMON_BEARER_TOKEN%"
if errorlevel 1 exit /b 1
echo Added 'docmon' MCP server to Claude Code for every project (%DOCMON_MCP_URL%). Restart Claude Code to pick it up.
endlocal
