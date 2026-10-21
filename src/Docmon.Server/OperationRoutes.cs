namespace Docmon.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docmon.Core.Models;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class OperationRoutes
    {
        private readonly DocmonServer _Host;
        internal OperationRoutes(DocmonServer host) { _Host = host; }
        internal void Register(Webserver server)
        {
            foreach (ApiOperation operation in ApiCatalog.Operations)
            {
                HttpMethod method = Enum.Parse<HttpMethod>(operation.Method);
                OpenApiRouteMetadata metadata = OpenApiRouteMetadata.Create(operation.Summary, operation.Name.Split('_')[0]);
                metadata.OperationId = operation.Name;
                metadata.Security = new List<string> { "Bearer" };
                foreach (string name in new[] { "id", "project" })
                    if (operation.Path.Contains("{" + name + "}")) metadata.WithParameter(OpenApiParameterMetadata.Path(name, "Resource identifier", OpenApiSchemaMetadata.String()));
                if (operation.Name == "containers_logs") metadata.WithParameter(OpenApiParameterMetadata.Query("tail", "Trailing log lines, from 1 through 10000. Default 200.", false, OpenApiSchemaMetadata.Integer()));
                if (operation.Name == "history_list" || operation.Name == "history_summary" || operation.Name == "metrics_summary") metadata.WithParameter(OpenApiParameterMetadata.Query("timeframe", "Rolling window: hour (60 one-minute buckets), day (96 fifteen-minute buckets), week (84 two-hour buckets), month (90 eight-hour buckets over 30 days).", false, OpenApiSchemaMetadata.String()));
                if (Enumeration.IsCollection(operation))
                    foreach (PropertyInfo property in typeof(EnumerationQuery).GetProperties())
                        metadata.WithParameter(OpenApiParameterMetadata.Query(JsonNamingPolicy.CamelCase.ConvertName(property.Name), property.Name == "Filters" ? "JSON object mapping scalar field names to substring filters." : "EnumerationQuery " + property.Name, false, property.Name == "Filters" ? OpenApiSchemaMetadata.String() : Schema(property.PropertyType)));
                if (operation.Method == "POST")
                {
                    OpenApiSchemaMetadata body = RequestSchema(operation);
                    foreach (string field in new[] { "id", "project" })
                        if (operation.Path.Contains("{" + field + "}")) { body.Properties.Remove(field); body.Required.Remove(field); }
                    metadata.WithRequestBody(OpenApiRequestBodyMetadata.Json(body, "Operation parameters. Path parameters are authoritative.", body.Required.Any()));
                }
                metadata.WithResponse(200, OpenApiResponseMetadata.Json("Completed request. Inspect exitCode for CLI and exec operations.", ResponseSchema(operation)));
                foreach (int code in new[] { 400, 401, 404, 409, 500, 502, 504 }) metadata.WithResponse(code, OpenApiResponseMetadata.Json("Structured error with code, message and requestId.", Schema(typeof(ApiError))));
                if (operation.Path.Contains('{')) server.Routes.PostAuthentication.Parameter.Add(method, operation.Path, ctx => HandleAsync(operation, ctx), openApiMetadata: metadata);
                else server.Routes.PostAuthentication.Static.Add(method, operation.Path, ctx => HandleAsync(operation, ctx), openApiMetadata: metadata);
            }
        }
        private async Task HandleAsync(ApiOperation operation, HttpContextBase context)
        {
            string requestId = Guid.NewGuid().ToString("N");
            context.Response.Headers.Set("X-Request-Id", requestId);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(30));
            try
            {
                OperationRequest request = new OperationRequest();
                if (operation.Method == "POST" && !string.IsNullOrWhiteSpace(context.Request.DataAsString))
                    request = JsonSerializer.Deserialize<OperationRequest>(context.Request.DataAsString, DocmonServer.Json) ?? throw new ArgumentException("JSON body must be an object.");
                if (operation.Path.Contains("{id}")) request.Id = Uri.UnescapeDataString(context.Request.Url.Parameters["id"] ?? string.Empty);
                if (operation.Path.Contains("{project}")) request.Project = Uri.UnescapeDataString(context.Request.Url.Parameters["project"] ?? string.Empty);
                if (operation.Name == "containers_logs" && context.Request.Query.Elements["tail"] != null)
                {
                    if (!int.TryParse(context.Request.Query.Elements["tail"], out int tail)) throw new ArgumentException("tail must be an integer.");
                    request.Tail = tail;
                }
                if (Enumeration.IsCollection(operation))
                {
                    Dictionary<string, object?> values = new Dictionary<string, object?>();
                    foreach (PropertyInfo property in typeof(EnumerationQuery).GetProperties())
                    {
                        string name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                        string? value = context.Request.Query.Elements[name];
                        if (value == null) continue;
                        value = WebUtility.UrlDecode(value);
                        if (property.Name == "MaxResults") { if (!int.TryParse(value, out int size)) throw new ArgumentException("maxResults must be an integer."); values[name] = size; }
                        else if (property.Name == "Filters") values[name] = JsonSerializer.Deserialize<Dictionary<string, string>>(value, DocmonServer.Json) ?? throw new ArgumentException("filters must be a JSON object.");
                        else values[name] = value;
                    }
                    EnumerationQuery query = JsonSerializer.Deserialize<EnumerationQuery>(JsonSerializer.Serialize(values, DocmonServer.Json), DocmonServer.Json)!;
                    foreach (PropertyInfo property in typeof(EnumerationQuery).GetProperties()) property.SetValue(request, property.GetValue(query));
                }
                if (operation.Name == "history_list" || operation.Name == "history_summary" || operation.Name == "metrics_summary") request.Timeframe = WebUtility.UrlDecode(context.Request.Query.Elements["timeframe"]);
                object result = await _Host.ExecuteAsync(operation.Name, request, timeout.Token).ConfigureAwait(false);
                await DocmonServer.SendJsonAsync(context, result).ConfigureAwait(false);
            }
            catch (JsonException ex) { await ErrorAsync(context, requestId, 400, "invalid_json", ex.Message).ConfigureAwait(false); }
            catch (ArgumentException ex) { await ErrorAsync(context, requestId, 400, "invalid_request", ex.Message).ConfigureAwait(false); }
            catch (KeyNotFoundException ex) { await ErrorAsync(context, requestId, 404, "not_found", ex.Message).ConfigureAwait(false); }
            catch (DockerApiException ex)
            {
                int status = (int)ex.StatusCode;
                if (status < 400 || status >= 500) status = 502;
                await ErrorAsync(context, requestId, status, "docker_error", ex.Message).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!context.Token.IsCancellationRequested) { await ErrorAsync(context, requestId, 504, "timeout", "Operation exceeded the 30 minute limit.").ConfigureAwait(false); }
            catch (OperationCanceledException) when (context.Token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine(requestId + " " + ex);
                await ErrorAsync(context, requestId, 502, "operation_failed", "Docker operation failed. Check the server logs using requestId.").ConfigureAwait(false);
            }
        }
        private static Task ErrorAsync(HttpContextBase context, string requestId, int status, string code, string message) => DocmonServer.SendJsonAsync(context, new ApiError { Code = code, Message = message, RequestId = requestId }, status);
        internal static OpenApiSchemaMetadata RequestSchema(ApiOperation operation)
        {
            HashSet<string> names = operation.Required.ToHashSet();
            if (operation.Name == "history_list" || operation.Name == "history_summary" || operation.Name == "metrics_summary") names.Add("timeframe");
            if (Enumeration.IsCollection(operation))
                foreach (PropertyInfo property in typeof(EnumerationQuery).GetProperties()) names.Add(JsonNamingPolicy.CamelCase.ConvertName(property.Name));
            if (operation.Name.EndsWith("_remove")) names.Add("force");
            if (operation.Name == "images_pull") { names.Add("image"); names.Add("id"); }
            if (operation.Name.StartsWith("stacks_") && operation.Method == "POST") names.Add("service");
            if (operation.Name == "containers_logs") names.Add("tail");
            Dictionary<string, OpenApiSchemaMetadata> fields = new Dictionary<string, OpenApiSchemaMetadata>();
            foreach (string name in names)
            {
                if (name == "maxResults") fields[name] = OpenApiSchemaMetadata.Integer();
                else if (name == "filters") fields[name] = new OpenApiSchemaMetadata { Type = "object", Description = "Scalar field substring filters." };
                else if (name == "force") fields[name] = OpenApiSchemaMetadata.Boolean();
                else if (name == "tail") fields[name] = OpenApiSchemaMetadata.Integer();
                else if (name == "command") fields[name] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String());
                else fields[name] = OpenApiSchemaMetadata.String();
            }
            return new OpenApiSchemaMetadata { Type = "object", Properties = fields, Required = operation.Required.ToList() };
        }
        internal static object ToolSchema(ApiOperation operation)
        {
            OpenApiSchemaMetadata schema = RequestSchema(operation);
            Dictionary<string, object> properties = new Dictionary<string, object>();
            foreach (KeyValuePair<string, OpenApiSchemaMetadata> item in schema.Properties)
                properties[item.Key] = item.Value.Type == "array" ? new { type = "array", items = new { type = "string" } } : (object)new { type = item.Value.Type };
            return new { type = "object", properties, required = operation.Required, additionalProperties = false };
        }
        private static OpenApiSchemaMetadata ResponseSchema(ApiOperation operation)
        {
            Type type = operation.Name switch
            {
                "containers_list" => typeof(ContainerInfo[]), "containers_inspect" => typeof(ContainerDetail), "containers_logs" => typeof(string[]),
                "images_list" => typeof(ImageInfo[]), "images_check" => typeof(ImageInfo), "images_tags" => typeof(string[]),
                "stacks_list" => typeof(ComposeStack[]), "system_usage" => typeof(SystemUsage), "events_list" => typeof(DockerEventInfo[]),
                "metrics_summary" => typeof(MetricHistoryBucket[]), "metrics_history" => typeof(ContainerStatsSample[]), "metrics_list" => typeof(ContainerStatsSample[]), "history_summary" => typeof(RequestHistorySummary), "history_list" => typeof(RequestRecord[]), "history_inspect" => typeof(RequestRecord), "system_info" => typeof(ServerInfo), _ => typeof(OperationResult)
            };
            if (type.IsArray) type = typeof(EnumerationResult<>).MakeGenericType(type.GetElementType()!);
            return Schema(type);
        }
        private static OpenApiSchemaMetadata Schema(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(string) || type.IsEnum) return OpenApiSchemaMetadata.String();
            if (type == typeof(DateTime)) return OpenApiSchemaMetadata.String("date-time");
            if (type == typeof(bool)) return OpenApiSchemaMetadata.Boolean();
            if (type == typeof(int) || type == typeof(long)) return OpenApiSchemaMetadata.Integer(type == typeof(long) ? "int64" : "int32");
            if (type == typeof(double) || type == typeof(float)) return OpenApiSchemaMetadata.Number();
            if (type.IsGenericType && type.GetGenericArguments().Length == 2) return new OpenApiSchemaMetadata { Type = "object", Description = "String key/value map." };
            if (type.IsArray) return OpenApiSchemaMetadata.CreateArray(Schema(type.GetElementType()!));
            if (type.IsGenericType && type.GetGenericArguments().Length == 1 && type.GetGenericTypeDefinition() != typeof(EnumerationResult<>)) return OpenApiSchemaMetadata.CreateArray(Schema(type.GetGenericArguments()[0]));
            if (type == typeof(object)) return new OpenApiSchemaMetadata { Type = "object" };
            return new OpenApiSchemaMetadata { Type = "object", Properties = type.GetProperties().ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), p => Schema(p.PropertyType)) };
        }
    }
}
