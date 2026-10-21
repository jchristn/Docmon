namespace Docmon.Server
{
    using System;
    /// <summary>Environment-based single-machine configuration.</summary>
    public sealed class ServerSettings
    {
        /// <summary>Required bearer token. Never logged or returned to clients.</summary>
        public string Token { get; }
        /// <summary>API bind host, default 127.0.0.1.</summary>
        public string Host { get; } = Environment.GetEnvironmentVariable("DOCMON_HOST") ?? "127.0.0.1";
        /// <summary>Public API port, default 8080.</summary>
        public int Port { get; } = ReadPort("DOCMON_PORT", 8080);
        /// <summary>Internal loopback MCP port, default 8081. Do not publish this port.</summary>
        public int McpPort { get; } = ReadPort("DOCMON_MCP_PORT", 8081);
        /// <summary>Allowed browser origin. Empty means same-origin only.</summary>
        public string AllowedOrigin { get; } = Environment.GetEnvironmentVariable("DOCMON_ALLOWED_ORIGIN") ?? string.Empty;
        /// <summary>Directory for durable request and metric history. Defaults to ./data.</summary>
        public string DataDirectory { get; } = Environment.GetEnvironmentVariable("DOCMON_DATA_DIRECTORY") ?? "./data";
        /// <summary>Creates explicit configuration, useful for embedding and isolated tests.</summary>
        /// <param name="token">Required bearer token.</param>
        /// <param name="host">Bind hostname.</param>
        /// <param name="port">Public port, from 1 through 65535.</param>
        /// <param name="mcpPort">Distinct private loopback port.</param>
        /// <param name="dataDirectory">Durable request and metric history directory.</param>
        public ServerSettings(string token, string host, int port, int mcpPort, string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Bearer token is required.", nameof(token));
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port < 1 || port > 65535 || mcpPort < 1 || mcpPort > 65535 || port == mcpPort) throw new ArgumentOutOfRangeException(nameof(port), "Distinct valid API and MCP ports are required.");
            Token = token; Host = host; Port = port; McpPort = mcpPort; DataDirectory = dataDirectory;
        }
        /// <summary>Creates validated configuration.</summary>
        public ServerSettings()
        {
            Token = Environment.GetEnvironmentVariable("DOCMON_BEARER_TOKEN") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Token)) throw new InvalidOperationException("DOCMON_BEARER_TOKEN must be set before starting the server.");
            if (Port == McpPort) throw new InvalidOperationException("DOCMON_PORT and DOCMON_MCP_PORT must differ.");
        }
        private static int ReadPort(string name, int fallback)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (value == null) return fallback;
            if (!int.TryParse(value, out int port) || port < 1 || port > 65535) throw new InvalidOperationException(name + " must be between 1 and 65535.");
            return port;
        }
    }
}
