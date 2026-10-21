#!/usr/bin/env python3
"""Export live Watson route metadata and derive reviewable documentation/requests.
Usage: python3 scripts/export-api.py http://127.0.0.1:8080
"""
import json
import sys
import urllib.request
from pathlib import Path
root = Path(__file__).resolve().parents[1]
base = (sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:8080').rstrip('/')
with urllib.request.urlopen(base + '/openapi.json') as response:
    spec = json.load(response)
def save(path, value):
    dest = root / path
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(value, indent=2) + '\n')
save('assets/openapi.json', spec)
operations = []
for path, methods in spec['paths'].items():
    for method, op in methods.items():
        if method not in ['get', 'post', 'put', 'delete', 'patch']: continue
        if not path.startswith('/api/v1/') and path != '/health': continue
        operations.append((path, method.upper(), op))
params = {'id':'{{containerId}}', 'project':'{{project}}', 'image':'{{image}}', 'command':['/bin/sh','-c','echo hello'], 'hostPath':'{{hostPath}}', 'path':'/tmp', 'tail':200, 'service':'{{service}}', 'force':False}
folders = {}
smoke = []
rest = ['# Docmon REST API', '', 'The live Watson route metadata is authoritative: `/openapi.json`; interactive Swagger is at `/swagger`. The checked-in snapshot is [assets/openapi.json](assets/openapi.json). Regenerate this document and the Postman collection with `python3 scripts/export-api.py http://127.0.0.1:8080` after changing routes.', '', '## Authentication and conventions', '', 'Every `/api/v1` operation and `/mcp` requires `Authorization: Bearer <DOCMON_BEARER_TOKEN>`. The token grants full control of one Docker machine. `/health`, `/openapi.json`, and `/swagger` are anonymous. The TUI uses the local Docker socket without a token by default.', '', 'All JSON uses camelCase and string enums. Encode IDs and path/query values with percent encoding. Fixed request fields are described below. Path parameters override corresponding body fields. Long operations run synchronously, with a 30-minute REST timeout; an interrupted request may already have changed Docker state. Inspect the resource before retrying. CLI/exec responses return `exitCode`, `output` (latest 2000 lines), and `reclaimedBytes`. A nonzero exit code indicates command failure even when HTTP status is 200.', '', 'Errors use `{ "code": "invalid_request", "message": "...", "requestId": "..." }`. Common statuses: 400 invalid input; 401 invalid/missing bearer token; 404 missing Docker/resource record; 409 Docker conflict; 502 Docker/MCP unavailable; 504 REST timeout. Docker may return other 4xx statuses. `X-Request-Id` correlates normal JSON operations with durable history.', '', 'Lists return JSON arrays of current Docker resources. Events retain at most 1000 records; metric history retains at most 10000 samples; request history returns at most 1000 summaries, newest first. Query `/history/{id}` for a redacted full record. Durable history uses daily JSONL files with 30-day retention in `DOCMON_DATA_DIRECTORY` (default `./data`), redacted auth headers and sensitive JSON keys, and 4096-character body limits. History responses and MCP payloads are excluded from body capture to avoid recursive and token-bearing payloads.', '', 'Compose operations use discovered labels and require the original project files mounted at identical host paths. `containers_update` pulls the image and recreates a Compose service. For standalone containers it only pulls, matching the TUI; a restart does not switch an existing container to the new image.', '', 'Browser archive endpoints transfer tar files directly. Upload bodies are raw `application/x-tar`, capped at 4 MiB. The copy-in/out JSON operations use paths on the server host. For a containerized backend these paths are inside its mounts. The remote TUI uses the archive endpoints to transfer files on the TUI client machine.', '', '## Endpoint inventory', '', '| Method | Path | Operation |', '| --- | --- | --- |']
for path, method, op in operations:
    rest.append(f'| {method} | `{path}` | {op.get("summary", "")} |')
rest += ['', '## Endpoint contracts', '']
inventory = []
for path, method, op in operations:
    name = op.get('operationId') or ('health' if path == '/health' else method.lower() + '_' + path)
    group = (op.get('tags') or ['system'])[0]
    content = op.get('requestBody', {}).get('content', {})
    schema = content.get('application/json', {}).get('schema', {})
    required = schema.get('required', [])
    parameter_lines = []
    url = path
    for p in op.get('parameters', []):
        parameter_lines.append(f'- `{p["name"]}` ({p["in"]}, {p.get("schema", {}).get("type", "string")}, {"required" if p.get("required") else "optional"}): {p.get("description", "")}')
        if p['in'] == 'path':
            var = 'imageId' if path.startswith('/api/v1/images/') else 'requestId' if path.startswith('/api/v1/history/') else 'project' if p['name'] == 'project' else 'containerId'
            url = url.replace('{'+p['name']+'}', '{{'+var+'}}')
        elif p.get('required'):
            url += ('&' if '?' in url else '?') + p['name'] + '=/tmp'
    body = {key: params.get(key, '') for key in schema.get('properties', {})}
    if name == 'images_pull': body = {'image':'{{image}}'}
    description = op.get('summary', name) + '.\n\n' + '\n'.join(parameter_lines)
    if method != 'GET': description += '\n\nChanges Docker state. Use disposable resources when testing mutation requests. A completed CLI/exec operation may return a nonzero exitCode with HTTP 200.'
    request = {'method':method, 'header':[], 'url':'{{baseUrl}}'+url, 'description':description}
    if path == '/health': request['auth'] = {'type':'noauth'}
    if 'application/x-tar' in content:
        request['header'] = [{'key':'Content-Type','value':'application/x-tar'}]
        request['body'] = {'mode':'file','file':{'src':''}}
    elif method in ['POST','PUT','PATCH']:
        request['header'] = [{'key':'Content-Type','value':'application/json'}]
        request['body'] = {'mode':'raw','raw':json.dumps(body,indent=2),'options':{'raw':{'language':'json'}}}
    assertion = ['pm.test("Successful HTTP status", () => pm.expect(pm.response.code).to.be.within(200, 299));']
    if '/archive' not in path:
        assertion += ['pm.test("JSON response", () => pm.expect(pm.response.headers.get("Content-Type")).to.include("application/json"));', 'pm.test("No reported operation failure", () => { const b = pm.response.json(); if (b.exitCode !== undefined) pm.expect(b.exitCode).to.equal(0); });']
    item = {'name':name,'request':request,'event':[{'listen':'test','script':{'type':'text/javascript','exec':assertion}}]}
    folders.setdefault(group, []).append(item)
    if method == 'GET' and '{' not in path and path != '/health': smoke.append(item)
    fields = [f'- `{key}` ({value.get("type", "object")}, {"required" if key in required else "optional"})' for key, value in schema.get('properties', {}).items()]
    status = ', '.join(f'`{code}` {r.get("description", "")}' for code,r in op.get('responses', {}).items())
    rest += [f'### `{method} {path}`', '', op.get('summary', ''), '', '**Authentication:** ' + ('None.' if path == '/health' else 'Bearer token required.'), '', '**Parameters:**', '', *(parameter_lines or ['None.']), '', '**Request body:**', '', *(fields or (['Raw tar archive (`application/x-tar`).'] if 'application/x-tar' in content else ['None (or an empty JSON object for POST).'])), '', '**Responses:** ' + status, '', 'Response schemas are defined under this operation in [OpenAPI](assets/openapi.json).', '', '**Example:**', '', '```bash', 'curl -X '+method+" 'http://127.0.0.1:8080"+path+"'"+('' if path == '/health' else " -H \"Authorization: Bearer $DOCMON_BEARER_TOKEN\"")+(" -H 'Content-Type: application/json' --data '"+json.dumps(body)+"'" if method == 'POST' else ''), '```', '']
    if name not in ['containers_archive_download', 'containers_archive_upload', 'health'] and path.startswith('/api/v1'):
        inventory.append({'name':name,'method':method,'path':path,'summary':op.get('summary', ''),'required':[p['name'] for p in op.get('parameters',[]) if p.get('required')]+required,'properties':{**{p['name']:p.get('schema',{}) for p in op.get('parameters',[])}, **schema.get('properties',{})}})
rest += ['## Interactive terminal', '', '`GET /api/v1/terminal` upgrades to a Watson WebSocket. Send a first JSON frame within 10 seconds: `{ "token": "<bearer token>", "id": "<container ID>", "command": ["/bin/sh"] }`. Subsequent text frames contain terminal input; server frames contain UTF-8 terminal output. A missing/invalid credential closes the socket before Docker exec is created. Tokens never belong in the URL. Sessions end when the shell exits or the socket disconnects. Terminal resize negotiation is not implemented yet.', '', '## MCP gateway', '', '`POST`, `GET`, and `DELETE /mcp` implement Voltaic streamable HTTP, with the same Authorization header. MCP session, protocol, replay, and SSE headers pass through the Watson gateway. See [MCP_API.md](MCP_API.md).', '']
(root/'REST_API.md').write_text('\n'.join(rest))
variables = [{'key':key,'value':value} for key,value in {'baseUrl':'http://127.0.0.1:8080','bearerToken':'','containerId':'','imageId':'','image':'alpine:3.20','project':'','service':'','hostPath':'/tmp','requestId':''}.items()]
def collection(title, items): return {'info':{'name':title,'schema':'https://schema.getpostman.com/json/collection/v2.1.0/collection.json','description':'Docmon single-machine monitoring and full Docker control. Set baseUrl and bearerToken in a local environment. No real credentials are committed. The complete collection includes destructive requests; select individual requests or use the read-only smoke collection. Generated from live Watson OpenAPI metadata by scripts/export-api.py.'},'auth':{'type':'bearer','bearer':[{'key':'token','value':'{{bearerToken}}','type':'string'}]},'variable':variables,'item':items}
save('Docmon.postman_collection.json', collection('Docmon API', [{'name':group,'description':f'{group.capitalize()} resources and lifecycle operations. Mutations require machine-operator bearer access. Inspect required variables before running.', 'item':items} for group,items in folders.items()]))
save('Docmon.read-only.postman_collection.json', collection('Docmon read-only smoke', smoke))
save('assets/api-inventory.json', inventory)
mcp = ['# Docmon MCP API', '', 'Voltaic runs in the API server process. Connect streamable HTTP clients to `http://127.0.0.1:8080/mcp` (or the dashboard origin `/mcp`). Set `Authorization: Bearer <DOCMON_BEARER_TOKEN>` on every request. Watson authenticates the public gateway; the internal Voltaic listener independently verifies the token and binds only to `127.0.0.1`. Do not publish the internal MCP port.', '', 'MCP implements initialize, initialized, tools/list, tools/call, cancellation and session management through Voltaic. Preserve `Mcp-Session-Id` and `MCP-Protocol-Version` returned by the transport. Diagnostic demo tools are disabled. No application resources or prompts are registered; those lists are empty.', '', 'Tools share the REST application service, models, and side effects. Successful results are returned as camelCase JSON in MCP text content. Nonzero CLI/exec exit codes also set isError. Resource reads return the JSON resource shape described in [REST_API.md](REST_API.md); mutations return `{ "exitCode": 0, "output": [], "reclaimedBytes": 0 }`. Inspect nonzero exit codes. Tool exceptions become MCP errors. The `containers_copy_in` and `containers_copy_out` host paths refer to the API server machine, not the agent machine. Binary archive transfer and the interactive WebSocket terminal are REST transports; use exec for agent commands.', '', '## Client configuration', '', '```json', json.dumps({'mcpServers':{'docmon':{'url':'https://your-machine/mcp','headers':{'Authorization':'Bearer <token>'}}}},indent=2), '```', '', '## Tools', '']
for op in inventory:
    mcp += [f'### `{op["name"]}`', '', op['summary']+'.', '', f'Equivalent REST operation: `{op["method"]} {op["path"]}`.', '', '| Parameter | Type | Required |', '| --- | --- | --- |']
    fields = dict(op['properties'])
    if op['name'].endswith('_remove'): fields['force']={'type':'boolean'}
    if op['name'] == 'images_pull': fields.update({'id':{'type':'string'}, 'image':{'type':'string'}})
    if op['name'].startswith('stacks_') and op['method']=='POST': fields['service']={'type':'string'}
    for field, shape in fields.items(): mcp += [f'| `{field}` | {shape.get("type", "string")} | {"Yes" if field in op["required"] else "No"} |']
    if not fields: mcp += ['| None | | |']
    example = {k: params.get(k, '<'+k+'>') for k in set(op['required'])}
    if op['name'].startswith('images_') and 'id' in example: example['id'] = '{{imageId}}'
    if op['name'].startswith('history_') and 'id' in example: example['id'] = '{{requestId}}'
    if op['name']=='images_pull':example={'image':'alpine:3.20'}
    mcp += ['', 'Example `tools/call`:', '', '```json', json.dumps({'jsonrpc':'2.0','id':1,'method':'tools/call','params':{'name':op['name'],'arguments':example}},indent=2), '```', '']
(root/'MCP_API.md').write_text('\n'.join(mcp))
print(f'Exported {len(operations)} REST operations and {len(inventory)} MCP tools.')
