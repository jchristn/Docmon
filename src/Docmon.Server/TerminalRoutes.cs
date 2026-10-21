namespace Docmon.Server
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Services;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.WebSockets;

    internal sealed class TerminalRoutes
    {
        private readonly DocmonRuntime _Runtime;
        private readonly BearerAuthentication _Authentication;
        internal TerminalRoutes(DocmonRuntime runtime, BearerAuthentication authentication) { _Runtime = runtime; _Authentication = authentication; }
        internal async Task HandleAsync(HttpContextBase context, WebSocketSession session)
        {
            using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
            try
            {
                using CancellationTokenSource handshake = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                handshake.CancelAfter(TimeSpan.FromSeconds(10));
                WebSocketMessage first = await session.ReceiveAsync(handshake.Token).ConfigureAwait(false);
                TerminalRequest request = JsonSerializer.Deserialize<TerminalRequest>(first.Text ?? "{}", DocmonServer.Json) ?? new TerminalRequest();
                if (!_Authentication.Validate("Bearer " + request.Token) || string.IsNullOrWhiteSpace(request.Id))
                {
                    await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Authentication and container ID required.", context.Token).ConfigureAwait(false); return;
                }
                if (request.Command == null || !request.Command.Any() || request.Command.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("An executable is required.");
                ContainerExecCreateResponse exec = await _Runtime.Provider.Client.Exec.ExecCreateContainerAsync(request.Id, new ContainerExecCreateParameters
                { AttachStdin = true, AttachStdout = true, AttachStderr = true, Tty = true, Cmd = request.Command, Env = new[] { "TERM=xterm-256color" } }, lifetime.Token).ConfigureAwait(false);
                using DockerTerminalConnection connection = await DockerTerminalConnection.OpenAsync(_Runtime.Provider.Endpoint, exec.ID, lifetime.Token).ConfigureAwait(false);
                Stream stream = connection.Stream;
                Task output = OutputAsync(stream, session, lifetime.Token);
                Task input = InputAsync(stream, session, lifetime.Token);
                await Task.WhenAny(output, input).ConfigureAwait(false);
                lifetime.Cancel();
                try { await Task.WhenAll(output, input).ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.Error.WriteLine("Terminal session failed: " + ex.Message); if (session.IsConnected) await session.SendTextAsync("\r\nTerminal operation failed. Check server logs for details.\r\n", CancellationToken.None).ConfigureAwait(false); }
            finally
            {
                lifetime.Cancel();
                if (session.IsConnected) await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended.", CancellationToken.None).ConfigureAwait(false);
            }
        }
        private static async Task OutputAsync(Stream stream, WebSocketSession session, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            Decoder decoder = Encoding.UTF8.GetDecoder();
            char[] chars = new char[8192];
            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                if (read == 0) return;
                int count = decoder.GetChars(buffer, 0, read, chars, 0);
                await session.SendTextAsync(new string(chars, 0, count), token).ConfigureAwait(false);
            }
        }
        private static async Task InputAsync(Stream stream, WebSocketSession session, CancellationToken token)
        {
            await foreach (WebSocketMessage message in session.ReadMessagesAsync(token).ConfigureAwait(false))
            {
                if (message.MessageType != WebSocketMessageType.Text) continue;
                byte[] bytes = Encoding.UTF8.GetBytes(message.Text ?? "");
                await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            }
        }
    }
}
