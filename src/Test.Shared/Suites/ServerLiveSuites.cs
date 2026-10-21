namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;
    using Docmon.Core.Services;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    internal static class ServerLiveSuites
    {
        internal static TestSuiteDescriptor ApiSuite()
        {
            Cases c = new Cases("Server.Docker", DockerFixture.SkipReason);
            return new TestSuiteDescriptor("Server.Docker", "Live server against disposable Docker resources", new[]
            {
                c.Async("RemoteLifecycle", "Authenticated remote lifecycle, exec, archive roundtrip, pull, and interactive terminal", async ct =>
                {
                    using DockerClientProvider provider = DockerFixture.CreateProvider();
                    using ServerFixture fixture = new ServerFixture();
                    using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
                    CancellationToken token = timeout.Token;
                    string? id = null; string directory = Path.Combine(Path.GetTempPath(), "docmon-transfer-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    try
                    {
                        id = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("server"), DockerFixture.IdleCommand, true, token).ConfigureAwait(false);
                        using RemoteDocmonClient client = new RemoteDocmonClient(fixture.Client.BaseAddress!, ServerFixture.Token);
                        Check.True((await client.ListContainersAsync(true, token).ConfigureAwait(false)).Any(item => item.Id == id), "container visible remotely");
                        Check.Equal(id, (await client.InspectAsync(id, token).ConfigureAwait(false)).Id, "inspect ID");
                        await client.RestartAsync(id, token).ConfigureAwait(false);
                        await client.PauseAsync(id, token).ConfigureAwait(false); await client.UnpauseAsync(id, token).ConfigureAwait(false);
                        await client.StopAsync(id, token).ConfigureAwait(false); await client.StartAsync(id, token).ConfigureAwait(false);
                        List<string> output = new List<string>();
                        ExecResult result = await client.RunAsync(id, new[] { "sh", "-c", "echo remote-ok; exit 7" }, new InlineProgress<string>(output.Add), token).ConfigureAwait(false);
                        Check.Equal(7, result.ExitCode, "exec exit code"); Check.True(output.Any(line => line.Contains("remote-ok")), "exec output");
                        string file = Path.Combine(directory, "roundtrip.txt"); await File.WriteAllTextAsync(file, "docmon archive roundtrip", token).ConfigureAwait(false);
                        await client.CopyInAsync(id, file, "/tmp", token).ConfigureAwait(false);
                        string destination = Path.Combine(directory, "download"); await client.CopyOutAsync(id, "/tmp/roundtrip.txt", destination, token).ConfigureAwait(false);
                        Check.Equal("docmon archive roundtrip", await File.ReadAllTextAsync(Path.Combine(destination, "roundtrip.txt"), token).ConfigureAwait(false), "remote archive bytes");
                        int lines = 0; await foreach (string line in client.PullAsync(DockerFixture.TestImage, token).ConfigureAwait(false)) { lines++; Check.False(line.StartsWith("error:", StringComparison.Ordinal), line); }
                        Check.True(lines > 0, "image pull output");
                        ImageInfo existing = (await client.ListImagesAsync(token).ConfigureAwait(false)).First(image => image.Repository + ":" + image.Tag == DockerFixture.TestImage);
                        using HttpResponseMessage pulledExisting = await fixture.Client.PostAsync("/api/v1/images/pull", new StringContent(JsonSerializer.Serialize(new { id = existing.Id }), Encoding.UTF8, "application/json"), token).ConfigureAwait(false);
                        Check.Equal(HttpStatusCode.OK, pulledExisting.StatusCode, "pull by existing image ID");
                        await TerminalAsync(fixture.Client.BaseAddress!, id, token).ConfigureAwait(false);
                        await client.RemoveAsync(id, true, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        await fixture.StopAsync().ConfigureAwait(false);
                        Directory.Delete(directory, true);
                    }
                })
            });
        }
        private static async Task TerminalAsync(Uri endpoint, string id, CancellationToken token)
        {
            using ClientWebSocket socket = new ClientWebSocket();
            UriBuilder url = new UriBuilder(endpoint) { Scheme = "ws", Path = "/api/v1/terminal" };
            await socket.ConnectAsync(url.Uri, token).ConfigureAwait(false);
            byte[] auth = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { token = ServerFixture.Token, id, command = new[] { "/bin/sh" } }));
            await socket.SendAsync(auth.AsMemory(), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            byte[] command = Encoding.UTF8.GetBytes("printf 'interactive-%s\n' success\n");
            await socket.SendAsync(command.AsMemory(), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            byte[] buffer = new byte[8192]; StringBuilder received = new StringBuilder();
            while (!received.ToString().Contains("interactive-success", StringComparison.Ordinal))
            {
                ValueWebSocketReceiveResult message = await socket.ReceiveAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                if (message.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Terminal closed before command output arrived.");
                received.Append(Encoding.UTF8.GetString(buffer, 0, message.Count));
            }
            socket.Abort();
        }
    }
}
