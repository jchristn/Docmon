namespace Docmon.Core.Services
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>Owns an HTTP-upgraded Docker exec stream for simultaneous terminal input and output.</summary>
    public sealed class DockerTerminalConnection : IDisposable
    {
        private readonly HttpClient _Http;
        private readonly HttpResponseMessage _Response;
        /// <summary>Raw, full duplex terminal stream. One reader and one writer may run concurrently.</summary>
        public Stream Stream { get; }
        private DockerTerminalConnection(HttpClient http, HttpResponseMessage response, Stream stream)
        { _Http = http; _Response = response; Stream = stream; }
        /// <summary>Starts an exec with explicit HTTP Upgrade headers. Docker.DotNet's legacy chunked response does not provide working stdin on newer engines.</summary>
        /// <param name="endpoint">Docker endpoint, including Unix socket, named pipe, HTTP, HTTPS, or TCP.</param>
        /// <param name="execId">Docker exec ID already created with stdin and TTY enabled.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Owned upgraded connection.</returns>
        public static async Task<DockerTerminalConnection> OpenAsync(string endpoint, string execId, CancellationToken token)
        {
            Uri uri = new Uri(endpoint);
            SocketsHttpHandler handler = new SocketsHttpHandler { AllowAutoRedirect = false };
            string baseUrl;
            if (uri.Scheme == "unix")
            {
                handler.ConnectCallback = async (context, cancellation) =>
                {
                    Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(uri.LocalPath), cancellation).ConfigureAwait(false); return new NetworkStream(socket, true); }
                    catch { socket.Dispose(); throw; }
                };
                baseUrl = "http://docker/";
            }
            else if (uri.Scheme == "npipe")
            {
                handler.ConnectCallback = async (context, cancellation) =>
                {
                    NamedPipeClientStream pipe = new NamedPipeClientStream(uri.Host, uri.AbsolutePath.Replace("/pipe/", "", StringComparison.Ordinal), PipeDirection.InOut, PipeOptions.Asynchronous);
                    try { await pipe.ConnectAsync(cancellation).ConfigureAwait(false); return pipe; } catch { pipe.Dispose(); throw; }
                };
                baseUrl = "http://docker/";
            }
            else baseUrl = (uri.Scheme == "tcp" ? "http://" + uri.Authority : uri.GetLeftPart(UriPartial.Authority)) + "/";
            HttpClient http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            HttpResponseMessage? response = null;
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "exec/" + Uri.EscapeDataString(execId) + "/start")
                { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact, Content = new StringContent(JsonSerializer.Serialize(new TerminalStartRequest()), Encoding.UTF8, "application/json") };
                request.Headers.Connection.Add("Upgrade"); request.Headers.TryAddWithoutValidation("Upgrade", "tcp");
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.SwitchingProtocols) throw new IOException("Docker exec did not upgrade to a duplex stream (HTTP " + (int)response.StatusCode + ").");
                Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                return new DockerTerminalConnection(http, response, stream);
            }
            catch { response?.Dispose(); http.Dispose(); throw; }
        }
        /// <summary>Closes the upgraded stream and its HTTP connection.</summary>
        public void Dispose() { Stream.Dispose(); _Response.Dispose(); _Http.Dispose(); }
    }
}
