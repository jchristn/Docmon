namespace Docmon.Core.Services.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// The primary Docker Engine surface Docmon uses for listing, inspecting, controlling, and pulling
    /// container and image resources. All methods are asynchronous and observe cancellation.
    /// </summary>
    public interface IDockerService
    {
        /// <summary>
        /// Lists containers.
        /// </summary>
        /// <param name="all">When true, include stopped containers; when false, only running ones.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The containers. Never null.</returns>
        Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(bool all, CancellationToken token);

        /// <summary>
        /// Inspects a container for detailed information.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The container detail.</returns>
        Task<ContainerDetail> InspectAsync(string id, CancellationToken token);

        /// <summary>
        /// Starts a container.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the request has been sent.</returns>
        Task StartAsync(string id, CancellationToken token);

        /// <summary>
        /// Stops a container, allowing it time to shut down gracefully.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the container has stopped or the timeout elapses.</returns>
        Task StopAsync(string id, CancellationToken token);

        /// <summary>
        /// Restarts a container.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the request has been sent.</returns>
        Task RestartAsync(string id, CancellationToken token);

        /// <summary>
        /// Kills a container immediately.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the request has been sent.</returns>
        Task KillAsync(string id, CancellationToken token);

        /// <summary>
        /// Pauses a running container.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the request has been sent.</returns>
        Task PauseAsync(string id, CancellationToken token);

        /// <summary>
        /// Unpauses a paused container.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the request has been sent.</returns>
        Task UnpauseAsync(string id, CancellationToken token);

        /// <summary>
        /// Removes a container.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="force">When true, remove even if the container is running.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the request has been sent.</returns>
        Task RemoveAsync(string id, bool force, CancellationToken token);

        /// <summary>
        /// Lists local images.
        /// </summary>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The images. Never null.</returns>
        Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken token);

        /// <summary>
        /// Pulls an image, yielding human-readable progress lines as they arrive.
        /// </summary>
        /// <param name="image">The image reference to pull, for example <c>nginx:1.27</c>. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>An async sequence of progress lines.</returns>
        IAsyncEnumerable<string> PullAsync(string image, CancellationToken token);

        /// <summary>
        /// Prunes dangling images.
        /// </summary>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The number of bytes reclaimed.</returns>
        Task<long> PruneImagesAsync(CancellationToken token);

        /// <summary>
        /// Reads a snapshot of a container's most recent log lines.
        /// </summary>
        /// <param name="id">The container ID. Must not be null.</param>
        /// <param name="tailLines">The number of trailing lines to read. Clamped to a sensible range.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The log lines, oldest first. Never null.</returns>
        Task<IReadOnlyList<string>> GetLogsAsync(string id, int tailLines, CancellationToken token);

        /// <summary>
        /// Reads a roll-up of local Docker resource usage.
        /// </summary>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The usage roll-up.</returns>
        Task<SystemUsage> GetSystemUsageAsync(CancellationToken token);
    }
}
