namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Services;
    using Docmon.Core.Services.Implementations;

    /// <summary>
    /// Shared plumbing for the live-daemon suites: a one-time availability probe, a lazily pulled test
    /// image, and helpers that create uniquely named, labeled throwaway containers. Every container the
    /// fixture creates carries the <see cref="TestLabel"/> label and is force-removed by its creator.
    /// Docmon tests never touch containers or images they did not create.
    /// </summary>
    internal static class DockerFixture
    {
        internal const string TestImage = "alpine:3.20";
        internal const string TestLabel = "io.docmon.test";
        internal const string SkipVariable = "DOCMON_TEST_SKIP_DOCKER";

        // PID 1 exits promptly on SIGTERM so stop and restart do not wait out the grace period.
        internal static readonly IList<string> IdleCommand = new List<string>
        {
            "sh", "-c", "trap 'exit 0' TERM; echo docmon-ready; while true; do sleep 1; done"
        };

        private static readonly Lazy<string?> _Unavailable = new Lazy<string?>(Probe, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly SemaphoreSlim _ImageLock = new SemaphoreSlim(1, 1);
        private static bool _ImageReady;

        /// <summary>
        /// Gets the reason live-daemon tests must be skipped, or null when a daemon is reachable.
        /// </summary>
        internal static string? SkipReason
        {
            get { return _Unavailable.Value; }
        }

        internal static DockerClientProvider CreateProvider()
        {
            return new DockerClientProvider();
        }

        internal static string UniqueName(string purpose)
        {
            return "docmon-test-" + purpose + "-" + Guid.NewGuid().ToString("N").Substring(0, 10);
        }

        internal static async Task EnsureImageAsync(DockerClientProvider provider, CancellationToken token)
        {
            await _ImageLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_ImageReady)
                    return;

                if (!await ImageExistsAsync(provider.Client, TestImage, token).ConfigureAwait(false))
                {
                    DockerService service = new DockerService(provider.Client);
                    await foreach (string line in service.PullAsync(TestImage, token).ConfigureAwait(false))
                    {
                        if (line.StartsWith("error:", StringComparison.Ordinal))
                            throw new InvalidOperationException("Could not pull the test image " + TestImage + ": " + line);
                    }
                }

                _ImageReady = true;
            }
            finally
            {
                _ImageLock.Release();
            }
        }

        internal static async Task<bool> ImageExistsAsync(IDockerClient client, string reference, CancellationToken token)
        {
            try
            {
                await client.Images.InspectImageAsync(reference, token).ConfigureAwait(false);
                return true;
            }
            catch (DockerImageNotFoundException)
            {
                return false;
            }
            catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        internal static async Task<string> CreateContainerAsync(
            DockerClientProvider provider,
            string name,
            IList<string> command,
            bool start,
            CancellationToken token,
            IDictionary<string, string>? extraLabels = null,
            IList<string>? environment = null)
        {
            await EnsureImageAsync(provider, token).ConfigureAwait(false);

            Dictionary<string, string> labels = new Dictionary<string, string> { { TestLabel, "true" } };
            if (extraLabels != null)
            {
                foreach (KeyValuePair<string, string> pair in extraLabels)
                    labels[pair.Key] = pair.Value;
            }

            CreateContainerParameters parameters = new CreateContainerParameters
            {
                Name = name,
                Image = TestImage,
                Cmd = command,
                Labels = labels,
                Env = environment ?? new List<string>()
            };

            CreateContainerResponse created = await provider.Client.Containers.CreateContainerAsync(parameters, token).ConfigureAwait(false);
            if (start)
                await provider.Client.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), token).ConfigureAwait(false);

            return created.ID;
        }

        internal static async Task RemoveContainerQuietlyAsync(DockerClientProvider provider, string? id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    await provider.Client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true }, cts.Token).ConfigureAwait(false);
                }
            }
            catch (DockerContainerNotFoundException)
            {
                // Already gone.
            }
            catch (DockerApiException)
            {
                // Removal already in progress or the daemon refused; nothing more a test can do.
            }
        }

        internal static async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> body, TimeSpan timeout, CancellationToken token)
        {
            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(timeout);
                return await body(cts.Token).ConfigureAwait(false);
            }
        }

        private static string? Probe()
        {
            string? skip = Environment.GetEnvironmentVariable(SkipVariable);
            if (!string.IsNullOrWhiteSpace(skip) && skip != "0" && !skip.Equals("false", StringComparison.OrdinalIgnoreCase))
                return SkipVariable + " is set";

            try
            {
                using (DockerClientProvider provider = new DockerClientProvider())
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    provider.VerifyConnectionAsync(cts.Token).GetAwaiter().GetResult();
                }

                return null;
            }
            catch (Exception ex)
            {
                return "Docker daemon not reachable (" + ex.GetType().Name + ")";
            }
        }
    }
}
