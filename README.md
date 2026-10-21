<div align="center">

<img src="assets/logo.png" alt="Docmon" width="192" height="192" />

# Docmon

**A terminal UI, web dashboard, REST API, and MCP server for your Docker machine.**

Docmon puts containers, live metrics, image freshness, and day-to-day operations
one screen away on Windows, Linux, and macOS. Use the local TUI without
authentication, or the authenticated React dashboard, REST API, and MCP tools
for remote operators and agents.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)

**v0.1.1 — Alpha.** This is an early release under active development; features,
behavior, and APIs are subject to change.

</div>

## What it does

Docmon connects to the Docker daemon on the machine it runs on and gives you a
single console for the things you actually do all day:

- **See everything running.** Containers with image, tag, short/full ID, exposed
  ports, state, health, uptime, and live CPU/memory — in a sortable table.
- **Watch performance over time.** CPU, memory, network, and block I/O plotted
  as charts for the whole deployment or for one container.
- **Know when images are stale.** Docmon compares your local image digests
  against the registry and flags what has an update waiting.
- **Group by compose project.** If you live in `compose.yaml` files, Docmon
  understands stacks: it groups containers by project and drives stack-level
  actions.
- **Operate.** Start, stop, restart, kill, pause, and remove containers; pull
  images; prune; and apply an update with one action.
- **Get inside a container.** Run a one-shot command with output captured
  in-pane, transfer files in and out, or open a full interactive shell.

## Web dashboard and agent access

### Running in Docker

The container monitors Docker through the mounted /var/run/docker.sock.

Compose mounts the host socket into the backend. Docmon sends Docker Engine API
requests through that socket to the host's Docker daemon for container state,
resource statistics, logs, exec, images, and lifecycle operations. The dashboard's
“All Docker workloads” view sums container metrics; it does not measure unrelated
host processes or provide host-wide OS monitoring. With Docker Desktop, the daemon
and Linux containers run inside Docker Desktop's Linux VM.

```bash
# docker/.env is included; default bearer token: docmonadmin
docker compose -f docker/compose.yaml up -d
```

Compose pulls `jchristn77/docmon-server:v0.1.0` and `jchristn77/docmon-dashboard:v0.1.0`. To build and publish both architectures, run `./build-all.sh v0.1.0` (or `build-all.bat v0.1.0`); see [image build instructions](docker/README.md#build-and-publish-images).

Open **http://127.0.0.1:8088** and enter the configured token. The dashboard
covers containers, images, Compose stacks, live metrics/history, Docker events,
request history, logs, commands, interactive shells, host and browser file
transfers, registry tags, and image update checks. It includes light/dark themes,
locale persistence, sortable/filterable tables, and a live OpenAPI explorer.

- REST: `http://127.0.0.1:8080/api/v1` or the dashboard origin `/api/v1`.
- Swagger: `/swagger`; OpenAPI metadata: `/openapi.json`.
- MCP: `/mcp` on the same public API/dashboard origin, powered by Voltaic.
- Every control/read API and MCP request requires `Authorization: Bearer <token>`.
  Health and OpenAPI/Swagger documentation are anonymous.
- The token grants full control of the Docker machine. Use HTTPS for remote access.

See [docker/README.md](docker/README.md) for networking, ports, token rotation,
Compose file mounts, and persistence. The stack runs only the backend and dashboard.
[REST_API.md](REST_API.md), [MCP_API.md](MCP_API.md), and the
[root Postman collection](Docmon.postman_collection.json) document the full agent surface.

### Run from source

Requires the .NET 10 SDK and Node.js 22 or newer for the dashboard:

```bash
export DOCMON_BEARER_TOKEN="$(openssl rand -hex 32)"
dotnet run --project src/Docmon.Server -f net10.0
# In another terminal:
cd dashboard
npm ci
npm run dev
```

The server binds `127.0.0.1:8080` by default and the dashboard dev server uses
`http://127.0.0.1:5173`. Configure `DOCMON_HOST`, `DOCMON_PORT`,
`DOCMON_MCP_PORT`, `DOCKER_HOST`, `DOCMON_DATA_DIRECTORY`, and optionally
`DOCMON_ALLOWED_ORIGIN` for direct cross-origin clients. Docker and MCP connections
are created once and shared. Docker may recover after a daemon restart without
restarting Docmon. JSON REST operations have a 30-minute timeout.

### Local and remote TUI

Running `docmon` uses the local Docker socket or Windows named pipe, with no
bearer token and no dependency on the web server. It deliberately ignores
`DOCKER_HOST` in local TUI mode. Opt in to remote access explicitly:

```bash
docmon --server https://your-machine --token "your-remote-machine-token"
```

The `--token` option overrides `DOCMON_BEARER_TOKEN`. You can still set that
environment variable and omit `--token`. Local TUI mode needs no token.

Remote mode implements the same service interfaces through the authenticated
API, including archive transfers and interactive WebSocket shells. Transfers use
the TUI client's filesystem. Remote image pulls and exec report bounded output
when the operation completes; monitoring polls the API every two seconds.
Terminal resize negotiation is not implemented yet. Compose operations run on the
server and resolve the server's discovered compose files.

## Screens

Docmon is organized into sections you move between with `Tab` / `Shift+Tab` or
the number keys `1`–`6`:

| # | Screen | What you get |
|---|---|---|
| 1 | **Containers** | The master table plus a live detail pane with inline CPU/memory charts |
| 2 | **Stacks** | Compose projects, their services, and stack-level actions |
| 3 | **Metrics** | Full-size CPU / memory / network / disk charts, overall or per container |
| 4 | **Images** | Local images, registry digests, and update status |
| 5 | **Events** | A live stream of Docker daemon events |
| 6 | **Tools** | Disk usage, prune, and housekeeping |

## Requirements

- The **.NET 8 or .NET 10 SDK** (or the matching runtime, if you install the
  packaged tool). See [Installing the .NET SDK](#installing-the-net-sdk) below.
- A reachable **Docker daemon** on the same host:
  - Windows: the `npipe://./pipe/docker_engine` named pipe (Docker Desktop).
  - Linux/macOS: the `unix:///var/run/docker.sock` socket. The backend honors `DOCKER_HOST` to
    override.
- The `docker` CLI on `PATH` — used only for the interactive shell and TTY exec.

Docmon runs in any modern terminal. For the best rendering, use a terminal with
truecolor and a font that includes box-drawing and block glyphs (Windows
Terminal, iTerm2, most Linux terminals).

## Install

### Installing the .NET SDK

Docmon needs the **.NET 8 or .NET 10 SDK**. Download it from Microsoft:

- All versions: <https://dotnet.microsoft.com/download>
- .NET 10 SDK: <https://dotnet.microsoft.com/download/dotnet/10.0>
- .NET 8 SDK: <https://dotnet.microsoft.com/download/dotnet/8.0>
- Linux, per-distro instructions: <https://learn.microsoft.com/dotnet/core/install/linux>

Quick install by platform:

```bash
# Windows (winget)
winget install Microsoft.DotNet.SDK.10

# macOS (Homebrew)
brew install --cask dotnet-sdk

# Linux (Microsoft install script; installs into ~/.dotnet)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
```

Confirm it is on your `PATH`:

```bash
dotnet --version
```

### Installing Docmon

Docmon ships as a **.NET global tool** named `docmon`. Once installed you can run
`docmon` from any directory.

```bash
dotnet tool install --global Docmon
docmon
```

Global tools install under `~/.dotnet/tools`; if `docmon` is not found, add that
directory to your `PATH` (the tool installer prints the exact path to use).

Building and installing from source:

```bat
git clone https://github.com/jchristn/Docmon.git
cd Docmon
REM Windows convenience scripts (pack + install the 'docmon' command).
REM Both accept an optional target framework: net8.0 or net10.0.
install-tool.bat                 REM auto-selects net10.0 if a .NET 10 SDK is present, else net8.0
install-tool.bat net8.0          REM force .NET 8
reinstall-tool.bat net10.0       REM rebuild and replace an existing install
remove-tool.bat                  REM uninstall
```

The application is multi-targeted for **net8.0** and **net10.0**; the framework
argument selects which runtime the global tool is installed against.

On Linux/macOS the same thing by hand (pick a framework you have installed):

```bash
dotnet pack src/Docmon.App/Docmon.App.csproj -c Release -p:TargetFrameworks=net8.0 -o ./nupkg
dotnet tool install --global --add-source ./nupkg --framework net8.0 Docmon
```

## Usage

```
docmon                 # connect to the local Docker daemon and open the TUI
docmon --no-splash     # skip the startup splash screen
docmon --version       # print version and exit
docmon --help          # print usage and exit
```

Common keys once you're in:

```
Tab / Shift+Tab   move between panes            1..6   jump to a screen
↑ ↓ PgUp PgDn      navigate a table/pane         /      search / filter
Enter              inspect selected              Ctrl+P command palette
s / x / l          shell / exec / logs           F1 / ? help
r S K p            restart / stop / kill / pause  u / U check updates / pull+apply
t                  transfer files                Ctrl+Q quit
```

## How it works

Docmon talks to Docker two ways. Most data and control goes through the Docker
Engine API using [Docker.DotNet](https://github.com/dotnet/Docker.DotNet):
listing, inspecting, stats streaming, lifecycle, image pulls, and archive-based
file transfer. Interactive work that needs a real terminal — opening a shell or
an `exec -it` session — shells out to the `docker` CLI, because a TUI pane can't
host a pseudo-terminal. For those, Docmon suspends its own UI, hands the terminal
to the child process, and restores the dashboard when you exit.

Web and remote-TUI terminals use an authenticated Watson WebSocket bridge to a
Docker exec connection with HTTP Upgrade.

The code is split into a `Docmon.Core` library (models, shared runtime, Docker
services, remote HTTP adapter, and registry abstraction), a `Docmon.Server`
Watson 7 API/Voltaic MCP host, a React `dashboard/`, and a `Docmon.App` executable
that hosts the
[TUIKit](https://www.nuget.org/packages/TUIKit/) interface. Update checks go
through an `IRegistryProvider` interface; Docker Hub ships first, and other
registries slot in behind the same interface.

## Dashboard and API conventions

The dashboard uses the Navy & Silver palette derived from the icon, compact navigation,
tabbed container/host-workload metrics with labeled interactive charts, sortable tables,
viewport-safe operation menus, a request inspector, and an OpenAPI endpoint selector.
See [dashboard internationalization](dashboard/I18N.md) for all twelve supported languages.
The host metrics view aggregates Docker container workloads, excluding other host processes.

Collection REST reads and MCP tools use `EnumerationQuery` and `EnumerationResult<T>`:
page through `objects` using `continuationToken` until `endOfResults` is true. This replaces
bare-array responses. Detail reads and actions retain their resource/result shapes.
The dashboard and remote TUI follow every page automatically. See [REST_API.md](REST_API.md)
for paging, ordering, filtering, and query-bound continuation tokens.

## Building

```bash
dotnet build src/Docmon.slnx -c Release
dotnet test  src/Docmon.slnx -c Release
```

The build treats warnings as errors and generates XML documentation.

## Testing

Tests are written once as [Touchstone](https://github.com/jchristn/touchstone)
descriptors in `src/Test.Shared` and run through any of three runners:

| Project          | Runner                                  | Command                                              |
|------------------|-----------------------------------------|------------------------------------------------------|
| `Test.Automated` | Touchstone console runner               | `dotnet run --project src/Test.Automated -f net10.0` |
| `Test.Xunit`     | xUnit (fact-style and per-case theory)  | `dotnet test src/Test.Xunit`                         |
| `Test.Nunit`     | NUnit (fact-style and per-case source)  | `dotnet test src/Test.Nunit`                         |

Add `-- --results results.json` to the console runner to export JSON results.

Most suites run offline (helpers, models, registry logic against an in-memory
HTTP handler, Docker payload mapping, argument validation, TUI state, and
off-screen rendering of the header, tabs, status bar, and screens through
TUIKit's testing surface). The
`Docker*` suites exercise a live daemon: they pull `alpine:3.20` if needed and
create uniquely named containers and compose projects labeled `io.docmon.test`,
which they always remove. They never touch existing containers or images, and
never prune. Those suites are skipped automatically when no daemon is
reachable, or explicitly with `DOCMON_TEST_SKIP_DOCKER=1`.

Source launchers are available for macOS/Linux and Windows:

```bash
./go.sh                         # alias for the local TUI
./run-tui.sh                    # build and run the TUI
./run-tui.sh net8.0 --no-splash  # choose a framework and forward app options
./run-server.sh                 # build and run the API/MCP server
```

Windows equivalents are `go.bat`, `run-tui.bat`, and `run-server.bat`.
All launchers work from any directory and default to .NET 10 when its SDK is
installed, otherwise .NET 8. Use `net8.0`, `net10.0`, or `--framework <version>`
to select explicitly. Server launchers preserve environment configuration and
use `docmonadmin` when `DOCMON_BEARER_TOKEN` is unset. Native server binding
still follows `DOCMON_HOST` (default `127.0.0.1`); Docker uses the included `.env`.

Request history offers last hour/day/week/month views, with respectively
60 one-minute, 96 fifteen-minute, 84 two-hour, and 90 eight-hour buckets.
Month means the rolling last 30 days. Charts use the retained journal, and
request details open in a dialog using 95% of the viewport.

## License

MIT — see [LICENSE.md](LICENSE.md). Copyright (c) 2026 Joel Christner.
