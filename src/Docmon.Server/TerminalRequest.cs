namespace Docmon.Server
{
    using System.Collections.Generic;
    /// <summary>First WebSocket frame authenticates and selects the container. Subsequent frames are terminal input.</summary>
    public sealed class TerminalRequest
    {
        /// <summary>Bearer token, sent only in the first TLS-protected frame, never in the URL.</summary>
        public string Token { get; set; } = string.Empty;
        /// <summary>Container identifier.</summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>Shell or interactive executable and arguments. Defaults to /bin/sh.</summary>
        public List<string> Command { get; set; } = new List<string> { "/bin/sh" };
    }
}
