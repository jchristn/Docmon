namespace Docmon.Core.Services.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// Streams Docker daemon events until cancelled.
    /// </summary>
    public interface IEventsMonitor
    {
        /// <summary>
        /// Monitors daemon events, reporting each one through <paramref name="progress"/>. The task runs
        /// until the token is cancelled.
        /// </summary>
        /// <param name="progress">Receives each event. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when monitoring ends.</returns>
        Task MonitorAsync(IProgress<DockerEventInfo> progress, CancellationToken token);
    }
}
