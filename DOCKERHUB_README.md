# Docmon

![Docmon](https://raw.githubusercontent.com/jchristn/Docmon/main/assets/logo.png)

**Docker monitoring and management through a web dashboard, REST API, MCP server, and terminal UI.**

Monitor a Docker machine remotely, manage its containers, and give agents authenticated access to the same operations. The C# backend uses Watson 7 and Voltaic; the React dashboard shares its API with the terminal client.

Docmon is under active development. Features and APIs may change.

## Images

| Image | Purpose | Container port |
| --- | --- | --- |
| `jchristn77/docmon-server:v0.1.0` | Docker management backend, REST API, and MCP gateway | `8080` |
| `jchristn77/docmon-dashboard:v0.1.0` | React dashboard and same-origin backend proxy | `80` |

The repository's release scripts build for `linux/amd64` and `linux/arm64/v8`. The included Compose deployment pins both images to `v0.1.0`.

## Quick start

Install Docker with Docker Compose, then download the deployment configuration:

```bash
git clone https://github.com/jchristn/Docmon.git
cd Docmon
docker compose -f docker/compose.yaml up -d
```

A working `docker/.env` is included. Open **http://localhost:8088** and log in with **`docmonadmin`**, the default bearer token. To access another machine, replace `localhost` with its hostname or IP address.

The dashboard and API listen on all host interfaces by default. Set your bearer token in `docker/.env` and use HTTPS for remote access. The token grants Docker management access through the dashboard, REST API, MCP, and remote TUI.

## What you can do

- View containers, images, Compose projects, live metrics, events, and request history.
- Start, stop, restart, kill, pause, unpause, and remove containers.
- Read logs, execute commands, open an interactive terminal, and copy files.
- Pull images by name and tag or from an existing image, check for updates, and remove or prune images.
- Manage Compose projects and pull and apply updates to Compose services.
- Use the API Explorer in the dashboard, Swagger, a Postman collection, or an MCP client to operate the machine.

Updating a standalone container's image pulls the image; recreating that container remains a separate operation. Compose updates can recreate the affected service.

## Endpoints and authentication

| Interface | Default URL |
| --- | --- |
| Dashboard | `http://localhost:8088` |
| REST API | `http://localhost:8080/api/v1` |
| Swagger UI | `http://localhost:8080/swagger` |
| OpenAPI document | `http://localhost:8080/openapi.json` |
| MCP | `http://localhost:8080/mcp` |
| Health check | `http://localhost:8080/health` |

The dashboard port also proxies the API, Swagger, OpenAPI, MCP, and WebSocket routes. Clients can use a single dashboard origin, such as `https://docmon.example.com` behind a reverse proxy. The internal Voltaic listener is not published separately.

REST and MCP requests use the bearer token configured in `DOCMON_BEARER_TOKEN`:

```bash
curl http://localhost:8080/api/v1/containers \
  -H 'Authorization: Bearer docmonadmin'
```

Configure an HTTP MCP client with the `/mcp` URL and the same `Authorization: Bearer <token>` header. Swagger, OpenAPI metadata, and health checks are public; management operations require authentication.

See the complete [REST API documentation](https://github.com/jchristn/Docmon/blob/main/REST_API.md), [MCP documentation](https://github.com/jchristn/Docmon/blob/main/MCP_API.md), and [Postman collection](https://github.com/jchristn/Docmon/blob/main/Docmon.postman_collection.json).

## Configuration

Edit `docker/.env`, then recreate the services with `docker compose -f docker/compose.yaml up -d`.

| Variable | Default | Purpose |
| --- | --- | --- |
| `DOCMON_BEARER_TOKEN` | `docmonadmin` | Shared remote-access token |
| `DOCMON_HOST` | `0.0.0.0` | Backend listener address inside the container |
| `DOCMON_API_BIND` | `0.0.0.0` | Host interface for the API port |
| `DOCMON_API_PORT` | `8080` | Published API port |
| `DOCMON_DASHBOARD_BIND` | `0.0.0.0` | Host interface for the dashboard port |
| `DOCMON_DASHBOARD_PORT` | `8088` | Published dashboard port |
| `DOCMON_DOCKER_SOCKET` | `/var/run/docker.sock` | Host Docker socket mounted into the backend |

Docker Desktop installations may use a socket such as `/Users/YOU/.docker/run/docker.sock`. Set `DOCMON_DOCKER_SOCKET` to the socket used by your installation.

### Compose projects and file access

Compose lifecycle operations need the original project files and dependencies. Mount each project directory into the backend at the same absolute path used on the host. For example, add `/opt/stacks:/opt/stacks` to the backend's volumes in `docker/compose.yaml`.

JSON file-copy operations refer to paths on the backend host or container filesystem. Browser and remote-TUI archive uploads are limited to 4 MiB. See the [deployment documentation](https://github.com/jchristn/Docmon/blob/main/docker/README.md) for details.

### Persistent data

The `docmon-data` named volume stores redacted request history in `/app/data`. Request history is retained for 30 days. Metrics and Docker event history are held in memory and reset when the backend restarts.

## Updating

From the repository root:

```bash
./docker/update.sh
```

On Windows, run `docker\update.bat`. These scripts pull the configured images and recreate the services while retaining the named data volume. The deployment remains pinned to the image tags in `docker/compose.yaml`.

## Building and publishing images

The repository includes executable shell scripts and Windows batch equivalents:

```bash
docker login
./build-all.sh v0.1.0
```

Use `build-server.sh` or `build-dashboard.sh` to build one image. Windows equivalents are `build-all.bat`, `build-server.bat`, and `build-dashboard.bat`.

Each script builds both supported architectures, pushes the requested tag and `latest` to Docker Hub, then pulls the images for the local machine's architecture. The default tag is `v0.1.0` and the default namespace is `jchristn77`; an optional second argument overrides the namespace. Set `DOCMON_BUILDER` to override the default Buildx builder, `cloud-jchristn77-jchristn77`.

## Terminal client

The terminal UI is also available as a .NET global tool:

```bash
dotnet tool install --global Docmon
docmon
```

The local TUI connects only to the local Docker daemon and requires no authentication. To connect it to a remote Docmon server:

```bash
export DOCMON_BEARER_TOKEN=docmonadmin
docmon --server https://docmon.example.com
```

## Links

- [GitHub and full documentation](https://github.com/jchristn/Docmon)
- [Discord community](https://discord.gg/tRAN8HgvK5)
- [MIT license](https://github.com/jchristn/Docmon/blob/main/LICENSE.md)

Copyright (c) 2026 Joel Christner.
