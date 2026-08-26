namespace Docmon.App
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Monitoring;
    using Docmon.App.Screens;
    using Docmon.App.Widgets;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using TUIKit.Testing;

    /// <summary>
    /// Renders Docmon's header and key screens against sample data (no Docker required) and prints the
    /// text snapshots. Used by the hidden <c>--snapshot</c> flag to preview and self-test rendering.
    /// </summary>
    internal static class SnapshotPreview
    {
        internal static void Run()
        {
            StatsHistory history = new StatsHistory(120);
            List<ContainerInfo> containers = BuildSampleContainers();
            PopulateHistory(history, containers);

            string[] logo = DocmonBanner.WordmarkLines();
            HeaderBanner header = new HeaderBanner(logo);
            header.HostSummary = "npipe://./pipe/docker_engine   2/3 running";

            TabBar tabs = new TabBar(new[] { "Containers", "Stacks", "Metrics", "Images", "Events", "Tools" });
            tabs.ActiveIndex = 0;

            ContainersScreen containersScreen = new ContainersScreen();
            containersScreen.SetContainers(containers, history);
            containersScreen.OnFocusChanged(true);

            MetricsScreen metricsScreen = new MetricsScreen();
            metricsScreen.SetData(history, containers[0].Id, containers[0].Name);

            Console.WriteLine("== Header ==");
            Console.WriteLine(Snapshot.RenderWidget(header, 100, Math.Max(3, logo.Length)));
            Console.WriteLine();
            Console.WriteLine("== Tabs ==");
            Console.WriteLine(Snapshot.RenderWidget(tabs, 100, 1));
            Console.WriteLine();
            Console.WriteLine("== Containers screen ==");
            Console.WriteLine(Snapshot.RenderWidget(containersScreen, 100, 22));
            Console.WriteLine();
            Console.WriteLine("== Metrics screen ==");
            Console.WriteLine(Snapshot.RenderWidget(metricsScreen, 100, 22));
        }

        private static List<ContainerInfo> BuildSampleContainers()
        {
            List<ContainerInfo> containers = new List<ContainerInfo>();

            ContainerInfo web = new ContainerInfo();
            web.Id = "3f9a1c7b2e4d0000000000000000000000000000000000000000000000000000";
            web.Name = "web-api";
            web.Image = "ghcr.io/acme/api:1.4.2";
            web.State = ContainerStateEnum.Running;
            web.Status = "Up 3 days (healthy)";
            web.Health = "healthy";
            web.CreatedUtc = DateTime.UtcNow.AddDays(-3);
            web.Ports = new List<PortMap> { new PortMap { HostPort = 8080, ContainerPort = 80, Protocol = "tcp" } };
            web.ComposeProject = "acme";
            web.ComposeService = "web-api";
            containers.Add(web);

            ContainerInfo db = new ContainerInfo();
            db.Id = "aa11bb22cc33000000000000000000000000000000000000000000000000000";
            db.Name = "postgres";
            db.Image = "postgres:16.2";
            db.State = ContainerStateEnum.Running;
            db.Status = "Up 3 days";
            db.CreatedUtc = DateTime.UtcNow.AddDays(-3);
            db.Ports = new List<PortMap> { new PortMap { HostPort = 5432, ContainerPort = 5432, Protocol = "tcp" } };
            containers.Add(db);

            ContainerInfo redis = new ContainerInfo();
            redis.Id = "dd44ee55ff66000000000000000000000000000000000000000000000000000";
            redis.Name = "redis";
            redis.Image = "redis:7.2";
            redis.State = ContainerStateEnum.Exited;
            redis.Status = "Exited (0) 2 hours ago";
            redis.CreatedUtc = DateTime.UtcNow.AddDays(-3);
            containers.Add(redis);

            return containers;
        }

        private static void PopulateHistory(StatsHistory history, List<ContainerInfo> containers)
        {
            DateTime start = DateTime.UtcNow.AddSeconds(-60);
            for (int i = 0; i < 60; i++)
            {
                DateTime at = start.AddSeconds(i);

                ContainerStatsSample web = new ContainerStatsSample();
                web.ContainerId = containers[0].Id;
                web.CpuPercent = 20 + 15 * Math.Sin(i / 4.0) + (i % 5);
                web.MemoryUsage = (long)((320 + i) * 1024L * 1024L);
                web.MemoryLimit = 512L * 1024L * 1024L;
                web.NetworkRxBytes = 1_000_000L * i;
                web.NetworkTxBytes = 400_000L * i;
                web.BlockReadBytes = 200_000L * i;
                web.BlockWriteBytes = 150_000L * i;
                web.AtUtc = at;
                history.AddSample(web);

                ContainerStatsSample db = new ContainerStatsSample();
                db.ContainerId = containers[1].Id;
                db.CpuPercent = 5 + 3 * Math.Cos(i / 6.0);
                db.MemoryUsage = (long)((1100 + i) * 1024L * 1024L);
                db.MemoryLimit = 2048L * 1024L * 1024L;
                db.NetworkRxBytes = 300_000L * i;
                db.NetworkTxBytes = 120_000L * i;
                db.AtUtc = at;
                history.AddSample(db);

                history.TickOverall();
            }
        }
    }
}
