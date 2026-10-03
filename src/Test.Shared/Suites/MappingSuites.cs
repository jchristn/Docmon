namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Docker.DotNet.Models;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Implementations;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Suites for the translation layer between Docker Engine API payloads and Docmon models. These run
    /// against hand-built Docker.DotNet response objects, so they need no daemon.
    /// </summary>
    public static class MappingSuites
    {
        /// <summary>
        /// Container, image, inspect, port, and log mapping in <see cref="DockerService"/>.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor DockerServiceMappingSuite()
        {
            Cases c = new Cases("DockerServiceMapping");
            return new TestSuiteDescriptor("DockerServiceMapping", "Docker service mapping", new List<TestCaseDescriptor>
            {
                c.Sync("MapState", "Engine state strings map to ContainerStateEnum (case-insensitive)", () =>
                {
                    Dictionary<string, ContainerStateEnum> expected = StateTable();
                    foreach (KeyValuePair<string, ContainerStateEnum> pair in expected)
                    {
                        Check.Equal(pair.Value, DockerService.MapState(pair.Key), "DockerService.MapState('" + pair.Key + "')");
                        Check.Equal(pair.Value, DockerService.MapState(pair.Key.ToUpperInvariant()), "DockerService.MapState upper '" + pair.Key + "'");
                    }
                }),
                c.Sync("MapStateUnknown", "Null, empty, and unrecognized states map to Unknown", () =>
                {
                    Check.Equal(ContainerStateEnum.Unknown, DockerService.MapState(null), "null");
                    Check.Equal(ContainerStateEnum.Unknown, DockerService.MapState(string.Empty), "empty");
                    Check.Equal(ContainerStateEnum.Unknown, DockerService.MapState("hibernating"), "unrecognized");
                }),
                c.Sync("ExtractHealth", "Health is extracted from the status string", () =>
                {
                    Check.Equal("healthy", DockerService.ExtractHealth("Up 3 minutes (healthy)"), "healthy");
                    Check.Equal("unhealthy", DockerService.ExtractHealth("Up 3 minutes (unhealthy)"), "unhealthy");
                    Check.Equal("health: starting", DockerService.ExtractHealth("Up 5 seconds (health: starting)"), "starting");
                }),
                c.Sync("ExtractHealthNone", "Statuses without a health marker yield empty", () =>
                {
                    Check.Equal(string.Empty, DockerService.ExtractHealth("Exited (0) 2 hours ago"), "exit code parens");
                    Check.Equal(string.Empty, DockerService.ExtractHealth("Up 2 days"), "no parens");
                    Check.Equal(string.Empty, DockerService.ExtractHealth("Up ) weird ("), "reversed parens");
                    Check.Equal(string.Empty, DockerService.ExtractHealth(null), "null");
                    Check.Equal(string.Empty, DockerService.ExtractHealth(string.Empty), "empty");
                }),
                c.Sync("ExtractName", "The first name is used without its leading slash", () =>
                {
                    Check.Equal("web", DockerService.ExtractName(new List<string> { "/web", "/alias" }), "first name");
                    Check.Equal(string.Empty, DockerService.ExtractName(null), "null names");
                    Check.Equal(string.Empty, DockerService.ExtractName(new List<string>()), "no names");
                }),
                c.Sync("MapContainer", "A container list entry maps every field, including compose labels", () =>
                {
                    DateTime created = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);
                    ContainerListResponse response = new ContainerListResponse
                    {
                        ID = "0123456789abcdef0123456789abcdef",
                        Names = new List<string> { "/shop-web-1" },
                        Image = "nginx:1.27",
                        ImageID = "sha256:img",
                        State = "running",
                        Status = "Up 2 hours (healthy)",
                        Created = created,
                        Ports = new List<Port> { new Port { IP = "0.0.0.0", PublicPort = 8080, PrivatePort = 80, Type = "tcp" } },
                        Labels = new Dictionary<string, string>
                        {
                            { "com.docker.compose.project", "shop" },
                            { "com.docker.compose.service", "web" }
                        }
                    };

                    ContainerInfo info = DockerService.MapContainer(response);
                    Check.Equal(response.ID, info.Id, "id");
                    Check.Equal("0123456789ab", info.ShortId, "short id");
                    Check.Equal("shop-web-1", info.Name, "name");
                    Check.Equal("nginx:1.27", info.Image, "image");
                    Check.Equal("sha256:img", info.ImageId, "image id");
                    Check.Equal(ContainerStateEnum.Running, info.State, "state");
                    Check.Equal("Up 2 hours (healthy)", info.Status, "status");
                    Check.Equal("healthy", info.Health, "health");
                    Check.Equal(DateTimeKind.Utc, info.CreatedUtc.Kind, "created kind");
                    Check.Equal(created.Ticks, info.CreatedUtc.Ticks, "created ticks");
                    Check.Equal(1, info.Ports.Count, "port count");
                    Check.Equal("8080->80/tcp", info.Ports[0].ToString(), "port");
                    Check.Equal("0.0.0.0", info.Ports[0].HostIp, "port host ip");
                    Check.Equal("shop", info.ComposeProject, "compose project");
                    Check.Equal("web", info.ComposeService, "compose service");
                }),
                c.Sync("MapContainerSparse", "A container entry with null fields maps to safe empty values", () =>
                {
                    ContainerInfo info = DockerService.MapContainer(new ContainerListResponse());
                    Check.Equal(string.Empty, info.Id, "id");
                    Check.Equal(string.Empty, info.Name, "name");
                    Check.Equal(string.Empty, info.Image, "image");
                    Check.Equal(string.Empty, info.Status, "status");
                    Check.Equal(string.Empty, info.Health, "health");
                    Check.Equal(ContainerStateEnum.Unknown, info.State, "state");
                    Check.Equal(0, info.Ports.Count, "ports");
                    Check.Equal(string.Empty, info.ComposeProject, "compose project");
                }),
                c.Sync("MapPorts", "Published and unpublished list ports map, defaulting the protocol to tcp", () =>
                {
                    IReadOnlyList<PortMap> ports = DockerService.MapPorts(new List<Port>
                    {
                        new Port { PrivatePort = 5432 },
                        new Port { IP = "127.0.0.1", PublicPort = 5353, PrivatePort = 53, Type = "udp" }
                    });
                    Check.Equal("5432/tcp", ports[0].ToString(), "unpublished default protocol");
                    Check.Equal("5353->53/udp", ports[1].ToString(), "published udp");
                    Check.Equal(0, DockerService.MapPorts(null).Count, "null ports");
                }),
                c.Sync("MapPortBindings", "Inspect port bindings expand per host binding and parse protocol", () =>
                {
                    Dictionary<string, IList<PortBinding>> bindings = new Dictionary<string, IList<PortBinding>>
                    {
                        { "80/tcp", new List<PortBinding> { new PortBinding { HostIP = "0.0.0.0", HostPort = "8080" }, new PortBinding { HostIP = "::", HostPort = "8080" } } },
                        { "53/udp", null! },
                        { "9000", new List<PortBinding>() },
                        { "443/tcp", new List<PortBinding> { new PortBinding { HostIP = null!, HostPort = "not-a-port" } } }
                    };

                    IReadOnlyList<PortMap> ports = DockerService.MapPortBindings(bindings);
                    Check.Equal(5, ports.Count, "port count");
                    Check.Equal("8080->80/tcp", ports[0].ToString(), "ipv4 binding");
                    Check.Equal("::", ports[1].HostIp, "ipv6 binding host");
                    Check.Equal("53/udp", ports[2].ToString(), "null bindings are unpublished");
                    Check.Equal("9000/tcp", ports[3].ToString(), "no protocol defaults to tcp");
                    Check.Equal("443/tcp", ports[4].ToString(), "unparseable host port is unpublished");
                    Check.Equal(string.Empty, ports[4].HostIp, "null host ip");
                    Check.Equal(0, DockerService.MapPortBindings(null).Count, "null map");
                }),
                c.Sync("BuildCommand", "Entrypoint and command are joined with spaces", () =>
                {
                    Check.Equal("/docker-entrypoint.sh nginx -g daemon off;", DockerService.BuildCommand(new List<string> { "/docker-entrypoint.sh" }, new List<string> { "nginx", "-g", "daemon off;" }), "both");
                    Check.Equal("sleep 10", DockerService.BuildCommand(null, new List<string> { "sleep", "10" }), "cmd only");
                    Check.Equal("/init", DockerService.BuildCommand(new List<string> { "/init" }, null), "entrypoint only");
                    Check.Equal(string.Empty, DockerService.BuildCommand(null, null), "neither");
                }),
                c.Sync("ParseDockerTime", "Docker RFC 3339 timestamps (with nanoseconds) parse as UTC", () =>
                {
                    DateTime expected = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);
                    DateTime parsed = DockerService.ParseDockerTime("2026-01-02T03:04:05.123456789Z");
                    Check.Equal(DateTimeKind.Utc, parsed.Kind, "kind");
                    Check.True(Math.Abs((parsed - expected).Ticks) <= 1, "nanosecond timestamp (to tick precision): " + parsed.ToString("o"));
                    Check.Equal(new DateTime(2026, 1, 2, 1, 4, 5, DateTimeKind.Utc), DockerService.ParseDockerTime("2026-01-02T03:04:05+02:00"), "offset normalized to UTC");
                }),
                c.Sync("ParseDockerTimeInvalid", "Empty or invalid timestamps yield DateTime.MinValue", () =>
                {
                    Check.Equal(DateTime.MinValue, DockerService.ParseDockerTime(null), "null");
                    Check.Equal(DateTime.MinValue, DockerService.ParseDockerTime(string.Empty), "empty");
                    Check.Equal(DateTime.MinValue, DockerService.ParseDockerTime("yesterday-ish"), "garbage");
                }),
                c.Sync("MapDetail", "Container inspect maps config, state, mounts, networks, labels, and ports", () =>
                {
                    ContainerInspectResponse response = new ContainerInspectResponse
                    {
                        ID = "abc",
                        Name = "/shop-db-1",
                        Created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                        Config = new Config
                        {
                            Image = "postgres:17",
                            Env = new List<string> { "POSTGRES_DB=shop" },
                            Entrypoint = new List<string> { "docker-entrypoint.sh" },
                            Cmd = new List<string> { "postgres" },
                            Labels = new Dictionary<string, string> { { "tier", "data" } }
                        },
                        State = new ContainerState
                        {
                            Status = "running",
                            StartedAt = "2026-01-01T00:00:10Z",
                            Health = new Health { Status = "healthy" }
                        },
                        HostConfig = new HostConfig { RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped } },
                        Mounts = new List<MountPoint>
                        {
                            new MountPoint { Name = "pgdata", Source = "/var/lib/docker/volumes/pgdata/_data", Destination = "/var/lib/postgresql/data" },
                            new MountPoint { Source = "/srv/init", Destination = "/docker-entrypoint-initdb.d" }
                        },
                        NetworkSettings = new NetworkSettings
                        {
                            Networks = new Dictionary<string, EndpointSettings> { { "shop_default", new EndpointSettings() } },
                            Ports = new Dictionary<string, IList<PortBinding>> { { "5432/tcp", new List<PortBinding> { new PortBinding { HostIP = "0.0.0.0", HostPort = "5432" } } } }
                        }
                    };

                    ContainerDetail detail = DockerService.MapDetail(response);
                    Check.Equal("abc", detail.Id, "id");
                    Check.Equal("shop-db-1", detail.Name, "name");
                    Check.Equal("postgres:17", detail.Image, "image");
                    Check.Equal("running", detail.State, "state");
                    Check.Equal("healthy", detail.Health, "health");
                    Check.Equal(DateTimeKind.Utc, detail.CreatedUtc.Kind, "created kind");
                    Check.Equal(new DateTime(2026, 1, 1, 0, 0, 10, DateTimeKind.Utc), detail.StartedUtc, "started");
                    Check.True(detail.RestartPolicy.Length > 0, "restart policy populated");
                    Check.Equal("docker-entrypoint.sh postgres", detail.Command, "command");
                    Check.Contains("POSTGRES_DB=shop", detail.Environment, "environment");
                    Check.Contains("pgdata -> /var/lib/postgresql/data", detail.Mounts, "named volume mount");
                    Check.Contains("/srv/init -> /docker-entrypoint-initdb.d", detail.Mounts, "bind mount");
                    Check.Contains("shop_default", detail.Networks, "networks");
                    Check.Equal("data", detail.Labels["tier"], "labels");
                    Check.Equal("5432->5432/tcp", detail.Ports[0].ToString(), "ports");
                }),
                c.Sync("MapDetailSparse", "A minimal inspect payload maps without null reference errors", () =>
                {
                    ContainerDetail detail = DockerService.MapDetail(new ContainerInspectResponse());
                    Check.Equal(string.Empty, detail.Name, "name");
                    Check.Equal(string.Empty, detail.Image, "image");
                    Check.Equal(string.Empty, detail.Command, "command");
                    Check.Equal(DateTime.MinValue, detail.StartedUtc, "started");
                    Check.Equal(0, detail.Mounts.Count, "mounts");
                    Check.Equal(0, detail.Networks.Count, "networks");
                    Check.Equal(0, detail.Ports.Count, "ports");
                }),
                c.Sync("MapImageTagged", "A tagged image splits repository and tag on the last colon", () =>
                {
                    ImagesListResponse response = new ImagesListResponse
                    {
                        ID = "sha256:abc",
                        Size = 4096,
                        Created = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Unspecified),
                        RepoTags = new List<string> { "ghcr.io/acme/api:1.4.2", "ghcr.io/acme/api:latest" },
                        RepoDigests = new List<string> { "ghcr.io/acme/api@sha256:def" }
                    };

                    ImageInfo image = DockerService.MapImage(response);
                    Check.Equal("ghcr.io/acme/api", image.Repository, "repository");
                    Check.Equal("1.4.2", image.Tag, "tag");
                    Check.Equal(4096L, image.SizeBytes, "size");
                    Check.Equal(DateTimeKind.Utc, image.CreatedUtc.Kind, "created kind");
                    Check.Contains("ghcr.io/acme/api@sha256:def", image.RepoDigests, "repo digests");
                    Check.False(image.IsDangling, "not dangling");
                }),
                c.Sync("MapImageRegistryPort", "A registry port is not mistaken for a tag", () =>
                {
                    ImageInfo image = DockerService.MapImage(new ImagesListResponse { RepoTags = new List<string> { "localhost:5000/app" } });
                    Check.Equal("localhost:5000/app", image.Repository, "repository");
                    Check.Equal(ImageInfo.NoneLabel, image.Tag, "tag");
                }),
                c.Sync("MapImageDangling", "Untagged and <none> images map as dangling", () =>
                {
                    Check.True(DockerService.MapImage(new ImagesListResponse()).IsDangling, "null repo tags");
                    Check.True(DockerService.MapImage(new ImagesListResponse { RepoTags = new List<string>() }).IsDangling, "empty repo tags");
                    ImageInfo none = DockerService.MapImage(new ImagesListResponse { RepoTags = new List<string> { "<none>:<none>" } });
                    Check.True(none.IsDangling, "<none> repo tags");
                    Check.Equal(ImageInfo.NoneLabel, none.Tag, "dangling tag label");
                }),
                c.Sync("IsNoneTag", "IsNoneTag is true only when every tag is a <none> placeholder", () =>
                {
                    Check.True(DockerService.IsNoneTag(new List<string> { "<none>:<none>" }), "only none");
                    Check.False(DockerService.IsNoneTag(new List<string> { "<none>:<none>", "nginx:1" }), "mixed");
                    Check.False(DockerService.IsNoneTag(new List<string> { "nginx:1" }), "tagged");
                }),
                c.Sync("BuildCreateParameters", "Pull references split into image and tag", () =>
                {
                    ExpectPull("nginx:1.27", "nginx", "1.27");
                    ExpectPull("nginx", "nginx", "latest");
                    ExpectPull("ghcr.io/acme/api:2", "ghcr.io/acme/api", "2");
                    ExpectPull("localhost:5000/app", "localhost:5000/app", "latest");
                    ExpectPull("localhost:5000/app:dev", "localhost:5000/app", "dev");
                }),
                c.Sync("BuildCreateParametersDigest", "Digest-pinned pull references are passed through whole", () =>
                {
                    string pinned = "nginx@sha256:" + new string('a', 64);
                    ImagesCreateParameters parameters = DockerService.BuildCreateParameters(pinned);
                    Check.Equal(pinned, parameters.FromImage, "from image");
                    Check.True(string.IsNullOrEmpty(parameters.Tag), "no tag for a digest pull");
                }),
                c.Sync("FormatPullMessage", "Pull progress messages render as readable lines", () =>
                {
                    Check.Equal("error: manifest unknown", DockerService.FormatPullMessage(new JSONMessage { ErrorMessage = "manifest unknown", Status = "ignored" }), "error wins");
                    Check.Equal("Pulling from library/nginx", DockerService.FormatPullMessage(new JSONMessage { Status = "Pulling from library/nginx" }), "status only");
                    Check.Equal("abc123: Pull complete", DockerService.FormatPullMessage(new JSONMessage { ID = "abc123", Status = "Pull complete" }), "id and status");
                    Check.Equal("abc123: Downloading [==>  ] 1MB/2MB", DockerService.FormatPullMessage(new JSONMessage { ID = "abc123", Status = "Downloading", ProgressMessage = "[==>  ] 1MB/2MB" }), "with progress");
                    Check.Null(DockerService.FormatPullMessage(new JSONMessage()), "empty message is dropped");
                }),
                c.Sync("SplitLines", "Log text splits on any newline style and drops blank lines", () =>
                {
                    Check.SequenceEqual(new List<string> { "a", "b", "c" }, new List<string>(DockerService.SplitLines("a\r\nb\n\nc\r")), "mixed newlines");
                    Check.Equal(0, DockerService.SplitLines(string.Empty).Count, "empty");
                    Check.SequenceEqual(new List<string> { "single" }, new List<string>(DockerService.SplitLines("single")), "no newline");
                })
            });
        }

        /// <summary>
        /// Compose CLI argument construction and label handling in <see cref="ComposeService"/>.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ComposeMappingSuite()
        {
            Cases c = new Cases("ComposeMapping");
            return new TestSuiteDescriptor("ComposeMapping", "Compose service mapping", new List<TestCaseDescriptor>
            {
                c.Sync("BaseArguments", "Base arguments include the project and, when known, the config file", () =>
                {
                    Check.SequenceEqual(new List<string> { "compose", "-p", "shop" }, ComposeService.BuildBaseArguments("shop", string.Empty), "no config file");
                    Check.SequenceEqual(new List<string> { "compose", "-p", "shop", "-f", "/srv/shop/compose.yaml" }, ComposeService.BuildBaseArguments("shop", "/srv/shop/compose.yaml"), "with config file");
                }),
                c.Sync("BaseArgumentsNullProject", "A null project throws ArgumentNullException", () =>
                    Check.Throws<ArgumentNullException>(() => ComposeService.BuildBaseArguments(null!, string.Empty), "BuildBaseArguments(null)")),
                c.Sync("FirstConfigFile", "Only the first of several comma-separated config files is used", () =>
                {
                    Check.Equal("/a/compose.yaml", ComposeService.FirstConfigFile("/a/compose.yaml,/a/compose.override.yaml"), "multiple");
                    Check.Equal("/a/compose.yaml", ComposeService.FirstConfigFile("  /a/compose.yaml  "), "trimmed");
                }),
                c.Sync("MapState", "Compose state mapping matches the Docker service mapping", () =>
                {
                    foreach (KeyValuePair<string, ContainerStateEnum> pair in StateTable())
                        Check.Equal(pair.Value, ComposeService.MapState(pair.Key), "ComposeService.MapState('" + pair.Key + "')");
                    Check.Equal(ContainerStateEnum.Unknown, ComposeService.MapState(null), "null");
                    Check.Equal(ContainerStateEnum.Unknown, ComposeService.MapState("bogus"), "bogus");
                })
            });
        }

        /// <summary>
        /// Stats sample, event, and exec output conversion.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor StreamConversionSuite()
        {
            Cases c = new Cases("StreamConversion");
            return new TestSuiteDescriptor("StreamConversion", "Stream conversion (stats, events, exec)", new List<TestCaseDescriptor>
            {
                c.Sync("StatsIncomplete", "Stats payloads without CPU sections are dropped", () =>
                {
                    Check.Null(StatsStreamer.Convert("id", null!), "null response");
                    Check.Null(StatsStreamer.Convert("id", new ContainerStatsResponse()), "no cpu stats");
                    Check.Null(StatsStreamer.Convert("id", new ContainerStatsResponse { CPUStats = new CPUStats() }), "no pre-cpu stats");
                }),
                c.Sync("StatsFull", "A full stats payload computes CPU, memory, network, and block IO", () =>
                {
                    ContainerStatsResponse response = new ContainerStatsResponse
                    {
                        CPUStats = new CPUStats { OnlineCPUs = 2, SystemUsage = 2000, CPUUsage = new CPUUsage { TotalUsage = 1500 } },
                        PreCPUStats = new CPUStats { SystemUsage = 1000, CPUUsage = new CPUUsage { TotalUsage = 1000 } },
                        MemoryStats = new MemoryStats { Usage = 256, Limit = 1024 },
                        Networks = new Dictionary<string, NetworkStats>
                        {
                            { "eth0", new NetworkStats { RxBytes = 100, TxBytes = 10 } },
                            { "eth1", new NetworkStats { RxBytes = 50, TxBytes = 5 } }
                        },
                        BlkioStats = new BlkioStats
                        {
                            IoServiceBytesRecursive = new List<BlkioStatEntry>
                            {
                                new BlkioStatEntry { Op = "Read", Value = 300 },
                                new BlkioStatEntry { Op = "write", Value = 40 },
                                new BlkioStatEntry { Op = "READ", Value = 2 },
                                new BlkioStatEntry { Op = "Total", Value = 9999 }
                            }
                        }
                    };

                    ContainerStatsSample? sample = StatsStreamer.Convert("cid", response);
                    Check.NotNull(sample, "sample");
                    Check.Equal("cid", sample!.ContainerId, "container id");
                    Check.Close(100.0, sample.CpuPercent, 0.001, "cpu percent");
                    Check.Equal(256L, sample.MemoryUsage, "memory usage");
                    Check.Equal(1024L, sample.MemoryLimit, "memory limit");
                    Check.Close(25.0, sample.MemoryPercent, 0.001, "memory percent");
                    Check.Equal(150L, sample.NetworkRxBytes, "rx summed across interfaces");
                    Check.Equal(15L, sample.NetworkTxBytes, "tx summed across interfaces");
                    Check.Equal(302L, sample.BlockReadBytes, "block read (case-insensitive op)");
                    Check.Equal(40L, sample.BlockWriteBytes, "block write");
                    Check.True((DateTime.UtcNow - sample.AtUtc).TotalMinutes < 1, "timestamp is current");
                }),
                c.Sync("StatsCpuCountFallback", "Online CPUs fall back to the per-CPU usage count", () =>
                {
                    ContainerStatsResponse response = new ContainerStatsResponse
                    {
                        CPUStats = new CPUStats { OnlineCPUs = 0, SystemUsage = 2000, CPUUsage = new CPUUsage { TotalUsage = 2000, PercpuUsage = new List<ulong> { 1, 1, 1, 1 } } },
                        PreCPUStats = new CPUStats { SystemUsage = 1000, CPUUsage = new CPUUsage { TotalUsage = 1000 } }
                    };

                    ContainerStatsSample? sample = StatsStreamer.Convert("cid", response);
                    Check.Close(400.0, sample!.CpuPercent, 0.001, "cpu percent with 4 per-cpu entries");
                    Check.Equal(0L, sample.MemoryUsage, "missing memory stats");
                    Check.Equal(0L, sample.NetworkRxBytes, "missing networks");
                }),
                c.Sync("EventActorName", "Event actor prefers the name attribute", () =>
                {
                    Message message = new Message
                    {
                        Type = "container",
                        Action = "start",
                        Time = 1767225600,
                        Actor = new Actor { ID = "0123456789abcdef", Attributes = new Dictionary<string, string> { { "name", "web" }, { "image", "nginx" } } }
                    };

                    DockerEventInfo info = EventsMonitor.Convert(message);
                    Check.Equal("container", info.Type, "type");
                    Check.Equal("start", info.Action, "action");
                    Check.Equal("web", info.Actor, "actor");
                    Check.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), info.TimeUtc, "time");
                }),
                c.Sync("EventActorFallbacks", "Event actor falls back to image, then short ID, then message ID", () =>
                {
                    Message imageOnly = new Message { Actor = new Actor { Attributes = new Dictionary<string, string> { { "name", string.Empty }, { "image", "nginx:1" } } } };
                    Check.Equal("nginx:1", EventsMonitor.Convert(imageOnly).Actor, "image");

                    Message idOnly = new Message { Actor = new Actor { ID = "0123456789abcdef0123" } };
                    Check.Equal("0123456789ab", EventsMonitor.Convert(idOnly).Actor, "short id");

                    Message shortId = new Message { Actor = new Actor { ID = "abc" } };
                    Check.Equal("abc", EventsMonitor.Convert(shortId).Actor, "already short id");

                    Message legacy = new Message { ID = "legacy-id" };
                    Check.Equal("legacy-id", EventsMonitor.Convert(legacy).Actor, "message id");

                    DockerEventInfo empty = EventsMonitor.Convert(new Message());
                    Check.Equal(string.Empty, empty.Actor, "nothing known");
                    Check.Equal(string.Empty, empty.Type, "type");
                    Check.True((DateTime.UtcNow - empty.TimeUtc).TotalMinutes < 1, "missing time defaults to now");
                }),
                c.Sync("ExecLineSplitting", "Exec output emits complete lines and keeps the partial remainder", () =>
                {
                    List<string> lines = new List<string>();
                    CallbackProgress<string> progress = new CallbackProgress<string>(lines.Add);
                    StringBuilder pending = new StringBuilder("first\r\nsecond\n\nthird-partial");
                    ExecService.EmitCompleteLines(pending, progress);
                    Check.SequenceEqual(new List<string> { "first", "second", string.Empty }, lines, "emitted lines");
                    Check.Equal("third-partial", pending.ToString(), "pending remainder");

                    pending.Append(" done\n");
                    ExecService.EmitCompleteLines(pending, progress);
                    Check.Equal("third-partial done", lines[lines.Count - 1], "completed line");
                    Check.Equal(0, pending.Length, "nothing pending");
                })
            });
        }

        private static Dictionary<string, ContainerStateEnum> StateTable()
        {
            return new Dictionary<string, ContainerStateEnum>
            {
                { "created", ContainerStateEnum.Created },
                { "running", ContainerStateEnum.Running },
                { "paused", ContainerStateEnum.Paused },
                { "restarting", ContainerStateEnum.Restarting },
                { "exited", ContainerStateEnum.Exited },
                { "dead", ContainerStateEnum.Dead },
                { "removing", ContainerStateEnum.Removing }
            };
        }

        private static void ExpectPull(string reference, string fromImage, string tag)
        {
            ImagesCreateParameters parameters = DockerService.BuildCreateParameters(reference);
            Check.Equal(fromImage, parameters.FromImage, "FromImage for '" + reference + "'");
            Check.Equal(tag, parameters.Tag, "Tag for '" + reference + "'");
        }
    }
}
