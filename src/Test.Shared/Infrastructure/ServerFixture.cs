namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Server;

    internal sealed class ServerFixture : IDisposable
    {
        internal const string Token = "isolated-docmon-test-token";
        private readonly CancellationTokenSource _Lifetime = new CancellationTokenSource();
        private readonly DocmonServer _Server;
        private readonly Task _Run;
        internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "docmon-tests-" + Guid.NewGuid().ToString("N"));
        internal HttpClient Client { get; }
        internal ServerFixture(Action<string>? seed = null)
        {
            int port = FreePort(), mcp = FreePort(); while (mcp == port) mcp = FreePort();
            seed?.Invoke(DirectoryPath);
            _Server = new DocmonServer(new ServerSettings(Token, "127.0.0.1", port, mcp, DirectoryPath));
            _Run = _Server.StartAsync(_Lifetime.Token);
            Client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(10) };
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }
        internal async Task StopAsync() { _Lifetime.Cancel(); await _Run.ConfigureAwait(false); }
        public void Dispose()
        {
            _Lifetime.Cancel(); Client.Dispose(); _Server.Dispose(); _Lifetime.Dispose();
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
        private static int FreePort() { using TcpListener listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
    }
}
