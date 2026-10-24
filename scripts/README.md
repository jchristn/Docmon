# Agent connection scripts

One-shot scripts to connect (or disconnect) an AI agent to the Docmon MCP server. There is an
`install-<agent>` and a `remove-<agent>` script per supported agent, in a folder per OS.

```
scripts/
  windows/   install-<agent>.bat   remove-<agent>.bat
  macos/     install-<agent>.sh    remove-<agent>.sh
  linux/     install-<agent>.sh    remove-<agent>.sh
```

Supported agents: **claude**, **codex**, **cursor**, **gemini**, **mux**.

Docmon serves MCP over Streamable HTTP at `/mcp` on the public API port (`DOCMON_PORT`, 8080 by default) or the
dashboard origin. Do not point agents at `DOCMON_MCP_PORT`: that listener binds to loopback inside the server and
is not reachable from outside it.

## Usage

Windows (Command Prompt / PowerShell):

```bat
scripts\windows\install-claude.bat
scripts\windows\remove-claude.bat
```

macOS / Linux:

```sh
sh scripts/macos/install-claude.sh      # or scripts/linux/... ; the two are identical
sh scripts/linux/remove-claude.sh
```

Each `install` script is idempotent — it replaces the existing `docmon` entry and preserves every other MCP server
in the config. Restart the agent afterward to pick up the change.

## What each script does

| Agent | How it connects | Config it edits |
| --- | --- | --- |
| **claude** | runs `claude mcp add --scope user` / `claude mcp remove` with an `Authorization` header | Claude Code's user config |
| **codex** | writes an `[mcp_servers.docmon]` table (`url`, `http_headers`) | `~/.codex/config.toml` |
| **cursor** | writes an `mcpServers.docmon` entry (`url`, `headers`) | `~/.cursor/mcp.json` |
| **gemini** | writes an `mcpServers.docmon` entry (`httpUrl`, `headers`) | `~/.gemini/settings.json` |
| **mux** | appends a `docmon` object to the `servers` array (`transport: http`, base `url`, `mcpPath`, bearer `auth`) | `~/.mux/mcp-servers.json` |

Every request carries `Authorization: Bearer <DOCMON_BEARER_TOKEN>`. The token grants full control of the Docker
machine and is written in plain text to the agent's config, so use HTTPS for remote servers. On Windows the
config is edited with PowerShell; on macOS/Linux with `python3` (required). The `claude` scripts require the
`claude` CLI on `PATH`.

## Overriding the defaults

Pass the token as the first argument, or set environment variables before running:

| Variable | Default | Applies to |
| --- | --- | --- |
| `DOCMON_MCP_URL` | `http://127.0.0.1:8080/mcp` | claude, codex, cursor, gemini |
| `DOCMON_MCP_BASE_URL` | `http://127.0.0.1:8080` | mux (path is `/mcp`) |
| `DOCMON_BEARER_TOKEN` | `docmonadmin` | all (argument #1 overrides it) |
| `DOCMON_CODEX_CONFIG` / `DOCMON_CURSOR_CONFIG` / `DOCMON_GEMINI_CONFIG` / `DOCMON_MUX_CONFIG` | the paths above | override a config file location |

Example (macOS/Linux), connecting to a remote server:

```sh
DOCMON_MCP_URL=http://docker-host:8080/mcp sh scripts/linux/install-claude.sh "$DOCMON_BEARER_TOKEN"
DOCMON_MCP_BASE_URL=http://docker-host:8080 sh scripts/linux/install-mux.sh "$DOCMON_BEARER_TOKEN"
```
