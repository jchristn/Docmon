namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using Docmon.Core.Registries;
    using Docmon.Core.Services;
    using Docmon.Core.Services.Implementations;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Suites for the Docmon.Core model types: validation, defaults, and computed properties.
    /// </summary>
    public static class ModelSuites
    {
        /// <summary>
        /// Model defaults, validation, and computed properties.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ModelSuite()
        {
            Cases c = new Cases("Models");
            return new TestSuiteDescriptor("Models", "Models", new List<TestCaseDescriptor>
            {
                c.Sync("ImageReferenceDefaults", "ImageReference defaults to docker.io and latest", () =>
                {
                    ImageReference reference = new ImageReference();
                    Check.Equal("docker.io", reference.RegistryHost, "default host");
                    Check.Equal("latest", reference.Tag, "default tag");
                    Check.Equal(string.Empty, reference.Repository, "default repository");
                    Check.True(reference.IsDockerHub, "default is Docker Hub");
                }),
                c.Sync("ImageReferenceRejectsEmpty", "ImageReference rejects null, empty, and whitespace values", () =>
                {
                    ImageReference reference = new ImageReference();
                    foreach (string? bad in new string?[] { null, string.Empty, "   " })
                    {
                        Check.Throws<ArgumentNullException>(() => reference.RegistryHost = bad!, "RegistryHost = '" + bad + "'");
                        Check.Throws<ArgumentNullException>(() => reference.Repository = bad!, "Repository = '" + bad + "'");
                        Check.Throws<ArgumentNullException>(() => reference.Tag = bad!, "Tag = '" + bad + "'");
                    }

                    Check.Equal("docker.io", reference.RegistryHost, "host unchanged after rejected sets");
                }),
                c.Sync("ImageReferenceNonHub", "IsDockerHub is false for other registries", () =>
                {
                    ImageReference reference = new ImageReference();
                    reference.RegistryHost = "ghcr.io";
                    reference.Repository = "acme/api";
                    reference.Tag = "1.0";
                    Check.False(reference.IsDockerHub, "ghcr.io is not Docker Hub");
                    Check.Equal("ghcr.io/acme/api:1.0", reference.ToString(), "ToString");
                }),
                c.Sync("ContainerShortId", "ContainerInfo.ShortId truncates to 12 characters only when longer", () =>
                {
                    ContainerInfo info = new ContainerInfo();
                    Check.Equal(string.Empty, info.ShortId, "empty id");
                    info.Id = "abc123";
                    Check.Equal("abc123", info.ShortId, "short id unchanged");
                    info.Id = "0123456789abcdef0123";
                    Check.Equal("0123456789ab", info.ShortId, "long id truncated");
                }),
                c.Sync("ContainerIsRunning", "ContainerInfo.IsRunning is true only for Running", () =>
                {
                    ContainerInfo info = new ContainerInfo();
                    foreach (ContainerStateEnum state in Enum.GetValues(typeof(ContainerStateEnum)))
                    {
                        info.State = state;
                        Check.Equal(state == ContainerStateEnum.Running, info.IsRunning, "IsRunning for " + state);
                    }
                }),
                c.Sync("ImageInfoDangling", "ImageInfo.IsDangling for placeholder, empty, and tagged repositories", () =>
                {
                    ImageInfo image = new ImageInfo();
                    Check.True(image.IsDangling, "default (placeholder) is dangling");
                    Check.Equal(UpdateStatusEnum.Unknown, image.Status, "default status");
                    Check.Null(image.RemoteDigest, "default remote digest");
                    image.Repository = string.Empty;
                    Check.True(image.IsDangling, "empty repository is dangling");
                    image.Repository = "nginx";
                    Check.False(image.IsDangling, "tagged image is not dangling");
                }),
                c.Sync("PortMapToString", "PortMap formats published and unpublished ports", () =>
                {
                    PortMap map = new PortMap();
                    Check.Equal("tcp", map.Protocol, "default protocol");
                    map.ContainerPort = 80;
                    Check.Equal("80/tcp", map.ToString(), "unpublished");
                    map.HostPort = 8080;
                    Check.Equal("8080->80/tcp", map.ToString(), "published");
                    map.Protocol = "udp";
                    map.HostPort = 0;
                    map.ContainerPort = 53;
                    Check.Equal("53/udp", map.ToString(), "udp unpublished");
                }),
                c.Sync("ComposeStackCounts", "ComposeStack counts running and total services", () =>
                {
                    ComposeStack empty = new ComposeStack();
                    Check.Equal(0, empty.ServiceCount, "empty service count");
                    Check.Equal(0, empty.RunningCount, "empty running count");

                    ComposeStack stack = new ComposeStack();
                    stack.Services = new List<ComposeServiceInfo>
                    {
                        new ComposeServiceInfo { Name = "web", State = ContainerStateEnum.Running },
                        new ComposeServiceInfo { Name = "db", State = ContainerStateEnum.Exited },
                        new ComposeServiceInfo { Name = "cache", State = ContainerStateEnum.Running },
                        new ComposeServiceInfo { Name = "worker", State = ContainerStateEnum.Paused }
                    };
                    Check.Equal(4, stack.ServiceCount, "service count");
                    Check.Equal(2, stack.RunningCount, "running count");
                }),
                c.Sync("StatsMemoryPercent", "ContainerStatsSample.MemoryPercent handles a zero limit", () =>
                {
                    ContainerStatsSample sample = new ContainerStatsSample();
                    sample.MemoryUsage = 256;
                    Check.Close(0.0, sample.MemoryPercent, 0.0001, "zero limit");
                    sample.MemoryLimit = 1024;
                    Check.Close(25.0, sample.MemoryPercent, 0.0001, "25 percent");
                }),
                c.Sync("ExecResultSucceeded", "ExecResult defaults to -1 and succeeds only on exit code 0", () =>
                {
                    ExecResult result = new ExecResult();
                    Check.Equal(-1, result.ExitCode, "default exit code");
                    Check.False(result.Succeeded, "default not succeeded");
                    result.ExitCode = 0;
                    Check.True(result.Succeeded, "exit 0 succeeded");
                    result.ExitCode = 3;
                    Check.False(result.Succeeded, "exit 3 not succeeded");
                }),
                c.Sync("DetailDefaults", "ContainerDetail and SystemUsage collections and strings default to empty, never null", () =>
                {
                    ContainerDetail detail = new ContainerDetail();
                    Check.Equal(0, detail.Ports.Count, "ports");
                    Check.Equal(0, detail.Environment.Count, "environment");
                    Check.Equal(0, detail.Mounts.Count, "mounts");
                    Check.Equal(0, detail.Networks.Count, "networks");
                    Check.Equal(0, detail.Labels.Count, "labels");
                    SystemUsage usage = new SystemUsage();
                    Check.Equal(string.Empty, usage.EngineVersion, "engine version");
                    Check.Equal(string.Empty, usage.OperatingSystem, "operating system");
                }),
                c.Sync("Exceptions", "Domain exceptions preserve message and inner exception", () =>
                {
                    InvalidOperationException inner = new InvalidOperationException("inner");
                    RegistryException registry = new RegistryException("registry failed", inner);
                    Check.Equal("registry failed", registry.Message, "registry message");
                    Check.True(ReferenceEquals(inner, registry.InnerException), "registry inner");
                    Check.Equal("plain", new RegistryException("plain").Message, "registry message only");

                    DockerConnectionException connection = new DockerConnectionException("no daemon", inner);
                    Check.Equal("no daemon", connection.Message, "connection message");
                    Check.True(ReferenceEquals(inner, connection.InnerException), "connection inner");
                    Check.Equal("plain", new DockerConnectionException("plain").Message, "connection message only");
                }),
                c.Sync("ComposeStackBuilderSorts", "ComposeStackBuilder sorts services case-insensitively and carries the config file", () =>
                {
                    ComposeStackBuilder builder = new ComposeStackBuilder("shop");
                    builder.ConfigFile = "/srv/shop/compose.yaml";
                    builder.Services.Add(new ComposeServiceInfo { Name = "web" });
                    builder.Services.Add(new ComposeServiceInfo { Name = "API" });
                    builder.Services.Add(new ComposeServiceInfo { Name = "db" });

                    ComposeStack stack = builder.Build();
                    Check.Equal("shop", stack.Project, "project");
                    Check.Equal("/srv/shop/compose.yaml", stack.ConfigFilePath, "config file");
                    List<string> names = new List<string>();
                    foreach (ComposeServiceInfo service in stack.Services)
                        names.Add(service.Name);
                    Check.SequenceEqual(new List<string> { "API", "db", "web" }, names, "service order");
                })
            });
        }
    }
}
