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
    using Docmon.Core.Models;
    using Docmon.Core.Monitoring;
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
                c.Async("MetricTimeframes", "Historical REST enumeration, remote adapter, and timeframe validation use durable buckets", async ct =>
                {
                    using ServerFixture fixture = new ServerFixture(directory =>
                    {
                        MetricHistoryStore store = new MetricHistoryStore(Path.Combine(directory, "metrics"));
                        store.Add(new ContainerStatsSample { ContainerId = "historical-container", AtUtc = DateTime.UtcNow.AddMinutes(-3), CpuPercent = 42, NetworkRxBytes = 1234 });
                        store.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
                    });
                    try
                    {
                        using RemoteDocmonClient remote = new RemoteDocmonClient(fixture.Client.BaseAddress!, ServerFixture.Token);
                        string[] names = { "hour", "day", "week", "month" };
                        int[] counts = { 60, 96, 84, 90 };
                        for (int i = 0; i < names.Length; i++)
                        {
                            System.Collections.Generic.IReadOnlyList<MetricHistoryBucket> rows = await remote.GetMetricHistoryAsync(names[i], "historical-container", ct).ConfigureAwait(false);
                            Check.Equal(counts[i], rows.Count, names[i]);
                            Check.Close(42, rows.Single(row => row.HasData).CpuPercent, 0, "durable value through HTTP adapter");
                        }
                        using HttpResponseMessage page = await fixture.Client.GetAsync("/api/v1/metrics/summary?timeframe=day&maxResults=10", ct).ConfigureAwait(false);
                        using JsonDocument json = JsonDocument.Parse(await page.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                        Check.Equal(96, json.RootElement.GetProperty("totalRecords").GetInt32(), "full historical count");
                        Check.Equal(10, json.RootElement.GetProperty("objects").GetArrayLength(), "enumeration page size");
                        string cursor = Uri.EscapeDataString(json.RootElement.GetProperty("continuationToken").GetString()!);
                        using HttpResponseMessage changed = await fixture.Client.GetAsync("/api/v1/metrics/summary?timeframe=week&maxResults=10&continuationToken=" + cursor, ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.BadRequest, changed.StatusCode, "cursor bound to timeframe");
                        using HttpResponseMessage invalid = await fixture.Client.GetAsync("/api/v1/metrics/summary?timeframe=year", ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.BadRequest, invalid.StatusCode, "invalid timeframe");
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
                c.Async("Enumeration", "Collection queries page, filter and order results, rejecting invalid sizes and cross-query tokens", async ct =>
                {
                    using ServerFixture fixture = new ServerFixture();
                    try
                    {
                        for (int i = 0; i < 4; i++) { using HttpResponseMessage generated = await fixture.Client.GetAsync("/api/v1/system/info", ct).ConfigureAwait(false); }
                        string query = "/api/v1/history?maxResults=1&sortBy=atUtc&ordering=Ascending&search=system";
                        using HttpResponseMessage first = await fixture.Client.GetAsync(query, ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.OK, first.StatusCode, "first page status");
                        using JsonDocument page = JsonDocument.Parse(await first.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                        JsonElement result = page.RootElement;
                        Check.Equal(1, result.GetProperty("objects").GetArrayLength(), "page size");
                        Check.True(result.GetProperty("totalRecords").GetInt32() >= 4, "filter total");
                        Check.False(result.GetProperty("endOfResults").GetBoolean(), "more pages");
                        string continuation = result.GetProperty("continuationToken").GetString()!;
                        using HttpResponseMessage second = await fixture.Client.GetAsync(query + "&continuationToken=" + Uri.EscapeDataString(continuation), ct).ConfigureAwait(false);
                        using JsonDocument next = JsonDocument.Parse(await second.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                        Check.False(result.GetProperty("objects")[0].GetProperty("id").GetString() == next.RootElement.GetProperty("objects")[0].GetProperty("id").GetString(), "next page advances");
                        foreach (string invalidQuery in new[] { "maxResults=0", "maxResults=1001", "maxResults=oops", "ordering=invalid", "sortBy=missing", "continuationToken=invalid", "startUtc=invalid", "filters=%7Bbad", "maxResults=2&search=system&continuationToken=" + Uri.EscapeDataString(continuation) })
                        { using HttpResponseMessage invalid = await fixture.Client.GetAsync("/api/v1/history?" + invalidQuery, ct).ConfigureAwait(false); Check.Equal(HttpStatusCode.BadRequest, invalid.StatusCode, invalidQuery); }
                        using HttpResponseMessage filtered = await fixture.Client.GetAsync("/api/v1/history?filters=" + Uri.EscapeDataString("{\"method\":\"POST\"}"), ct).ConfigureAwait(false);
                        using JsonDocument empty = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                        Check.Equal(0, empty.RootElement.GetProperty("totalRecords").GetInt32(), "no matching records");
                        Check.True(empty.RootElement.GetProperty("endOfResults").GetBoolean(), "empty page is final");
                    }
                    finally { await fixture.StopAsync().ConfigureAwait(false); }
                }),
                c.Sync("HistoryBuckets", "History windows have exact bucket counts, intervals and boundary semantics", () =>
                {
                    DateTime end = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
                    foreach (string timeframe in new[] { "hour", "day", "week", "month" })
                    {
                        RequestHistorySummary summary = RequestHistorySummary.Create(timeframe, end);
                        int count = timeframe switch { "hour" => 60, "day" => 96, "week" => 84, _ => 90 };
                        int width = timeframe switch { "hour" => 1, "day" => 15, "week" => 120, _ => 480 };
                        Check.Equal(count, summary.Buckets.Count, timeframe + " bucket count");
                        Check.Equal(width, summary.BucketMinutes, timeframe + " bucket interval");
                        Check.Equal(end.AddMinutes(-count * width), summary.StartUtc, "window start");
                        summary.Add(new RequestRecord { AtUtc = summary.StartUtc, Status = 200 });
                        summary.Add(new RequestRecord { AtUtc = summary.StartUtc.AddMinutes(width), Status = 500 });
                        summary.Add(new RequestRecord { AtUtc = end.AddTicks(-1), Status = 204 });
                        summary.Add(new RequestRecord { AtUtc = end, Status = 200 });
                        summary.Add(new RequestRecord { AtUtc = summary.StartUtc.AddTicks(-1), Status = 200 });
                        Check.Equal(3L, summary.TotalCount, "half-open boundaries");
                        Check.Equal(1L, summary.Buckets[0].Ok, "first bucket");
                        Check.Equal(1L, summary.Buckets[1].Failed, "boundary goes to next bucket");
                        Check.Equal(1L, summary.Buckets[count - 1].Ok, "last bucket");
                        Check.Equal(2L, summary.TotalSuccess, "success total");
                        Check.Equal(1L, summary.TotalFailure, "failure total");
                    }
                    Check.Throws<ArgumentException>(() => RequestHistorySummary.Create("invalid", end), "invalid window rejected");
                }),
                c.Async("HistorySummaryRoute", "History summaries expose complete windows and reject invalid timeframes", async ct =>
                {
                    using ServerFixture fixture = new ServerFixture();
                    try
                    {
                        foreach (string timeframe in new[] { "hour", "day", "week", "month" })
                        {
                            using HttpResponseMessage response = await fixture.Client.GetAsync("/api/v1/history/summary?timeframe=" + timeframe, ct).ConfigureAwait(false);
                            Check.Equal(HttpStatusCode.OK, response.StatusCode, timeframe + " route");
                            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                            int count = timeframe switch { "hour" => 60, "day" => 96, "week" => 84, _ => 90 };
                            Check.Equal(count, json.RootElement.GetProperty("buckets").GetArrayLength(), timeframe + " complete buckets");
                        }
                        using HttpResponseMessage invalid = await fixture.Client.GetAsync("/api/v1/history/summary?timeframe=invalid", ct).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.BadRequest, invalid.StatusCode, "invalid timeframe");
                    }
                    finally { await fixture.StopAsync().ConfigureAwait(false); }
                }),
                c.Async("DurableRequestDetails", "Request details remain available after the summary cache evicts an entry", async ct =>
                {
                    string directory = Path.Combine(Path.GetTempPath(), "docmon-history-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        using (RequestHistoryStore history = new RequestHistoryStore(directory, "test-token"))
                        {
                            RequestRecord first = new RequestRecord { Path = "/original", RequestBody = "original-body" };
                            await history.AppendAsync(first, ct).ConfigureAwait(false);
                            for (int i = 0; i < 1001; i++) await history.AppendAsync(new RequestRecord { Path = "/later" }, ct).ConfigureAwait(false);
                            Check.False(history.ReadSummary().Any(record => record.Id == first.Id), "original evicted from summary cache");
                            RequestRecord? retained = await history.ReadByIdAsync(first.Id, ct).ConfigureAwait(false);
                            Check.Equal("original-body", retained?.RequestBody, "full details recovered from retained journal");
                            Check.True(await history.ReadByIdAsync("missing", ct).ConfigureAwait(false) == null, "missing records remain missing");
                            RequestHistorySummary summary = await history.SummarizeAsync("month", ct).ConfigureAwait(false);
                            Check.Equal(1002L, summary.TotalCount, "summary includes journal records outside the cache");
                            RequestRecord[] window = await history.ReadWindowAsync("month", ct).ConfigureAwait(false);
                            Check.Equal(1002, window.Length, "timeframe list includes retained journal");
                            Check.True(window.All(record => record.RequestBody == ""), "window list omits bodies");
                        }
                    }
                    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
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
