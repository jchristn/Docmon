namespace Docmon.Core.Services.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// Runs non-interactive commands inside a container, capturing their output in-process.
    /// </summary>
    public interface IExecService
    {
        /// <summary>
        /// Runs a command inside a container, reporting output lines through <paramref name="output"/> as
        /// they arrive, and returning the exit code.
        /// </summary>
        /// <param name="containerId">The container ID. Must not be null.</param>
        /// <param name="command">The command and arguments. Must not be null or empty.</param>
        /// <param name="output">Receives combined stdout/stderr lines. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The execution result, including the exit code.</returns>
        Task<ExecResult> RunAsync(string containerId, IReadOnlyList<string> command, IProgress<string> output, CancellationToken token);
    }
}
