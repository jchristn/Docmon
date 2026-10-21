namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Docmon.Core.Models;
    using Docmon.Core.Monitoring;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>Persistent metric history, bucket definitions, gaps, isolation, and retention.</summary>
    public static class MetricHistorySuites
    {
        /// <summary>Gets deterministic historical metrics checks without a Docker daemon.</summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            Cases c = new Cases("MetricHistory");
            return new TestSuiteDescriptor("MetricHistory", "Durable metric time windows", new[]
            {
                c.Async("PersistenceAndAggregation", "Reload retains observed values, scope isolation, means, counters, and gaps", async token =>
                {
                    string directory = Path.Combine(Path.GetTempPath(), "docmon-metric-test-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        DateTime now = DateTime.UtcNow;
                        DateTime at = new DateTime(now.Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc).AddMinutes(-2);
                        MetricHistoryStore store = new MetricHistoryStore(directory);
                        store.Add(new ContainerStatsSample { ContainerId = "a", AtUtc = at, CpuPercent = 10, MemoryUsage = 100, NetworkRxBytes = 100, NetworkTxBytes = 200, BlockReadBytes = 300, BlockWriteBytes = 400 });
                        store.Add(new ContainerStatsSample { ContainerId = "a", AtUtc = at.AddSeconds(20), CpuPercent = 30, MemoryUsage = 300, NetworkRxBytes = 150, NetworkTxBytes = 250, BlockReadBytes = 350, BlockWriteBytes = 450 });
                        store.Add(new ContainerStatsSample { ContainerId = "b", AtUtc = at, CpuPercent = 5, MemoryUsage = 50, NetworkRxBytes = 10, NetworkTxBytes = 20, BlockReadBytes = 30, BlockWriteBytes = 40 });
                        IReadOnlyList<MetricHistoryBucket> host = store.Query("hour", "", now, token);
                        MetricHistoryBucket point = host.Single(row => row.HasData);
                        Check.Close(25, point.CpuPercent, 0.001, "host sums minute means");
                        Check.Equal(250L, point.MemoryUsage, "host memory mean");
                        Check.Equal(160L, point.NetworkRxBytes, "last rx counters summed");
                        Check.Equal(270L, point.NetworkTxBytes, "last tx counters summed");
                        Check.Equal(380L, point.BlockReadBytes, "last read counters summed");
                        Check.Equal(490L, point.BlockWriteBytes, "last write counters summed");
                        Check.Equal(3L, point.SampleCount, "observation count");
                        Check.Equal(59, host.Count(row => !row.HasData), "empty periods are gaps");
                        Check.Close(20, store.Query("hour", "a", now, token).Single(row => row.HasData).CpuPercent, 0.001, "container isolation");
                        await store.FlushAsync(token).ConfigureAwait(false);
                        MetricHistoryStore reloaded = new MetricHistoryStore(directory);
                        Check.Equal(160L, reloaded.Query("month", "", now, token).Single(row => row.HasData).NetworkRxBytes, "retained through restart");
                        // Different containers observed in disjoint minutes must not double host CPU.
                        reloaded.Add(new ContainerStatsSample { ContainerId = "c", AtUtc = at.AddMinutes(-1), CpuPercent = 75 });
                        Check.Close(50, reloaded.Query("month", "", now, token).Single(row => row.HasData).CpuPercent, 0.001, "mean of host minute totals");
                        reloaded.Add(new ContainerStatsSample { ContainerId = "a", AtUtc = at.AddSeconds(10), NetworkRxBytes = 2 });
                        Check.Equal(150L, reloaded.Query("hour", "a", now, token).Single(row => row.HasData).NetworkRxBytes, "out of order sample cannot replace latest counter");
                    }
                    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                }),
                c.Async("WindowsAndRetention", "All requested bucket sizes are stable; old data prunes and invalid windows reject", async token =>
                {
                    string directory = Path.Combine(Path.GetTempPath(), "docmon-metric-test-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        MetricHistoryStore store = new MetricHistoryStore(directory);
                        DateTime now = DateTime.UtcNow;
                        string[] names = { "hour", "day", "week", "month" };
                        int[] counts = { 60, 96, 84, 90 }, widths = { 1, 15, 120, 480 };
                        for (int i = 0; i < names.Length; i++)
                        {
                            IReadOnlyList<MetricHistoryBucket> rows = store.Query(names[i], "", now, token);
                            Check.Equal(counts[i], rows.Count, names[i] + " bucket count");
                            Check.Close(widths[i], (rows[0].EndUtc - rows[0].AtUtc).TotalMinutes, 0, names[i] + " width");
                            for (int j = 1; j < rows.Count; j++) Check.Equal(rows[j - 1].EndUtc, rows[j].AtUtc, "contiguous window");
                            Check.True(rows.All(row => !row.HasData), "no fabricated zero usage");
                        }
                        Check.Throws<ArgumentException>(() => store.Query("year", "", now, token), "invalid timeframe");
                        using CancellationTokenSource cancelled = new CancellationTokenSource(); cancelled.Cancel();
                        Check.Throws<OperationCanceledException>(() => store.Query("hour", "", now, cancelled.Token), "cancellation");
                        store.Add(new ContainerStatsSample { ContainerId = "old", AtUtc = now.AddDays(-31) });
                        await store.FlushAsync(token).ConfigureAwait(false);
                        Check.Equal(0, Directory.GetFiles(directory, "*.json").Length, "expired data removed");
                    }
                    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                })
            });
        }
    }
}
