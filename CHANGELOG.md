# Changelog

All notable changes to Docmon are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- Pull with layer progress, and a pull-and-apply update action.
- Stacks screen with compose-project grouping, discovery, and stack-level
  lifecycle actions.
- Non-interactive exec with in-pane output capture.
- Interactive shell and TTY exec via TUI suspend/resume around the `docker` CLI.
- File transfer into and out of containers using the archive API.
- Live Docker daemon event stream.
- Cross-platform Docker endpoint detection (named pipe on Windows, Unix socket
  elsewhere, `DOCKER_HOST` override).
- Packaged as the `docmon` .NET global tool, with `install-tool.bat`,
  `reinstall-tool.bat`, and `remove-tool.bat` helper scripts.

[Unreleased]: https://github.com/jchristn/Docmon/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/jchristn/Docmon/releases/tag/v0.1.0
