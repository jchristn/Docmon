namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docmon.Core.Models;
    using Docmon.Core.Services;
    using Docmon.Core.Services.Implementations;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Suites for argument validation on every service and for Docker endpoint resolution. These use a
    /// client pointed at a closed loopback port, so argument checks are proven to run before any I/O.
    /// </summary>
    public static class GuardSuites
    {
        private const string _ClosedEndpoint = "http://127.0.0.1:1";

        /// <summary>
        /// Constructor and argument guards on the Docker-backed services.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ServiceGuardSuite()
        {
            Cases c = new Cases("ServiceGuards");
            return new TestSuiteDescriptor("ServiceGuards", "Service argument guards", new List<TestCaseDescriptor>
            {
                c.Sync("NullClient", "Every Docker-backed service rejects a null client", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new DockerService(null!), "DockerService");
                    Check.Throws<ArgumentNullException>(() => new ComposeService(null!), "ComposeService");
                    Check.Throws<ArgumentNullException>(() => new ExecService(null!), "ExecService");
                    Check.Throws<ArgumentNullException>(() => new TransferService(null!), "TransferService");
                    Check.Throws<ArgumentNullException>(() => new StatsStreamer(null!), "StatsStreamer");
                    Check.Throws<ArgumentNullException>(() => new EventsMonitor(null!), "EventsMonitor");
                }),
                c.Async("DockerServiceNullIds", "DockerService container and image operations reject null IDs", async ct =>
                {
                    using (IDockerClient client = OfflineClient())
                    {
                        DockerService service = new DockerService(client);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.InspectAsync(null!, ct), "InspectAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.StartAsync(null!, ct), "StartAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.StopAsync(null!, ct), "StopAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.RestartAsync(null!, ct), "RestartAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.KillAsync(null!, ct), "KillAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.PauseAsync(null!, ct), "PauseAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.UnpauseAsync(null!, ct), "UnpauseAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.RemoveAsync(null!, true, ct), "RemoveAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.RemoveImageAsync(null!, false, ct), "RemoveImageAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.GetLogsAsync(null!, 10, ct), "GetLogsAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(async () =>
                        {
                            await foreach (string line in service.PullAsync(null!, ct).ConfigureAwait(false))
                                throw new TestAssertionException("PullAsync(null) yielded '" + line + "'");
                        }, "PullAsync").ConfigureAwait(false);
                    }
                }),
                c.Sync("DockerServiceClamps", "Stop timeout and log tail limits clamp to their documented ranges", () =>
                {
                    using (IDockerClient client = OfflineClient())
                    {
                        DockerService service = new DockerService(client);
                        Check.Equal(10, service.StopTimeoutSeconds, "default stop timeout");
                        Check.Equal(5000, service.MaxLogTailLines, "default log tail");
                        service.StopTimeoutSeconds = 0;
                        Check.Equal(1, service.StopTimeoutSeconds, "stop timeout minimum");
                        service.StopTimeoutSeconds = 10000;
                        Check.Equal(300, service.StopTimeoutSeconds, "stop timeout maximum");
                        service.StopTimeoutSeconds = 42;
                        Check.Equal(42, service.StopTimeoutSeconds, "stop timeout in range");
                        service.MaxLogTailLines = 1;
                        Check.Equal(10, service.MaxLogTailLines, "log tail minimum");
                        service.MaxLogTailLines = int.MaxValue;
                        Check.Equal(100000, service.MaxLogTailLines, "log tail maximum");
                    }
                }),
                c.Async("ExecGuards", "ExecService rejects null container, null or empty command, and null output", async ct =>
                {
                    using (IDockerClient client = OfflineClient())
                    {
                        ExecService service = new ExecService(client);
                        CallbackProgress<string> output = new CallbackProgress<string>(line => { });
                        List<string> command = new List<string> { "true" };
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.RunAsync(null!, command, output, ct), "null container").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentException>(() => service.RunAsync("id", null!, output, ct), "null command").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentException>(() => service.RunAsync("id", new List<string>(), output, ct), "empty command").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.RunAsync("id", command, null!, ct), "null output").ConfigureAwait(false);
                    }
                }),
                c.Async("TransferGuards", "TransferService rejects null arguments in both directions", async ct =>
                {
                    using (IDockerClient client = OfflineClient())
                    {
                        TransferService service = new TransferService(client);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.CopyOutAsync(null!, "/etc", "out", ct), "CopyOut container").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.CopyOutAsync("id", null!, "out", ct), "CopyOut path").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.CopyOutAsync("id", "/etc", null!, ct), "CopyOut host directory").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.CopyInAsync(null!, "in", "/tmp", ct), "CopyIn container").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.CopyInAsync("id", null!, "/tmp", ct), "CopyIn host path").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.CopyInAsync("id", "in", null!, ct), "CopyIn container directory").ConfigureAwait(false);
                    }
                }),
                c.Async("TransferMissingHostPath", "Copying a nonexistent host path in fails with FileNotFoundException before contacting Docker", async ct =>
                {
                    using (IDockerClient client = OfflineClient())
                    {
                        TransferService service = new TransferService(client);
                        string missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "docmon-missing-" + Guid.NewGuid().ToString("N"));
                        await Check.ThrowsAsync<System.IO.FileNotFoundException>(() => service.CopyInAsync("id", missing, "/tmp", ct), "CopyInAsync").ConfigureAwait(false);
                    }
                }),
                c.Async("StreamGuards", "Stats and event streams reject null arguments", async ct =>
                {
                    using (IDockerClient client = OfflineClient())
                    {
                        StatsStreamer stats = new StatsStreamer(client);
                        await Check.ThrowsAsync<ArgumentNullException>(() => stats.StreamAsync(null!, new CallbackProgress<ContainerStatsSample>(s => { }), ct), "stats container").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => stats.StreamAsync("id", null!, ct), "stats progress").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => new EventsMonitor(client).MonitorAsync(null!, ct), "events progress").ConfigureAwait(false);
                    }
                }),
                c.Async("ComposeGuards", "Compose actions reject a null project or output sink", async ct =>
                {
                    using (IDockerClient client = OfflineClient())
                    {
                        ComposeService service = new ComposeService(client);
                        CallbackProgress<string> output = new CallbackProgress<string>(line => { });
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.UpAsync(null!, string.Empty, null, output, ct), "UpAsync project").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.DownAsync(null!, string.Empty, output, ct), "DownAsync project").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.RestartAsync(null!, string.Empty, output, ct), "RestartAsync project").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.PullAsync(null!, string.Empty, output, ct), "PullAsync project").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.UpAsync("p", string.Empty, null, null!, ct), "UpAsync output").ConfigureAwait(false);
                    }
                }),
                c.Async("ShellGuards", "Shell launcher rejects a null container or empty command", async ct =>
                {
                    ShellLauncher launcher = new ShellLauncher();
                    await Check.ThrowsAsync<ArgumentNullException>(() => launcher.OpenShellAsync(null!, ct), "OpenShellAsync").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentNullException>(() => launcher.ExecInteractiveAsync(null!, new List<string> { "sh" }, ct), "ExecInteractiveAsync container").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentException>(() => launcher.ExecInteractiveAsync("id", null!, ct), "ExecInteractiveAsync null command").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentException>(() => launcher.ExecInteractiveAsync("id", new List<string>(), ct), "ExecInteractiveAsync empty command").ConfigureAwait(false);
                }),
                c.Sync("ShellCliDetection", "CLI detection reflects PATH and is false when PATH is empty", () =>
                {
                    string? original = Environment.GetEnvironmentVariable("PATH");
                    try
                    {
                        Environment.SetEnvironmentVariable("PATH", string.Empty);
                        Check.False(ShellLauncher.DetectCli(), "empty PATH");

                        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "docmon-path-" + Guid.NewGuid().ToString("N"));
                        System.IO.Directory.CreateDirectory(directory);
                        try
                        {
                            Environment.SetEnvironmentVariable("PATH", directory);
                            Check.False(ShellLauncher.DetectCli(), "PATH without docker");
                            string executable = OperatingSystem.IsWindows() ? "docker.exe" : "docker";
                            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, executable), string.Empty);
                            Environment.SetEnvironmentVariable("PATH", "  " + System.IO.Path.PathSeparator + directory);
                            Check.True(ShellLauncher.DetectCli(), "PATH with docker (and a blank entry)");
                        }
                        finally
                        {
                            System.IO.Directory.Delete(directory, true);
                        }
                    }
                    finally
                    {
                        Environment.SetEnvironmentVariable("PATH", original);
                    }
                })
            });
        }

        /// <summary>
        /// Docker endpoint resolution and connection failure handling.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ClientProviderSuite()
        {
            Cases c = new Cases("DockerClientProvider");
            return new TestSuiteDescriptor("DockerClientProvider", "Docker client provider", new List<TestCaseDescriptor>
            {
                c.Sync("DefaultEndpoint", "Without DOCKER_HOST the platform default socket or pipe is used", () =>
                    WithDockerHost(null, () =>
                    {
                        string expected = OperatingSystem.IsWindows() ? "npipe://./pipe/docker_engine" : "unix:///var/run/docker.sock";
                        Check.Equal(expected, DockerClientProvider.ResolveEndpoint(), "default endpoint");
                    })),
                c.Sync("DockerHostHonored", "DOCKER_HOST overrides the default endpoint", () =>
                    WithDockerHost("tcp://10.1.2.3:2375", () =>
                        Check.Equal("tcp://10.1.2.3:2375", DockerClientProvider.ResolveEndpoint(), "endpoint"))),
                c.Sync("BlankDockerHostIgnored", "A whitespace DOCKER_HOST is ignored", () =>
                    WithDockerHost("   ", () =>
                        Check.False(DockerClientProvider.ResolveEndpoint().Trim().Length == 0, "endpoint is not blank"))),
                c.Sync("ExplicitOverride", "An explicit endpoint wins over auto-detection", () =>
                {
                    using (DockerClientProvider provider = new DockerClientProvider(_ClosedEndpoint))
                    {
                        Check.Equal(_ClosedEndpoint, provider.Endpoint, "endpoint");
                        Check.NotNull(provider.Client, "client");
                    }
                }),
                c.Sync("InvalidEndpoint", "A malformed endpoint raises DockerConnectionException naming the endpoint", () =>
                {
                    DockerConnectionException ex = Check.Throws<DockerConnectionException>(() => new DockerClientProvider("not a uri"), "new DockerClientProvider");
                    Check.True(ex.Message.Contains("not a uri"), "message names the endpoint: " + ex.Message);
                    Check.NotNull(ex.InnerException, "inner exception");
                }),
                c.Async("UnreachableDaemon", "An unreachable daemon raises DockerConnectionException from VerifyConnectionAsync", async ct =>
                {
                    using (DockerClientProvider provider = new DockerClientProvider(_ClosedEndpoint))
                    {
                        DockerConnectionException ex = await Check.ThrowsAsync<DockerConnectionException>(() => provider.VerifyConnectionAsync(ct), "VerifyConnectionAsync").ConfigureAwait(false);
                        Check.True(ex.Message.Contains("127.0.0.1:1"), "message names the endpoint: " + ex.Message);
                    }
                }),
                c.Async("VerifyCancellation", "A cancelled verification propagates OperationCanceledException", async ct =>
                {
                    using (DockerClientProvider provider = new DockerClientProvider(_ClosedEndpoint))
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        cts.Cancel();
                        await Check.ThrowsAsync<OperationCanceledException>(() => provider.VerifyConnectionAsync(cts.Token), "VerifyConnectionAsync").ConfigureAwait(false);
                    }
                }),
                c.Sync("DisposeTwice", "Disposing the provider twice is safe", () =>
                {
                    DockerClientProvider provider = new DockerClientProvider(_ClosedEndpoint);
                    provider.Dispose();
                    provider.Dispose();
                })
            });
        }

        private static IDockerClient OfflineClient()
        {
            using (DockerClientConfiguration configuration = new DockerClientConfiguration(new Uri(_ClosedEndpoint)))
            {
                return configuration.CreateClient();
            }
        }

        private static void WithDockerHost(string? value, Action body)
        {
            string? original = Environment.GetEnvironmentVariable("DOCKER_HOST");
            try
            {
                Environment.SetEnvironmentVariable("DOCKER_HOST", value);
                body();
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOCKER_HOST", original);
            }
        }
    }
}
