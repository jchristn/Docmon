#!/usr/bin/env bash
# Connect Claude Code to the Docmon MCP server at user scope (every project sees it), via the claude CLI.
# Idempotent: an existing 'docmon' entry is replaced; every other server is kept.
# Authenticates with the Docmon bearer token (Authorization: Bearer <token>). The token grants
# full control of the Docker machine, so use HTTPS for remote servers.
# WSL 1 version: configures Claude Code inside this distro (not on Windows). For Claude Code on Windows, run
# scripts\windows\install-claude.bat from Windows.
# WSL 1 shares localhost with Windows, so 127.0.0.1 also reaches a server running on Windows.
# Usage: scripts/wsl/install-claude.sh [TOKEN]
#   TOKEN           overrides DOCMON_BEARER_TOKEN (default: docmonadmin)
#   DOCMON_MCP_URL  server URL (default: http://127.0.0.1:8080/mcp)
# Requires the claude CLI.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -le 1 ] || die "Unexpected argument: $2 (usage: install-claude.sh [TOKEN])"
TOKEN="${1:-${DOCMON_BEARER_TOKEN:-docmonadmin}}"
wsl_require_cmd claude "Install Claude Code inside WSL: curl -fsSL https://claude.ai/install.sh | bash (see https://code.claude.com/docs/en/setup)."
# The same rule as the Windows scripts: http:// or https://, a host, and no whitespace.
URL_RE='^https?://[^/?#[:space:]]+[^[:space:]]*$'
check_url() { [[ "$1" =~ $URL_RE ]] || die "'$1' is not an http(s) URL."; }
if [ -n "${DOCMON_MCP_URL:-}" ]; then
    URL="$DOCMON_MCP_URL"
else
    # WSL 1 shares localhost with Windows, so wsl_resolve_local_url leaves the URL unchanged here.
    URL="$(wsl_resolve_local_url "http://127.0.0.1:8080/mcp")"
fi
check_url "$URL"
wsl_log "This configures Claude Code inside this WSL distro. For Claude Code on Windows, run scripts\\windows\\install-claude.bat from Windows."

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
