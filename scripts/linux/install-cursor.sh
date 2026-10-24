#!/usr/bin/env sh
# Connect Cursor to the Docmon MCP server by adding a 'docmon' entry to ~/.cursor/mcp.json.
# Authenticates with the Docmon bearer token (Authorization: Bearer <token>).
# Usage: install-cursor.sh [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
# Override with DOCMON_MCP_URL / DOCMON_CURSOR_CONFIG.
set -e

URL="${DOCMON_MCP_URL:-http://127.0.0.1:8080/mcp}"
TOKEN="${1:-${DOCMON_BEARER_TOKEN:-docmonadmin}}"
CONFIG="${DOCMON_CURSOR_CONFIG:-$HOME/.cursor/mcp.json}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$URL" "$TOKEN" <<'PY'
import json, os, sys
path, url, token = sys.argv[1], sys.argv[2], sys.argv[3]
d = os.path.dirname(path)
if d and not os.path.isdir(d):
    os.makedirs(d, exist_ok=True)
try:
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    if not isinstance(cfg, dict):
        cfg = {}
except Exception:
    cfg = {}
servers = cfg.get("mcpServers")
if not isinstance(servers, dict):
    servers = {}
    cfg["mcpServers"] = servers
servers["docmon"] = {"url": url, "headers": {"Authorization": "Bearer " + token}}
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
print("Added 'docmon' to " + path)
PY
echo "Restart Cursor to pick up the change (Settings -> MCP)."
