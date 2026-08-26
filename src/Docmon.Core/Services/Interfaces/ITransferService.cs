namespace Docmon.Core.Services.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Transfers files and directories into and out of a container using the Docker archive API.
    /// </summary>
    public interface ITransferService
    {
        /// <summary>
        /// Copies a path out of a container to a directory on the host.
        /// </summary>
        /// <param name="containerId">The container ID. Must not be null.</param>
        /// <param name="containerPath">The absolute path inside the container. Must not be null.</param>
        /// <param name="hostDirectory">The destination directory on the host. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the copy finishes.</returns>
        Task CopyOutAsync(string containerId, string containerPath, string hostDirectory, CancellationToken token);

        /// <summary>
        /// Copies a file or directory from the host into a directory in the container.
        /// </summary>
        /// <param name="containerId">The container ID. Must not be null.</param>
        /// <param name="hostPath">The file or directory on the host. Must not be null.</param>
        /// <param name="containerDirectory">The destination directory inside the container. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the copy finishes.</returns>
        Task CopyInAsync(string containerId, string hostPath, string containerDirectory, CancellationToken token);
    }
}
