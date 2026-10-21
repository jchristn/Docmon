namespace Docmon.App
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Services.Interfaces;

    internal sealed class RemoteShellLauncher : IShellLauncher
    {
        private readonly Uri _Endpoint;
        private readonly string _Token;
        internal RemoteShellLauncher(Uri endpoint, string token) { _Endpoint = endpoint; _Token = token; }
        public bool IsCliAvailable => true;
        public Task<int> OpenShellAsync(string containerId, CancellationToken token) => ExecInteractiveAsync(containerId, new[] { "/bin/sh" }, token);
        public async Task<int> ExecInteractiveAsync(string containerId, IReadOnlyList<string> command, CancellationToken token)
        {
            UriBuilder builder = new UriBuilder(_Endpoint) { Scheme = _Endpoint.Scheme == "https" ? "wss" : "ws", Path = _Endpoint.AbsolutePath.TrimEnd('/') + "/api/v1/terminal" };
            using ClientWebSocket socket = new ClientWebSocket();
            using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            await socket.ConnectAsync(builder.Uri, token).ConfigureAwait(false);
            byte[] hello = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { token = _Token, id = containerId, command }));
            await socket.SendAsync(hello.AsMemory(), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            bool previous = Console.TreatControlCAsInput;
            Console.TreatControlCAsInput = true;
            try
            {
                Task output = OutputAsync(socket, lifetime.Token), input = InputAsync(socket, lifetime.Token);
                await Task.WhenAny(output, input).ConfigureAwait(false); lifetime.Cancel();
                try { await Task.WhenAll(output, input).ConfigureAwait(false); } catch (OperationCanceledException) { }
                return 0;
            }
            finally { Console.TreatControlCAsInput = previous; socket.Abort(); }
        }
        private static async Task OutputAsync(ClientWebSocket socket, CancellationToken token)
        {
            byte[] buffer = new byte[8192]; using Stream output = Console.OpenStandardOutput();
            while (!token.IsCancellationRequested)
            {
                ValueWebSocketReceiveResult message = await socket.ReceiveAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                if (message.MessageType == WebSocketMessageType.Close) return;
                await output.WriteAsync(buffer.AsMemory(0, message.Count), token).ConfigureAwait(false);
            }
        }
        private static async Task InputAsync(ClientWebSocket socket, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!Console.KeyAvailable) { await Task.Delay(20, token).ConfigureAwait(false); continue; }
                ConsoleKeyInfo key = Console.ReadKey(true);
                string text = key.Key switch { ConsoleKey.UpArrow => "\u001b[A", ConsoleKey.DownArrow => "\u001b[B", ConsoleKey.RightArrow => "\u001b[C", ConsoleKey.LeftArrow => "\u001b[D", ConsoleKey.Backspace => "\u007f", _ => key.KeyChar.ToString() };
                await socket.SendAsync(Encoding.UTF8.GetBytes(text).AsMemory(), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            }
        }
    }
}
