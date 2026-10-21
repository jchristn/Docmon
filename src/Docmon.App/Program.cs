namespace Docmon.App
{
    using System;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Services;
    using Docmon.Core.Services.Implementations;
    using TUIKit.Hosting;
    using TUIKit.Terminal;

    /// <summary>
    /// Entry point for the Docmon terminal application. It connects to the local Docker daemon, builds
    /// the services and controller, and runs the TUI, suspending and resuming around interactive shell
    /// sessions. Runs on Windows, Linux, and macOS.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                PrintUsage();
                return 0;
            }

            if (HasFlag(args, "--version") || HasFlag(args, "-v"))
            {
                Console.WriteLine("docmon " + Version());
                return 0;
            }

            TrySetUtf8();

            if (HasFlag(args, "--snapshot"))
            {
                SnapshotPreview.Run();
                return 0;
            }

            bool showSplash = !HasFlag(args, "--no-splash");

            int serverFlag = Array.IndexOf(args, "--server");
            int tokenFlag = Array.IndexOf(args, "--token");
            if (tokenFlag >= 0 && serverFlag < 0)
            {
                Console.Error.WriteLine("--token requires --server."); return 1;
            }
            if (tokenFlag >= 0 && (tokenFlag + 1 >= args.Length || string.IsNullOrWhiteSpace(args[tokenFlag + 1]) || args[tokenFlag + 1].StartsWith("--", StringComparison.Ordinal)))
            {
                Console.Error.WriteLine("--token requires a nonempty bearer token."); return 1;
            }
            if (serverFlag >= 0)
            {
                if (serverFlag + 1 >= args.Length || !Uri.TryCreate(args[serverFlag + 1], UriKind.Absolute, out Uri? endpoint))
                {
                    Console.Error.WriteLine("--server requires an HTTP(S) Docmon server URL."); return 1;
                }
                string token = tokenFlag >= 0 ? args[tokenFlag + 1] : Environment.GetEnvironmentVariable("DOCMON_BEARER_TOKEN") ?? string.Empty;
                try
                {
                    using RemoteDocmonClient remote = new RemoteDocmonClient(endpoint, token);
                    await remote.VerifyAsync(CancellationToken.None).ConfigureAwait(false);
                    DocmonController remoteController = new DocmonController(remote, remote, remote, remote, remote,
                        new RemoteShellLauncher(endpoint, token), remote, remote, endpoint.ToString(), Version(), showSplash);
                    return await RunAsync(remoteController).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is System.Net.Http.HttpRequestException)
                {
                    Console.Error.WriteLine("Could not connect to remote Docmon: " + ex.Message); return 1;
                }
            }
            // Local mode is deliberately pinned to the local socket. It never needs a bearer token.
            string localEndpoint = OperatingSystem.IsWindows() ? "npipe://./pipe/docker_engine" : "unix:///var/run/docker.sock";
            using DocmonRuntime runtime = new DocmonRuntime(localEndpoint);
            try
            {
                await runtime.Provider.VerifyConnectionAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (DockerConnectionException ex)
            {
                Console.Error.WriteLine("Docmon could not connect to Docker: " + ex.Message);
                return 1;
            }
            DocmonController controller = new DocmonController(runtime.Docker, runtime.Stats, runtime.Events,
                runtime.Exec, runtime.Transfer, new ShellLauncher(), runtime.Compose, runtime.Registry,
                runtime.Provider.Endpoint, Version(), showSplash);
            return await RunAsync(controller).ConfigureAwait(false);
        }

        private static async Task<int> RunAsync(DocmonController controller)
        {
            using (ConsoleBackend backend = new ConsoleBackend())
            {
                using (TuiApplication app = new TuiApplication(backend))
                {
                    controller.Configure(app);

                    try
                    {
                        while (true)
                        {
                            await app.RunAsync(CancellationToken.None).ConfigureAwait(false);

                            Func<Task>? pending = controller.TakePendingSuspend();
                            if (pending == null)
                                break;

                            // Restore the terminal, hand it to the interactive child process, then resume.
                            app.Stop();
                            try
                            {
                                await pending().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine("Session error: " + ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        app.Stop();
                        Console.Error.WriteLine();
                        Console.Error.WriteLine("Docmon stopped unexpectedly: " + ex.Message);
                        return 2;
                    }
                    finally
                    {
                        await controller.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
                        app.Stop();
                    }
                }
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("docmon " + Version() + " - a terminal UI for managing and monitoring your Docker stack.");
            Console.WriteLine();
            Console.WriteLine("Remote: docmon --server https://your-host --token <bearer-token>");
            Console.WriteLine("  --token overrides DOCMON_BEARER_TOKEN; the environment variable is the fallback.");
            Console.WriteLine("Usage:");
            Console.WriteLine("  docmon               Connect to the local Docker daemon and open the TUI.");
            Console.WriteLine("  docmon --no-splash   Skip the startup splash screen.");
            Console.WriteLine("  docmon --version     Print the version and exit.");
            Console.WriteLine("  docmon --help        Print this help and exit.");
            Console.WriteLine();
            Console.WriteLine("Local Docker endpoint: " + (OperatingSystem.IsWindows() ? "npipe://./pipe/docker_engine" : "unix:///var/run/docker.sock") + " (local TUI ignores DOCKER_HOST).");
        }

        private static void TrySetUtf8()
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (Exception)
            {
                // Some redirected consoles reject encoding changes; the TUI still renders.
            }
        }

        private static string Version()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
                return "0.1.1";

            return version.Major + "." + version.Minor + "." + version.Build;
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null)
                return false;

            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
