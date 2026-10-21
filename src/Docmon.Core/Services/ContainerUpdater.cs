namespace Docmon.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>Pulls an image and applies it to one discovered Compose service, with explicit progress and failure reporting.</summary>
    public sealed class ContainerUpdater
    {
        private readonly IDockerService _Docker;
        private readonly IComposeService _Compose;

        /// <summary>Creates an updater using local services or authenticated remote adapters.</summary>
        /// <param name="docker">Image pull service.</param>
        /// <param name="compose">Compose discovery and lifecycle service.</param>
        public ContainerUpdater(IDockerService docker, IComposeService compose)
        {
            _Docker = docker ?? throw new ArgumentNullException(nameof(docker));
            _Compose = compose ?? throw new ArgumentNullException(nameof(compose));
        }

        /// <summary>Pulls the selected image, then applies it to its Compose service. Standalone containers are left intact.</summary>
        /// <param name="container">Selected container.</param>
        /// <param name="progress">Receives pull, Compose, and completion messages.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A summary describing exactly what changed.</returns>
        /// <exception cref="ArgumentNullException">The container or progress callback is null.</exception>
        /// <exception cref="InvalidOperationException">Compose metadata is missing, or Compose exits unsuccessfully.</exception>
        /// <exception cref="DockerConnectionException">Docker reports a pull error.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public async Task<string> UpdateAsync(ContainerInfo container, IProgress<string> progress, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(progress);
            token.ThrowIfCancellationRequested();
            ComposeStack? stack = null;
            if (!string.IsNullOrWhiteSpace(container.ComposeProject))
            {
                progress.Report("Resolving Compose service " + container.ComposeProject + "/" + container.ComposeService + "...");
                if (string.IsNullOrWhiteSpace(container.ComposeService))
                    throw new InvalidOperationException("The selected container has no Compose service label; refusing to update the whole project.");
                IReadOnlyList<ComposeStack> stacks = await _Compose.DiscoverAsync(token).ConfigureAwait(false);
                stack = stacks.FirstOrDefault(item => item.Project == container.ComposeProject);
                if (stack == null || string.IsNullOrWhiteSpace(stack.ConfigFilePath))
                    throw new InvalidOperationException("Compose configuration is unavailable for " + container.ComposeProject + ". Mount the original Compose files at their host paths.");
            }

            progress.Report("Pulling " + container.Image + "...");
            await foreach (string line in _Docker.PullAsync(container.Image, token).ConfigureAwait(false))
            {
                progress.Report(line);
                if (line.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                    throw new DockerConnectionException(line);
            }
            token.ThrowIfCancellationRequested();
            string summary;
            if (stack != null)
            {
                progress.Report("Applying image to " + stack.Project + "/" + container.ComposeService + "...");
                int exitCode = await _Compose.UpAsync(stack.Project, stack.ConfigFilePath, container.ComposeService, progress, token).ConfigureAwait(false);
                if (exitCode != 0)
                    throw new InvalidOperationException("Compose up failed with exit code " + exitCode + ". The update was not confirmed; see the output above.");
                summary = "Applied latest " + container.Image + " to " + stack.Project + "/" + container.ComposeService + " (recreated if the image changed).";
            }
            else
            {
                summary = "Image pulled. Standalone container unchanged: recreate it to apply the image. Restarting keeps the original image.";
            }
            progress.Report(summary);
            return summary;
        }
    }
}
