namespace Docmon.Core.Models
{
    /// <summary>
    /// The outcome of a non-interactive command executed inside a container.
    /// </summary>
    public class ExecResult
    {
        /// <summary>
        /// Gets or sets the process exit code, or -1 when it could not be determined.
        /// </summary>
        public int ExitCode { get; set; } = -1;

        /// <summary>
        /// Gets a value indicating whether the command exited successfully (exit code zero).
        /// </summary>
        public bool Succeeded
        {
            get { return ExitCode == 0; }
        }
    }
}
