namespace Docmon.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Formats.Tar;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>Authenticated HTTP adapter for the same services used by the local TUI. Thread safe.</summary>
    public sealed class RemoteDocmonClient : IDockerService, IComposeService, IStatsStreamer, IEventsMonitor, IExecService, ITransferService, IRegistryService, IDisposable
    {
        private readonly HttpClient _Http;
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
        /// <summary>Creates an authenticated client. Redirects are disabled to keep credentials on the configured host.</summary>
        /// <param name="endpoint">HTTP or HTTPS Docmon server URL.</param>
        /// <param name="token">Bearer token.</param>
        public RemoteDocmonClient(Uri endpoint, string token)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != "http" && endpoint.Scheme != "https") || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)) throw new ArgumentException("A valid HTTP(S) server URL without credentials, query, or fragment is required.", nameof(endpoint));
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("DOCMON_BEARER_TOKEN is required for remote TUI access.", nameof(token));
            _Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(endpoint.ToString().TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(30) };
            _Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        /// <summary>Verifies authentication and server availability.</summary>
        public async Task VerifyAsync(CancellationToken token) { await GetAsync<object>("system/info", token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(bool all, CancellationToken token)
        {
            List<ContainerInfo> containers = await GetCollectionAsync<ContainerInfo>("containers", token).ConfigureAwait(false);
            return all ? containers : containers.Where(c => c.IsRunning).ToArray();
        }
        /// <inheritdoc/>
        public Task<ContainerDetail> InspectAsync(string id, CancellationToken token) => GetAsync<ContainerDetail>("containers/" + Escape(id), token);
        /// <inheritdoc/>
        public async Task StartAsync(string id, CancellationToken token) { await PostAsync<OperationResult>("containers/" + Escape(id) + "/start", new OperationRequest(), token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task StopAsync(string id, CancellationToken token) { await PostAsync<OperationResult>("containers/" + Escape(id) + "/stop", new OperationRequest(), token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task RestartAsync(string id, CancellationToken token) { await PostAsync<OperationResult>("containers/" + Escape(id) + "/restart", new OperationRequest(), token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task KillAsync(string id, CancellationToken token) { await PostAsync<OperationResult>("containers/" + Escape(id) + "/kill", new OperationRequest(), token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task PauseAsync(string id, CancellationToken token) { await PostAsync<OperationResult>("containers/" + Escape(id) + "/pause", new OperationRequest(), token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task UnpauseAsync(string id, CancellationToken token) { await PostAsync<OperationResult>("containers/" + Escape(id) + "/unpause", new OperationRequest(), token).ConfigureAwait(false); }

        /// <inheritdoc/>
        public async Task RemoveAsync(string id, bool force, CancellationToken token) { await PostAsync<OperationResult>("containers/" + Escape(id) + "/remove", new OperationRequest { Force = force }, token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken token) => await GetCollectionAsync<ImageInfo>("images", token).ConfigureAwait(false);
        /// <inheritdoc/>
        public async IAsyncEnumerable<string> PullAsync(string image, [EnumeratorCancellation] CancellationToken token)
        {
            OperationResult result = await PostAsync<OperationResult>("images/pull", new OperationRequest { Image = image }, token).ConfigureAwait(false);
            foreach (string line in result.Output) { token.ThrowIfCancellationRequested(); yield return line; }
        }
        /// <inheritdoc/>
        public async Task RemoveImageAsync(string imageId, bool force, CancellationToken token) { await PostAsync<OperationResult>("images/" + Escape(imageId) + "/remove", new OperationRequest { Force = force }, token).ConfigureAwait(false); }
        /// <inheritdoc/>
        public async Task<long> PruneImagesAsync(CancellationToken token) => (await PostAsync<OperationResult>("images/prune", new OperationRequest(), token).ConfigureAwait(false)).ReclaimedBytes;
        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetLogsAsync(string id, int tailLines, CancellationToken token) => await GetCollectionAsync<string>("containers/" + Escape(id) + "/logs?tail=" + Math.Clamp(tailLines, 1, 10000), token).ConfigureAwait(false);
        /// <inheritdoc/>
        public Task<SystemUsage> GetSystemUsageAsync(CancellationToken token) => GetAsync<SystemUsage>("system/usage", token);
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ComposeStack>> DiscoverAsync(CancellationToken token) => await GetCollectionAsync<ComposeStack>("stacks", token).ConfigureAwait(false);
        /// <inheritdoc/>
        public Task<int> UpAsync(string project, string configFile, string? service, IProgress<string> output, CancellationToken token) => ComposeAsync("up", project, service, output, token);
        /// <inheritdoc/>
        public Task<int> DownAsync(string project, string configFile, IProgress<string> output, CancellationToken token) => ComposeAsync("down", project, null, output, token);
        /// <inheritdoc/>
        public Task<int> RestartAsync(string project, string configFile, IProgress<string> output, CancellationToken token) => ComposeAsync("restart", project, null, output, token);
        /// <inheritdoc/>
        public Task<int> PullAsync(string project, string configFile, IProgress<string> output, CancellationToken token) => ComposeAsync("pull", project, null, output, token);

        /// <inheritdoc/>
        public async Task<ExecResult> RunAsync(string containerId, IReadOnlyList<string> command, IProgress<string> output, CancellationToken token)
        {
            OperationResult result = await PostAsync<OperationResult>("containers/" + Escape(containerId) + "/exec", new OperationRequest { Command = command.ToList() }, token).ConfigureAwait(false);
            foreach (string line in result.Output) output.Report(line);
            return new ExecResult { ExitCode = result.ExitCode };
        }
        /// <inheritdoc/>
        public async Task<UpdateStatusEnum> CheckAsync(ImageInfo image, CancellationToken token)
        {
            ImageInfo checkedImage = await PostAsync<ImageInfo>("images/" + Escape(image.Id) + "/check", new OperationRequest(), token).ConfigureAwait(false);
            image.RemoteDigest = checkedImage.RemoteDigest; image.Status = checkedImage.Status; return image.Status;
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> ListTagsAsync(ImageInfo image, CancellationToken token) => await GetCollectionAsync<string>("images/" + Escape(image.Id) + "/tags", token).ConfigureAwait(false);
        /// <inheritdoc/>
        public async Task StreamAsync(string containerId, IProgress<ContainerStatsSample> progress, CancellationToken token)
        {
            DateTime latest = DateTime.MinValue;
            while (!token.IsCancellationRequested)
            {
                ContainerStatsSample? sample = (await GetCollectionAsync<ContainerStatsSample>("metrics", token).ConfigureAwait(false)).FirstOrDefault(s => s.ContainerId == containerId);
                if (sample != null && sample.AtUtc > latest) { latest = sample.AtUtc; progress.Report(sample); }
                await Task.Delay(2000, token).ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public async Task MonitorAsync(IProgress<DockerEventInfo> progress, CancellationToken token)
        {
            HashSet<string> seen = new HashSet<string>(); Queue<string> ordered = new Queue<string>();
            while (!token.IsCancellationRequested)
            {
                foreach (DockerEventInfo item in await GetCollectionAsync<DockerEventInfo>("events", token).ConfigureAwait(false))
                {
                    string key = item.TimeUtc.Ticks + ":" + item.Type + ":" + item.Actor + ":" + item.Action;
                    if (seen.Add(key)) { ordered.Enqueue(key); progress.Report(item); }
                    while (ordered.Count > 2000) seen.Remove(ordered.Dequeue());
                }
                await Task.Delay(2000, token).ConfigureAwait(false);
            }
        }
        /// <inheritdoc/>
        public async Task CopyOutAsync(string containerId, string containerPath, string hostDirectory, CancellationToken token)
        {
            Directory.CreateDirectory(hostDirectory);
            using HttpResponseMessage response = await _Http.GetAsync("api/v1/containers/" + Escape(containerId) + "/archive?path=" + Escape(containerPath), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using FileStream spool = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await source.CopyToAsync(spool, token).ConfigureAwait(false); spool.Position = 0;
            await TarFile.ExtractToDirectoryAsync(spool, hostDirectory, true, token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task CopyInAsync(string containerId, string hostPath, string containerDirectory, CancellationToken token)
        {
            using FileStream spool = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            if (Directory.Exists(hostPath)) await TarFile.CreateFromDirectoryAsync(hostPath, spool, true, token).ConfigureAwait(false);
            else { using TarWriter writer = new TarWriter(spool, TarEntryFormat.Pax, true); await writer.WriteEntryAsync(hostPath, Path.GetFileName(hostPath), token).ConfigureAwait(false); }
            spool.Position = 0;
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, "api/v1/containers/" + Escape(containerId) + "/archive?path=" + Escape(containerDirectory));
            request.Content = new StreamContent(spool); request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-tar");
            using HttpResponseMessage response = await _Http.SendAsync(request, token).ConfigureAwait(false); response.EnsureSuccessStatusCode();
        }
        /// <summary>Disposes the HTTP connection pool.</summary>
        public void Dispose() { _Http.Dispose(); }
        private async Task<int> ComposeAsync(string action, string project, string? service, IProgress<string> output, CancellationToken token)
        {
            OperationResult result = await PostAsync<OperationResult>("stacks/" + Escape(project) + "/" + action, new OperationRequest { Service = service }, token).ConfigureAwait(false);
            foreach (string line in result.Output) output.Report(line); return result.ExitCode;
        }
        private async Task<List<T>> GetCollectionAsync<T>(string path, CancellationToken token)
        {
            List<T> objects = new List<T>();
            string? continuation = null;
            do
            {
                string query = path + (path.Contains('?') ? "&" : "?") + "maxResults=1000" + (continuation == null ? "" : "&continuationToken=" + Escape(continuation));
                EnumerationResult<T> page = await GetAsync<EnumerationResult<T>>(query, token).ConfigureAwait(false);
                objects.AddRange(page.Objects);
                continuation = page.EndOfResults ? null : page.ContinuationToken ?? throw new InvalidDataException("Missing enumeration continuation token.");
            } while (continuation != null);
            return objects;
        }
        private Task<T> GetAsync<T>(string path, CancellationToken token) => SendAsync<T>(HttpMethod.Get, path, null, token);
        private Task<T> PostAsync<T>(string path, OperationRequest body, CancellationToken token) => SendAsync<T>(HttpMethod.Post, path, body, token);
        private async Task<T> SendAsync<T>(HttpMethod method, string path, OperationRequest? body, CancellationToken token)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, "api/v1/" + path);
            if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body, _Json), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _Http.SendAsync(request, token).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("Docmon server returned " + (int)response.StatusCode + ": " + json, null, response.StatusCode);
            return JsonSerializer.Deserialize<T>(json, _Json) ?? throw new InvalidDataException("Docmon returned an empty response.");
        }
        private static string Escape(string value) => Uri.EscapeDataString(value ?? throw new ArgumentNullException(nameof(value)));
    }
}
