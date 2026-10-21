# Changelog

All notable changes to Docmon are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Persistent thirty-day metric history shared by the server and local/remote TUI, with hour/day/week/month controls in both interfaces, fixed-size buckets, gap rendering, and REST/MCP enumeration.
- Remote TUI `--token` option, with precedence over the existing environment-variable fallback.
- Cross-platform source launchers (`go`, `run-tui`, `run-server`) and complete retained-journal request summaries for hour/day/week/30-day windows.
- Watson 7 REST server with bearer authentication, typed route metadata, OpenAPI and Swagger.
- Colocated Voltaic MCP gateway with the same Docker operations and authentication.
- React operator dashboard with token login, resource control, live metrics, logs,
  interactive shells, transfers, request history, API explorer, themes and locale support.
- Shared runtime/application operations and opt-in authenticated remote TUI access.
- Docker Compose packaging with named server/dashboard images and cross-platform build/push helpers for amd64 and arm64.
- REST/MCP documentation, OpenAPI snapshot, Postman collections, backend and browser tests.

### Changed
- Metrics now accepts `/` to advance host/container scope and shows explicit next/previous key hints.
- TUI pull-and-apply now shows pull and Compose progress in a modal, reports failures and standalone limitations, handles Shift+u, and preserves status visibility with long shortcuts.
- TUI Metrics now matches all six dashboard metric charts, with full-width tabs, direct host/container selection, time/value axes, and keyboard sample inspection.
- TUI colors, explicit backgrounds, splash, dialogs, and status indicators now match the dashboard’s dark Navy & Silver palette.
- Denser dashboard rows, icon-only sign out after the community links, no idle chart hint, 95% viewport request inspector, and explicit Docker workload metric scope.
- Dashboard now uses Navy & Silver, compact navigation, topbar community links,
  tabbed and labeled metrics charts, taller request charts, request detail inspection,
  sortable tables, top-layer operation menus, and an endpoint dropdown above the API explorer.
- All twelve target languages have complete lazy-loaded catalogs, with locale persistence,
  RTL/pseudo-locale support, translation parity tests, and UI string checks.
- Collection REST/MCP responses now use EnumerationQuery/EnumerationResult paging,
  filtering, and ordering. Remote TUI, dashboard, OpenAPI, Postman, and docs follow the contract.
- Images retain all valid tags, skip placeholder tags, recover digest-only repositories,
  and show image IDs and explicit untagged labels in the dashboard.
- Local TUI composition now reuses the core runtime and pins its default connection to
  the local socket/named pipe. Remote access requires the explicit `--server` option.
- Docker image-pull progress reports synchronously so final progress messages are retained.

## [0.1.1] - 2026-10-03

### Changed
- Updated TUIKit from 0.8.4 to 1.2.0.
- Updated test dependencies: Touchstone.Core, Touchstone.Cli,
  Touchstone.XunitAdapter, and Touchstone.NunitAdapter (0.1.12 → 0.2.0);
  Microsoft.NET.Test.Sdk (17.14.1 → 18.10.1); coverlet.collector
  (6.0.4 → 10.1.0); xunit.runner.visualstudio (3.1.4 → 4.0.0); NUnit
  (4.3.2 → 5.0.0); NUnit.Analyzers (4.7.0 → 4.15.0); NUnit3TestAdapter
  (5.0.0 → 6.3.0).

### Added
- `Rendering` test suite that renders the header, tab bar, status bar,
  containers screen, and metrics screen off-screen through TUIKit's
  `Snapshot`/`WidgetTester`, covering text output, key-driven selection, empty
  data, and undersized surfaces, so TUIKit upgrades are guarded by tests.
- Touchstone-based test infrastructure: shared descriptors in `Test.Shared`
  executed by `Test.Automated` (console), `Test.Xunit`, and `Test.Nunit`,
  replacing the previous ad-hoc `test/Docmon.Test` runner. Coverage spans
  helpers, models, registry logic, Docker payload mapping, argument guards, TUI
  state, and live-daemon container, exec, transfer, stats, events, and compose
  workflows.

### Fixed
- Copying files out of a container failed with `EndOfStreamException` for every
  path; the archive is now spooled to a temporary file before extraction.
- Exec output glued an unterminated stdout fragment onto the next stderr line;
  stdout and stderr are now line-buffered separately.
- Docker Hub transport failures, timeouts, and malformed JSON escaped as raw
  exceptions instead of `RegistryException`, which aborted "recheck all" and
  could leave an image stuck in the Checking state. They now report `Error`.
- Pulling a digest-pinned reference (`name@sha256:...`) split the digest as if
  it were a tag.

## [0.1.0] - 2026-08-26

Initial alpha.

### Added
- Multi-pane TUIKit terminal interface with a startup splash and a FIGlet
  "docmon" wordmark, cross-platform on Windows, Linux, and macOS.
- Tabbed navigation across Containers, Stacks, Metrics, Images, Events, and
  Tools screens (`Tab`/`Shift+Tab` and number keys `1`–`6`).
- Containers screen: sortable table of running/all containers with image, tag,
  ID, exposed ports, state, health, uptime, and live CPU/memory, plus a detail
  pane with inline charts.
- Full container lifecycle actions: start, stop, restart, kill, pause, remove.
- Live performance monitoring with CPU, memory, network, and block I/O charts,
  overall and per container, backed by streamed Docker stats.
- Images screen with local-vs-registry digest comparison and an update flag,
  driven by an `IRegistryProvider` abstraction with a Docker Hub provider.
  Actions: recheck updates, pull selected, pull a new image by name/tag through
  a prompt with a live progress modal (Escape cancels), and delete an image.
- Pull with layer progress, and a pull-and-apply update action.
- Scrollable log viewer: line and page scrolling (Up/Down, PageUp/PageDown,
  Home/End), copy-to-clipboard (`c`), and Escape to close.
- Dangling-image prune restricted with an explicit `dangling=true` filter so it
  never removes tagged images or anything a container depends on.
- Stacks screen with compose-project grouping, discovery, and stack-level
  lifecycle actions.
- Non-interactive exec with in-pane output capture.
- Interactive shell and TTY exec via TUI suspend/resume around the `docker` CLI.
- File transfer into and out of containers using the archive API.
- Live Docker daemon event stream.
- Cross-platform Docker endpoint detection (named pipe on Windows, Unix socket
  elsewhere, `DOCKER_HOST` override).
- Packaged as the `docmon` .NET global tool, multi-targeted for net8.0 and
  net10.0, with `install-tool.bat`, `reinstall-tool.bat`, and `remove-tool.bat`
  helper scripts; the install and reinstall scripts accept an optional target
  framework argument (net8.0 or net10.0).

[Unreleased]: https://github.com/jchristn/Docmon/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/jchristn/Docmon/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/jchristn/Docmon/releases/tag/v0.1.0
