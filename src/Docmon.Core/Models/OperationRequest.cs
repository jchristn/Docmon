namespace Docmon.Core.Models
{
    using System.Collections.Generic;
    /// <summary>Typed parameters shared by REST operations and MCP tools.</summary>
    public sealed class OperationRequest : EnumerationQuery
    {
        /// <summary>Optional request-history window: hour, day, week, or month.</summary>
        public string? Timeframe { get; set; }
        /// <summary>Container or image identifier.</summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>Image name with optional tag or digest.</summary>
        public string Image { get; set; } = string.Empty;
        /// <summary>Whether to force removal. Defaults to false.</summary>
        public bool Force { get; set; }
        /// <summary>Command executable and arguments.</summary>
        public List<string> Command { get; set; } = new List<string>();
        /// <summary>Compose project name.</summary>
        public string Project { get; set; } = string.Empty;
        /// <summary>Optional compose service.</summary>
        public string? Service { get; set; }
        /// <summary>Path inside a container.</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>Path on the server host.</summary>
        public string HostPath { get; set; } = string.Empty;
        /// <summary>Trailing log lines, from 1 through 10000. Defaults to 200.</summary>
        public int Tail { get; set; } = 200;
    }
}
