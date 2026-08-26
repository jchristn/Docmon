namespace Docmon.Core.Services.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// Discovers compose projects (stacks) and drives stack-level lifecycle actions through the
    /// <c>docker compose</c> CLI.
    /// </summary>
    public interface IComposeService
    {
        /// <summary>
        /// Discovers stacks from the labels on the current set of containers.
        /// </summary>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The discovered stacks. Never null.</returns>
        Task<IReadOnlyList<ComposeStack>> DiscoverAsync(CancellationToken token);

        /// <summary>
        /// Brings a stack (or one of its services) up in the background, streaming CLI output.
        /// </summary>
        /// <param name="project">The compose project name. Must not be null.</param>
        /// <param name="configFile">The compose file path, or an empty string to let the CLI resolve it.</param>
        /// <param name="service">A single service to target, or null for the whole stack.</param>
        /// <param name="output">Receives CLI output lines. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The CLI exit code.</returns>
        Task<int> UpAsync(string project, string configFile, string? service, IProgress<string> output, CancellationToken token);

        /// <summary>
        /// Stops and removes a stack's resources, streaming CLI output.
        /// </summary>
        /// <param name="project">The compose project name. Must not be null.</param>
        /// <param name="configFile">The compose file path, or an empty string to let the CLI resolve it.</param>
        /// <param name="output">Receives CLI output lines. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The CLI exit code.</returns>
        Task<int> DownAsync(string project, string configFile, IProgress<string> output, CancellationToken token);

        /// <summary>
        /// Restarts a stack's services, streaming CLI output.
        /// </summary>
        /// <param name="project">The compose project name. Must not be null.</param>
        /// <param name="configFile">The compose file path, or an empty string to let the CLI resolve it.</param>
        /// <param name="output">Receives CLI output lines. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The CLI exit code.</returns>
        Task<int> RestartAsync(string project, string configFile, IProgress<string> output, CancellationToken token);

        /// <summary>
        /// Pulls the images declared by a stack, streaming CLI output.
        /// </summary>
        /// <param name="project">The compose project name. Must not be null.</param>
        /// <param name="configFile">The compose file path, or an empty string to let the CLI resolve it.</param>
        /// <param name="output">Receives CLI output lines. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The CLI exit code.</returns>
        Task<int> PullAsync(string project, string configFile, IProgress<string> output, CancellationToken token);
    }
}
