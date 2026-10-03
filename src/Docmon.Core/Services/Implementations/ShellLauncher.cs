namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="IShellLauncher"/> implementation that shells out to the <c>docker</c> CLI with
    /// the child process attached to the real terminal. Callers must suspend the TUI before invoking
    /// these methods and resume afterward. Thread safety: safe for concurrent use, though only one
    /// interactive session is meaningful at a time.
    /// </summary>
    public sealed class ShellLauncher : IShellLauncher
    {
        private const string _ShellSelector = "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi";

        private readonly Lazy<bool> _CliAvailable = new Lazy<bool>(DetectCli);

        /// <inheritdoc/>
        public bool IsCliAvailable
        {
            get { return _CliAvailable.Value; }
        }

        /// <inheritdoc/>
        public Task<int> OpenShellAsync(string containerId, CancellationToken token)
        {
            if (containerId == null) throw new ArgumentNullException(nameof(containerId));

            List<string> arguments = new List<string> { "exec", "-it", containerId, "sh", "-lc", _ShellSelector };
            return RunAttachedAsync(arguments, token);
        }

        /// <inheritdoc/>
        public Task<int> ExecInteractiveAsync(string containerId, IReadOnlyList<string> command, CancellationToken token)
        {
            if (containerId == null) throw new ArgumentNullException(nameof(containerId));
            if (command == null || command.Count == 0) throw new ArgumentException("A command must be supplied.", nameof(command));

            List<string> arguments = new List<string> { "exec", "-it", containerId };
            arguments.AddRange(command);
            return RunAttachedAsync(arguments, token);
        }

        private static async Task<int> RunAttachedAsync(IReadOnlyList<string> arguments, CancellationToken token)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false
            };

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                if (!process.Start())
                    throw new InvalidOperationException("Failed to start the docker CLI.");

                await process.WaitForExitAsync(token).ConfigureAwait(false);
                return process.ExitCode;
            }
        }

        internal static bool DetectCli()
        {
            string? pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathValue))
                return false;

            string executable = OperatingSystem.IsWindows() ? "docker.exe" : "docker";
            string[] directories = pathValue.Split(Path.PathSeparator);
            foreach (string directory in directories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                try
                {
                    string candidate = Path.Combine(directory.Trim(), executable);
                    if (File.Exists(candidate))
                        return true;
                }
                catch (ArgumentException)
                {
                    // Skip malformed PATH entries.
                }
            }

            return false;
        }
    }
}
