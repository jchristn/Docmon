#!/usr/bin/env sh
# Disconnect Cursor from Docmon by removing the 'docmon' entry from ~/.cursor/mcp.json.
set -e

CONFIG="${DOCMON_CURSOR_CONFIG:-$HOME/.cursor/mcp.json}"
command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" <<'PY'
import json, os, sys
path = sys.argv[1]
if not os.path.isfile(path):
    print("Nothing to remove at " + path); sys.exit(0)
try:
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
except Exception:
    sys.exit(0)
servers = cfg.get("mcpServers")
if isinstance(servers, dict) and "docmon" in servers:
    del servers["docmon"]
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
print("Removed 'docmon' from " + path)
PY
