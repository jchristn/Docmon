namespace Docmon.Server
{
    /// <summary>Server identity, connection health, and operator links. No credentials are returned.</summary>
    public sealed class ServerInfo
    {
        /// <summary>Current application version.</summary>
        public string Version { get; set; } = "0.1.1";
        /// <summary>Docker daemon endpoint.</summary>
        public string Endpoint { get; set; } = string.Empty;
        /// <summary>Project repository.</summary>
        public string Github { get; set; } = "https://github.com/jchristn/Docmon";
        /// <summary>Community invite.</summary>
        public string Discord { get; set; } = "https://discord.gg/tRAN8HgvK5";
        /// <summary>Most recent monitoring failure, or null.</summary>
        public string? MonitoringError { get; set; }
    }
}
