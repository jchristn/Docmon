#!/usr/bin/env sh
# Connect Mux to the Docmon MCP server by adding a 'docmon' entry to Mux's mcp-servers.json.
# Authenticates with the Docmon bearer token (Authorization: Bearer <token>).
# Usage: install-mux.sh [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
# Override the endpoint with DOCMON_MCP_BASE_URL (the path is /mcp) and the config path with DOCMON_MUX_CONFIG.
set -e

BASE_URL="${DOCMON_MCP_BASE_URL:-http://127.0.0.1:8080}"
TOKEN="${1:-${DOCMON_BEARER_TOKEN:-docmonadmin}}"
CONFIG="${DOCMON_MUX_CONFIG:-$HOME/.mux/mcp-servers.json}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$BASE_URL" "$TOKEN" <<'PY'
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
servers = cfg.get("servers")
if not isinstance(servers, list):
    servers = []
servers = [s for s in servers if not (isinstance(s, dict) and s.get("name") == "docmon")]
servers.append({"name": "docmon", "transport": "http", "command": "", "args": [], "env": {}, "url": url, "mcpPath": "/mcp",
                "auth": {"type": "bearer", "bearerToken": token, "apiKeyHeader": "X-API-Key", "apiKeyValue": ""}})
cfg["servers"] = servers
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
print("Added 'docmon' to " + path + " (bearer auth)")
PY
echo "Restart Mux to pick up the change."
