namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Monitoring;
    using Docmon.App.Rendering;
    using Docmon.Core.Models;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using TUIKit.Input;

    /// <summary>
    /// Suites for the Docmon TUI's non-visual state: metric ring buffers, per-container rate history,
    /// overall roll-ups, and table selection.
    /// </summary>
    public static class MonitoringSuites
    {
        /// <summary>
        /// Metric series, container history, and stats history.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor MetricsSuite()
        {
            Cases c = new Cases("Metrics");
            return new TestSuiteDescriptor("Metrics", "Metric history", new List<TestCaseDescriptor>
            {
                c.Sync("SeriesEmpty", "An empty series reports zero latest and max and an empty snapshot", () =>
                {
                    MetricSeries series = new MetricSeries(5);
                    Check.Close(0.0, series.Latest, 0.0, "latest");
                    Check.Close(0.0, series.Max, 0.0, "max");
                    Check.Equal(0, series.Snapshot().Count, "snapshot");
                }),
                c.Sync("SeriesOrder", "Snapshot is oldest first and Latest is the newest value", () =>
                {
                    MetricSeries series = new MetricSeries(5);
                    series.Add(1);
                    series.Add(7);
                    series.Add(3);
                    Check.SequenceEqual(new List<double> { 1, 7, 3 }, series.Snapshot(), "snapshot");
                    Check.Close(3.0, series.Latest, 0.0, "latest");
                    Check.Close(7.0, series.Max, 0.0, "max");
                }),
                c.Sync("SeriesWraps", "The ring buffer evicts the oldest values at capacity", () =>
                {
                    MetricSeries series = new MetricSeries(3);
                    for (int i = 1; i <= 7; i++)
                        series.Add(i);
                    Check.SequenceEqual(new List<double> { 5, 6, 7 }, series.Snapshot(), "snapshot after wrap");
                    Check.Close(7.0, series.Max, 0.0, "max after wrap");
                    Check.Close(7.0, series.Latest, 0.0, "latest after wrap");
                }),
                c.Sync("SeriesMaxIgnoresEvicted", "Max only considers values still in the window", () =>
                {
                    MetricSeries series = new MetricSeries(2);
                    series.Add(100);
                    series.Add(1);
                    series.Add(2);
                    Check.Close(2.0, series.Max, 0.0, "max");
                }),
                c.Sync("SeriesNegativeMax", "Max floors at zero for all-negative input", () =>
                {
                    MetricSeries series = new MetricSeries(3);
                    series.Add(-5);
                    series.Add(-1);
                    Check.Close(0.0, series.Max, 0.0, "max");
                    Check.Close(-1.0, series.Latest, 0.0, "latest");
                }),
                c.Sync("SeriesMinimumCapacity", "Capacities below one are raised to one", () =>
                {
                    MetricSeries series = new MetricSeries(0);
                    series.Add(1);
                    series.Add(2);
                    Check.SequenceEqual(new List<double> { 2 }, series.Snapshot(), "capacity 0");
                    MetricSeries negative = new MetricSeries(-10);
                    negative.Add(3);
                    Check.Close(3.0, negative.Latest, 0.0, "capacity -10");
                }),
                c.Sync("HistoryFirstSampleRatesZero", "The first sample records CPU and memory with zero rates", () =>
                {
                    ContainerHistory history = new ContainerHistory(10);
                    history.Add(Sample("c", 0, cpu: 12.5, memory: 512, limit: 2048, rx: 1000, tx: 500, read: 10, write: 20));
                    Check.Close(12.5, history.Cpu.Latest, 0.0001, "cpu");
                    Check.Close(512.0, history.MemoryUsage.Latest, 0.0001, "memory usage");
                    Check.Close(25.0, history.MemoryPercent.Latest, 0.0001, "memory percent");
                    Check.Equal(2048L, history.MemoryLimit, "memory limit");
                    Check.Close(0.0, history.NetRxRate.Latest, 0.0, "rx rate");
                    Check.Close(0.0, history.DiskRate.Latest, 0.0, "disk rate");
                }),
                c.Sync("HistoryRates", "Subsequent samples compute per-second network and disk rates", () =>
                {
                    ContainerHistory history = new ContainerHistory(10);
                    history.Add(Sample("c", 0, rx: 1000, tx: 500, read: 0, write: 0));
                    history.Add(Sample("c", 2, rx: 3000, tx: 1500, read: 400, write: 600));
                    Check.Close(1000.0, history.NetRxRate.Latest, 0.0001, "rx rate");
                    Check.Close(500.0, history.NetTxRate.Latest, 0.0001, "tx rate");
                    Check.Close(500.0, history.DiskRate.Latest, 0.0001, "disk rate (read + write)");
                }),
                c.Sync("HistoryCounterReset", "Counters that go backwards (container restart) yield a zero rate", () =>
                {
                    ContainerHistory history = new ContainerHistory(10);
                    history.Add(Sample("c", 0, rx: 5000, tx: 5000, read: 5000, write: 5000));
                    history.Add(Sample("c", 1, rx: 10, tx: 10, read: 10, write: 10));
                    Check.Close(0.0, history.NetRxRate.Latest, 0.0, "rx rate");
                    Check.Close(0.0, history.NetTxRate.Latest, 0.0, "tx rate");
                    Check.Close(0.0, history.DiskRate.Latest, 0.0, "disk rate");
                }),
                c.Sync("HistorySameTimestamp", "A sample with no elapsed time records zero rates instead of dividing by zero", () =>
                {
                    ContainerHistory history = new ContainerHistory(10);
                    history.Add(Sample("c", 5, rx: 0));
                    history.Add(Sample("c", 5, rx: 1000));
                    Check.Close(0.0, history.NetRxRate.Latest, 0.0, "rx rate");
                    Check.False(double.IsInfinity(history.NetRxRate.Max) || double.IsNaN(history.NetRxRate.Max), "no infinity or NaN");
                }),
                c.Sync("HistoryNullSample", "Adding a null sample throws ArgumentNullException", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new ContainerHistory(5).Add(null!), "ContainerHistory.Add(null)");
                    Check.Throws<ArgumentNullException>(() => new StatsHistory(5).AddSample(null!), "StatsHistory.AddSample(null)");
                }),
                c.Sync("StatsHistoryPerContainer", "Samples are tracked per container", () =>
                {
                    StatsHistory stats = new StatsHistory(10);
                    stats.AddSample(Sample("a", 0, cpu: 10));
                    stats.AddSample(Sample("b", 0, cpu: 30));
                    stats.AddSample(Sample("a", 1, cpu: 20));
                    Check.Equal(2, stats.Get("a")!.Cpu.Snapshot().Count, "a sample count");
                    Check.Close(30.0, stats.Get("b")!.Cpu.Latest, 0.0, "b cpu");
                    Check.Null(stats.Get("missing"), "unknown container");
                    Check.Throws<ArgumentNullException>(() => stats.Get(null!), "Get(null)");
                }),
                c.Sync("StatsHistoryOverall", "TickOverall sums the latest values across containers", () =>
                {
                    StatsHistory stats = new StatsHistory(10);
                    stats.TickOverall();
                    Check.Close(0.0, stats.OverallCpu.Latest, 0.0, "empty tick cpu");

                    stats.AddSample(Sample("a", 0, cpu: 10, memory: 100, rx: 0, tx: 0, read: 0, write: 0));
                    stats.AddSample(Sample("a", 1, cpu: 15, memory: 150, rx: 100, tx: 50, read: 10, write: 0));
                    stats.AddSample(Sample("b", 0, cpu: 25, memory: 200));
                    stats.TickOverall();
                    Check.Close(40.0, stats.OverallCpu.Latest, 0.0001, "overall cpu");
                    Check.Close(350.0, stats.OverallMemory.Latest, 0.0001, "overall memory");
                    Check.Close(150.0, stats.OverallNetRate.Latest, 0.0001, "overall net rate");
                    Check.Close(10.0, stats.OverallDiskRate.Latest, 0.0001, "overall disk rate");
                    Check.Equal(2, stats.OverallCpu.Snapshot().Count, "two ticks recorded");
                }),
                c.Sync("StatsHistoryPrune", "Prune drops containers that are no longer live", () =>
                {
                    StatsHistory stats = new StatsHistory(10);
                    stats.AddSample(Sample("keep", 0, cpu: 5));
                    stats.AddSample(Sample("gone", 0, cpu: 50));
                    stats.Prune(new HashSet<string> { "keep" });
                    Check.NotNull(stats.Get("keep"), "kept container");
                    Check.Null(stats.Get("gone"), "pruned container");
                    stats.TickOverall();
                    Check.Close(5.0, stats.OverallCpu.Latest, 0.0001, "pruned container excluded from roll-up");
                    Check.Throws<ArgumentNullException>(() => stats.Prune(null!), "Prune(null)");
                })
            });
        }

        /// <summary>
        /// Table selection and keyboard navigation.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor TableStateSuite()
        {
            Cases c = new Cases("TableState");
            return new TestSuiteDescriptor("TableState", "Table selection", new List<TestCaseDescriptor>
            {
                c.Sync("Empty", "An empty table has no selection and still consumes navigation keys", () =>
                {
                    TableState table = new TableState();
                    Check.Equal(0, table.Count, "count");
                    Check.Equal(-1, table.SelectedIndex, "selected index");
                    Check.Null(table.SelectedTag, "selected tag");
                    Check.True(table.HandleKey(Key(KeyCode.Down)), "Down consumed");
                    Check.Equal(-1, table.SelectedIndex, "still nothing selected");
                }),
                c.Sync("Navigation", "Arrow, Home, and End keys move and clamp the selection", () =>
                {
                    TableState table = Table(5);
                    Check.Equal(0, table.SelectedIndex, "initial selection");
                    Check.Equal("row0", table.SelectedTag, "initial tag");
                    table.HandleKey(Key(KeyCode.Up));
                    Check.Equal(0, table.SelectedIndex, "Up clamps at top");
                    table.HandleKey(Key(KeyCode.Down));
                    table.HandleKey(Key(KeyCode.Down));
                    Check.Equal(2, table.SelectedIndex, "Down twice");
                    table.HandleKey(Key(KeyCode.End));
                    Check.Equal(4, table.SelectedIndex, "End");
                    table.HandleKey(Key(KeyCode.Down));
                    Check.Equal(4, table.SelectedIndex, "Down clamps at bottom");
                    table.HandleKey(Key(KeyCode.Home));
                    Check.Equal(0, table.SelectedIndex, "Home");
                }),
                c.Sync("Paging", "PageUp and PageDown move by a page and clamp", () =>
                {
                    TableState table = Table(25);
                    table.HandleKey(Key(KeyCode.PageDown));
                    Check.Equal(10, table.SelectedIndex, "PageDown");
                    table.HandleKey(Key(KeyCode.PageDown));
                    table.HandleKey(Key(KeyCode.PageDown));
                    Check.Equal(24, table.SelectedIndex, "PageDown clamps");
                    table.HandleKey(Key(KeyCode.PageUp));
                    Check.Equal(14, table.SelectedIndex, "PageUp");
                }),
                c.Sync("UnhandledKey", "Non-navigation keys are not consumed", () =>
                {
                    TableState table = Table(3);
                    Check.False(table.HandleKey(KeyEvent.Char('x', KeyModifiers.None)), "character key");
                    Check.False(table.HandleKey(Key(KeyCode.Enter)), "Enter");
                    Check.Equal(0, table.SelectedIndex, "selection unchanged");
                }),
                c.Sync("SetRowsPreservesSelection", "Replacing rows keeps the index when still valid and clamps when the table shrinks", () =>
                {
                    TableState table = Table(5);
                    table.HandleKey(Key(KeyCode.End));
                    table.SetRows(Rows(10));
                    Check.Equal(4, table.SelectedIndex, "index preserved when growing");
                    table.SetRows(Rows(2));
                    Check.Equal(1, table.SelectedIndex, "index clamped when shrinking");
                    table.SetRows(Rows(0));
                    Check.Equal(-1, table.SelectedIndex, "empty after clearing");
                    Check.Null(table.SelectedTag, "no tag after clearing");
                    Check.Throws<ArgumentNullException>(() => table.SetRows(null!), "SetRows(null)");
                }),
                c.Sync("RowItemGuards", "RowItem requires cells and allows a null tag and color", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new RowItem(null!, "tag"), "new RowItem(null)");
                    RowItem row = new RowItem(new[] { "a" }, null);
                    Check.Null(row.Tag, "tag");
                    Check.Null(row.Color, "color");
                })
            });
        }

        private static ContainerStatsSample Sample(string id, int seconds, double cpu = 0, long memory = 0, long limit = 0, long rx = 0, long tx = 0, long read = 0, long write = 0)
        {
            ContainerStatsSample sample = new ContainerStatsSample();
            sample.ContainerId = id;
            sample.AtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
            sample.CpuPercent = cpu;
            sample.MemoryUsage = memory;
            sample.MemoryLimit = limit;
            sample.NetworkRxBytes = rx;
            sample.NetworkTxBytes = tx;
            sample.BlockReadBytes = read;
            sample.BlockWriteBytes = write;
            return sample;
        }

        private static KeyEvent Key(KeyCode code)
        {
            return KeyEvent.Special(code, KeyModifiers.None);
        }

        private static List<RowItem> Rows(int count)
        {
            List<RowItem> rows = new List<RowItem>();
            for (int i = 0; i < count; i++)
                rows.Add(new RowItem(new[] { "cell" + i }, "row" + i));
            return rows;
        }

        private static TableState Table(int rows)
        {
            TableState table = new TableState();
            table.SetRows(Rows(rows));
            return table;
        }
    }
}
