namespace Docmon.Core.Services.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// Streams live resource-usage samples for a container until cancelled.
    /// </summary>
    public interface IStatsStreamer
    {
        /// <summary>
        /// Streams stats samples for a container, reporting each computed sample through
        /// <paramref name="progress"/>. The task runs until the token is cancelled or the container stops.
        /// </summary>
        /// <param name="containerId">The container ID. Must not be null.</param>
        /// <param name="progress">Receives each computed sample. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when streaming ends.</returns>
        Task StreamAsync(string containerId, IProgress<ContainerStatsSample> progress, CancellationToken token);
    }
}
