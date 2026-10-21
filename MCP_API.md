# Docmon MCP API

Voltaic runs in the API server process. Connect streamable HTTP clients to `http://127.0.0.1:8080/mcp` (or the dashboard origin `/mcp`). Set `Authorization: Bearer <DOCMON_BEARER_TOKEN>` on every request. Watson authenticates the public gateway; the internal Voltaic listener independently verifies the token and binds only to `127.0.0.1`. Do not publish the internal MCP port.

MCP implements initialize, initialized, tools/list, tools/call, cancellation and session management through Voltaic. Preserve `Mcp-Session-Id` and `MCP-Protocol-Version` returned by the transport. Diagnostic demo tools are disabled. No application resources or prompts are registered; those lists are empty.

Tools share the REST application service, models, and side effects. Successful results are returned as camelCase JSON in MCP text content. Nonzero CLI/exec exit codes also set isError. Resource reads return the JSON resource shape described in [REST_API.md](REST_API.md); mutations return `{ "exitCode": 0, "output": [], "reclaimedBytes": 0 }`. Inspect nonzero exit codes. Tool exceptions become MCP errors. The `containers_copy_in` and `containers_copy_out` host paths refer to the API server machine, not the agent machine. Binary archive transfer and the interactive WebSocket terminal are REST transports; use exec for agent commands.

Collection tools accept the same EnumerationQuery fields documented in REST_API.md and return EnumerationResult objects. Pass `filters` as an object in MCP arguments (REST query parameters encode that object as a JSON string). Follow `continuationToken` with unchanged query fields until `endOfResults` is true.

## Client configuration

```json
{
  "mcpServers": {
    "docmon": {
      "url": "https://your-machine/mcp",
      "headers": {
        "Authorization": "Bearer <token>"
      }
    }
  }
}
```

## Tools

### `containers_list`

List all containers.

Equivalent REST operation: `GET /api/v1/containers`.

| Parameter | Type | Required |
| --- | --- | --- |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_list",
    "arguments": {}
  }
}
```

### `images_list`

List local images.

Equivalent REST operation: `GET /api/v1/images`.

| Parameter | Type | Required |
| --- | --- | --- |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "images_list",
    "arguments": {}
  }
}
```

### `images_pull`

Pull an image by name/tag or existing image ID.

Equivalent REST operation: `POST /api/v1/images/pull`.

| Parameter | Type | Required |
| --- | --- | --- |
| `image` | string | No |
| `id` | string | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "images_pull",
    "arguments": {
      "image": "alpine:3.20"
    }
  }
}
```

### `images_prune`

Prune dangling images.

Equivalent REST operation: `POST /api/v1/images/prune`.

| Parameter | Type | Required |
| --- | --- | --- |
| None | | |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "images_prune",
    "arguments": {}
  }
}
```

### `stacks_list`

Discover Compose projects.

Equivalent REST operation: `GET /api/v1/stacks`.

| Parameter | Type | Required |
| --- | --- | --- |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "stacks_list",
    "arguments": {}
  }
}
```

### `system_usage`

Docker disk and resource usage.

Equivalent REST operation: `GET /api/v1/system/usage`.

| Parameter | Type | Required |
| --- | --- | --- |
| None | | |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "system_usage",
    "arguments": {}
  }
}
```

### `system_info`

Server connection and version.

Equivalent REST operation: `GET /api/v1/system/info`.

| Parameter | Type | Required |
| --- | --- | --- |
| None | | |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "system_info",
    "arguments": {}
  }
}
```

### `events_list`

Recent daemon events.

Equivalent REST operation: `GET /api/v1/events`.

| Parameter | Type | Required |
| --- | --- | --- |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "events_list",
    "arguments": {}
  }
}
```

### `metrics_history`

Bounded recent container metric history.

Equivalent REST operation: `GET /api/v1/metrics/history`.

| Parameter | Type | Required |
| --- | --- | --- |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "metrics_history",
    "arguments": {}
  }
}
```

### `metrics_list`

Latest live container metrics.

Equivalent REST operation: `GET /api/v1/metrics`.

| Parameter | Type | Required |
| --- | --- | --- |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "metrics_list",
    "arguments": {}
  }
}
```

### `history_summary`

Request history time buckets across the retained journal.

Equivalent REST operation: `GET /api/v1/history/summary`.

| Parameter | Type | Required |
| --- | --- | --- |
| `timeframe` | string | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "history_summary",
    "arguments": {}
  }
}
```

### `history_list`

Recent API requests without credentials.

Equivalent REST operation: `GET /api/v1/history`.

| Parameter | Type | Required |
| --- | --- | --- |
| `timeframe` | string | No |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "history_list",
    "arguments": {}
  }
}
```

### `containers_inspect`

Inspect a container.

Equivalent REST operation: `GET /api/v1/containers/{id}`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_inspect",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_logs`

Read container logs.

Equivalent REST operation: `GET /api/v1/containers/{id}/logs`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |
| `tail` | integer | No |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_logs",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_start`

Start container.

Equivalent REST operation: `POST /api/v1/containers/{id}/start`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_start",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_stop`

Stop container.

Equivalent REST operation: `POST /api/v1/containers/{id}/stop`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_stop",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_restart`

Restart container.

Equivalent REST operation: `POST /api/v1/containers/{id}/restart`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_restart",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_kill`

Kill container.

Equivalent REST operation: `POST /api/v1/containers/{id}/kill`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_kill",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_pause`

Pause container.

Equivalent REST operation: `POST /api/v1/containers/{id}/pause`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_pause",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_unpause`

Unpause container.

Equivalent REST operation: `POST /api/v1/containers/{id}/unpause`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_unpause",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_remove`

Remove container.

Equivalent REST operation: `POST /api/v1/containers/{id}/remove`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |
| `force` | boolean | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_remove",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_exec`

Exec container.

Equivalent REST operation: `POST /api/v1/containers/{id}/exec`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |
| `command` | array | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_exec",
    "arguments": {
      "command": [
        "/bin/sh",
        "-c",
        "echo hello"
      ],
      "id": "{{containerId}}"
    }
  }
}
```

### `containers_copy_in`

Copy in container.

Equivalent REST operation: `POST /api/v1/containers/{id}/copy-in`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |
| `hostPath` | string | Yes |
| `path` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_copy_in",
    "arguments": {
      "id": "{{containerId}}",
      "hostPath": "{{hostPath}}",
      "path": "/tmp"
    }
  }
}
```

### `containers_copy_out`

Copy out container.

Equivalent REST operation: `POST /api/v1/containers/{id}/copy-out`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |
| `hostPath` | string | Yes |
| `path` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_copy_out",
    "arguments": {
      "id": "{{containerId}}",
      "hostPath": "{{hostPath}}",
      "path": "/tmp"
    }
  }
}
```

### `containers_update`

Update container.

Equivalent REST operation: `POST /api/v1/containers/{id}/update`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "containers_update",
    "arguments": {
      "id": "{{containerId}}"
    }
  }
}
```

### `images_remove`

Remove image.

Equivalent REST operation: `POST /api/v1/images/{id}/remove`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |
| `force` | boolean | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "images_remove",
    "arguments": {
      "id": "{{imageId}}"
    }
  }
}
```

### `images_check`

Check image.

Equivalent REST operation: `POST /api/v1/images/{id}/check`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "images_check",
    "arguments": {
      "id": "{{imageId}}"
    }
  }
}
```

### `images_tags`

Tags image.

Equivalent REST operation: `GET /api/v1/images/{id}/tags`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |
| `maxResults` | integer | No |
| `continuationToken` | string | No |
| `search` | string | No |
| `sortBy` | string | No |
| `ordering` | string | No |
| `containerIdFilter` | string | No |
| `startUtc` | string | No |
| `endUtc` | string | No |
| `filters` | object | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "images_tags",
    "arguments": {
      "id": "{{imageId}}"
    }
  }
}
```

### `stacks_up`

Up Compose project.

Equivalent REST operation: `POST /api/v1/stacks/{project}/up`.

| Parameter | Type | Required |
| --- | --- | --- |
| `project` | string | Yes |
| `service` | string | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "stacks_up",
    "arguments": {
      "project": "{{project}}"
    }
  }
}
```

### `stacks_down`

Down Compose project.

Equivalent REST operation: `POST /api/v1/stacks/{project}/down`.

| Parameter | Type | Required |
| --- | --- | --- |
| `project` | string | Yes |
| `service` | string | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "stacks_down",
    "arguments": {
      "project": "{{project}}"
    }
  }
}
```

### `stacks_restart`

Restart Compose project.

Equivalent REST operation: `POST /api/v1/stacks/{project}/restart`.

| Parameter | Type | Required |
| --- | --- | --- |
| `project` | string | Yes |
| `service` | string | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "stacks_restart",
    "arguments": {
      "project": "{{project}}"
    }
  }
}
```

### `stacks_pull`

Pull Compose project.

Equivalent REST operation: `POST /api/v1/stacks/{project}/pull`.

| Parameter | Type | Required |
| --- | --- | --- |
| `project` | string | Yes |
| `service` | string | No |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "stacks_pull",
    "arguments": {
      "project": "{{project}}"
    }
  }
}
```

### `history_inspect`

Inspect a redacted request record.

Equivalent REST operation: `GET /api/v1/history/{id}`.

| Parameter | Type | Required |
| --- | --- | --- |
| `id` | string | Yes |

Example `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "history_inspect",
    "arguments": {
      "id": "{{requestId}}"
    }
  }
}
```
