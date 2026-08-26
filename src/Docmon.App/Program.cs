namespace Docmon.App
{
    using System;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Registries;
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

            DockerClientProvider provider;
            try
            {
                provider = new DockerClientProvider();
                await provider.VerifyConnectionAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Docmon could not connect to Docker: " + ex.Message);
                Console.Error.WriteLine("Endpoint tried: " + DockerClientProvider.ResolveEndpoint());
                Console.Error.WriteLine("Make sure Docker is running and that you have permission to access it.");
                return 1;
            }

            RegistryProviderFactory factory = new RegistryProviderFactory();
            try
            {
                DocmonController controller = BuildController(provider, factory, showSplash);
                return await RunAsync(controller).ConfigureAwait(false);
            }
            finally
            {
                factory.Dispose();
                provider.Dispose();
            }
        }

        private static DocmonController BuildController(DockerClientProvider provider, RegistryProviderFactory factory, bool showSplash)
        {
            DockerService docker = new DockerService(provider.Client);
            StatsStreamer stats = new StatsStreamer(provider.Client);
            EventsMonitor events = new EventsMonitor(provider.Client);
            ExecService exec = new ExecService(provider.Client);
            TransferService transfer = new TransferService(provider.Client);
            ShellLauncher shell = new ShellLauncher();
            ComposeService compose = new ComposeService(provider.Client);
            RegistryService registry = new RegistryService(factory);

            return new DocmonController(docker, stats, events, exec, transfer, shell, compose, registry, provider.Endpoint, Version(), showSplash);
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
                        controller.Shutdown();
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
            Console.WriteLine("Usage:");
            Console.WriteLine("  docmon               Connect to the local Docker daemon and open the TUI.");
            Console.WriteLine("  docmon --no-splash   Skip the startup splash screen.");
            Console.WriteLine("  docmon --version     Print the version and exit.");
            Console.WriteLine("  docmon --help        Print this help and exit.");
            Console.WriteLine();
            Console.WriteLine("Docker endpoint: " + DockerClientProvider.ResolveEndpoint() + " (override with DOCKER_HOST).");
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
                return "0.1.0";

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
