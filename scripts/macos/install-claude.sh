#!/usr/bin/env bash
# Connect Claude Code to the Docmon MCP server at user scope (every project sees it), via the claude CLI.
# Idempotent: an existing 'docmon' entry is replaced; every other server is kept.
# Authenticates with the Docmon bearer token (Authorization: Bearer <token>). The token grants
# full control of the Docker machine, so use HTTPS for remote servers.
# Usage: scripts/macos/install-claude.sh [TOKEN]
#   TOKEN           overrides DOCMON_BEARER_TOKEN (default: docmonadmin)
#   DOCMON_MCP_URL  server URL (default: http://127.0.0.1:8080/mcp)
# Requires the claude CLI.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

if [ "$(uname -s)" != Darwin ]; then
    die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 or scripts/wsl under WSL, or scripts\\windows on Windows."
fi

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -le 1 ] || die "Unexpected argument: $2 (usage: install-claude.sh [TOKEN])"
TOKEN="${1:-${DOCMON_BEARER_TOKEN:-docmonadmin}}"
command -v claude >/dev/null 2>&1 || die "'claude' was not found. Install Claude Code: curl -fsSL https://claude.ai/install.sh | bash (see https://code.claude.com/docs/en/setup)."
# The same rule as the Windows scripts: http:// or https://, a host, and no whitespace.
URL_RE='^https?://[^/?#[:space:]]+[^[:space:]]*$'
check_url() { [[ "$1" =~ $URL_RE ]] || die "'$1' is not an http(s) URL."; }
URL="${DOCMON_MCP_URL:-http://127.0.0.1:8080/mcp}"
check_url "$URL"

# Prints "removed" or "absent"; fails on any other error from the claude CLI.
claude_remove() {
    local out
    if out="$(claude mcp remove --scope user docmon 2>&1)"; then echo removed; return 0; fi
    case "$out" in
        *"No MCP server named"*) echo absent ;;
        *) die "claude mcp remove failed: $out" ;;
    esac
}
claude_add() {
    claude mcp add --scope user --transport http docmon "$URL" --header "Authorization: Bearer $TOKEN" >/dev/null || die "claude mcp add failed."
}

STATUS="$(claude_remove)"
claude_add
if [ "$STATUS" = removed ]; then
    echo "Replaced the existing 'docmon' entry in Claude Code at user scope ($URL)."
else
    echo "Added 'docmon' to Claude Code at user scope ($URL)."
fi
echo "Restart Claude Code to pick up the change."
