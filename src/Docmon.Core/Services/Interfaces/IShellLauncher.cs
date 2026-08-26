namespace Docmon.Core.Services.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Launches interactive terminal sessions against a container by shelling out to the <c>docker</c>
    /// CLI. Because a TUI pane cannot host a pseudo-terminal, callers suspend the TUI around these
    /// calls so the child process owns the real terminal, then resume when it returns.
    /// </summary>
    public interface IShellLauncher
    {
        /// <summary>
        /// Gets a value indicating whether the <c>docker</c> CLI is available on the current PATH.
        /// </summary>
        bool IsCliAvailable { get; }

        /// <summary>
        /// Opens an interactive shell inside a container (trying common shells in order), attached to
        /// the real terminal.
        /// </summary>
        /// <param name="containerId">The container ID. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The process exit code.</returns>
        Task<int> OpenShellAsync(string containerId, CancellationToken token);

        /// <summary>
        /// Runs an interactive command inside a container attached to the real terminal.
        /// </summary>
        /// <param name="containerId">The container ID. Must not be null.</param>
        /// <param name="command">The command and arguments. Must not be null or empty.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The process exit code.</returns>
        Task<int> ExecInteractiveAsync(string containerId, IReadOnlyList<string> command, CancellationToken token);
    }
}
