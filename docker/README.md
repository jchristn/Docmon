# Docker deployment

From the repository root:

```bash
# docker/.env is included; default bearer token: docmonadmin.
docker compose -f docker/compose.yaml up -d
```

Open `http://127.0.0.1:8088`, then log in using `docmonadmin` or your configured token. The API is also available at `http://127.0.0.1:8080/api/v1`, Swagger at `/swagger`, and MCP at `/mcp` on either port. The internal Voltaic port is not published. API and dashboard have separate images; Nginx proxies HTTP, WebSocket, and MCP streaming through one dashboard origin.


For remote access, put an HTTPS reverse proxy in front of the dashboard port. The supplied `.env` binds both published ports to `0.0.0.0` (all host interfaces). `DOCMON_HOST=0.0.0.0` configures the backend listener. API access grants full Docker machine control. Keep the Docker socket private. Rotate the token by editing `.env` and running `docker compose -f docker/compose.yaml up -d --force-recreate backend`. Existing browser sessions must sign in with the replacement token.

## Compose project access

Discovery works from the Docker socket alone. Stack up/down/restart/pull and container pull-and-apply need the original compose files and referenced files mounted at their original absolute paths. For example, add `- /opt/stacks:/opt/stacks` under backend volumes if your compose projects live there. Bind mounts inside those compose files are interpreted by the host daemon, so their paths must also refer to host paths. The backend image includes the Docker CLI and Compose plugin. It does not mount the whole host filesystem.

The API copy-in/copy-out operations use backend filesystem paths. Mount specific working directories when using these operations. The browser and remote TUI can transfer tar archives directly without a host workspace mount. Tar uploads are limited to 4 MiB; larger host-side transfers can use mounted working directories.

On Docker Desktop, set `DOCMON_DOCKER_SOCKET` to the actual socket if `/var/run/docker.sock` is unavailable, for example `$HOME/.docker/run/docker.sock` on macOS.

## Persistence and maintenance

`docmon-data` stores redacted daily request journals and persistent metric minute aggregates (30-day retention). The API retains the newest 1000 request summaries in memory. Historical metric windows survive backend restarts; only the near-real-time metric buffer and in-memory events reset. Docker resources stay in the host daemon. `docker compose down` preserves the data volume. The dashboard and remote TUI can select near-real-time metrics or the last hour, day, week, and thirty days. History accumulates while the backend runs; it cannot backfill observations from before monitoring started.

Compose uses named images without build contexts: `jchristn77/docmon-server:v0.1.0` and `jchristn77/docmon-dashboard:v0.1.0`. Run `./docker/update.sh` (Linux/macOS) or `docker\update.bat` (Windows) to pull the configured tags, recreate the stack, and report container status without removing named volumes.

## Build and publish images

Run the helpers from the repository root (they also work from another working directory):

```bash
docker login
./build-all.sh v0.1.0
# Or build one component:
./build-server.sh v0.1.0
./build-dashboard.sh v0.1.0
```

On Windows, use `build-all.bat`, `build-server.bat`, or `build-dashboard.bat` with the same arguments. Both tag and namespace are optional; defaults are `v0.1.0` and `jchristn77`. For example, `./build-all.sh v0.1.0 yournamespace` publishes to a different Docker Hub namespace.

Each component helper builds `linux/amd64` and `linux/arm64/v8` using Docker Buildx, pushes a multi-architecture image under the requested tag and `latest`, then pulls both tags into the local Docker image store using the daemon's native architecture. Passing `latest` builds and pulls that tag once. Failures stop the script immediately, including failed local pulls.

The default builder is `cloud-jchristn77-jchristn77`, matching the other projects. Override it with `DOCMON_BUILDER` to use another configured multi-platform builder. A publish-capable Docker Hub login is required. These helpers publish images when run; committing the scripts does not publish a release. Compose remains pinned to `v0.1.0` until its image references are edited.

## Docker socket and metric scope

The container monitors Docker through the mounted /var/run/docker.sock. It asks the host Docker daemon for container statistics and management operations. The all-workloads view sums Docker container metrics; unrelated host OS processes are excluded. On Docker Desktop, the daemon runs in its Linux VM.
