namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="IExecService"/> implementation. It creates and attaches a non-interactive
    /// exec instance, streams the multiplexed output as lines, and reports the exit code. Thread safety:
    /// safe for concurrent use.
    /// </summary>
    public sealed class ExecService : IExecService
    {
        private readonly IDockerClient _Client;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecService"/> class.
        /// </summary>
        /// <param name="client">The connected Docker client. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
        public ExecService(IDockerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc/>
        public async Task<ExecResult> RunAsync(string containerId, IReadOnlyList<string> command, IProgress<string> output, CancellationToken token)
        {
            if (containerId == null) throw new ArgumentNullException(nameof(containerId));
            if (command == null || command.Count == 0) throw new ArgumentException("A command must be supplied.", nameof(command));
            if (output == null) throw new ArgumentNullException(nameof(output));

            ContainerExecCreateParameters createParameters = new ContainerExecCreateParameters
            {
                AttachStdout = true,
                AttachStderr = true,
                AttachStdin = false,
                Tty = false,
                Cmd = new List<string>(command)
            };

            ContainerExecCreateResponse created = await _Client.Exec.ExecCreateContainerAsync(containerId, createParameters, token).ConfigureAwait(false);

            using (MultiplexedStream stream = await _Client.Exec.StartAndAttachContainerExecAsync(created.ID, false, token).ConfigureAwait(false))
            {
                await PumpOutputAsync(stream, output, token).ConfigureAwait(false);
            }

            ContainerExecInspectResponse inspect = await _Client.Exec.InspectContainerExecAsync(created.ID, token).ConfigureAwait(false);

            ExecResult result = new ExecResult();
            result.ExitCode = (int)inspect.ExitCode;
            return result;
        }

        private static async Task PumpOutputAsync(MultiplexedStream stream, IProgress<string> output, CancellationToken token)
        {
            byte[] buffer = new byte[8192];

            // Stdout and stderr arrive as interleaved frames; keep a partial-line buffer per stream so an
            // unterminated fragment on one stream is never glued onto a line from the other.
            StringBuilder pendingOut = new StringBuilder();
            StringBuilder pendingErr = new StringBuilder();

            while (true)
            {
                token.ThrowIfCancellationRequested();
                MultiplexedStream.ReadResult result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (result.EOF)
                    break;
                if (result.Count <= 0)
                    continue;

                StringBuilder pending = result.Target == MultiplexedStream.TargetStream.StandardError ? pendingErr : pendingOut;
                pending.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                EmitCompleteLines(pending, output);
            }

            if (pendingOut.Length > 0)
                output.Report(pendingOut.ToString());
            if (pendingErr.Length > 0)
                output.Report(pendingErr.ToString());
        }

        internal static void EmitCompleteLines(StringBuilder pending, IProgress<string> output)
        {
            while (true)
            {
                string text = pending.ToString();
                int newline = text.IndexOf('\n');
                if (newline < 0)
                    break;

                string line = text.Substring(0, newline).TrimEnd('\r');
                output.Report(line);
                pending.Remove(0, newline + 1);
            }
        }
    }
}
