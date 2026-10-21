namespace Docmon.Core.Models
{
    using System.Collections.Generic;
    /// <summary>Completed operation output. Nonzero exit codes indicate CLI failure.</summary>
    public sealed class OperationResult
    {
        /// <summary>Process exit code. Zero means success.</summary>
        public int ExitCode { get; set; }
        /// <summary>Bounded output, keeping the most recent 2000 lines.</summary>
        public IReadOnlyList<string> Output { get; set; } = new List<string>();
        /// <summary>Space reclaimed by prune, in bytes.</summary>
        public long ReclaimedBytes { get; set; }
    }
}
