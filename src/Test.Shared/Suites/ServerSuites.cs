namespace Test.Shared.Suites
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Services;
    using Docmon.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    internal static class ServerSuites
    {
        internal static TestSuiteDescriptor ApiSuite()
        {
            Cases c = new Cases("Server.Api");
            return new TestSuiteDescriptor("Server.Api", "REST, MCP, and remote adapter contracts", new[]
            {
                c.Sync("Bearer", "Bearer verifier accepts exact token and rejects missing, wrong, and malformed credentials", () =>
                {
                    BearerAuthentication auth = new BearerAuthentication("expected");
                    Check.True(auth.Validate("bearer expected"), "valid bearer");
                    foreach (string? invalid in new string?[] { null, "", "expected", "Bearer wrong", "Bearer expected ", "Basic expected" }) Check.False(auth.Validate(invalid), "reject " + invalid);
                }),
                c.Async("RestAuthentication", "Every catalog endpoint is authenticated, including mutation requests", async ct =>
                {
                    using ServerFixture fixture = new ServerFixture();
                    try
                    {
                        fixture.Client.DefaultRequestHeaders.Authorization = null;
                        foreach (ApiOperation operation in ApiCatalog.Operations)
                        {
                            string path = operation.Path.Replace("{id}", "missing").Replace("{project}", "missing");
                            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(operation.Method), path);
                            if (operation.Method == "POST") request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                            using HttpResponseMessage response = await fixture.Client.SendAsync(request, ct).ConfigureAwait(false);
                            Check.Equal(HttpStatusCode.Unauthorized, response.StatusCode, operation.Name);
                        }
                        foreach (string path in new[] { "/mcp", "/api/v1/containers/missing/archive?path=/tmp" })
                        { using HttpResponseMessage response = await fixture.Client.GetAsync(path, ct).ConfigureAwait(false); Check.Equal(HttpStatusCode.Unauthorized, response.StatusCode, path); }
                    }
                    finally { await fixture.StopAsync().ConfigureAwait(false); }
                }),
                c.Async("OpenApiAndValidation", "OpenAPI describes bearer security and typed requests; invalid exec is rejected before Docker", async ct =>
                {
                    using ServerFixture fixture = new ServerFixture();
                    try
                    {
                        using HttpResponseMessage spec = await fixture.Client.GetAsync("/openapi.json", ct).ConfigureAwait(false);
                        using JsonDocument document = JsonDocument.Parse(await spec.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                        JsonElement paths = document.RootElement.GetProperty("paths");
                        foreach (ApiOperation operation in ApiCatalog.Operations) Check.True(paths.GetProperty(operation.Path).GetProperty(operation.Method.ToLowerInvariant()).GetProperty("security").GetArrayLength() > 0, operation.Name + " security");
                        using HttpResponseMessage invalid = await fixture.Client.PostAsync("/api/v1/containers/missing/exec", new StringContent("{\"command\":[]}", Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.BadRequest, invalid.StatusCode, "empty command");
                        using HttpResponseMessage badJson = await fixture.Client.PostAsync("/api/v1/images/pull", new StringContent("{", Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.BadRequest, badJson.StatusCode, "malformed JSON");
                        using RemoteDocmonClient remote = new RemoteDocmonClient(fixture.Client.BaseAddress!, ServerFixture.Token);
                        await remote.VerifyAsync(ct).ConfigureAwait(false);
                        using RemoteDocmonClient denied = new RemoteDocmonClient(fixture.Client.BaseAddress!, "wrong");
                        bool rejected = false; try { await denied.VerifyAsync(ct).ConfigureAwait(false); } catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { rejected = true; }
                        Check.True(rejected, "remote rejects invalid token");
                    }
                    finally { await fixture.StopAsync().ConfigureAwait(false); }
                    string audit = string.Join("", Directory.GetFiles(fixture.DirectoryPath, "*.jsonl").Select(File.ReadAllText));
                    Check.False(audit.Contains(ServerFixture.Token, StringComparison.Ordinal), "credentials redacted from disk");
                }),
                c.Async("TerminalAuthentication", "Terminal rejects an invalid first-frame token before creating a Docker exec", async ct =>
                {
                    using ServerFixture fixture = new ServerFixture();
                    try
                    {
                        using ClientWebSocket socket = new ClientWebSocket();
                        UriBuilder url = new UriBuilder(fixture.Client.BaseAddress!) { Scheme = "ws", Path = "/api/v1/terminal" };
                        await socket.ConnectAsync(url.Uri, ct).ConfigureAwait(false);
                        byte[] body = Encoding.UTF8.GetBytes("{\"token\":\"wrong\",\"id\":\"missing\"}");
                        await socket.SendAsync(body.AsMemory(), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
                        byte[] buffer = new byte[512];
                        ValueWebSocketReceiveResult message = await socket.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                        Check.Equal(WebSocketMessageType.Close, message.MessageType, "invalid terminal token closes connection");
                        Check.Equal(WebSocketCloseStatus.PolicyViolation, socket.CloseStatus!.Value, "terminal auth rejection");
                    }
                    finally { await fixture.StopAsync().ConfigureAwait(false); }
                }),
                c.Async("Mcp", "MCP initialize and tools/list pass through Watson with session continuity and no diagnostic tools", async ct =>
                {
                    using ServerFixture fixture = new ServerFixture();
                    try
                    {
                        fixture.Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        fixture.Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                        using HttpResponseMessage init = await RpcAsync(fixture.Client, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\",\"capabilities\":{},\"clientInfo\":{\"name\":\"tests\",\"version\":\"1\"}}}", ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.OK, init.StatusCode, "MCP initialize");
                        fixture.Client.DefaultRequestHeaders.Add("Mcp-Session-Id", init.Headers.GetValues("Mcp-Session-Id").Single());
                        fixture.Client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-03-26");
                        using HttpResponseMessage initialized = await RpcAsync(fixture.Client, "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", ct).ConfigureAwait(false);
                        using HttpResponseMessage tools = await RpcAsync(fixture.Client, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}", ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.OK, tools.StatusCode, "MCP tools/list");
                        using JsonDocument document = JsonDocument.Parse(await tools.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                        string[] names = document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).ToArray();
                        foreach (ApiOperation operation in ApiCatalog.Operations) Check.True(names.Contains(operation.Name), "tool " + operation.Name);
                        Check.False(names.Contains("echo") || names.Contains("ping"), "no diagnostics");
                        using HttpResponseMessage called = await RpcAsync(fixture.Client, "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"system_info\",\"arguments\":{}}}", ct).ConfigureAwait(false);
                        string calledBody = await called.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.OK, called.StatusCode, "MCP tool invocation");
                        Check.True(calledBody.Contains("version", StringComparison.Ordinal), "MCP resource payload");
                        Check.False(calledBody.Contains(ServerFixture.Token, StringComparison.Ordinal), "MCP does not expose bearer token");
                    }
                    finally { await fixture.StopAsync().ConfigureAwait(false); }
                })
            });
        }
        private static Task<HttpResponseMessage> RpcAsync(HttpClient client, string json, CancellationToken token) => client.PostAsync("/mcp", new StringContent(json, Encoding.UTF8, "application/json"), token);
    }
}
