namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using Docmon.Core.Services;
    using Docmon.Core.Services.Implementations;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// End-to-end suites against the local Docker daemon. Each case creates uniquely named, labeled
    /// resources from a small Alpine image and removes them in a finally block; nothing pre-existing on
    /// the host is modified. Cases are skipped when no daemon is reachable or when
    /// <c>DOCMON_TEST_SKIP_DOCKER</c> is set.
    /// </summary>
    public static class DockerIntegrationSuites
    {
        private static readonly TimeSpan _Timeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Engine-level operations: connectivity, usage, images, and pulls.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor EngineSuite()
        {
            Cases c = new Cases("DockerEngine", DockerFixture.SkipReason);
            return new TestSuiteDescriptor("DockerEngine", "Docker engine (live)", new List<TestCaseDescriptor>
            {
                c.Async("VerifyConnection", "VerifyConnectionAsync succeeds against the local daemon", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await provider.VerifyConnectionAsync(ct).ConfigureAwait(false);
                        Check.True(provider.Endpoint.Length > 0, "endpoint resolved");
                    }
                }),
                c.Async("SystemUsage", "GetSystemUsageAsync reports engine version and consistent counts", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await DockerFixture.EnsureImageAsync(provider, ct).ConfigureAwait(false);
                        SystemUsage usage = await new DockerService(provider.Client).GetSystemUsageAsync(ct).ConfigureAwait(false);
                        Check.True(usage.EngineVersion.Length > 0, "engine version");
                        Check.True(usage.OperatingSystem.Length > 0, "operating system");
                        Check.True(usage.ImageCount >= 1, "at least the test image");
                        Check.True(usage.ImageSizeBytes > 0, "image size");
                        Check.True(usage.RunningCount >= 0 && usage.RunningCount <= usage.ContainerCount, "running <= total");
                        Check.True(usage.DanglingImageCount >= 0 && usage.VolumeCount >= 0, "non-negative counts");
                    }
                }),
                c.Async("ListImages", "ListImagesAsync includes the test image with size and digests", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await DockerFixture.EnsureImageAsync(provider, ct).ConfigureAwait(false);
                        IReadOnlyList<ImageInfo> images = await new DockerService(provider.Client).ListImagesAsync(ct).ConfigureAwait(false);
                        ImageInfo? alpine = null;
                        foreach (ImageInfo image in images)
                        {
                            if (image.Repository == "alpine" && image.Tag == "3.20")
                                alpine = image;
                        }

                        Check.NotNull(alpine, "alpine:3.20 listed");
                        Check.True(alpine!.SizeBytes > 0, "size");
                        Check.True(alpine.Id.StartsWith("sha256:", StringComparison.Ordinal), "id is a digest");
                        Check.True(alpine.RepoDigests.Count > 0, "pulled image has repo digests");
                    }
                }),
                c.Async("PullExisting", "Pulling an image that is already present streams status lines and completes", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await DockerFixture.EnsureImageAsync(provider, ct).ConfigureAwait(false);
                        List<string> lines = new List<string>();
                        await foreach (string line in new DockerService(provider.Client).PullAsync(DockerFixture.TestImage, ct).ConfigureAwait(false))
                            lines.Add(line);

                        Check.True(lines.Count > 0, "status lines streamed");
                        foreach (string line in lines)
                            Check.False(line.StartsWith("error:", StringComparison.Ordinal), "unexpected pull error: " + line);
                    }
                }),
                c.Async("PullMissing", "Pulling a nonexistent image fails loudly rather than silently succeeding", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string missing = "docmon-test-does-not-exist-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ":nope";
                        bool failed = false;
                        try
                        {
                            await foreach (string line in new DockerService(provider.Client).PullAsync(missing, ct).ConfigureAwait(false))
                            {
                                if (line.StartsWith("error:", StringComparison.Ordinal))
                                    failed = true;
                            }
                        }
                        catch (DockerApiException)
                        {
                            failed = true;
                        }

                        Check.True(failed, "pull of a nonexistent image reported an error");
                        Check.False(await DockerFixture.ImageExistsAsync(provider.Client, missing, ct).ConfigureAwait(false), "image not created");
                    }
                }),
                c.Async("RemoveImageTag", "RemoveImageAsync removes a tag created by the test", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await DockerFixture.EnsureImageAsync(provider, ct).ConfigureAwait(false);
                        string repository = "docmon-test/alias";
                        string tag = Guid.NewGuid().ToString("N").Substring(0, 10);
                        string reference = repository + ":" + tag;
                        await provider.Client.Images.TagImageAsync(DockerFixture.TestImage, new ImageTagParameters { RepositoryName = repository, Tag = tag }, ct).ConfigureAwait(false);
                        try
                        {
                            DockerService service = new DockerService(provider.Client);
                            bool listed = false;
                            foreach (ImageInfo image in await service.ListImagesAsync(ct).ConfigureAwait(false))
                            {
                                if (image.Repository == repository && image.Tag == tag)
                                    listed = true;
                            }

                            // The alias shares an image ID with alpine, so it may be listed under either tag;
                            // the authoritative check is inspect before and after removal.
                            Check.True(listed || await DockerFixture.ImageExistsAsync(provider.Client, reference, ct).ConfigureAwait(false), "alias exists");
                            await service.RemoveImageAsync(reference, false, ct).ConfigureAwait(false);
                            Check.False(await DockerFixture.ImageExistsAsync(provider.Client, reference, ct).ConfigureAwait(false), "alias removed");
                            Check.True(await DockerFixture.ImageExistsAsync(provider.Client, DockerFixture.TestImage, ct).ConfigureAwait(false), "original tag untouched");
                        }
                        finally
                        {
                            try
                            {
                                await provider.Client.Images.DeleteImageAsync(reference, new ImageDeleteParameters(), CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (DockerApiException)
                            {
                                // Already removed by the test.
                            }
                        }
                    }
                }),
                c.Async("RemoveImageMissing", "RemoveImageAsync on a nonexistent image throws", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await Check.ThrowsAsync<DockerApiException>(() => new DockerService(provider.Client).RemoveImageAsync("docmon-test/none:" + Guid.NewGuid().ToString("N"), false, ct), "RemoveImageAsync").ConfigureAwait(false);
                    }
                })
            });
        }

        /// <summary>
        /// Container lifecycle, inspect, logs, and not-found handling.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ContainerSuite()
        {
            Cases c = new Cases("DockerContainers", DockerFixture.SkipReason);
            return new TestSuiteDescriptor("DockerContainers", "Docker containers (live)", new List<TestCaseDescriptor>
            {
                c.Async("Lifecycle", "Start, pause, unpause, restart, stop, and remove move the container through each state", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string name = DockerFixture.UniqueName("life");
                        string? id = null;
                        try
                        {
                            id = await DockerFixture.CreateContainerAsync(provider, name, DockerFixture.IdleCommand, false, ct).ConfigureAwait(false);
                            DockerService service = new DockerService(provider.Client);
                            service.StopTimeoutSeconds = 5;

                            Check.Equal(ContainerStateEnum.Created, await StateOfAsync(service, id, ct).ConfigureAwait(false), "after create");
                            await service.StartAsync(id, ct).ConfigureAwait(false);
                            Check.Equal(ContainerStateEnum.Running, await StateOfAsync(service, id, ct).ConfigureAwait(false), "after start");
                            await service.PauseAsync(id, ct).ConfigureAwait(false);
                            Check.Equal(ContainerStateEnum.Paused, await StateOfAsync(service, id, ct).ConfigureAwait(false), "after pause");
                            await service.UnpauseAsync(id, ct).ConfigureAwait(false);
                            Check.Equal(ContainerStateEnum.Running, await StateOfAsync(service, id, ct).ConfigureAwait(false), "after unpause");
                            await service.RestartAsync(id, ct).ConfigureAwait(false);
                            Check.Equal(ContainerStateEnum.Running, await StateOfAsync(service, id, ct).ConfigureAwait(false), "after restart");
                            await service.StopAsync(id, ct).ConfigureAwait(false);
                            Check.Equal(ContainerStateEnum.Exited, await StateOfAsync(service, id, ct).ConfigureAwait(false), "after stop");

                            IReadOnlyList<ContainerInfo> running = await service.ListContainersAsync(false, ct).ConfigureAwait(false);
                            Check.Null(Find(running, id), "stopped container excluded when all=false");

                            await service.RemoveAsync(id, false, ct).ConfigureAwait(false);
                            Check.Null(Find(await service.ListContainersAsync(true, ct).ConfigureAwait(false), id), "removed container not listed");
                            id = null;
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        }
                    }
                }),
                c.Async("KillAndForceRemove", "Kill stops a running container immediately; force remove deletes a running one", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string? killed = null;
                        string? forced = null;
                        try
                        {
                            DockerService service = new DockerService(provider.Client);
                            killed = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("kill"), DockerFixture.IdleCommand, true, ct).ConfigureAwait(false);
                            await service.KillAsync(killed, ct).ConfigureAwait(false);
                            await DockerFixture.WithTimeoutAsync(token => provider.Client.Containers.WaitContainerAsync(killed, token), _Timeout, ct).ConfigureAwait(false);
                            Check.Equal(ContainerStateEnum.Exited, await StateOfAsync(service, killed, ct).ConfigureAwait(false), "after kill");

                            forced = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("force"), DockerFixture.IdleCommand, true, ct).ConfigureAwait(false);
                            await Check.ThrowsAsync<DockerApiException>(() => service.RemoveAsync(forced, false, ct), "non-forced remove of a running container").ConfigureAwait(false);
                            await service.RemoveAsync(forced, true, ct).ConfigureAwait(false);
                            Check.Null(Find(await service.ListContainersAsync(true, ct).ConfigureAwait(false), forced), "force-removed container gone");
                            forced = null;
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, killed).ConfigureAwait(false);
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, forced).ConfigureAwait(false);
                        }
                    }
                }),
                c.Async("ListAndInspect", "List and inspect map name, image, labels, environment, and command", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string name = DockerFixture.UniqueName("inspect");
                        string? id = null;
                        try
                        {
                            Dictionary<string, string> labels = new Dictionary<string, string>
                            {
                                { "com.docker.compose.project", "docmon-fake-project" },
                                { "com.docker.compose.service", "fake-service" }
                            };
                            id = await DockerFixture.CreateContainerAsync(provider, name, DockerFixture.IdleCommand, true, ct, labels, new List<string> { "DOCMON_TEST=1" }).ConfigureAwait(false);
                            DockerService service = new DockerService(provider.Client);

                            ContainerInfo? listed = Find(await service.ListContainersAsync(false, ct).ConfigureAwait(false), id);
                            Check.NotNull(listed, "running container listed");
                            Check.Equal(name, listed!.Name, "list name");
                            Check.Equal(DockerFixture.TestImage, listed.Image, "list image");
                            Check.Equal("docmon-fake-project", listed.ComposeProject, "compose project label");
                            Check.Equal("fake-service", listed.ComposeService, "compose service label");
                            Check.True(listed.IsRunning, "is running");
                            Check.Equal(id.Substring(0, 12), listed.ShortId, "short id");

                            ContainerDetail detail = await service.InspectAsync(id, ct).ConfigureAwait(false);
                            Check.Equal(id, detail.Id, "inspect id");
                            Check.Equal(name, detail.Name, "inspect name");
                            Check.Equal(DockerFixture.TestImage, detail.Image, "inspect image");
                            Check.Equal("running", detail.State, "inspect state");
                            Check.Contains("DOCMON_TEST=1", detail.Environment, "environment");
                            Check.Equal("true", detail.Labels[DockerFixture.TestLabel], "test label");
                            Check.True(detail.Command.StartsWith("sh -c", StringComparison.Ordinal), "command: " + detail.Command);
                            Check.True(detail.StartedUtc > DateTime.UtcNow.AddMinutes(-10) && detail.StartedUtc <= DateTime.UtcNow.AddMinutes(1), "started time is recent: " + detail.StartedUtc.ToString("o"));
                            Check.True(detail.Networks.Count > 0, "attached to a network");
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        }
                    }
                }),
                c.Async("Logs", "GetLogsAsync returns stdout and stderr lines and honors the tail limit", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string? id = null;
                        try
                        {
                            List<string> command = new List<string> { "sh", "-c", "for i in 1 2 3 4 5; do echo out-$i; done; echo err-line 1>&2" };
                            id = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("logs"), command, true, ct).ConfigureAwait(false);
                            await provider.Client.Containers.WaitContainerAsync(id, ct).ConfigureAwait(false);
                            DockerService service = new DockerService(provider.Client);

                            IReadOnlyList<string> all = await service.GetLogsAsync(id, 100, ct).ConfigureAwait(false);
                            Check.Contains("out-1", all, "stdout");
                            Check.Contains("out-5", all, "stdout last");
                            Check.Contains("err-line", all, "stderr");
                            Check.Equal(6, all.Count, "line count");

                            IReadOnlyList<string> tail = await service.GetLogsAsync(id, 2, ct).ConfigureAwait(false);
                            Check.Equal(2, tail.Count, "tail 2");

                            IReadOnlyList<string> clamped = await service.GetLogsAsync(id, 0, ct).ConfigureAwait(false);
                            Check.Equal(1, clamped.Count, "tail 0 is clamped to 1");
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        }
                    }
                }),
                c.Async("NotFound", "Operations on a nonexistent container raise DockerContainerNotFoundException", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        DockerService service = new DockerService(provider.Client);
                        string missing = "docmon-test-missing-" + Guid.NewGuid().ToString("N");
                        await Check.ThrowsAsync<DockerContainerNotFoundException>(() => service.InspectAsync(missing, ct), "InspectAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<DockerContainerNotFoundException>(() => service.StartAsync(missing, ct), "StartAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<DockerContainerNotFoundException>(() => service.StopAsync(missing, ct), "StopAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<DockerContainerNotFoundException>(() => service.RemoveAsync(missing, true, ct), "RemoveAsync").ConfigureAwait(false);
                        await Check.ThrowsAsync<DockerContainerNotFoundException>(() => service.GetLogsAsync(missing, 10, ct), "GetLogsAsync").ConfigureAwait(false);
                    }
                }),
                c.Async("PauseStopped", "Pausing a stopped container is rejected by the daemon", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string? id = null;
                        try
                        {
                            id = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("pause"), DockerFixture.IdleCommand, false, ct).ConfigureAwait(false);
                            await Check.ThrowsAsync<DockerApiException>(() => new DockerService(provider.Client).PauseAsync(id, ct), "PauseAsync on a created container").ConfigureAwait(false);
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        }
                    }
                })
            });
        }

        /// <summary>
        /// Exec, file transfer, stats streaming, and event monitoring.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor RuntimeSuite()
        {
            Cases c = new Cases("DockerRuntime", DockerFixture.SkipReason);
            return new TestSuiteDescriptor("DockerRuntime", "Docker exec, transfer, stats, events (live)", new List<TestCaseDescriptor>
            {
                c.Async("Exec", "ExecService streams stdout and stderr lines separately and returns the exit code", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string? id = null;
                        try
                        {
                            id = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("exec"), DockerFixture.IdleCommand, true, ct).ConfigureAwait(false);
                            List<string> lines = new List<string>();
                            CallbackProgress<string> output = new CallbackProgress<string>(line => { lock (lines) { lines.Add(line); } });
                            ExecResult result = await new ExecService(provider.Client).RunAsync(id, new List<string> { "sh", "-c", "echo one; printf partial; sleep 0.3; echo two 1>&2; sleep 0.3; printf ' done'; exit 3" }, output, ct).ConfigureAwait(false);
                            Check.Equal(3, result.ExitCode, "exit code");
                            Check.False(result.Succeeded, "not succeeded");
                            Check.Contains("one", lines, "stdout line");
                            Check.Contains("two", lines, "stderr line is not merged with a partial stdout line");
                            Check.Contains("partial done", lines, "unterminated stdout line flushed intact");

                            ExecResult ok = await new ExecService(provider.Client).RunAsync(id, new List<string> { "true" }, output, ct).ConfigureAwait(false);
                            Check.True(ok.Succeeded, "true succeeds");
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        }
                    }
                }),
                c.Async("ExecMissingContainer", "Exec against a nonexistent container throws", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        CallbackProgress<string> output = new CallbackProgress<string>(line => { });
                        await Check.ThrowsAsync<DockerApiException>(() => new ExecService(provider.Client).RunAsync("docmon-test-missing-" + Guid.NewGuid().ToString("N"), new List<string> { "true" }, output, ct), "RunAsync").ConfigureAwait(false);
                    }
                }),
                c.Async("TransferRoundTrip", "Files and directories copy into a container and back out intact", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string root = Path.Combine(Path.GetTempPath(), "docmon-test-" + Guid.NewGuid().ToString("N"));
                        string? id = null;
                        try
                        {
                            string source = Path.Combine(root, "payload");
                            Directory.CreateDirectory(Path.Combine(source, "nested"));
                            File.WriteAllText(Path.Combine(source, "hello.txt"), "hello docmon");
                            File.WriteAllText(Path.Combine(source, "nested", "deep.txt"), "deep content");
                            string single = Path.Combine(root, "single.txt");
                            File.WriteAllText(single, "single file");

                            id = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("xfer"), DockerFixture.IdleCommand, true, ct).ConfigureAwait(false);
                            TransferService transfer = new TransferService(provider.Client);
                            await transfer.CopyInAsync(id, source + Path.DirectorySeparatorChar, "/tmp", ct).ConfigureAwait(false);
                            await transfer.CopyInAsync(id, single, "/tmp", ct).ConfigureAwait(false);

                            List<string> lines = new List<string>();
                            CallbackProgress<string> output = new CallbackProgress<string>(line => { lock (lines) { lines.Add(line); } });
                            ExecResult cat = await new ExecService(provider.Client).RunAsync(id, new List<string> { "sh", "-c", "cat /tmp/payload/hello.txt; echo; cat /tmp/payload/nested/deep.txt; echo; cat /tmp/single.txt" }, output, ct).ConfigureAwait(false);
                            Check.True(cat.Succeeded, "files exist in container");
                            Check.Contains("hello docmon", lines, "top-level file");
                            Check.Contains("deep content", lines, "nested file");
                            Check.Contains("single file", lines, "single file");

                            string destination = Path.Combine(root, "out");
                            await transfer.CopyOutAsync(id, "/tmp/payload", destination, ct).ConfigureAwait(false);
                            Check.Equal("hello docmon", File.ReadAllText(Path.Combine(destination, "payload", "hello.txt")), "copied out top-level file");
                            Check.Equal("deep content", File.ReadAllText(Path.Combine(destination, "payload", "nested", "deep.txt")), "copied out nested file");

                            await transfer.CopyOutAsync(id, "/tmp/single.txt", destination, ct).ConfigureAwait(false);
                            Check.Equal("single file", File.ReadAllText(Path.Combine(destination, "single.txt")), "copied out single file");
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                            if (Directory.Exists(root))
                                Directory.Delete(root, true);
                        }
                    }
                }),
                c.Async("TransferMissingPath", "Copying out a path that does not exist in the container throws", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string root = Path.Combine(Path.GetTempPath(), "docmon-test-" + Guid.NewGuid().ToString("N"));
                        string? id = null;
                        try
                        {
                            id = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("xfer404"), DockerFixture.IdleCommand, true, ct).ConfigureAwait(false);
                            await Check.ThrowsAsync<DockerApiException>(() => new TransferService(provider.Client).CopyOutAsync(id, "/no/such/path", root, ct), "CopyOutAsync").ConfigureAwait(false);
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                            if (Directory.Exists(root))
                                Directory.Delete(root, true);
                        }
                    }
                }),
                c.Async("Stats", "StatsStreamer reports samples for a running container until cancelled", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string? id = null;
                        try
                        {
                            id = await DockerFixture.CreateContainerAsync(provider, DockerFixture.UniqueName("stats"), DockerFixture.IdleCommand, true, ct).ConfigureAwait(false);
                            TaskCompletionSource<ContainerStatsSample> first = new TaskCompletionSource<ContainerStatsSample>(TaskCreationOptions.RunContinuationsAsynchronously);
                            CallbackProgress<ContainerStatsSample> progress = new CallbackProgress<ContainerStatsSample>(sample => first.TrySetResult(sample));

                            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                            {
                                Task stream = new StatsStreamer(provider.Client).StreamAsync(id, progress, cts.Token);
                                Task winner = await Task.WhenAny(first.Task, Task.Delay(_Timeout, ct)).ConfigureAwait(false);
                                Check.True(winner == first.Task, "a stats sample arrived");
                                cts.Cancel();
                                await stream.ConfigureAwait(false);
                            }

                            ContainerStatsSample sample = await first.Task.ConfigureAwait(false);
                            Check.Equal(id, sample.ContainerId, "sample container id");
                            Check.True(sample.MemoryUsage > 0, "memory usage reported");
                            Check.True(sample.CpuPercent >= 0, "cpu non-negative");
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        }
                    }
                }),
                c.Async("StatsMissingContainer", "Streaming stats for a nonexistent container ends quietly", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        int samples = 0;
                        CallbackProgress<ContainerStatsSample> progress = new CallbackProgress<ContainerStatsSample>(sample => Interlocked.Increment(ref samples));
                        await DockerFixture.WithTimeoutAsync(async token =>
                        {
                            await new StatsStreamer(provider.Client).StreamAsync("docmon-test-missing-" + Guid.NewGuid().ToString("N"), progress, token).ConfigureAwait(false);
                            return true;
                        }, _Timeout, ct).ConfigureAwait(false);
                        Check.Equal(0, samples, "no samples");
                    }
                }),
                c.Async("Events", "EventsMonitor reports lifecycle events for a container by name", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await DockerFixture.EnsureImageAsync(provider, ct).ConfigureAwait(false);
                        string name = DockerFixture.UniqueName("events");
                        string? id = null;
                        List<DockerEventInfo> events = new List<DockerEventInfo>();
                        TaskCompletionSource<bool> sawStart = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        CallbackProgress<DockerEventInfo> progress = new CallbackProgress<DockerEventInfo>(info =>
                        {
                            lock (events)
                            {
                                events.Add(info);
                            }

                            if (info.Type == "container" && info.Action == "start" && info.Actor == name)
                                sawStart.TrySetResult(true);
                        });

                        using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            Task monitor = new EventsMonitor(provider.Client).MonitorAsync(progress, cts.Token);
                            try
                            {
                                await Task.Delay(500, ct).ConfigureAwait(false);
                                id = await DockerFixture.CreateContainerAsync(provider, name, DockerFixture.IdleCommand, true, ct).ConfigureAwait(false);
                                Task winner = await Task.WhenAny(sawStart.Task, Task.Delay(_Timeout, ct)).ConfigureAwait(false);
                                Check.True(winner == sawStart.Task, "start event observed for " + name);
                            }
                            finally
                            {
                                cts.Cancel();
                                await monitor.ConfigureAwait(false);
                                await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                            }
                        }

                        lock (events)
                        {
                            DockerEventInfo? start = events.Find(e => e.Actor == name && e.Action == "start");
                            Check.True(start != null && (DateTime.UtcNow - start.TimeUtc).TotalMinutes < 5, "event time is recent");
                        }
                    }
                })
            });
        }

        /// <summary>
        /// Compose stack discovery and lifecycle through the <c>docker compose</c> CLI.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ComposeSuite()
        {
            Cases c = new Cases("DockerCompose", DockerFixture.SkipReason);
            return new TestSuiteDescriptor("DockerCompose", "Docker compose (live)", new List<TestCaseDescriptor>
            {
                c.Async("StackLifecycle", "Up, discover, restart, single-service up, and down a two-service stack", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        await DockerFixture.EnsureImageAsync(provider, ct).ConfigureAwait(false);
                        string project = DockerFixture.UniqueName("stack");
                        string directory = Path.Combine(Path.GetTempPath(), project);
                        Directory.CreateDirectory(directory);
                        string file = Path.Combine(directory, "compose.yaml");
                        File.WriteAllText(file, ComposeYaml());

                        ComposeService compose = new ComposeService(provider.Client);
                        StringBuilder log = new StringBuilder();
                        CallbackProgress<string> output = new CallbackProgress<string>(line => { lock (log) { log.AppendLine(line); } });
                        try
                        {
                            int up = await compose.UpAsync(project, file, null, output, ct).ConfigureAwait(false);
                            Check.Equal(0, up, "up exit code. Output: " + log);

                            ComposeStack? stack = FindStack(await compose.DiscoverAsync(ct).ConfigureAwait(false), project);
                            Check.NotNull(stack, "stack discovered");
                            Check.Equal(2, stack!.ServiceCount, "service count");
                            Check.Equal(2, stack.RunningCount, "running count");
                            Check.Equal("api", stack.Services[0].Name, "services sorted (api first)");
                            Check.Equal("worker", stack.Services[1].Name, "services sorted (worker second)");
                            Check.True(stack.ConfigFilePath.EndsWith("compose.yaml", StringComparison.Ordinal), "config file discovered: " + stack.ConfigFilePath);
                            Check.Equal(DockerFixture.TestImage, stack.Services[0].RunningImage, "running image");
                            Check.True(stack.Services[0].ContainerId.Length > 0, "container id");

                            Check.Equal(0, await compose.RestartAsync(project, file, output, ct).ConfigureAwait(false), "restart exit code. Output: " + log);
                            Check.Equal(0, await compose.UpAsync(project, file, "api", output, ct).ConfigureAwait(false), "single-service up. Output: " + log);

                            // With no config file the CLI resolves the stack by project name alone.
                            int down = await compose.DownAsync(project, string.Empty, output, ct).ConfigureAwait(false);
                            Check.Equal(0, down, "down by project name exit code. Output: " + log);
                            Check.Null(FindStack(await compose.DiscoverAsync(ct).ConfigureAwait(false), project), "stack gone after down");
                        }
                        finally
                        {
                            try
                            {
                                await compose.DownAsync(project, file, new CallbackProgress<string>(line => { }), CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                // Best-effort cleanup.
                            }

                            Directory.Delete(directory, true);
                        }
                    }
                }),
                c.Async("InvalidComposeFile", "A malformed compose file yields a non-zero exit code and error output", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string project = DockerFixture.UniqueName("bad");
                        string directory = Path.Combine(Path.GetTempPath(), project);
                        Directory.CreateDirectory(directory);
                        string file = Path.Combine(directory, "compose.yaml");
                        File.WriteAllText(file, "services:\n  broken: [this is: not valid\n");
                        try
                        {
                            List<string> lines = new List<string>();
                            CallbackProgress<string> output = new CallbackProgress<string>(line => { lock (lines) { lines.Add(line); } });
                            int exit = await new ComposeService(provider.Client).UpAsync(project, file, null, output, ct).ConfigureAwait(false);
                            Check.True(exit != 0, "non-zero exit code");
                            Check.True(lines.Count > 0, "error output captured");
                        }
                        finally
                        {
                            Directory.Delete(directory, true);
                        }
                    }
                }),
                c.Async("MissingComposeFile", "A nonexistent compose file yields a non-zero exit code", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string missing = Path.Combine(Path.GetTempPath(), "docmon-missing-" + Guid.NewGuid().ToString("N"), "compose.yaml");
                        int exit = await new ComposeService(provider.Client).PullAsync(DockerFixture.UniqueName("nofile"), missing, new CallbackProgress<string>(line => { }), ct).ConfigureAwait(false);
                        Check.True(exit != 0, "non-zero exit code");
                    }
                }),
                c.Async("DiscoverIgnoresUnlabeled", "Discovery ignores containers without a compose project label", async ct =>
                {
                    using (DockerClientProvider provider = DockerFixture.CreateProvider())
                    {
                        string name = DockerFixture.UniqueName("plain");
                        string? id = null;
                        try
                        {
                            id = await DockerFixture.CreateContainerAsync(provider, name, DockerFixture.IdleCommand, false, ct).ConfigureAwait(false);
                            foreach (ComposeStack stack in await new ComposeService(provider.Client).DiscoverAsync(ct).ConfigureAwait(false))
                            {
                                foreach (ComposeServiceInfo service in stack.Services)
                                    Check.False(service.ContainerId == id, "unlabeled container appeared in stack " + stack.Project);
                            }
                        }
                        finally
                        {
                            await DockerFixture.RemoveContainerQuietlyAsync(provider, id).ConfigureAwait(false);
                        }
                    }
                })
            });
        }

        private static async Task<ContainerStateEnum> StateOfAsync(DockerService service, string id, CancellationToken token)
        {
            ContainerInfo? info = Find(await service.ListContainersAsync(true, token).ConfigureAwait(false), id);
            return info == null ? ContainerStateEnum.Unknown : info.State;
        }

        private static ContainerInfo? Find(IReadOnlyList<ContainerInfo> containers, string id)
        {
            foreach (ContainerInfo container in containers)
            {
                if (container.Id == id)
                    return container;
            }

            return null;
        }

        private static ComposeStack? FindStack(IReadOnlyList<ComposeStack> stacks, string project)
        {
            foreach (ComposeStack stack in stacks)
            {
                if (stack.Project == project)
                    return stack;
            }

            return null;
        }

        private static string ComposeYaml()
        {
            string service =
                "    image: " + DockerFixture.TestImage + "\n" +
                "    pull_policy: missing\n" +
                "    init: true\n" +
                "    labels:\n" +
                "      " + DockerFixture.TestLabel + ": \"true\"\n" +
                "    command: [\"sh\", \"-c\", \"while true; do sleep 1; done\"]\n";

            return "services:\n  worker:\n" + service + "  api:\n" + service;
        }
    }
}
