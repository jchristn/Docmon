namespace Docmon.Core.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;

    /// <summary>
    /// Creates and owns the <see cref="IDockerClient"/> used throughout Docmon, resolving the correct
    /// Docker Engine endpoint for the current operating system (a named pipe on Windows, a Unix domain
    /// socket elsewhere), and honoring the <c>DOCKER_HOST</c> environment variable when set. Thread
    /// safety: the underlying client is safe for concurrent use; this provider is created once and
    /// shared.
    /// </summary>
    public sealed class DockerClientProvider : IDisposable
    {
        private readonly IDockerClient _Client;
        private readonly string _Endpoint;
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DockerClientProvider"/> class, connecting to the
        /// resolved Docker endpoint.
        /// </summary>
        /// <param name="endpointOverride">An explicit endpoint URI to use instead of auto-detection, or
        /// null to auto-detect. Defaults to null.</param>
        /// <exception cref="DockerConnectionException">Thrown when the endpoint URI is invalid or a client
        /// cannot be created.</exception>
        public DockerClientProvider(string? endpointOverride = null)
        {
            _Endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? ResolveEndpoint() : endpointOverride!;

            try
            {
                Uri uri = new Uri(_Endpoint);
                DockerClientConfiguration configuration = new DockerClientConfiguration(uri);
                _Client = configuration.CreateClient();
            }
            catch (Exception ex)
            {
                throw new DockerConnectionException("Could not create a Docker client for endpoint '" + _Endpoint + "': " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Gets the connected Docker client. Never null.
        /// </summary>
        public IDockerClient Client
        {
            get { return _Client; }
        }

        /// <summary>
        /// Gets the resolved Docker endpoint URI in use.
        /// </summary>
        public string Endpoint
        {
            get { return _Endpoint; }
        }

        /// <summary>
        /// Verifies the daemon is reachable by issuing a ping.
        /// </summary>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the ping succeeds.</returns>
        /// <exception cref="DockerConnectionException">Thrown when the daemon cannot be reached.</exception>
        public async Task VerifyConnectionAsync(CancellationToken token)
        {
            try
            {
                await _Client.System.PingAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DockerConnectionException(
                    "Could not reach the Docker daemon at '" + _Endpoint + "'. Is Docker running, and do you have permission to access it? (" + ex.Message + ")",
                    ex);
            }
        }

        /// <summary>
        /// Resolves the default Docker endpoint for the current platform, honoring <c>DOCKER_HOST</c>.
        /// </summary>
        /// <returns>The endpoint URI string.</returns>
        public static string ResolveEndpoint()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable("DOCKER_HOST");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment!;

            if (OperatingSystem.IsWindows())
                return "npipe://./pipe/docker_engine";

            return "unix:///var/run/docker.sock";
        }

        /// <summary>
        /// Disposes the underlying Docker client.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed)
                return;

            _Client.Dispose();
            _Disposed = true;
        }
    }
}
