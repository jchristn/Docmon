#!/usr/bin/env sh
# Connect Claude Code to the Docmon MCP server at user scope (every project sees it), via the claude CLI.
# Authenticates with the Docmon bearer token (Authorization: Bearer <token>). The token grants full control of
# the Docker machine, so use HTTPS for remote servers.
# Usage: install-claude.sh [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
# Override the endpoint with DOCMON_MCP_URL.
set -e

URL="${DOCMON_MCP_URL:-http://127.0.0.1:8080/mcp}"
TOKEN="${1:-${DOCMON_BEARER_TOKEN:-docmonadmin}}"

command -v claude >/dev/null 2>&1 || {
  echo "Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/claude-code" >&2
  exit 1
}

# Re-running replaces the user-scope entry instead of failing on a duplicate.
claude mcp remove --scope user docmon >/dev/null 2>&1 || true
claude mcp add --scope user --transport http docmon "$URL" --header "Authorization: Bearer $TOKEN"
echo "Added 'docmon' MCP server to Claude Code for every project ($URL). Restart Claude Code to pick it up."
