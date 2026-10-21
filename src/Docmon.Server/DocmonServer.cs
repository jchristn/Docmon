namespace Docmon.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docmon.Core.Models;
    using Docmon.Core.Services;
    using HttpMethod = WatsonWebserver.Core.HttpMethod;
    using Voltaic.Core;
    using Voltaic.Mcp;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Watson API host and authenticated public gateway to a colocated Voltaic MCP server.</summary>
    public sealed class DocmonServer : IDisposable
    {
        internal static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        private readonly ServerSettings _Settings;
        private readonly DocmonRuntime _Runtime;
        private readonly DocmonOperations _Operations;
        private readonly BearerAuthentication _Authentication;
        private readonly Webserver _Server;
        private readonly McpHttpServer _Mcp;
        private readonly HttpClient _Proxy = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        private readonly MonitoringService _Monitoring;
        private readonly RequestHistoryStore _History;

        /// <summary>Creates the host. Docker connections are lazy and may recover after daemon restarts.</summary>
        /// <param name="settings">Validated environment configuration.</param>
        public DocmonServer(ServerSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _History = new RequestHistoryStore(settings.DataDirectory, settings.Token);
            _Runtime = new DocmonRuntime();
            _Operations = new DocmonOperations(_Runtime);
            _Monitoring = new MonitoringService(_Runtime, settings.DataDirectory);
            _Authentication = new BearerAuthentication(settings.Token);
            WebserverSettings web = new WebserverSettings(settings.Host, settings.Port, false);
            web.Headers.DefaultHeaders.Clear();
            web.Timeout.DefaultTimeout = TimeSpan.FromMinutes(31);
            web.WebSockets.Enable = true;
            web.WebSockets.MaxMessageSize = 65536;
            web.IO.MaxRequestBodySize = 4 * 1024 * 1024;
            _Server = new Webserver(web, NotFoundAsync);
            _Server.Events.ExceptionEncountered += (sender, args) => Console.Error.WriteLine(args.Exception);
            _Mcp = new McpHttpServer("127.0.0.1", settings.McpPort, includeDiagnosticTools: false)
            {
                ServerName = "Docmon", ServerVersion = "0.1.1", EnableCors = false, EnableLegacyEndpoints = false, IncludeToolExceptionMessages = true,
                AuthenticationHandler = request => Task.FromResult(new Voltaic.Core.AuthenticationResult { IsAuthenticated = _Authentication.Validate(request.Headers["Authorization"]), Principal = "machine-operator" })
            };
            Configure();
        }

        /// <summary>Runs until cancellation or Ctrl+C.</summary>
        public static async Task RunAsync()
        {
            using CancellationTokenSource lifetime = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (sender, args) => { args.Cancel = true; lifetime.Cancel(); };
            Console.CancelKeyPress += cancel;
            using PosixSignalRegistration? terminate = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; lifetime.Cancel(); });
            try
            {
                using DocmonServer host = new DocmonServer(new ServerSettings());
                await host.StartAsync(lifetime.Token).ConfigureAwait(false);
            }
            finally { Console.CancelKeyPress -= cancel; }
        }

        /// <summary>Runs HTTP, MCP, and monitoring until cancelled.</summary>
        /// <param name="token">Host lifetime.</param>
        public async Task StartAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Task mcp = _Mcp.StartAsync(token);
            if (mcp.IsCompleted) throw new IOException("The private MCP listener could not start. Check DOCMON_MCP_PORT for conflicts.");
            using CancellationTokenRegistration stopping = token.Register(_Mcp.Stop);
            _Server.Start();
            Console.WriteLine("Docmon API listening on " + _Settings.Host + ":" + _Settings.Port + "; MCP at /mcp");
            try { await Task.WhenAll(mcp, _Monitoring.RunAsync(token)).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally { _Server.Stop(); _Mcp.Stop(); }
        }

        internal async Task<object> ExecuteAsync(string operation, OperationRequest request, CancellationToken token)
        {
            object result = await ExecuteCoreAsync(operation, request, token).ConfigureAwait(false);
            return operation switch
            {
                "containers_list" => Enumeration.Page((IEnumerable<ContainerInfo>)result, request, operation),
                "images_list" => Enumeration.Page((IEnumerable<ImageInfo>)result, request, operation),
                "stacks_list" => Enumeration.Page((IEnumerable<ComposeStack>)result, request, operation),
                "events_list" => Enumeration.Page((IEnumerable<DockerEventInfo>)result, request, operation),
                "metrics_list" or "metrics_history" => Enumeration.Page((IEnumerable<ContainerStatsSample>)result, request, operation),
                "metrics_summary" => Enumeration.Page((IEnumerable<MetricHistoryBucket>)result, request, operation + ":" + request.Timeframe),
                "history_list" => Enumeration.Page((IEnumerable<RequestRecord>)result, request, operation + ":" + request.Timeframe),
                "containers_logs" or "images_tags" => Enumeration.Page((IEnumerable<string>)result, request, operation + ":" + request.Id),
                _ => result
            };
        }
        private async Task<object> ExecuteCoreAsync(string operation, OperationRequest request, CancellationToken token)
        {
            switch (operation)
            {
                case "system_info": return new ServerInfo { Endpoint = _Runtime.Provider.Endpoint, MonitoringError = _Monitoring.Error };
                case "metrics_list": return _Monitoring.Metrics;
                case "metrics_history": return _Monitoring.History;
                case "metrics_summary": return await _Monitoring.Journal.GetMetricHistoryAsync(request.Timeframe ?? "hour", request.ContainerIdFilter ?? string.Empty, token).ConfigureAwait(false);
                case "events_list": return _Monitoring.Events;
                case "history_list": return request.Timeframe == null ? _History.ReadSummary() : await _History.ReadWindowAsync(request.Timeframe, token).ConfigureAwait(false);
                case "history_summary": return await _History.SummarizeAsync(request.Timeframe, token).ConfigureAwait(false);
                case "history_inspect": return await _History.ReadByIdAsync(request.Id, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Request record not found: " + request.Id);
                default: return await _Operations.ExecuteAsync(operation, request, token).ConfigureAwait(false);
            }
        }

        private void Configure()
        {
            _Server.Routes.AuthenticateRequest = AuthenticateAsync;
            _Server.Routes.Preflight = PreflightAsync;
            _Server.Routes.PostRouting = PostRoutingAsync;
            _Server.UseOpenApi(api =>
            {
                api.Info.Title = "Docmon Machine API"; api.Info.Version = "0.1.1";
                api.Info.Description = "Authenticated Docker machine monitoring and control. All bearer tokens grant full machine control.";
                api.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = "http", Scheme = "bearer" };
            });
            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/health", HealthAsync, openApiMetadata: OpenApiRouteMetadata.Create("Docker daemon readiness", "system").WithResponse(200, OpenApiResponseMetadata.Json("Docker is reachable.", new OpenApiSchemaMetadata { Type = "object" })).WithResponse(503, OpenApiResponseMetadata.Json("Docker is unavailable.", new OpenApiSchemaMetadata { Type = "object" })));
            new OperationRoutes(this).Register(_Server);
            new ArchiveRoutes(_Runtime).Register(_Server);
            _Server.WebSocket("/api/v1/terminal", new TerminalRoutes(_Runtime, _Authentication).HandleAsync);
            foreach (HttpMethod method in new[] { HttpMethod.GET, HttpMethod.POST, HttpMethod.DELETE })
                _Server.Routes.PostAuthentication.Static.Add(method, "/mcp", ProxyMcpAsync);
            foreach (ApiOperation operation in ApiCatalog.Operations)
            {
                _Mcp.RegisterTool(operation.Name, operation.Summary, OperationRoutes.ToolSchema(operation), async (RpcParameters? args, CancellationToken token) =>
                {
                    OperationRequest request = JsonSerializer.Deserialize<OperationRequest>(args?.RawJson ?? "{}", Json) ?? new OperationRequest();
                    object result = await ExecuteAsync(operation.Name, request, token).ConfigureAwait(false);
                    McpToolCallResult response = McpToolCallResult.FromText(JsonSerializer.Serialize(result, Json));
                    response.IsError = result is OperationResult completed && completed.ExitCode != 0;
                    return response;
                });
            }
        }

        private async Task AuthenticateAsync(HttpContextBase context)
        {
            AddCors(context);
            if (_Authentication.Validate(context.Request.RetrieveHeaderValue("Authorization"))) { context.Metadata = new RequestContext(); return; }
            context.Response.Headers.Add("WWW-Authenticate", "Bearer realm=\"Docmon\"");
            await SendJsonAsync(context, new ApiError { Code = "unauthorized", Message = "A valid bearer token is required." }, 401).ConfigureAwait(false);
        }
        private async Task HealthAsync(HttpContextBase context)
        {
            try { await _Runtime.Provider.VerifyConnectionAsync(context.Token).ConfigureAwait(false); await SendJsonAsync(context, new HealthResponse()).ConfigureAwait(false); }
            catch (DockerConnectionException) { await SendJsonAsync(context, new HealthResponse { Status = "unavailable" }, 503).ConfigureAwait(false); }
        }
        private Task NotFoundAsync(HttpContextBase context) => SendJsonAsync(context, new ApiError { Code = "not_found", Message = "Route not found." }, 404);
        private Task PreflightAsync(HttpContextBase context)
        {
            AddCors(context); context.Response.StatusCode = 204; return context.Response.Send(context.Token);
        }
        private async Task PostRoutingAsync(HttpContextBase context)
        {
            context.Timestamp.End = DateTime.UtcNow;
            AddCors(context);
            RequestRecord record = new RequestRecord { Method = context.Request.Method.ToString(), Path = context.Request.Url.RawWithoutQuery, Status = context.Response.StatusCode, ElapsedMs = context.Timestamp.TotalMs ?? 0 };
            record.Id = context.Response.Headers["X-Request-Id"] ?? record.Id;
            record.PrincipalName = (context.Metadata as RequestContext)?.PrincipalName;
            record.RequestHeaders = _History.Headers(context.Request.Headers);
            record.ResponseHeaders = _History.Headers(context.Response.Headers);
            // Avoid recursive history capture and binary streams.
            if (!record.Path.EndsWith("/history", StringComparison.Ordinal) && record.Path != "/mcp")
            {
                string requestBody = context.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true ? context.Request.DataAsString : string.Empty;
                string responseBody = context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true ? context.Response.DataAsString : string.Empty;
                record.Truncated = requestBody.Length > 4096 || responseBody.Length > 4096;
                record.RequestBody = _History.Body(requestBody); record.ResponseBody = _History.Body(responseBody);
            }
            try { await _History.AppendAsync(record, CancellationToken.None).ConfigureAwait(false); }
            catch (IOException ex) { Console.Error.WriteLine("Request history write failed: " + ex.Message); }
            Console.WriteLine(record.Method + " " + record.Path + " " + record.Status + " " + record.ElapsedMs.ToString("F1") + "ms");
        }
        private void AddCors(HttpContextBase context)
        {
            context.Response.Headers.Set("Cache-Control", "no-store");
            string origin = context.Request.RetrieveHeaderValue("Origin");
            if (string.IsNullOrEmpty(_Settings.AllowedOrigin) || origin != _Settings.AllowedOrigin) return;
            context.Response.Headers.Set("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Set("Vary", "Origin");
            context.Response.Headers.Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            context.Response.Headers.Set("Access-Control-Allow-Headers", "Authorization, Content-Type, Mcp-Session-Id, MCP-Protocol-Version, Last-Event-ID");
            context.Response.Headers.Set("Access-Control-Expose-Headers", "Mcp-Session-Id, X-Request-Id");
        }
        internal static async Task SendJsonAsync(HttpContextBase context, object value, int status = 200)
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Set("Cache-Control", "no-store");
            await context.Response.Send(JsonSerializer.Serialize(value, Json), context.Token).ConfigureAwait(false);
        }
        private async Task ProxyMcpAsync(HttpContextBase context)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod(context.Request.Method.ToString()), "http://127.0.0.1:" + _Settings.McpPort + "/mcp");
            foreach (string header in new[] { "Authorization", "Accept", "Mcp-Session-Id", "MCP-Protocol-Version", "Last-Event-ID" })
            {
                string value = context.Request.RetrieveHeaderValue(header);
                if (!string.IsNullOrEmpty(value)) request.Headers.TryAddWithoutValidation(header, value);
            }
            if (context.Request.Method == HttpMethod.POST) request.Content = new StringContent(context.Request.DataAsString ?? "", Encoding.UTF8, "application/json");
            try
            {
                using HttpResponseMessage response = await _Proxy.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.Token).ConfigureAwait(false);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                foreach (string header in new[] { "Mcp-Session-Id", "MCP-Protocol-Version", "WWW-Authenticate" })
                    if (response.Headers.TryGetValues(header, out IEnumerable<string>? values)) context.Response.Headers.Set(header, string.Join(", ", values));
                if (context.Response.ContentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ChunkedTransfer = true;
                    using Stream stream = await response.Content.ReadAsStreamAsync(context.Token).ConfigureAwait(false);
                    byte[] buffer = new byte[8192]; int read;
                    while ((read = await stream.ReadAsync(buffer, context.Token).ConfigureAwait(false)) > 0)
                        if (!await context.Response.SendChunk(buffer.AsSpan(0, read).ToArray(), false, context.Token).ConfigureAwait(false)) break;
                    await context.Response.SendChunk(Array.Empty<byte>(), true, context.Token).ConfigureAwait(false);
                }
                else await context.Response.Send(await response.Content.ReadAsByteArrayAsync(context.Token).ConfigureAwait(false), context.Token).ConfigureAwait(false);
            }
            catch (HttpRequestException) { await SendJsonAsync(context, new ApiError { Code = "mcp_unavailable", Message = "MCP transport is unavailable." }, 502).ConfigureAwait(false); }
        }
        /// <summary>Stops listeners and disposes owned services.</summary>
        public void Dispose() { _Server.Dispose(); _Mcp.Dispose(); _Proxy.Dispose(); _Runtime.Dispose(); _History.Dispose(); }
    }
}
