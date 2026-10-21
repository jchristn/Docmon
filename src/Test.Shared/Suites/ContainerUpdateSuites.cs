namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;
    using Docmon.Core.Services;
    using Docmon.Core.Services.Interfaces;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>Pull-and-apply sequencing, error propagation, cancellation, and standalone behavior.</summary>
    public static class ContainerUpdateSuites
    {
        /// <summary>Gets deterministic updater checks without changing Docker resources.</summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            Cases c = new Cases("ContainerUpdate");
            return new TestSuiteDescriptor("ContainerUpdate", "TUI pull and apply", new[]
            {
                c.Async("ComposeSuccess", "Pull precedes applying exactly the selected Compose service", async token =>
                {
                    List<string> calls = new List<string>();
                    ContainerUpdater updater = Updater(calls, new[] { "Status: Downloaded newer image" });
                    string summary = await updater.UpdateAsync(Target(), new CallbackProgress<string>(_ => { }), token).ConfigureAwait(false);
                    Check.SequenceEqual(new[] { "discover", "pull alpine:3.20", "up project /tmp/compose.yaml api" }, calls, "update sequence and target");
                    Check.True(summary.Contains("project/api"), "completion names the service");
                }),
                c.Async("PullFailure", "A streamed pull error prevents applying a Compose service", async token =>
                {
                    List<string> calls = new List<string>();
                    ContainerUpdater updater = Updater(calls, new[] { "error: access denied" });
                    await Check.ThrowsAsync<DockerConnectionException>(() => updater.UpdateAsync(Target(), new CallbackProgress<string>(_ => { }), token), "streamed pull error").ConfigureAwait(false);
                    Check.Equal(2, calls.Count, "Compose up was not called");
                }),
                c.Async("ComposeFailure", "Nonzero Compose exit is surfaced instead of reporting success", async token =>
                {
                    ContainerUpdater updater = Updater(new List<string>(), new[] { "Already exists" }, 17);
                    InvalidOperationException error = await Check.ThrowsAsync<InvalidOperationException>(() => updater.UpdateAsync(Target(), new CallbackProgress<string>(_ => { }), token), "Compose exit").ConfigureAwait(false);
                    Check.True(error.Message.Contains("17"), "exit code included");
                }),
                c.Async("MissingMetadata", "Missing config or service metadata blocks any pull or unrelated stack update", async token =>
                {
                    List<string> calls = new List<string>();
                    ContainerUpdater updater = Updater(calls, Array.Empty<string>(), config: "");
                    await Check.ThrowsAsync<InvalidOperationException>(() => updater.UpdateAsync(Target(), new CallbackProgress<string>(_ => { }), token), "missing config").ConfigureAwait(false);
                    Check.SequenceEqual(new[] { "discover" }, calls, "no pull when apply cannot resolve config");
                    ContainerInfo target = Target(); target.ComposeService = "";
                    calls.Clear();
                    await Check.ThrowsAsync<InvalidOperationException>(() => updater.UpdateAsync(target, new CallbackProgress<string>(_ => { }), token), "missing service").ConfigureAwait(false);
                    Check.Equal(0, calls.Count, "no whole-project operation");
                }),
                c.Async("StandaloneAndCancellation", "Standalone behavior is explicit and cancellation stops before applying", async token =>
                {
                    List<string> calls = new List<string>();
                    ContainerUpdater updater = Updater(calls, new[] { "Image up to date" });
                    ContainerInfo target = Target(); target.ComposeProject = "";
                    string summary = await updater.UpdateAsync(target, new CallbackProgress<string>(_ => { }), token).ConfigureAwait(false);
                    Check.True(summary.Contains("Standalone container unchanged"), "does not claim apply");
                    Check.SequenceEqual(new[] { "pull alpine:3.20" }, calls, "standalone only pulls");
                    using CancellationTokenSource cancel = new CancellationTokenSource();
                    calls.Clear();
                    await Check.ThrowsAsync<OperationCanceledException>(() => updater.UpdateAsync(Target(), new CallbackProgress<string>(line => { if (line == "Image up to date") cancel.Cancel(); }), cancel.Token), "cancel after pull").ConfigureAwait(false);
                    Check.Equal(2, calls.Count, "cancel prevents applying");
                })
            });
        }

        private static ContainerInfo Target() => new ContainerInfo { Image = "alpine:3.20", ComposeProject = "project", ComposeService = "api" };

        private static ContainerUpdater Updater(List<string> calls, string[] lines, int exitCode = 0, string config = "/tmp/compose.yaml")
        {
            IDockerService docker = StubServiceProxy.For<IDockerService>((method, args) =>
            {
                if (method.Name != nameof(IDockerService.PullAsync)) throw new NotSupportedException(method.Name);
                calls.Add("pull " + args[0]);
                return Pull(lines, (CancellationToken)args[1]!);
            });
            IComposeService compose = StubServiceProxy.For<IComposeService>((method, args) =>
            {
                if (method.Name == nameof(IComposeService.DiscoverAsync))
                {
                    calls.Add("discover");
                    return Task.FromResult<IReadOnlyList<ComposeStack>>(new[] { new ComposeStack { Project = "project", ConfigFilePath = config } });
                }
                if (method.Name == nameof(IComposeService.UpAsync))
                {
                    calls.Add("up " + args[0] + " " + args[1] + " " + args[2]);
                    return Task.FromResult(exitCode);
                }
                throw new NotSupportedException(method.Name);
            });
            return new ContainerUpdater(docker, compose);
        }

        private static async IAsyncEnumerable<string> Pull(string[] lines, [EnumeratorCancellation] CancellationToken token)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            foreach (string line in lines) { token.ThrowIfCancellationRequested(); yield return line; }
        }
    }
}
