# Validation and operator walkthrough

The implementation was exercised locally against the local Docker daemon using disposable, labeled containers. No pre-existing workload was stopped, restarted or removed. Local stack verification used alternate host ports to avoid existing services. Test services and the terminal probe were removed after verification; images and named data volumes were retained.

## Surface inventory

| Surface | Operator job | Evidence |
| --- | --- | --- |
| Local TUI | Monitor/control local Docker without a token | Shared runtime build; existing TUI/core regression suites |
| Remote TUI adapter | Authenticate, query/control, pull, exec, transfer files | Live HTTP adapter tests with disposable containers |
| Terminal | Interactive shell over authenticated WebSocket | Invalid-token rejection; live command-output test |
| REST | Read all resources and change Docker state | Catalog authentication checks; live lifecycle/exec/pull/archive round trips |
| MCP | Initialize, preserve sessions, discover and call tools | Voltaic handshake, tools/list catalog parity, system_info call, credential exclusion |
| Login/logout | Reject a bad token, protect deep links, clear session | Browser tests against the packaged dashboard |
| Overview | Resource KPIs, request activity, workload attention and diagnostic links | Browser walkthrough and screenshot inspection |
| Containers | Inspect, lifecycle dialogs, logs, exec, transfers and terminal | Browser navigation/inspection/cancelled confirmation; backend live operations |
| Images | List, pull by name or existing image ID, tags/check/remove/prune controls | Browser navigation; live name/ID pull tests |
| Compose | Discover projects and operate services | Browser navigation; existing live Compose lifecycle tests |
| Metrics/events | Poll snapshots and inspect recent time series/events | Browser navigation; existing stream tests and live server monitoring |
| Request history | Filter/sort/page summaries and inspect redacted full records | Browser tables/inspection; journal credential-redaction test |
| API Explorer | Discover OpenAPI routes and execute authenticated requests | Live read-only execution; binary archive transport tested through the shared API |
| Settings/help | Inspect server, change locale/theme, replay onboarding | Browser walkthrough and locale persistence tests |
| Deployment | Build and start all services in dependency order | Backend and dashboard health checks |

## Checks run

- Release solution build: zero errors and warnings, targeting .NET 8 and .NET 10.
- xUnit descriptor suite on .NET 10 with Docker: 207 passed. It covers the existing core/TUI behavior and new server/remote integration paths.
- xUnit descriptor suite on .NET 8 without live Docker suites: 182 passed. The skipped descriptors are omitted by the existing suite factory.
- Frontend lint and production build passed. Six unit tests passed (API auth/routing, table behavior, locale persistence/direction and formatting).
- Nine Playwright tests passed against the packaged Nginx/dashboard and Watson backend. They covered login/logout, protected routes, all navigation sections, record inspection, empty filters, read-only API execution, locale persistence, desktop/tablet/mobile widths (1440/768/390), both themes, and custom mutation confirmation dialogs without submitting destructive actions.
- The read-only Postman collection passed nine requests and 27 assertions against the live packaged API. This was a local run; no Postman workspace artifacts were published.
- Docker builds and Compose startup passed for the backend and dashboard.
- `npm audit`: zero vulnerabilities at verification time.
- GitHub Actions configuration was added for every push and pull request. Remote CI execution and repository branch-protection settings were not changed by this local work.

## Review notes and limits

The visual pass caught cramped mobile columns and inconsistent login theme application; both were corrected and rechecked. Runtime tests caught encoded-path handling, chunked archive responses, MCP JSON casing, listener cancellation and duplex terminal input. Those paths were corrected and retested.

Full-screen local/remote TUI interaction was not manually driven in a terminal emulator; compilation, existing TUI tests, remote service tests and the actual WebSocket command path were exercised. Mutation buttons were not submitted against existing workloads through the browser. The same HTTP operations were verified on disposable resources. Pruning/removing unrelated images was deliberately not exercised against the user's daemon.

Compose operations require mounted source project files. Standalone container image updates pull but do not recreate the container, matching the existing TUI. Browser/remote-TUI tar uploads are limited to 4 MiB. Remote pull/exec progress is delivered on completion. Terminal resize negotiation and complete translations beyond the English fallback remain future work. Settings are environment-based and require service recreation; there is no dashboard secret editor.

The REST catalog, OpenAPI snapshot, Postman collections and MCP documentation can be regenerated with `python3 scripts/export-api.py <server-origin>`. Playwright screenshots are generated under `dashboard/test-results/` and intentionally excluded from Git.

## Simplified deployment verification

The two-service deployment was rebuilt and started after removing the external diagnostic stack. Compose configuration confirms that the backend listener and both published service bindings use `0.0.0.0`. The root Postman collections were regenerated from the running server. The solution build, 182 offline backend tests, six frontend unit tests, frontend lint/build, nine packaged browser tests and the read-only Postman assertions passed. Temporary verification containers were removed afterward.
