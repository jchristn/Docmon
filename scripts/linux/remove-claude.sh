#!/usr/bin/env sh
# Disconnect Claude Code from the Docmon MCP server (via the claude CLI).
set -e

command -v claude >/dev/null 2>&1 || { echo "Claude CLI not found on PATH." >&2; exit 1; }

claude mcp remove --scope user docmon
echo "Removed 'docmon' MCP server from Claude Code."
