# Docmon

**A terminal UI for managing and monitoring your Docker stack.**

Docmon is a keyboard-driven, multi-pane TUI for your containers: live metrics,
image-update checks, lifecycle control, exec, file transfer, and an interactive
shell — on Windows, Linux, and macOS.

![Docmon](https://raw.githubusercontent.com/jchristn/Docmon/main/assets/docmon.svg)

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
code is split into a `Docmon.Core` library and a `Docmon.App` host.

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
