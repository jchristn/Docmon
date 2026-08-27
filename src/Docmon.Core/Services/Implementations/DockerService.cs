namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="IDockerService"/> implementation backed by Docker.DotNet. Thread safety: the
    /// underlying client is safe for concurrent use, so this service is too.
    /// </summary>
    public sealed class DockerService : IDockerService
    {
        #region Private-Members

        private readonly IDockerClient _Client;
        private int _StopTimeoutSeconds = 10;
        private int _MaxLogTailLines = 5000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="DockerService"/> class.
        /// </summary>
        /// <param name="client">The connected Docker client. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
        public DockerService(IDockerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the graceful stop timeout in seconds. Defaults to 10; clamped to 1 through 300.
        /// </summary>
        public int StopTimeoutSeconds
        {
            get
            {
                return _StopTimeoutSeconds;
            }
            set
            {
                _StopTimeoutSeconds = Math.Clamp(value, 1, 300);
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of log lines a snapshot read may return. Defaults to 5000;
        /// clamped to 10 through 100000.
        /// </summary>
        public int MaxLogTailLines
        {
            get
            {
                return _MaxLogTailLines;
            }
            set
            {
                _MaxLogTailLines = Math.Clamp(value, 10, 100000);
            }
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(bool all, CancellationToken token)
        {
            ContainersListParameters parameters = new ContainersListParameters { All = all };
            IList<ContainerListResponse> responses = await _Client.Containers.ListContainersAsync(parameters, token).ConfigureAwait(false);

            List<ContainerInfo> result = new List<ContainerInfo>(responses.Count);
            foreach (ContainerListResponse response in responses)
                result.Add(MapContainer(response));

            return result;
        }

        /// <inheritdoc/>
        public async Task<ContainerDetail> InspectAsync(string id, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));

            ContainerInspectResponse response = await _Client.Containers.InspectContainerAsync(id, token).ConfigureAwait(false);
            return MapDetail(response);
        }

        /// <inheritdoc/>
        public async Task StartAsync(string id, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            await _Client.Containers.StartContainerAsync(id, new ContainerStartParameters(), token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task StopAsync(string id, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            ContainerStopParameters parameters = new ContainerStopParameters { WaitBeforeKillSeconds = (uint)_StopTimeoutSeconds };
            await _Client.Containers.StopContainerAsync(id, parameters, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task RestartAsync(string id, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            ContainerRestartParameters parameters = new ContainerRestartParameters { WaitBeforeKillSeconds = (uint)_StopTimeoutSeconds };
            await _Client.Containers.RestartContainerAsync(id, parameters, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task KillAsync(string id, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            await _Client.Containers.KillContainerAsync(id, new ContainerKillParameters(), token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task PauseAsync(string id, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            await _Client.Containers.PauseContainerAsync(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task UnpauseAsync(string id, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            await _Client.Containers.UnpauseContainerAsync(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task RemoveAsync(string id, bool force, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            ContainerRemoveParameters parameters = new ContainerRemoveParameters { Force = force };
            await _Client.Containers.RemoveContainerAsync(id, parameters, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken token)
        {
            ImagesListParameters parameters = new ImagesListParameters { All = false };
            IList<ImagesListResponse> responses = await _Client.Images.ListImagesAsync(parameters, token).ConfigureAwait(false);

            List<ImageInfo> result = new List<ImageInfo>(responses.Count);
            foreach (ImagesListResponse response in responses)
                result.Add(MapImage(response));

            return result;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<string> PullAsync(string image, [EnumeratorCancellation] CancellationToken token)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            Channel<string> channel = Channel.CreateUnbounded<string>();
            ImagesCreateParameters parameters = BuildCreateParameters(image);

            Progress<JSONMessage> progress = new Progress<JSONMessage>(message =>
            {
                string? line = FormatPullMessage(message);
                if (line != null)
                    channel.Writer.TryWrite(line);
            });

            Task pull = Task.Run(async () =>
            {
                try
                {
                    await _Client.Images.CreateImageAsync(parameters, null, progress, token).ConfigureAwait(false);
                }
                finally
                {
                    channel.Writer.TryComplete();
                }
            }, token);

            while (await channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out string? line))
                    yield return line;
            }

            await pull.ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task RemoveImageAsync(string imageId, bool force, CancellationToken token)
        {
            if (imageId == null) throw new ArgumentNullException(nameof(imageId));

            ImageDeleteParameters parameters = new ImageDeleteParameters { Force = force };
            await _Client.Images.DeleteImageAsync(imageId, parameters, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<long> PruneImagesAsync(CancellationToken token)
        {
            // Restrict the prune to dangling (untagged) images with an explicit filter. The Docker daemon
            // additionally never removes an image that a container depends on, so a running or stopped
            // deployment is never disrupted and no tagged image is deleted.
            ImagesPruneParameters parameters = new ImagesPruneParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "dangling", new Dictionary<string, bool> { { "true", true } } }
                }
            };

            ImagesPruneResponse response = await _Client.Images.PruneImagesAsync(parameters, token).ConfigureAwait(false);
            return (long)response.SpaceReclaimed;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetLogsAsync(string id, int tailLines, CancellationToken token)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));

            int tail = Math.Clamp(tailLines, 1, _MaxLogTailLines);
            ContainerLogsParameters parameters = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Timestamps = false,
                Tail = tail.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Follow = false
            };

            using (MultiplexedStream stream = await _Client.Containers.GetContainerLogsAsync(id, false, parameters, token).ConfigureAwait(false))
            {
                string text = await ReadMultiplexedTextAsync(stream, token).ConfigureAwait(false);
                return SplitLines(text);
            }
        }

        /// <inheritdoc/>
        public async Task<SystemUsage> GetSystemUsageAsync(CancellationToken token)
        {
            SystemUsage usage = new SystemUsage();

            SystemInfoResponse info = await _Client.System.GetSystemInfoAsync(token).ConfigureAwait(false);
            usage.ContainerCount = (int)info.Containers;
            usage.RunningCount = (int)info.ContainersRunning;
            usage.ImageCount = (int)info.Images;
            usage.EngineVersion = info.ServerVersion ?? string.Empty;
            usage.OperatingSystem = info.OperatingSystem ?? string.Empty;

            ImagesListParameters imageParameters = new ImagesListParameters { All = false };
            IList<ImagesListResponse> images = await _Client.Images.ListImagesAsync(imageParameters, token).ConfigureAwait(false);
            long totalSize = 0;
            int dangling = 0;
            foreach (ImagesListResponse image in images)
            {
                totalSize += image.Size;
                if (image.RepoTags == null || image.RepoTags.Count == 0 || IsNoneTag(image.RepoTags))
                    dangling++;
            }

            usage.ImageSizeBytes = totalSize;
            usage.DanglingImageCount = dangling;

            try
            {
                VolumesListResponse volumes = await _Client.Volumes.ListAsync(token).ConfigureAwait(false);
                usage.VolumeCount = volumes.Volumes != null ? volumes.Volumes.Count : 0;
            }
            catch (DockerApiException)
            {
                usage.VolumeCount = 0;
            }

            return usage;
        }

        #endregion

        #region Private-Methods

        private static ContainerInfo MapContainer(ContainerListResponse response)
        {
            ContainerInfo info = new ContainerInfo();
            info.Id = response.ID ?? string.Empty;
            info.Name = ExtractName(response.Names);
            info.Image = response.Image ?? string.Empty;
            info.ImageId = response.ImageID ?? string.Empty;
            info.State = MapState(response.State);
            info.Status = response.Status ?? string.Empty;
            info.Health = ExtractHealth(response.Status);
            info.CreatedUtc = DateTime.SpecifyKind(response.Created, DateTimeKind.Utc);
            info.Ports = MapPorts(response.Ports);

            if (response.Labels != null)
            {
                if (response.Labels.TryGetValue("com.docker.compose.project", out string? project) && project != null)
                    info.ComposeProject = project;
                if (response.Labels.TryGetValue("com.docker.compose.service", out string? service) && service != null)
                    info.ComposeService = service;
            }

            return info;
        }

        private static ContainerDetail MapDetail(ContainerInspectResponse response)
        {
            ContainerDetail detail = new ContainerDetail();
            detail.Id = response.ID ?? string.Empty;
            detail.Name = (response.Name ?? string.Empty).TrimStart('/');
            detail.Image = response.Config?.Image ?? string.Empty;
            detail.State = response.State?.Status ?? string.Empty;
            detail.Health = response.State?.Health?.Status ?? string.Empty;
            detail.CreatedUtc = DateTime.SpecifyKind(response.Created, DateTimeKind.Utc);
            detail.StartedUtc = ParseDockerTime(response.State?.StartedAt);
            detail.RestartPolicy = response.HostConfig?.RestartPolicy?.Name.ToString() ?? string.Empty;
            detail.Command = BuildCommand(response.Config?.Entrypoint, response.Config?.Cmd);

            if (response.Config?.Env != null)
                detail.Environment = new List<string>(response.Config.Env);

            List<string> mounts = new List<string>();
            if (response.Mounts != null)
            {
                foreach (MountPoint mount in response.Mounts)
                {
                    string source = string.IsNullOrEmpty(mount.Name) ? mount.Source : mount.Name;
                    mounts.Add((source ?? string.Empty) + " -> " + (mount.Destination ?? string.Empty));
                }
            }
            detail.Mounts = mounts;

            List<string> networks = new List<string>();
            if (response.NetworkSettings?.Networks != null)
            {
                foreach (KeyValuePair<string, EndpointSettings> pair in response.NetworkSettings.Networks)
                    networks.Add(pair.Key);
            }
            detail.Networks = networks;

            if (response.Config?.Labels != null)
                detail.Labels = new Dictionary<string, string>(response.Config.Labels);

            detail.Ports = MapPortBindings(response.NetworkSettings?.Ports);
            return detail;
        }

        private static ImageInfo MapImage(ImagesListResponse response)
        {
            ImageInfo info = new ImageInfo();
            info.Id = response.ID ?? string.Empty;
            info.SizeBytes = response.Size;
            info.CreatedUtc = DateTime.SpecifyKind(response.Created, DateTimeKind.Utc);

            if (response.RepoDigests != null)
                info.RepoDigests = new List<string>(response.RepoDigests);

            string repository = ImageInfo.NoneLabel;
            string tag = ImageInfo.NoneLabel;
            if (response.RepoTags != null && response.RepoTags.Count > 0 && !IsNoneTag(response.RepoTags))
            {
                string primary = response.RepoTags[0];
                int lastColon = primary.LastIndexOf(':');
                if (lastColon > 0 && primary.IndexOf('/', lastColon) < 0)
                {
                    repository = primary.Substring(0, lastColon);
                    tag = primary.Substring(lastColon + 1);
                }
                else
                {
                    repository = primary;
                }
            }

            info.Repository = repository;
            info.Tag = tag;
            return info;
        }

        private static string ExtractName(IList<string>? names)
        {
            if (names == null || names.Count == 0)
                return string.Empty;

            return names[0].TrimStart('/');
        }

        private static ContainerStateEnum MapState(string? state)
        {
            if (string.IsNullOrEmpty(state))
                return ContainerStateEnum.Unknown;

            switch (state.ToLowerInvariant())
            {
                case "created": return ContainerStateEnum.Created;
                case "running": return ContainerStateEnum.Running;
                case "paused": return ContainerStateEnum.Paused;
                case "restarting": return ContainerStateEnum.Restarting;
                case "exited": return ContainerStateEnum.Exited;
                case "dead": return ContainerStateEnum.Dead;
                case "removing": return ContainerStateEnum.Removing;
                default: return ContainerStateEnum.Unknown;
            }
        }

        private static string ExtractHealth(string? status)
        {
            if (string.IsNullOrEmpty(status))
                return string.Empty;

            int open = status.IndexOf('(');
            int close = status.IndexOf(')');
            if (open >= 0 && close > open)
            {
                string inner = status.Substring(open + 1, close - open - 1).Trim();
                if (inner.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0)
                    return inner;
                if (inner.Equals("healthy", StringComparison.OrdinalIgnoreCase) || inner.Equals("unhealthy", StringComparison.OrdinalIgnoreCase))
                    return inner;
            }

            return string.Empty;
        }

        private static IReadOnlyList<PortMap> MapPorts(IList<Port>? ports)
        {
            List<PortMap> result = new List<PortMap>();
            if (ports == null)
                return result;

            foreach (Port port in ports)
            {
                PortMap map = new PortMap();
                map.HostIp = port.IP ?? string.Empty;
                map.HostPort = port.PublicPort;
                map.ContainerPort = port.PrivatePort;
                map.Protocol = port.Type ?? "tcp";
                result.Add(map);
            }

            return result;
        }

        private static IReadOnlyList<PortMap> MapPortBindings(IDictionary<string, IList<PortBinding>>? ports)
        {
            List<PortMap> result = new List<PortMap>();
            if (ports == null)
                return result;

            foreach (KeyValuePair<string, IList<PortBinding>> pair in ports)
            {
                string containerPort = pair.Key;
                string protocol = "tcp";
                int slash = containerPort.IndexOf('/');
                int portNumber = 0;
                if (slash > 0)
                {
                    int.TryParse(containerPort.Substring(0, slash), out portNumber);
                    protocol = containerPort.Substring(slash + 1);
                }
                else
                {
                    int.TryParse(containerPort, out portNumber);
                }

                if (pair.Value == null || pair.Value.Count == 0)
                {
                    PortMap unpublished = new PortMap();
                    unpublished.ContainerPort = portNumber;
                    unpublished.Protocol = protocol;
                    result.Add(unpublished);
                    continue;
                }

                foreach (PortBinding binding in pair.Value)
                {
                    PortMap map = new PortMap();
                    map.ContainerPort = portNumber;
                    map.Protocol = protocol;
                    map.HostIp = binding.HostIP ?? string.Empty;
                    int.TryParse(binding.HostPort, out int hostPort);
                    map.HostPort = hostPort;
                    result.Add(map);
                }
            }

            return result;
        }

        private static string BuildCommand(IList<string>? entrypoint, IList<string>? cmd)
        {
            StringBuilder builder = new StringBuilder();
            if (entrypoint != null)
            {
                foreach (string part in entrypoint)
                    builder.Append(part).Append(' ');
            }
            if (cmd != null)
            {
                foreach (string part in cmd)
                    builder.Append(part).Append(' ');
            }

            return builder.ToString().Trim();
        }

        private static bool IsNoneTag(IList<string> repoTags)
        {
            foreach (string tag in repoTags)
            {
                if (!tag.StartsWith("<none>", StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static DateTime ParseDockerTime(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return DateTime.MinValue;

            if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime parsed))
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

            return DateTime.MinValue;
        }

        private static ImagesCreateParameters BuildCreateParameters(string image)
        {
            string fromImage = image;
            string tag = "latest";

            int lastColon = image.LastIndexOf(':');
            if (lastColon > 0 && image.IndexOf('/', lastColon) < 0)
            {
                fromImage = image.Substring(0, lastColon);
                tag = image.Substring(lastColon + 1);
            }

            return new ImagesCreateParameters { FromImage = fromImage, Tag = tag };
        }

        private static string? FormatPullMessage(JSONMessage message)
        {
            if (!string.IsNullOrEmpty(message.ErrorMessage))
                return "error: " + message.ErrorMessage;

            string status = message.Status ?? string.Empty;
            if (status.Length == 0)
                return null;

            string id = string.IsNullOrEmpty(message.ID) ? string.Empty : message.ID + ": ";
            string progress = string.IsNullOrEmpty(message.ProgressMessage) ? string.Empty : " " + message.ProgressMessage;
            return id + status + progress;
        }

        private static async Task<string> ReadMultiplexedTextAsync(MultiplexedStream stream, CancellationToken token)
        {
            StringBuilder builder = new StringBuilder();
            byte[] buffer = new byte[8192];

            while (true)
            {
                token.ThrowIfCancellationRequested();
                MultiplexedStream.ReadResult result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (result.EOF)
                    break;
                if (result.Count > 0)
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }

            return builder.ToString();
        }

        private static IReadOnlyList<string> SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] parts = normalized.Split('\n');
            List<string> lines = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                if (part.Length > 0)
                    lines.Add(part);
            }

            return lines;
        }

        #endregion
    }
}
