# Docmon — a TUI for managing & monitoring a Docker stack

> A multi-pane terminal UI, built in C# on **TUIKit**, for observing and
> operating the Docker deployment on the local host: containers, metrics,
> image freshness, exec, file transfer, and shell access.

---

## 1. Vision & scope

Docmon is a full-screen, keyboard-driven "mission control" for a Docker host.
Think **k9s / lazydocker**, but native .NET on TUIKit, tuned for a single local
host with room to grow.

**Locked-in decisions (from kickoff):**

| Decision | Choice | Consequence |
|---|---|---|
| Docker access | **Hybrid**: `Docker.DotNet` for data/control + shell out to `docker` CLI for interactive TTY | Rich typed data & streaming stats, plus a real shell/exec-TTY without an embedded PTY |
| Topology | **Local host only** | Connect over named pipe (`npipe://./pipe/docker_engine`) on Windows or `unix:///var/run/docker.sock`; no TLS/SSH plumbing in v1 |
| Control level | **Full lifecycle** | start/stop/restart/kill/pause/remove containers, pull images, prune, recreate |
| Registries | **Docker Hub first, extensible** | An `IRegistryProvider` interface with `DockerHubRegistryProvider` shipped in v1; GHCR/ECR/self-hosted providers slot in later with no call-site changes |
| Compose | **First-class** | You use `compose.yaml` heavily, so Docmon groups containers by compose project and reads the stack files (v1); compose-diff against registry follows |
| Shell UX | **Suspend/resume the TUI** | Chosen over a second terminal window — clean and self-contained (see §2) |
| Updates | **Visibility + one-click apply** | Surface "update available"; user action pulls the new image and recreates/restarts the container |

**Non-goals (v1):** remote/multi-host management, Kubernetes, Swarm
orchestration, building images from Dockerfiles. All are natural v2+ extensions
and the architecture leaves room for them.

**Governing conventions:** all C# code follows the house standards in
`c:\code\agents\requirements` — see **§12 Coding standards** for the concrete
rules this project commits to.

---

## 2. Why TUIKit fits (and the one gap)

TUIKit is a **retained-mode, concurrent, double-buffered** TUI framework —
purpose-built for the "many background threads write content while a diffing
renderer repaints" pattern, which is exactly a live monitoring dashboard.

What we lean on directly:

- **`TuiApplication` host** with a `Layout` of `Region`s — flat, explicitly
  positioned panes (dock helpers for the shell, explicit constraints for the
  grid). Matches the flagship `HarnessApp` demo almost 1:1.
- **Thread-safe `Pane`** — any background task can `WriteLine` into a log/monitor
  pane; the render loop coalesces at `TargetFps` (default 60). No manual
  invalidation. `PaneLineHandle` mutates a line in place (`pulling… → done`).
- **`DataTable<T>`** — sortable, virtualized, typed rows → the container list.
- **`Gauge`, `Sparkline`, `LineChart`, `BarChart`, `ProgressBar`,
  `MultiProgress`** → metrics panels and pull progress.
- **Command routing** (`app.Commands` + `RegisterCommand`, `KeyChord.Parse`,
  focus-scoped commands, two-key chords) + **`MenuBar`/`Menu`** + a
  `CommandRegistry` that feeds keybindings, menu bar, and a **command palette**
  (`Ctrl+P`) from one command list.
- **Modals**: `ConfirmAsync`, `PromptAsync`, `SelectAsync`, `ShowAsync<T>`,
  `FileSelectModal`, plus custom `DialogModal` subclasses. Focus-trapping,
  awaitable, typed results (marshal continuations back with `app.Post`).
- **Toasts** (`app.Notify`) for non-blocking success/error feedback.
- **Theming** (`Theme.Dark/Light/HighContrast`, named roles) + fluent `Text`
  styling and markup.
- **`FrameTimer.Every(ms, …)`** for tick-driven refresh of non-pane widgets.

**The one gap — no embedded PTY (decided: suspend/resume).** TUIKit has no
ConPTY/pseudo-console and no subprocess-into-a-pane, so an interactive shell or
`exec -it` cannot render *inside* a pane. **Chosen approach:** when you open a
shell, Docmon **suspends the whole TUI** (`app.Stop()`), hands the real terminal
to `docker exec -it <c> <shell>` attached to the console (a genuine, fully
functional shell), then **resumes** (`app.Start()`) when you `exit`, restoring
the dashboard exactly where you left it. This is why the hybrid CLI decision
matters. The trade-off — the dashboard is not visible *during* a shell session —
is accepted; it is how lazydocker/k9s behave. Non-interactive exec (run a
command, capture output) *does* render in-pane via `Docker.DotNet`'s exec +
`MultiplexedStream`, so quick one-shot commands never leave the UI.

---

## 3. High-level architecture

```
┌──────────────────────────────────────────────────────────────┐
│                         Docmon.App (TUIKit)                    │
│  Screens · Panes · Widgets · Command routing · Modals · Theme │
└───────────────┬───────────────────────────┬──────────────────┘
                │ view-models (Observable)   │ commands / actions
┌───────────────▼───────────────┐ ┌─────────▼──────────────────┐
│        Docmon.Core             │ │      Docmon.Services       │
│  ViewModels, state store,      │ │  DockerService  (DotNet)   │
│  polling/streaming coordinators│ │  StatsStreamer  (DotNet)   │
│  event bus, formatting         │ │  RegistryProvider (Hub 1st)│
└───────────────┬────────────────┘ │  ShellLauncher  (CLI)      │
                │                   │  ExecService    (DotNet)   │
                │                   │  TransferService(archive)  │
                │                   │  EventsMonitor  (DotNet)   │
                │                   └─────────┬──────────────────┘
                │                             │
        ┌───────▼─────────────────────────────▼───────┐
        │   Docker Engine API (npipe / unix socket)    │
        │   + `docker` CLI  +  Registry v2 HTTP APIs    │
        └──────────────────────────────────────────────┘
```

**Threading model:** all Docker I/O is async and off the UI thread. Streaming
sources (`GetContainerStatsAsync` with `Stream=true`, `MonitorEventsAsync`) push
into thread-safe view-models / panes. Polling sources (list refresh, update
checks) run on `FrameTimer`/`PeriodicTimer`. UI mutations that must touch widget
state marshal via `app.Post(...)`.

**Data flow example (metrics):** `StatsStreamer` opens one stats stream per
"watched" container → decodes CPU%/mem/net/block IO → updates an
`Observable<ContainerStats>` → the metrics pane's `Sparkline`/`Gauge` read it on
the next frame.

---

## 4. Tech stack

- **.NET 10** (TUIKit targets net8/net10 dependency-free; net10 chosen).
- **TUIKit** `0.8.4+` (NuGet `TUIKit`).
- **Docker.DotNet** (NuGet `Docker.DotNet`) — Engine API client.
- **System.Net.Http** + `System.Text.Json` — Registry v2 / Docker Hub clients.
- **`System.Formats.Tar`** (built-in .NET 7+) — tar packing/unpacking for the
  archive-based file transfer (no third-party dependency).
- The `docker` CLI on PATH — only for interactive TTY (shell / `exec -it`).

Project layout — follows the house rule that **all source lives under `src/`**,
**one class/enum per file**, with a `Core` library and a thin executable host
(mirrors the `ProjectName.Core` / `ProjectName.Server` split, adapted for a TUI
where the host is the app rather than a web server):

```
project-root/
├─ src/
│  ├─ Docmon.sln
│  ├─ Docmon.Core/                # dependency-light domain + services library
│  │  ├─ Docmon.Core.csproj
│  │  ├─ Constants.cs             # only for true constants; see §12 config rule
│  │  ├─ Enums/                   # UpdateStatusEnum, ContainerStateEnum, …
│  │  ├─ Models/                  # ContainerRow, ContainerStats, PortMap, ImageRow, ComposeStack…
│  │  ├─ ViewModels/              # observable state objects bound to the UI
│  │  ├─ Services/
│  │  │  ├─ Interfaces/           # IDockerService, IStatsStreamer, IEventsMonitor,
│  │  │  │                        #   IRegistryProvider, IExecService, ITransferService,
│  │  │  │                        #   IShellLauncher, IComposeService
│  │  │  └─ Implementations/      # DockerService, StatsStreamer, EventsMonitor,
│  │  │                           #   ExecService, TransferService, ShellLauncher, ComposeService
│  │  ├─ Registries/              # registry-provider abstraction (see §4.5)
│  │  │  ├─ IRegistryProvider.cs
│  │  │  ├─ RegistryProviderFactory.cs
│  │  │  └─ DockerHub/            # DockerHubRegistryProvider + client + models
│  │  └─ Helpers/                 # formatting (bytes, uptime), CPU% math, guards
│  └─ Docmon.App/                 # TUIKit host executable
│     ├─ Docmon.App.csproj
│     ├─ Program.cs               # composition root / DI wiring
│     ├─ DocmonApp.cs             # builds Layout, panes, commands, theme
│     ├─ Screens/                 # ContainersScreen, MetricsScreen, ImagesScreen,
│     │                           #   StacksScreen, EventsScreen, one file each
│     ├─ Widgets/                 # app-specific composite widgets
│     └─ Commands/                # command registry + key map
├─ test/
│  └─ Docmon.Test/                # service + parser tests, headless TUIKit snapshots
├─ .gitignore
├─ .dockerignore                  # only if we ship a container image
├─ README.md
├─ CHANGELOG.md
├─ LICENSE.md                     # MIT
└─ DOCMON.md                      # this document
```

### 4.5 Registry-provider abstraction (extensible by design)

You use Docker Hub today and expect other registries later, so update-checking
is built on an interface from day one and Docker Hub is merely the first
implementation. No feature code talks to a registry directly — it talks to
`IRegistryProvider`, and `RegistryProviderFactory` picks the right provider from
the image reference's registry host (`docker.io` → Docker Hub, `ghcr.io` → a
future GHCR provider, etc.).

```csharp
namespace Docmon.Core.Registries
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// Abstraction over a container image registry for resolving the remote
    /// digest/tags of an image so update availability can be determined.
    /// Implementations are selected per image by <see cref="RegistryProviderFactory"/>.
    /// Thread safety: implementations must be safe for concurrent use.
    /// </summary>
    public interface IRegistryProvider
    {
        /// <summary>
        /// Registry host this provider handles (for example "docker.io").
        /// </summary>
        string RegistryHost { get; }

        /// <summary>
        /// Resolves the remote manifest digest for a repository and tag.
        /// </summary>
        /// <param name="reference">Parsed image reference (host, repository, tag).</param>
        /// <param name="token">Token to observe for cancellation.</param>
        /// <returns>The remote digest, or null when the tag is absent.</returns>
        /// <exception cref="RegistryException">Thrown on registry transport or auth failure.</exception>
        Task<string?> ResolveDigestAsync(ImageReference reference, CancellationToken token);

        /// <summary>
        /// Lists available tags for a repository (newest first where supported).
        /// </summary>
        Task<IReadOnlyList<string>> ListTagsAsync(ImageReference reference, CancellationToken token);
    }
}
```

- **v1:** `DockerHubRegistryProvider` (Registry v2 + `auth.docker.io` anonymous
  bearer token). Credentials, when needed, come from a pluggable
  `IRegistryCredentialSource` (v1 supports anonymous + `~/.docker/config.json`;
  a Docmon config file source can follow).
- **Later, no call-site changes:** `GitHubContainerRegistryProvider`,
  `EcrRegistryProvider`, `GenericV2RegistryProvider` for self-hosted (Harbor,
  Nexus, distribution). Each is one file behind the same interface, registered
  in the factory.

---

## 5. UI design — layout & panes

Docmon is a **screen-per-domain** app with a persistent shell (menu bar top,
status bar bottom, context/help). The default screen is the **Dashboard**.

### 5.1 App shell (always present)

```
┌ Docmon ─────────────────────────────────────── host: localhost · 14 running ┐
│ Containers  Stacks  Metrics  Images  Events  Tools  [F1 Help] [Ctrl+P Palette]│  ← MenuBar
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│                          (active screen content)                             │
│                                                                              │
├──────────────────────────────────────────────────────────────────────────────┤
│ ↑↓ move  ⏎ inspect  s shell  x exec  l logs  r restart  u check-updates  ? help│  ← StatusBar (context)
└──────────────────────────────────────────────────────────────────────────────┘
```

- **MenuBar** (`MenuBar`/`Menu`) — top-level domains + global actions. Every
  menu item is also a routed command, so the palette and shortcuts stay in sync
  via a single `CommandRegistry`.
- **StatusBar** — context-sensitive key hints for the focused pane; host name;
  running/total counts; a spinner while a background op is in flight.
- **Command palette** (`Ctrl+P`, `FuzzyList<Command>`) — fuzzy access to every
  action.
- **Toasts** — `app.Notify(...)` for "Pulled nginx:latest", "Restart failed", etc.

### 5.2 Dashboard / Containers screen (default)

Master–detail: a sortable table on the left/top, a live detail pane on the
right, and a slim host-metrics strip.

```
┌ Containers (14 running / 17 total) ──────────────────┐┌ Detail: web-api ───────────────┐
│ NAME          IMAGE          TAG     STATE   CPU  MEM ││ ID     3f9a1c…  (full on ⏎)     │
│▶web-api       ghcr/acme/api  1.4.2   up 3d   12%  340M││ Image  ghcr.io/acme/api:1.4.2   │
│ postgres      postgres       16.2    up 3d    3%  1.2G││ Created 2026-08-20 09:14        │
│ redis         redis          7.2     up 3d    1%   90M││ Status  Up 3 days (healthy)      │
│ nginx         nginx          1.27 ⇧  up 3d    2%   28M││ Ports   0.0.0.0:8080→80/tcp      │
│ worker        ghcr/acme/wkr  1.4.2   up 3d    8%  210M││         0.0.0.0:8443→443/tcp     │
│ grafana       grafana        11.1 ⇧  up 1d    4%  120M││ Mounts  /data → pgdata (vol)     │
│ …                                                     ││ Health  ✓ 200 OK  (30s interval) │
│                                                       ││ ┌ CPU ───────────────────────┐  │
│  ⇧ = newer image available                            ││ │▁▂▅▇▆▄▂▁▂▃▅  12%             │  │
│                                                       ││ └────────────────────────────┘  │
│                                                       ││ ┌ MEM 340M/512M ────────────┐  │
│                                                       ││ │███████████░░░░░  66%        │  │
│                                                       ││ └────────────────────────────┘  │
└───────────────────────────────────────────────────────┘└─────────────────────────────────┘
┌ Host ── CPU ▁▂▃▅▇ 34% · MEM 11.4/32G · Net ↓2.1MB/s ↑340KB/s · Disk 41% · 14 up · 0 unhealthy ┐
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

- Left: **`DataTable<ContainerRow>`** — columns sortable (CPU/MEM/name/uptime);
  the `⇧` glyph marks "update available" (populated lazily via `IRegistryProvider`).
  Color by state (green up / yellow paused / red exited / dim created). Rows can
  be **grouped by compose project** (toggle) using the
  `com.docker.compose.project` label — see the Stacks screen (§5.7).
- Right: **detail pane** — `DefinitionList` for facts + inline `Gauge`/`Sparkline`
  fed by the stats stream for the selected row. Enter → full inspect modal/screen.
- Bottom: **host strip** — aggregate metrics (`Sparkline` + text), refreshed on a
  timer.
- Row actions (context keys, also in a right-click/`Enter` action menu via
  `ActionListView` semantics): `s` shell, `x` exec, `l` logs, `r` restart,
  `S` stop, `K` kill, `p` pause, `u` check updates, `U` pull+apply,
  `t` transfer files, `d`/`Del` remove (guarded by `ConfirmAsync`).

### 5.3 Metrics screen (deep dive)

Toggle between **overall deployment** and **single container**.

```
┌ Metrics · [Overall ▾]  range: [1m 5m 15m]  ──────────────────────────────────┐
│ ┌ CPU % (stacked by container) ───────────┐┌ Memory (MB) ───────────────────┐│
│ │        ╭╮        ╭─╮                     ││   ▁▂▃▄▅▆▇█ per-container bars    ││
│ │  ╭─╮╭──╯╰──╮╭────╯ ╰─╮   LineChart       ││   BarChart                      ││
│ │──╯ ╰╯      ╰╯        ╰──                  ││                                 ││
│ └──────────────────────────────────────────┘└─────────────────────────────────┘│
│ ┌ Network I/O ↓/↑ ────────────────────────┐┌ Block I/O r/w ─────────────────┐│
│ │  LineChart (dual series)                 ││  LineChart (dual series)        ││
│ └──────────────────────────────────────────┘└─────────────────────────────────┘│
│ Top consumers:  1) postgres 1.2G  2) web-api 340M  3) worker 210M  …           │
└───────────────────────────────────────────────────────────────────────────────┘
```

- **Overall mode:** aggregate across all running containers (sum CPU, sum mem,
  sum net/block IO) + a "top consumers" leaderboard.
- **Container mode:** four `LineChart`s for the selected container with a
  rolling window (ring buffer per metric; 1/5/15-min ranges).
- Data source: **one persistent stats stream per watched container**
  (`GetContainerStatsAsync`, `Stream=true`). Docmon computes CPU% the standard
  way (delta cpu / delta system × online CPUs × 100). To bound cost, only
  *visible/selected* containers stream at high frequency; the rest poll at a
  slow cadence (configurable).

### 5.4 Images screen (update management)

```
┌ Images / Update check ───────────────────────────────────────────────────────┐
│ REPOSITORY            TAG    LOCAL DIGEST   REGISTRY DIGEST  STATUS   SIZE     │
│ ghcr.io/acme/api      1.4.2  sha256:3f..    sha256:3f..      current  180MB    │
│ nginx                 1.27   sha256:9a..    sha256:c1..      UPDATE ⇧ 142MB    │
│ grafana               11.1   sha256:7d..    sha256:e8..      UPDATE ⇧ 410MB    │
│ postgres              16.2   sha256:aa..    sha256:aa..      current  1.1GB    │
│ <none>                <none> sha256:12..    —                dangling  90MB    │
│                                                                               │
│ [u] recheck all   [Enter] details   [p] pull   [P] pull+apply     [x] prune  │
└───────────────────────────────────────────────────────────────────────────────┘
```

- For each image, the resolution goes through the **`IRegistryProvider`**
  abstraction (§4.5): Docmon reads the local digest (`RepoDigests`) and asks the
  provider for the remote manifest digest for that repo:tag. Mismatch ⇒ update
  available. v1 ships `DockerHubRegistryProvider`; the registry host on the image
  ref selects the provider via `RegistryProviderFactory`.
- **Pull** shows a `MultiProgress` of layer downloads (from `CreateImageAsync`'s
  progress stream).
- **Pull + apply** (the update action you asked for): pull the new image, then
  recreate/restart the container. For **compose-managed** containers Docmon
  prefers `docker compose up -d <service>` (via the CLI) so the recreate honors
  the stack's declared config — this is the reliable path and, since you use
  compose heavily, the default. For standalone containers it falls back to
  stop → remove → recreate from inspect data, with an explicit "this is
  disruptive / config may be approximate" confirm.
- Prune dangling images (`ConfirmAsync` guard).

### 5.5 Events screen (live)

A streaming log of Docker daemon events (`MonitorEventsAsync`) — create, start,
die, health_status, pull, volume mount — written straight into a scrolling
`Pane` with severity colors, search (`/`), and pause/resume. Doubles as the app's
audit trail of actions Docmon itself performs.

### 5.6 Logs pane (per container)

`l` on a row opens container logs (`GetContainerLogsAsync`, follow=true) in a
scrolling searchable `Pane` with `PaneLineHandle` and tail/wrap toggles.

### 5.7 Stacks screen (compose — first-class)

Because you live in `compose.yaml`, Docmon treats a **stack** (a compose
project) as a top-level object. It discovers stacks two ways: (a) from running
containers grouped by the `com.docker.compose.project` /
`com.docker.compose.project.config_files` labels, and (b) by reading
`compose.yaml`/`docker-compose.yaml` files (a configurable set of known paths,
plus the config-file path Docker records in those labels).

```
┌ Stacks ───────────────────────────────────────────────────────────────────────┐
│ PROJECT        SERVICES  STATE          UPDATES   FILE                          │
│▶acme-platform  6/6 up    ● healthy      2 ⇧       ~/dev/acme/compose.yaml        │
│ monitoring     3/3 up    ● healthy      1 ⇧       ~/dev/mon/compose.yaml         │
│ scratch        0/2 up    ○ stopped      —         ~/tmp/scratch/compose.yaml     │
├─ acme-platform ▸ services ─────────────────────────────────────────────────────┤
│  SERVICE   IMAGE (running → declared)          STATE    UPDATE                   │
│  web-api   ghcr/acme/api:1.4.2                 up       current                  │
│  worker    ghcr/acme/wkr:1.4.2                 up       current                  │
│  nginx     nginx:1.27  (declared 1.27)         up       ⇧ 1.29 on registry       │
│  …                                                                              │
│ [Enter] expand  [u] up -d  [d] down  [r] restart  [P] pull+up  [e] edit file    │
└────────────────────────────────────────────────────────────────────────────────┘
```

- Stack-level lifecycle via the CLI: `docker compose up -d` / `down` /
  `restart` / `pull`, run through `IComposeService` (shell-out) with progress
  streamed into a pane and a confirm on `down`.
- **Three-way image view** per service: *running* vs *declared in compose* vs
  *latest on registry* — this is the compose-diff you get once compose-awareness
  and the registry provider are combined. (v1: running vs declared; registry
  column back-fills as checks complete.)
- `e` opens the compose file in the detail pane (read-only view in v1; external
  `$EDITOR` via suspend/resume is a fast-follow).

---

## 6. Feature breakdown (the six asks + how)

### ① List containers, versions, ports, images, IDs
- `Containers.ListContainersAsync(All=true)` → `DataTable<ContainerRow>`.
- Row: name, image repo+tag, short/full ID, state+health, uptime, ports
  (`PortBindings` formatted `host→container/proto`), CPU/MEM (from stats),
  update flag. Enter → full **Inspect** view (env, mounts, networks, labels,
  restart policy, entrypoint/cmd) via `ContainerInspectAsync`.

### ② Monitor performance — overall or per container
- `StatsStreamer` (§5.3). Overall = aggregate; per-container = selected row.
- Widgets: `Gauge` (mem vs limit), `Sparkline` (inline trends), `LineChart`
  (deep-dive), `BarChart` (top consumers). Ring buffers per metric for ranges.

### ③ Check Docker Hub / registry for updates
- **`IRegistryProvider` abstraction** (§4.5), v1 implementation
  `DockerHubRegistryProvider`: anonymous bearer token from `auth.docker.io`,
  then compare local (`RepoDigests`) vs remote manifest digest. Batched, cached,
  rate-limit-aware; runs off-thread and back-fills the `⇧` flags on the
  container/stack/image tables. Future providers (GHCR, ECR, self-hosted) drop
  in behind the same interface with no call-site changes.
- **Apply the update:** notification/visibility everywhere, plus the
  **pull + apply** action (§5.4) — pull the newer image and recreate/restart the
  container, compose-aware where applicable.

### ④ Execute commands in a container (non-interactive)
- `ExecService` via `Docker.DotNet`: `ExecCreateContainerAsync` (Cmd, AttachStdout/Stderr) →
  `StartAndAttachContainerExecAsync` → read `MultiplexedStream` → write to an
  output `Pane`. A `PromptModal` collects the command; output streams live;
  exit code shown in a toast. No TTY suspension needed — renders in-pane.

### ⑤ Transfer data in/out of a container
- `TransferService` using the archive API:
  - **Out:** `GetArchiveFromContainerAsync(id, path)` → tar stream → unpack to a
    host path chosen via `FileSelectModal`.
  - **In:** pick host file(s) via `FileSelectModal` → tar them
    (`System.Formats.Tar`) → `ExtractArchiveToContainerAsync(id, destPath, tar)`.
  - Progress via `ProgressBar`; a small **dual FileBrowser** ("host ↔ container")
    is a stretch enhancement (container-side browse = `exec ls`/stat).

### ⑥ Open a shell into a container
- `ShellLauncher` (CLI, the PTY workaround): detect an available shell
  (`bash`→`sh` fallback), `app.Stop()` to release the terminal, run
  `docker exec -it <id> <shell>` attached to the real console, `app.Start()` on
  exit, then `Notify` "shell session ended". Interactive `exec -it <cmd>` uses
  the same suspend/resume path.

---

## 7. Ideas beyond the six (prioritized)

**High value, low effort**
- **Global search / jump** (`/`) across containers by name/image/label.
- **Quick actions menu** per row (`ActionListView`) so newcomers don't need to
  memorize keys.
- **Health badges** and an "unhealthy" filter; surface failing healthchecks.
- **Copy to clipboard** (container ID, image ref, `docker` command equivalents)
  via `SystemClipboard`.
- **"Show the docker command"** for any action — teaches + builds trust before
  destructive ops.
- **Confirmation guards** on stop/kill/remove/prune (`ConfirmAsync`), with a
  "don't ask again this session" option.

**High value, medium effort**
- **Compose/stack awareness** — *promoted to a v1 first-class feature* (§5.7):
  grouping by `com.docker.compose.project`, stack lifecycle, running-vs-declared
  image diff.
- **Log multiplexing** — tail several containers (e.g. all services in a stack)
  into one merged, color-keyed pane.
- **Volumes & networks screens** — list, inspect, prune; show which containers
  use each.
- **Port map view** — everything the host exposes, at a glance (conflict/alerts).
- **Restart-on-update workflow** — one action: check → pull → recreate, with a
  dry-run diff first.
- **Disk usage** (`df`-style: images/containers/volumes/build cache) + reclaim.

**Nice to have / v2+**
- **Alerts & thresholds** — toast/log when CPU/mem/restart-count crosses a
  limit; restart-loop detection.
- **Session recording / action log** persisted to disk (audit).
- **Config profiles & themes** — `Theme.Dark/Light/HighContrast`, saved layout.
- **Remote hosts** — the topology extension (TCP/TLS, SSH tunnel), host switcher.
- **Snapshot/export** — dump inspect + stats to JSON/Markdown for a bug report.
- **Container "top"** (`ContainerProcessesAsync`) — process list inside a pane.

---

## 8. Data & service sketch

> **Illustrative only.** The shapes below communicate intent compactly. The
> *actual* implementation follows §12: **one type per file**, **no positional
> records** for models that need validation (use classes with explicit
> properties + backing fields, guard clauses, and XML docs), enums suffixed
> `Enum`, and every async method takes a `CancellationToken`. UI binding uses
> TUIKit's `Observable<T>` (namespace `TUIKit.Reactive`), not raw `IObservable`.

Models (final form = explicit properties per §12):

```
ContainerRow    : Id, Name, Image, Tag, State, Health, CreatedUtc, Uptime,
                  Ports (IReadOnlyList<PortMap>), Update (UpdateStatusEnum),
                  ComposeProject, ComposeService
ContainerStats  : CpuPercent, MemUsage, MemLimit, NetRx, NetTx,
                  BlkRead, BlkWrite, AtUtc
PortMap         : HostIp, HostPort, ContainerPort, Protocol
ImageRow        : Repo, Tag, LocalDigest, RemoteDigest?, SizeBytes,
                  Status (UpdateStatusEnum), Dangling
ImageReference  : RegistryHost, Repository, Tag        // parsed image ref
ComposeStack    : Project, ConfigFilePath, Services (IReadOnlyList<ComposeService>)
ComposeService  : Name, DeclaredImage, RunningImage, State, Update (UpdateStatusEnum)

UpdateStatusEnum   { Unknown, Checking, Current, UpdateAvailable, Error }
ContainerStateEnum { Created, Running, Paused, Restarting, Exited, Dead }
```

Service interfaces (all async, cancellation-aware, `ConfigureAwait(false)`
internally):

```csharp
interface IDockerService {                     // Docker.DotNet
    Task<IReadOnlyList<ContainerRow>> ListContainersAsync(CancellationToken token);
    Task<ContainerInspectResponse> InspectAsync(string id, CancellationToken token);
    Task StartAsync(string id, CancellationToken token);
    Task StopAsync(string id, CancellationToken token);
    Task RestartAsync(string id, CancellationToken token);
    Task KillAsync(string id, CancellationToken token);
    Task PauseAsync(string id, CancellationToken token);
    Task RemoveAsync(string id, bool force, CancellationToken token);
    Task<IReadOnlyList<ImageRow>> ListImagesAsync(CancellationToken token);
    IAsyncEnumerable<JSONMessage> PullAsync(string image, AuthConfig? auth, CancellationToken token);
}
interface IStatsStreamer  { Observable<ContainerStats> Watch(string id); }
interface IEventsMonitor  { Observable<DockerEventMessage> Events { get; } }
interface IExecService    { IAsyncEnumerable<string> RunAsync(string id, string[] command, CancellationToken token); }
interface ITransferService{ Task CopyOutAsync(string id, string containerPath, string hostPath, CancellationToken token);
                            Task CopyInAsync(string id, string hostPath, string containerPath, CancellationToken token); }
interface IShellLauncher  { Task<int> OpenShellAsync(TuiApplication app, string id, CancellationToken token); } // Stop/Start around CLI
interface IComposeService { Task<IReadOnlyList<ComposeStack>> DiscoverAsync(CancellationToken token);
                            IAsyncEnumerable<string> UpAsync(string project, string? service, CancellationToken token);
                            IAsyncEnumerable<string> DownAsync(string project, CancellationToken token); }
// Registry access is via IRegistryProvider / RegistryProviderFactory — see §4.5.
```

---

## 9. Key map (draft)

| Key | Scope | Action |
|---|---|---|
| `F1` / `?` | global | Help overlay |
| `Ctrl+P` | global | Command palette |
| `Ctrl+Q` / `Ctrl+C×2` | global | Quit (DoubleTapToExit) |
| `Tab` / `Shift+Tab` | global | Cycle pane focus |
| `1..6` | global | Jump to screen (Containers/Stacks/Metrics/Images/Events/Tools) |
| `↑ ↓` `PgUp/Dn` | table/pane | Navigate |
| `/` | table/pane | Search/filter |
| `Enter` | table | Inspect |
| `s` / `x` / `l` | row | Shell / Exec / Logs |
| `r` `S` `K` `p` | row | Restart / Stop / Kill / Pause |
| `u` / `U` | row/images | Check updates / Pull+apply |
| `t` | row | Transfer files |
| `d` / `Del` | row | Remove (confirm) |
| `Ctrl+K Ctrl+T` | global | Cycle theme |

All defined through one `CommandRegistry` so shortcuts, menu bar, and palette
never drift.

---

## 10. Milestones / roadmap

**M0 — Skeleton (spike).** Solution, DI, connect to local Docker, TUIKit host
booting the app shell (menu bar + status bar + empty dashboard). Prove
`Docker.DotNet` connectivity and one live `Pane`.

**M1 — Containers screen.** `DataTable` list + detail pane + inspect modal;
lifecycle actions (start/stop/restart/kill/pause/remove) with confirm guards and
toasts. *(Delivers ①, most of full-lifecycle.)*

**M2 — Metrics.** `StatsStreamer`; inline gauges/sparklines on the dashboard;
deep-dive Metrics screen (overall + per-container, ranges). *(Delivers ②.)*

**M3 — Exec, shell, transfer.** In-pane exec; suspend/resume shell; archive
file transfer with FileSelect modals. *(Delivers ④⑤⑥.)*

**M4 — Images & updates.** Images screen; `IRegistryProvider` +
`DockerHubRegistryProvider` + `RegistryProviderFactory`; `⇧` flags on the
container table; pull with `MultiProgress`; **pull + apply**; prune.
*(Delivers ③ and the update-apply action.)*

**M5 — Stacks & compose (first-class).** `IComposeService` discovery (labels +
`compose.yaml` files); Stacks screen; container grouping toggle; stack lifecycle
(`up -d`/`down`/`restart`/`pull`); running-vs-declared image diff; compose-aware
pull + apply. *(Delivers the compose requirements.)*

**M6 — Polish & extras.** Events screen, log multiplexing, volumes/networks,
themes/config, alerts, disk-usage reclaim, snapshot/export. *(From §7.)*

Registry extensibility (additional `IRegistryProvider`s for GHCR/ECR/self-hosted)
lands whenever you actually adopt those registries — no roadmap milestone needed,
just a new file behind the existing interface.

---

## 11. Risks & open questions

**Risks / tricky bits**
- **CPU% math** must match Docker's own formula (system-delta × online CPUs);
  easy to get subtly wrong — unit-test against known samples.
- **Stats stream cost** — one stream per container is fine for a dozen; needs
  throttling/only-visible strategy at scale.
- **Named-pipe access on Windows** — Docmon may need to run elevated / in the
  `docker-users` group; surface a clear error if the pipe is unreachable.
- **Shell suspend/resume** — restoring the TUIKit terminal state cleanly after a
  child process (esp. on odd exits / Ctrl+C inside the shell). TUIKit hardened
  Ctrl+C/terminal-restore in 0.8.1, but the suspend/resume dance around an
  external process is our code to get right.
- **Registry rate limits** (Docker Hub anonymous pulls) — cache digests, batch,
  honor `Retry-After`.
- **Pull + apply fidelity** — for standalone containers, recreating "the same"
  from inspect data is imperfect. Mitigated because your containers are mostly
  **compose-managed**, where `docker compose up -d <service>` recreates faithfully.
- **TUIKit is alpha (0.8.x)** — API churn possible; pin the version.

**Resolved decisions (from kickoff + follow-up)**
- Shell UX = **suspend/resume** the TUI around `docker exec -it` (§2).
- Registries = **`IRegistryProvider` interface, Docker Hub first**, others later
  with no call-site changes (§4.5).
- Compose = **first-class in v1** — grouping, discovery, stack lifecycle,
  running-vs-declared diff (§5.7, M5).
- Updates = **visibility + one-click pull + apply** (never silent/auto) (§5.4, ③).

**Remaining questions (not blocking M0)**
1. **Registry credential source (for the *future* private providers):** prefer
   auto-reading `~/.docker/config.json`, a dedicated Docmon config file, env
   vars, or on-demand prompt? (v1 Docker Hub is anonymous, so this is a
   forward-looking choice, not urgent.)
2. **Compose file discovery:** besides the paths Docker records in container
   labels, should Docmon scan a configurable list of directories for
   `compose.yaml`? If so, what roots (e.g. `~/dev`)?
3. **Persistence:** persist config/theme/known-compose-paths/action-history to
   disk in v1 (e.g. a `docmon.json`), or keep it stateless for now?
4. **Destructive-action policy:** confirm every stop/kill/remove/down, or offer a
   "power-user, no prompts this session" toggle?
5. **Auto-refresh cadence:** default poll intervals for list refresh and slow-
   path stats — sensible defaults (say 2s list / 1s visible-stats) OK, exposed in
   config per §12's "no magic constants" rule?

---

## 12. Coding standards & conventions

All C# in this repo **must** follow the house rules in
`c:\code\agents\requirements` (`CODE_STYLE.md` and the *Code Standards* /
*Project Structure* / *Repository Requirements* sections of
`BACKEND_ARCHITECTURE.md`). The rules that concretely shape Docmon:

**Language & formatting**
- **No `var`** — always the explicit type.
- **No tuples** — return a named type or set a property instead.
- `using` directives go **inside** the `namespace` block; Microsoft/System usings
  first (alphabetical), then others (alphabetical).
- **One class or one enum per file.** No nested/multiple top-level types; no
  `partial` classes. Enums are suffixed `Enum` (`UpdateStatusEnum`).
- Private fields are `_PascalCase` (e.g. `_DockerClient`, not `_dockerClient`).
- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`).
- Classic `using (...) { }` blocks, **not** `using` declarations.
- Regions (`Public-Members`, `Private-Members`, `Constructors-and-Factories`,
  `Public-Methods`, `Private-Methods`) are optional under 500 lines; use them
  consistently once a file grows.

**Members & documentation**
- **XML docs on every public** class, constructor, property, method, and enum;
  **no** docs on private members/methods. Document default/min/max values and
  their meaning; document nullability and thread-safety guarantees; document
  thrown exceptions with `<exception>` tags.
- Public properties that need range/null validation use **explicit get/set over a
  backing field** with guard clauses (`ArgumentNullException.ThrowIfNull`,
  `Math.Clamp`), not auto-properties.
- **No magic constants** for anything a user might tune (poll intervals, stats
  window sizes, cache TTLs, registry timeouts) — expose a public property with a
  sensible default backing field.

**Async, cancellation, disposal**
- Every async method that does cancellable work **takes a `CancellationToken`**
  (unless the class already holds a `CancellationTokenSource` member) and checks
  it at sensible points.
- **`ConfigureAwait(false)`** on awaits in `Docmon.Core` / services (non-UI code).
  In `Docmon.App`, **do not** rely on a captured context to get back to the UI
  thread — marshal UI/widget mutations explicitly via **`app.Post(...)`** (this
  is the clean, framework-blessed pattern and sidesteps the ConfigureAwait/UI
  tension entirely).
- For `IEnumerable`-returning methods, also provide an **async variant** taking a
  `CancellationToken`.
- Implement **`IDisposable`/`IAsyncDisposable`** for anything holding the
  `DockerClient`, HTTP clients, streams, or subscriptions; full dispose pattern
  (`protected virtual void Dispose(bool)`), `using` blocks for disposables.

**Errors, null-safety, collections**
- **Specific exception types**, never bare `Exception`; meaningful messages with
  context; **custom domain exceptions** where useful — e.g. `DockerConnectionException`,
  `RegistryException`, `ContainerNotFoundException`.
- Guard-clause parameter validation at method entry; proactively eliminate
  possible null-deref paths.
- Prefer LINQ where it reads well; `.Any()` over `.Count() > 0`; `.FirstOrDefault()`
  + null check over `.First()`; `.ToList()` to avoid multiple enumeration.
- Thread safety: `Interlocked` for simple atomics; `ReaderWriterLockSlim` for
  read-heavy shared state (e.g. the stats ring buffers).
- **No `Console.WriteLine` in library code** (`Docmon.Core`) — all output goes
  through TUIKit panes/logging.

**Repository housekeeping** (per `REPOSITORY_REQUIREMENTS.md`)
- Source under `src/`, tests under `test/`.
- Ship `.gitignore`, `README.md` (kept accurate), `CHANGELOG.md`, and
  `LICENSE.md` (**MIT**). Add `.dockerignore` + `DOCKERHUB_README.md` **only if**
  we decide to distribute Docmon as a container image; use `.yaml` (not `.yml`)
  for any Docker/compose files we add.
- Compile clean: **zero errors and zero warnings** (treat warnings as errors).

---

*Next step:* on your go-ahead I can scaffold **M0** (solution + DI + Docker
connectivity + booting TUIKit shell, all conforming to §12) so we have a running
skeleton to iterate on.
