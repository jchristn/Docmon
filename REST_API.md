# Docmon REST API

The live Watson route metadata is authoritative: `/openapi.json`; interactive Swagger is at `/swagger`. The checked-in snapshot is [assets/openapi.json](assets/openapi.json). Regenerate this document and the Postman collection with `python3 scripts/export-api.py http://127.0.0.1:8080` after changing routes.

## Authentication and conventions

Every `/api/v1` operation and `/mcp` requires `Authorization: Bearer <DOCMON_BEARER_TOKEN>`. The token grants full control of one Docker machine. `/health`, `/openapi.json`, and `/swagger` are anonymous. The TUI uses the local Docker socket without a token by default.

All JSON uses camelCase and string enums. Encode IDs and path/query values with percent encoding. Fixed request fields are described below. Path parameters override corresponding body fields. Long operations run synchronously, with a 30-minute REST timeout; an interrupted request may already have changed Docker state. Inspect the resource before retrying. CLI/exec responses return `exitCode`, `output` (latest 2000 lines), and `reclaimedBytes`. A nonzero exit code indicates command failure even when HTTP status is 200.

Errors use `{ "code": "invalid_request", "message": "...", "requestId": "..." }`. Common statuses: 400 invalid input; 401 invalid/missing bearer token; 404 missing Docker/resource record; 409 Docker conflict; 502 Docker/MCP unavailable; 504 REST timeout. Docker may return other 4xx statuses. `X-Request-Id` correlates normal JSON operations with durable history.

Lists return JSON arrays of current Docker resources. Events retain at most 1000 records; metric history retains at most 10000 samples; request history returns at most 1000 summaries, newest first. Query `/history/{id}` for a redacted full record. Durable history uses daily JSONL files with 30-day retention in `DOCMON_DATA_DIRECTORY` (default `./data`), redacted auth headers and sensitive JSON keys, and 4096-character body limits. History responses and MCP payloads are excluded from body capture to avoid recursive and token-bearing payloads.

Compose operations use discovered labels and require the original project files mounted at identical host paths. `containers_update` pulls the image and recreates a Compose service. For standalone containers it only pulls, matching the TUI; a restart does not switch an existing container to the new image.

Browser archive endpoints transfer tar files directly. Upload bodies are raw `application/x-tar`, capped at 4 MiB. The copy-in/out JSON operations use paths on the server host. For a containerized backend these paths are inside its mounts. The remote TUI uses the archive endpoints to transfer files on the TUI client machine.

## Endpoint inventory

| Method | Path | Operation |
| --- | --- | --- |
| GET | `/health` | Docker daemon readiness |
| GET | `/api/v1/containers` | List all containers |
| GET | `/api/v1/images` | List local images |
| POST | `/api/v1/images/pull` | Pull an image by name/tag or existing image ID |
| POST | `/api/v1/images/prune` | Prune dangling images |
| GET | `/api/v1/stacks` | Discover Compose projects |
| GET | `/api/v1/system/usage` | Docker disk and resource usage |
| GET | `/api/v1/system/info` | Server connection and version |
| GET | `/api/v1/events` | Recent daemon events |
| GET | `/api/v1/metrics/history` | Bounded recent container metric history |
| GET | `/api/v1/metrics` | Latest live container metrics |
| GET | `/api/v1/history` | Recent API requests without credentials |
| GET | `/api/v1/containers/{id}` | Inspect a container |
| GET | `/api/v1/containers/{id}/logs` | Read container logs |
| POST | `/api/v1/containers/{id}/start` | Start container |
| POST | `/api/v1/containers/{id}/stop` | Stop container |
| POST | `/api/v1/containers/{id}/restart` | Restart container |
| POST | `/api/v1/containers/{id}/kill` | Kill container |
| POST | `/api/v1/containers/{id}/pause` | Pause container |
| POST | `/api/v1/containers/{id}/unpause` | Unpause container |
| POST | `/api/v1/containers/{id}/remove` | Remove container |
| POST | `/api/v1/containers/{id}/exec` | Exec container |
| POST | `/api/v1/containers/{id}/copy-in` | Copy in container |
| POST | `/api/v1/containers/{id}/copy-out` | Copy out container |
| POST | `/api/v1/containers/{id}/update` | Update container |
| POST | `/api/v1/images/{id}/remove` | Remove image |
| POST | `/api/v1/images/{id}/check` | Check image |
| GET | `/api/v1/images/{id}/tags` | Tags image |
| POST | `/api/v1/stacks/{project}/up` | Up Compose project |
| POST | `/api/v1/stacks/{project}/down` | Down Compose project |
| POST | `/api/v1/stacks/{project}/restart` | Restart Compose project |
| POST | `/api/v1/stacks/{project}/pull` | Pull Compose project |
| GET | `/api/v1/history/{id}` | Inspect a redacted request record |
| GET | `/api/v1/containers/{id}/archive` | Download a container path as a tar archive |
| PUT | `/api/v1/containers/{id}/archive` | Upload a tar archive into a container directory |

## Endpoint contracts

### `GET /health`

Docker daemon readiness

**Authentication:** None.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Docker is reachable., `503` Docker is unavailable.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/health'
```

### `GET /api/v1/containers`

List all containers

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/containers' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/images`

List local images

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/images' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `POST /api/v1/images/pull`

Pull an image by name/tag or existing image ID

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

- `image` (string, optional)
- `id` (string, optional)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/images/pull' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"image": "{{image}}"}'
```

### `POST /api/v1/images/prune`

Prune dangling images

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/images/prune' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `GET /api/v1/stacks`

Discover Compose projects

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/stacks' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/system/usage`

Docker disk and resource usage

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/system/usage' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/system/info`

Server connection and version

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/system/info' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/events`

Recent daemon events

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/events' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/metrics/history`

Bounded recent container metric history

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/metrics/history' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/metrics`

Latest live container metrics

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/metrics' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/history`

Recent API requests without credentials

**Authentication:** Bearer token required.

**Parameters:**

None.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/history' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/containers/{id}`

Inspect a container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/containers/{id}' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/containers/{id}/logs`

Read container logs

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier
- `tail` (query, integer, optional): Trailing log lines, from 1 through 10000. Default 200.

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/containers/{id}/logs' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `POST /api/v1/containers/{id}/start`

Start container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/start' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `POST /api/v1/containers/{id}/stop`

Stop container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/stop' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `POST /api/v1/containers/{id}/restart`

Restart container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/restart' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `POST /api/v1/containers/{id}/kill`

Kill container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/kill' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `POST /api/v1/containers/{id}/pause`

Pause container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/pause' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `POST /api/v1/containers/{id}/unpause`

Unpause container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/unpause' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `POST /api/v1/containers/{id}/remove`

Remove container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

- `force` (boolean, optional)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/remove' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"force": false}'
```

### `POST /api/v1/containers/{id}/exec`

Exec container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

- `command` (array, required)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/exec' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"command": ["/bin/sh", "-c", "echo hello"]}'
```

### `POST /api/v1/containers/{id}/copy-in`

Copy in container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

- `hostPath` (string, required)
- `path` (string, required)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/copy-in' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"hostPath": "{{hostPath}}", "path": "/tmp"}'
```

### `POST /api/v1/containers/{id}/copy-out`

Copy out container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

- `hostPath` (string, required)
- `path` (string, required)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/copy-out' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"hostPath": "{{hostPath}}", "path": "/tmp"}'
```

### `POST /api/v1/containers/{id}/update`

Update container

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/containers/{id}/update' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `POST /api/v1/images/{id}/remove`

Remove image

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

- `force` (boolean, optional)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/images/{id}/remove' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"force": false}'
```

### `POST /api/v1/images/{id}/check`

Check image

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/images/{id}/check' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{}'
```

### `GET /api/v1/images/{id}/tags`

Tags image

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/images/{id}/tags' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `POST /api/v1/stacks/{project}/up`

Up Compose project

**Authentication:** Bearer token required.

**Parameters:**

- `project` (path, string, required): Resource identifier

**Request body:**

- `service` (string, optional)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/stacks/{project}/up' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"service": "{{service}}"}'
```

### `POST /api/v1/stacks/{project}/down`

Down Compose project

**Authentication:** Bearer token required.

**Parameters:**

- `project` (path, string, required): Resource identifier

**Request body:**

- `service` (string, optional)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/stacks/{project}/down' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"service": "{{service}}"}'
```

### `POST /api/v1/stacks/{project}/restart`

Restart Compose project

**Authentication:** Bearer token required.

**Parameters:**

- `project` (path, string, required): Resource identifier

**Request body:**

- `service` (string, optional)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/stacks/{project}/restart' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"service": "{{service}}"}'
```

### `POST /api/v1/stacks/{project}/pull`

Pull Compose project

**Authentication:** Bearer token required.

**Parameters:**

- `project` (path, string, required): Resource identifier

**Request body:**

- `service` (string, optional)

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X POST 'http://127.0.0.1:8080/api/v1/stacks/{project}/pull' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN" -H 'Content-Type: application/json' --data '{"service": "{{service}}"}'
```

### `GET /api/v1/history/{id}`

Inspect a redacted request record

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Resource identifier

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Completed request. Inspect exitCode for CLI and exec operations., `400` Structured error with code, message and requestId., `401` Structured error with code, message and requestId., `404` Structured error with code, message and requestId., `409` Structured error with code, message and requestId., `500` Structured error with code, message and requestId., `502` Structured error with code, message and requestId., `504` Structured error with code, message and requestId.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/history/{id}' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `GET /api/v1/containers/{id}/archive`

Download a container path as a tar archive

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Container ID
- `path` (query, string, required): Absolute path inside container

**Request body:**

None (or an empty JSON object for POST).

**Responses:** `200` Tar archive (application/x-tar).

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X GET 'http://127.0.0.1:8080/api/v1/containers/{id}/archive' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

### `PUT /api/v1/containers/{id}/archive`

Upload a tar archive into a container directory

**Authentication:** Bearer token required.

**Parameters:**

- `id` (path, string, required): Container ID
- `path` (query, string, required): Absolute path inside container

**Request body:**

Raw tar archive (`application/x-tar`).

**Responses:** `200` Archive extracted.

Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).

**Example:**

```bash
curl -X PUT 'http://127.0.0.1:8080/api/v1/containers/{id}/archive' -H "Authorization: Bearer $DOCMON_BEARER_TOKEN"
```

## Interactive terminal

`GET /api/v1/terminal` upgrades to a Watson WebSocket. Send a first JSON frame within 10 seconds: `{ "token": "<bearer token>", "id": "<container ID>", "command": ["/bin/sh"] }`. Subsequent text frames contain terminal input; server frames contain UTF-8 terminal output. A missing/invalid credential closes the socket before Docker exec is created. Tokens never belong in the URL. Sessions end when the shell exits or the socket disconnects. Terminal resize negotiation is not implemented yet.

## MCP gateway

`POST`, `GET`, and `DELETE /mcp` implement Voltaic streamable HTTP, with the same Authorization header. MCP session, protocol, replay, and SSE headers pass through the Watson gateway. See [MCP_API.md](MCP_API.md).
