# Docmon

**A terminal UI, authenticated web dashboard, REST API, and MCP server for Docker.**

Docmon is a keyboard-driven, multi-pane TUI for your containers: live metrics,
image-update checks, lifecycle control, exec, file transfer, and an interactive
shell — on Windows, Linux, and macOS.

![Docmon](https://raw.githubusercontent.com/jchristn/Docmon/main/assets/logo.png)

**v0.1.1 — Alpha.** Early and under active development; features, behavior, and
APIs are subject to change.

## Use cases

- Watch what's running and how hard it's working, without leaving the terminal.
- Spot containers whose images have a newer version on the registry, and apply
  the update in one action.
- Manage compose stacks as a unit — group by project and drive stack-level
  start/stop/restart.
- Drop into a container's shell, run one-off commands, or move files in and out.

## Architecture

Docmon speaks to the local Docker daemon through the Docker Engine API
(Docker.DotNet) for data, metrics, and control, and shells out to the `docker`
CLI for interactive TTY work (shell and `exec -it`). The interface is built on
TUIKit. Update checks go through a registry-provider interface, with Docker Hub
supported first and other registries slotting in behind the same contract. The
shared `Docmon.Core` runtime powers the `Docmon.App` TUI and the Watson 7 `Docmon.Server` host. A React dashboard uses the same REST API; Voltaic provides MCP through the API server gateway.

## Docker dashboard and API

Clone the repository and use the included `docker/.env`. Edit
`DOCMON_BEARER_TOKEN` if you want to change the default `docmonadmin`. Run `docker compose -f docker/compose.yaml up -d`.
Compose uses `jchristn77/docmon-server:v0.1.0` and `jchristn77/docmon-dashboard:v0.1.0`.

Open `http://127.0.0.1:8088` and enter the bearer token. REST lives at `/api/v1`,
Swagger at `/swagger`, and MCP at `/mcp` on either the dashboard or API port
(`8080`). The same bearer token grants full Docker control through all remote
surfaces. The local TUI needs no authentication; `docmon --server <url>` enables
an authenticated remote TUI. Use HTTPS when accessing a machine remotely.

The stack runs the backend and dashboard, mounts the Docker socket, and persists
redacted request history. Both services have health checks. Compose lifecycle operations
also require original compose files mounted into the backend at their host paths.
See the repository's `docker/README.md`, `REST_API.md`, and `MCP_API.md` for the
complete deployment and interface contracts.

## Getting started

Docmon is distributed as a .NET global tool:

```bash
dotnet tool install --global Docmon
docmon
```

It needs a reachable Docker daemon on the same host (the Docker Desktop named
pipe on Windows, or `/var/run/docker.sock` on Linux/macOS; `DOCKER_HOST`
overrides), and the `docker` CLI on `PATH` for the interactive shell.

Run `docmon --help` for options. Source, full documentation, and the design
notes are at https://github.com/jchristn/Docmon.

## License

MIT. Copyright (c) 2026 Joel Christner.
