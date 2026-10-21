namespace Docmon.Core.Models
{
    /// <summary>Docker Engine exec/start request with a full duplex TTY connection.</summary>
    public sealed class TerminalStartRequest
    {
        /// <summary>Keep the execution attached.</summary>
        public bool Detach { get; set; }
        /// <summary>Enable raw terminal output rather than multiplexed frames.</summary>
        public bool Tty { get; set; } = true;
    }
}
