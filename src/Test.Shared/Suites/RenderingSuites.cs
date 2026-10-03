namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using Docmon.App;
    using Docmon.App.Monitoring;
    using Docmon.App.Screens;
    using Docmon.App.Widgets;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using TUIKit.Input;
    using TUIKit.Testing;

    /// <summary>
    /// Suites that render Docmon widgets and screens off-screen through TUIKit's testing surface, guarding
    /// against regressions in layout, text output, and key handling when the TUIKit dependency changes.
    /// </summary>
    public static class RenderingSuites
    {
        /// <summary>
        /// Header, tab bar, status bar, containers screen, and metrics screen rendering.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor WidgetRenderingSuite()
        {
            Cases c = new Cases("Rendering");
            return new TestSuiteDescriptor("Rendering", "TUI widget rendering", new List<TestCaseDescriptor>
            {
                c.Sync("Wordmark", "The FIGlet wordmark renders as non-empty rows", () =>
                {
                    string[] logo = DocmonBanner.WordmarkLines();
                    Check.True(logo.Length >= 3, "wordmark has at least three rows");
                    foreach (string row in logo)
                        Check.NotNull(row, "wordmark row");
                }),
                c.Sync("HeaderBanner", "The header shows the tagline, project URL, and host summary", () =>
                {
                    string[] logo = DocmonBanner.WordmarkLines();
                    HeaderBanner header = new HeaderBanner(logo);
                    header.HostSummary = "unix:///var/run/docker.sock   2/3 running";
                    string text = Snapshot.RenderWidget(header, 100, Math.Max(3, logo.Length) + 1);
                    Check.True(text.Contains(DocmonBanner.Tagline), "tagline rendered");
                    Check.True(text.Contains(DocmonBanner.ProjectUrl), "project URL rendered");
                    Check.True(text.Contains("2/3 running"), "host summary rendered");
                }),
                c.Sync("TabBar", "The tab bar numbers every tab in order", () =>
                {
                    TabBar tabs = new TabBar(new[] { "Containers", "Stacks", "Metrics", "Images", "Events", "Tools" });
                    tabs.ActiveIndex = 2;
                    string text = Snapshot.RenderWidget(tabs, 100, 1);
                    Check.True(text.Contains("1 Containers"), "first tab");
                    Check.True(text.Contains("6 Tools"), "last tab");
                    Check.True(text.IndexOf("Stacks", StringComparison.Ordinal) < text.IndexOf("Metrics", StringComparison.Ordinal), "tab order");
                }),
                c.Sync("StatusBar", "The status bar shows hints and a message, and tolerates null assignment", () =>
                {
                    StatusBar status = new StatusBar();
                    status.Hints = "q quit";
                    status.Message = "Pulled nginx:latest";
                    string text = Snapshot.RenderWidget(status, 80, 1);
                    Check.True(text.Contains("q quit"), "hints rendered");
                    Check.True(text.Contains("Pulled nginx:latest"), "message rendered");
                    status.Message = null!;
                    Check.Equal(string.Empty, status.Message, "null message coerced to empty");
                    Check.False(Snapshot.RenderWidget(status, 80, 1).Contains("Pulled"), "message cleared");
                }),
                c.Sync("ContainersScreen", "The containers screen lists containers with a detail pane for the selection", () =>
                {
                    StatsHistory history = new StatsHistory(120);
                    List<ContainerInfo> containers = SampleContainers();
                    Populate(history, containers);
                    ContainersScreen screen = new ContainersScreen();
                    screen.SetContainers(containers, history);
                    screen.OnFocusChanged(true);
                    string text = Snapshot.RenderWidget(screen, 100, 22);
                    Check.True(text.Contains("Containers (2)"), "title with count");
                    Check.True(text.Contains("web-api"), "first container");
                    Check.True(text.Contains("redis"), "second container");
                    Check.True(text.Contains("Detail"), "detail pane");
                    Check.True(text.Contains("8080->80/tcp"), "selected container ports");
                }),
                c.Sync("ContainersScreenNavigation", "Down moves the selection to the next container", () =>
                {
                    StatsHistory history = new StatsHistory(120);
                    List<ContainerInfo> containers = SampleContainers();
                    ContainersScreen screen = new ContainersScreen();
                    screen.SetContainers(containers, history);
                    screen.OnFocusChanged(true);
                    WidgetTester tester = WidgetTester.For(screen, 100, 22);
                    tester.Render();
                    object? first = screen.SelectedTag;
                    tester.Press(KeyCode.Down);
                    Check.True(tester.LastKeyHandled, "Down handled");
                    Check.False(Equals(first, screen.SelectedTag), "selection moved");
                    tester.Render();
                    tester.AssertContains("Exited (0) 2 hours ago");
                }),
                c.Sync("ContainersScreenEmpty", "An empty containers screen renders without throwing", () =>
                {
                    ContainersScreen screen = new ContainersScreen();
                    screen.SetContainers(new List<ContainerInfo>(), new StatsHistory(10));
                    string text = Snapshot.RenderWidget(screen, 80, 12);
                    Check.True(text.Contains("Containers (0)"), "zero count");
                    Check.Null(screen.SelectedTag, "no selection");
                }),
                c.Sync("MetricsScreen", "The metrics screen renders all four charts", () =>
                {
                    StatsHistory history = new StatsHistory(120);
                    List<ContainerInfo> containers = SampleContainers();
                    Populate(history, containers);
                    MetricsScreen screen = new MetricsScreen();
                    screen.SetData(history, containers[0].Id, containers[0].Name);
                    string text = Snapshot.RenderWidget(screen, 100, 22);
                    Check.True(text.Contains("CPU %"), "CPU chart");
                    Check.True(text.Contains("Memory"), "memory chart");
                    Check.True(text.Contains("Network"), "network chart");
                    Check.True(text.Contains("Block I/O"), "block I/O chart");
                }),
                c.Sync("TinySurface", "Widgets render into a very small surface without throwing", () =>
                {
                    TabBar tabs = new TabBar(new[] { "Containers", "Stacks" });
                    Snapshot.RenderWidget(tabs, 3, 1);
                    StatusBar status = new StatusBar();
                    status.Hints = "a long set of hints that cannot fit";
                    status.Message = "and a message";
                    Snapshot.RenderWidget(status, 5, 1);
                    MetricsScreen metrics = new MetricsScreen();
                    metrics.SetData(new StatsHistory(10), "none", "none");
                    Snapshot.RenderWidget(metrics, 10, 4);
                })
            });
        }

        private static List<ContainerInfo> SampleContainers()
        {
            ContainerInfo web = new ContainerInfo();
            web.Id = "3f9a1c7b2e4d0000000000000000000000000000000000000000000000000000";
            web.Name = "web-api";
            web.Image = "ghcr.io/acme/api:1.4.2";
            web.State = ContainerStateEnum.Running;
            web.Status = "Up 3 days (healthy)";
            web.Health = "healthy";
            web.CreatedUtc = DateTime.UtcNow.AddDays(-3);
            web.Ports = new List<PortMap> { new PortMap { HostPort = 8080, ContainerPort = 80, Protocol = "tcp" } };

            ContainerInfo redis = new ContainerInfo();
            redis.Id = "dd44ee55ff66000000000000000000000000000000000000000000000000000";
            redis.Name = "redis";
            redis.Image = "redis:7.2";
            redis.State = ContainerStateEnum.Exited;
            redis.Status = "Exited (0) 2 hours ago";
            redis.CreatedUtc = DateTime.UtcNow.AddDays(-3);

            return new List<ContainerInfo> { web, redis };
        }

        private static void Populate(StatsHistory history, List<ContainerInfo> containers)
        {
            DateTime start = DateTime.UtcNow.AddSeconds(-30);
            for (int i = 0; i < 30; i++)
            {
                ContainerStatsSample sample = new ContainerStatsSample();
                sample.ContainerId = containers[0].Id;
                sample.CpuPercent = 10 + i;
                sample.MemoryUsage = (256L + i) * 1024L * 1024L;
                sample.MemoryLimit = 512L * 1024L * 1024L;
                sample.NetworkRxBytes = 100_000L * i;
                sample.NetworkTxBytes = 50_000L * i;
                sample.BlockReadBytes = 20_000L * i;
                sample.BlockWriteBytes = 10_000L * i;
                sample.AtUtc = start.AddSeconds(i);
                history.AddSample(sample);
                history.TickOverall();
            }
        }
    }
}
