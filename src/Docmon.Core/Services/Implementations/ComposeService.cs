namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="IComposeService"/> implementation. Stack discovery reads the compose labels on
    /// the current containers through the Docker client; lifecycle actions shell out to
    /// <c>docker compose</c>. Thread safety: safe for concurrent use.
    /// </summary>
    public sealed class ComposeService : IComposeService
    {
        private const string _ProjectLabel = "com.docker.compose.project";
        private const string _ServiceLabel = "com.docker.compose.service";
        private const string _ConfigFilesLabel = "com.docker.compose.project.config_files";

        private readonly IDockerClient _Client;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComposeService"/> class.
        /// </summary>
        /// <param name="client">The connected Docker client. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
        public ComposeService(IDockerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ComposeStack>> DiscoverAsync(CancellationToken token)
        {
            ContainersListParameters parameters = new ContainersListParameters { All = true };
            IList<ContainerListResponse> containers = await _Client.Containers.ListContainersAsync(parameters, token).ConfigureAwait(false);

            Dictionary<string, ComposeStackBuilder> builders = new Dictionary<string, ComposeStackBuilder>(StringComparer.Ordinal);
            foreach (ContainerListResponse container in containers)
            {
                if (container.Labels == null)
                    continue;
                if (!container.Labels.TryGetValue(_ProjectLabel, out string? project) || string.IsNullOrEmpty(project))
                    continue;

                if (!builders.TryGetValue(project, out ComposeStackBuilder? builder))
                {
                    builder = new ComposeStackBuilder(project);
                    builders[project] = builder;
                }

                container.Labels.TryGetValue(_ConfigFilesLabel, out string? configFiles);
                if (!string.IsNullOrEmpty(configFiles) && string.IsNullOrEmpty(builder.ConfigFile))
                    builder.ConfigFile = FirstConfigFile(configFiles!);

                container.Labels.TryGetValue(_ServiceLabel, out string? service);
                ComposeServiceInfo info = new ComposeServiceInfo();
                info.Name = service ?? string.Empty;
                info.RunningImage = container.Image ?? string.Empty;
                info.ContainerId = container.ID ?? string.Empty;
                info.State = MapState(container.State);
                builder.Services.Add(info);
            }

            List<ComposeStack> stacks = new List<ComposeStack>();
            foreach (ComposeStackBuilder builder in builders.Values)
                stacks.Add(builder.Build());

            stacks.Sort((a, b) => string.Compare(a.Project, b.Project, StringComparison.OrdinalIgnoreCase));
            return stacks;
        }

        /// <inheritdoc/>
        public Task<int> UpAsync(string project, string configFile, string? service, IProgress<string> output, CancellationToken token)
        {
            List<string> arguments = BuildBaseArguments(project, configFile);
            arguments.Add("up");
            arguments.Add("-d");
            if (!string.IsNullOrEmpty(service))
                arguments.Add(service);

            return RunComposeAsync(arguments, output, token);
        }

        /// <inheritdoc/>
        public Task<int> DownAsync(string project, string configFile, IProgress<string> output, CancellationToken token)
        {
            List<string> arguments = BuildBaseArguments(project, configFile);
            arguments.Add("down");
            return RunComposeAsync(arguments, output, token);
        }

        /// <inheritdoc/>
        public Task<int> RestartAsync(string project, string configFile, IProgress<string> output, CancellationToken token)
        {
            List<string> arguments = BuildBaseArguments(project, configFile);
            arguments.Add("restart");
            return RunComposeAsync(arguments, output, token);
        }

        /// <inheritdoc/>
        public Task<int> PullAsync(string project, string configFile, IProgress<string> output, CancellationToken token)
        {
            List<string> arguments = BuildBaseArguments(project, configFile);
            arguments.Add("pull");
            return RunComposeAsync(arguments, output, token);
        }

        internal static List<string> BuildBaseArguments(string project, string configFile)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            List<string> arguments = new List<string> { "compose", "-p", project };
            if (!string.IsNullOrEmpty(configFile))
            {
                arguments.Add("-f");
                arguments.Add(configFile);
            }

            return arguments;
        }

        private static async Task<int> RunComposeAsync(IReadOnlyList<string> arguments, IProgress<string> output, CancellationToken token)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output.Report(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output.Report(e.Data);
                };

                if (!process.Start())
                    throw new InvalidOperationException("Failed to start the docker compose CLI.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(token).ConfigureAwait(false);
                return process.ExitCode;
            }
        }

        internal static string FirstConfigFile(string configFiles)
        {
            string[] parts = configFiles.Split(',');
            return parts.Length > 0 ? parts[0].Trim() : string.Empty;
        }

        internal static ContainerStateEnum MapState(string? state)
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
    }
}
