#!/usr/bin/env sh
# Connect Codex to the Docmon MCP server by writing an [mcp_servers.docmon] table to ~/.codex/config.toml.
# Authenticates with the Docmon bearer token (Authorization: Bearer <token>).
# Usage: install-codex.sh [TOKEN]  (arg #1 overrides DOCMON_BEARER_TOKEN)
# Override with DOCMON_MCP_URL / DOCMON_CODEX_CONFIG.
set -e

URL="${DOCMON_MCP_URL:-http://127.0.0.1:8080/mcp}"
TOKEN="${1:-${DOCMON_BEARER_TOKEN:-docmonadmin}}"
CONFIG="${DOCMON_CODEX_CONFIG:-$HOME/.codex/config.toml}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$URL" "$TOKEN" <<'PY'
import json, os, re, sys
path, url, token = sys.argv[1], sys.argv[2], sys.argv[3]
d = os.path.dirname(path)
if d and not os.path.isdir(d):
    os.makedirs(d, exist_ok=True)
try:
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
except FileNotFoundError:
    lines = []
# Drop any existing [mcp_servers.docmon] table and its sub-tables; keep everything else verbatim.
out, skipping = [], False
for line in lines:
    m = re.match(r"\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$", line)
    if m:
        name = m.group(1).replace('"', "")
        skipping = name == "mcp_servers.docmon" or name.startswith("mcp_servers.docmon.")
    if not skipping:
        out.append(line)
while out and not out[-1].strip():
    out.pop()
if out:
    out.append("")
out += ["[mcp_servers.docmon]",
        "url = " + json.dumps(url),
        "http_headers = { Authorization = " + json.dumps("Bearer " + token) + " }"]
with open(path, "w", encoding="utf-8") as f:
    f.write("\n".join(out) + "\n")
print("Added 'docmon' to " + path)
PY
echo "Restart Codex to pick up the change."
