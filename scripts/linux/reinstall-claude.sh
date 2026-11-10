#!/usr/bin/env bash
# Re-register Claude Code with the Docmon MCP server at user scope: removes the entry, then adds it again.
# Continues when there is no 'docmon' entry to remove; every other server is kept.
# Authenticates with the Docmon bearer token (Authorization: Bearer <token>). The token grants
# full control of the Docker machine, so use HTTPS for remote servers.
# Usage: scripts/linux/reinstall-claude.sh [TOKEN]
#   TOKEN           overrides DOCMON_BEARER_TOKEN (default: docmonadmin)
#   DOCMON_MCP_URL  server URL (default: http://127.0.0.1:8080/mcp)
# Requires the claude CLI.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

if [ "$(uname -s)" != Linux ]; then
    die "This script is for Linux. Use scripts/macos on macOS or scripts\\windows on Windows."
fi
# A Microsoft kernel alone is not WSL (Docker Desktop containers have one too); WSL also has interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1); they handle the Windows PATH, line endings and the WSL 2 network."
fi

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -le 1 ] || die "Unexpected argument: $2 (usage: reinstall-claude.sh [TOKEN])"
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

# Assigned first: a failure inside $(...) in an if test would not stop the script.
STATUS="$(claude_remove)"
if [ "$STATUS" = removed ]; then
    echo "Removed 'docmon' from Claude Code at user scope."
else
    echo "No 'docmon' entry in Claude Code at user scope; continuing."
fi
claude_add
echo "Added 'docmon' to Claude Code at user scope ($URL)."
echo "Restart Claude Code to pick up the change."
