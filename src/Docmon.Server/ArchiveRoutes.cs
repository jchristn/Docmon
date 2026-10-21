namespace Docmon.Server
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    internal sealed class ArchiveRoutes
    {
        private readonly DocmonRuntime _Runtime;
        internal ArchiveRoutes(DocmonRuntime runtime) { _Runtime = runtime; }
        internal void Register(Webserver server)
        {
            foreach (HttpMethod method in new[] { HttpMethod.GET, HttpMethod.PUT })
            {
                OpenApiRouteMetadata metadata = OpenApiRouteMetadata.Create(method == HttpMethod.GET ? "Download a container path as a tar archive" : "Upload a tar archive into a container directory", "containers");
                metadata.OperationId = method == HttpMethod.GET ? "containers_archive_download" : "containers_archive_upload";
                metadata.Security = new System.Collections.Generic.List<string> { "Bearer" };
                metadata.WithParameter(OpenApiParameterMetadata.Path("id", "Container ID", OpenApiSchemaMetadata.String()));
                metadata.WithParameter(OpenApiParameterMetadata.Query("path", "Absolute path inside container", true, OpenApiSchemaMetadata.String()));
                if (method == HttpMethod.PUT) metadata.WithRequestBody(new OpenApiRequestBodyMetadata { Required = true, Content = new System.Collections.Generic.Dictionary<string, OpenApiMediaTypeMetadata> { ["application/x-tar"] = new OpenApiMediaTypeMetadata { Schema = OpenApiSchemaMetadata.String("binary") } } });
                metadata.WithResponse(200, new OpenApiResponseMetadata { Description = method == HttpMethod.GET ? "Tar archive (application/x-tar)." : "Archive extracted." });
                server.Routes.PostAuthentication.Parameter.Add(method, "/api/v1/containers/{id}/archive", HandleAsync, openApiMetadata: metadata);
            }
        }
        private async Task HandleAsync(HttpContextBase context)
        {
            try
            {
                string id = Uri.UnescapeDataString(context.Request.Url.Parameters["id"] ?? string.Empty);
                string path = Uri.UnescapeDataString(context.Request.Query.Elements["path"] ?? string.Empty);
                if (!path.StartsWith('/')) throw new ArgumentException("path must be an absolute container path.");
                if (context.Request.Method == HttpMethod.GET)
                {
                    GetArchiveFromContainerResponse archive = await _Runtime.Provider.Client.Containers.GetArchiveFromContainerAsync(id, new GetArchiveFromContainerParameters { Path = path }, false, context.Token).ConfigureAwait(false);
                    using Stream stream = archive.Stream;
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/x-tar";
                    context.Response.ChunkedTransfer = true;
                    context.Response.Headers.Set("Content-Disposition", "attachment; filename=container-files.tar");
                    byte[] buffer = new byte[81920]; int read;
                    while ((read = await stream.ReadAsync(buffer, context.Token).ConfigureAwait(false)) > 0)
                        if (!await context.Response.SendChunk(buffer.AsSpan(0, read).ToArray(), false, context.Token).ConfigureAwait(false)) break;
                    await context.Response.SendChunk(Array.Empty<byte>(), true, context.Token).ConfigureAwait(false);
                }
                else
                {
                    using MemoryStream archive = new MemoryStream(context.Request.DataAsBytes);
                    await _Runtime.Provider.Client.Containers.ExtractArchiveToContainerAsync(id, new ContainerPathStatParameters { Path = path }, archive, context.Token).ConfigureAwait(false);
                    await DocmonServer.SendJsonAsync(context, new Docmon.Core.Models.OperationResult()).ConfigureAwait(false);
                }
            }
            catch (ArgumentException ex) { await DocmonServer.SendJsonAsync(context, new ApiError { Code = "invalid_request", Message = ex.Message }, 400).ConfigureAwait(false); }
            catch (DockerApiException ex) when (!context.Response.ResponseStarted) { await DocmonServer.SendJsonAsync(context, new ApiError { Code = "docker_error", Message = ex.Message }, (int)ex.StatusCode).ConfigureAwait(false); }
        }
    }
}
