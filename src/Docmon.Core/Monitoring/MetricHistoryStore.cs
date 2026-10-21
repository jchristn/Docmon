namespace Docmon.Core.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Thread-safe thirty-day metric journal, stored as atomic daily JSON snapshots of minute aggregates.
    /// One collector owns a directory. Flush on a background cadence and orderly shutdown.
    /// </summary>
    public sealed class MetricHistoryStore : IMetricHistoryProvider
    {
        private readonly object _Lock = new object();
        private readonly object _FlushLock = new object();
        private readonly string _Directory;
        private readonly Dictionary<string, Dictionary<string, MetricMinute>> _Days = new Dictionary<string, Dictionary<string, MetricMinute>>(StringComparer.Ordinal);
        private readonly HashSet<string> _Dirty = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Loads retained minute aggregates. The caller must use a dedicated collector directory.</summary>
        /// <param name="directory">Metric journal directory.</param>
        /// <exception cref="ArgumentException">The directory is blank.</exception>
        /// <exception cref="IOException">The metric journal cannot be read.</exception>
        public MetricHistoryStore(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            _Directory = directory;
            Directory.CreateDirectory(directory);
            string cutoff = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (string path in Directory.EnumerateFiles(directory, "????-??-??.json"))
            {
                string day = Path.GetFileNameWithoutExtension(path);
                if (string.CompareOrdinal(day, cutoff) < 0) { File.Delete(path); continue; }
                List<MetricMinute> rows = JsonSerializer.Deserialize<List<MetricMinute>>(File.ReadAllText(path)) ?? new List<MetricMinute>();
                _Days[day] = rows.ToDictionary(Key, StringComparer.Ordinal);
            }
        }

        /// <summary>Adds an observation to its container's minute aggregate. Does no disk I/O.</summary>
        /// <param name="sample">Docker observation.</param>
        /// <exception cref="ArgumentNullException">The observation is null.</exception>
        public void Add(ContainerStatsSample sample)
        {
            ArgumentNullException.ThrowIfNull(sample);
            lock (_Lock)
            {
                string day = sample.AtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!_Days.TryGetValue(day, out Dictionary<string, MetricMinute>? rows))
                {
                    rows = new Dictionary<string, MetricMinute>(StringComparer.Ordinal);
                    _Days[day] = rows;
                }
                DateTime minute = new DateTime(sample.AtUtc.Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
                string key = sample.ContainerId + ":" + minute.Ticks.ToString(CultureInfo.InvariantCulture);
                if (!rows.TryGetValue(key, out MetricMinute? row))
                {
                    row = new MetricMinute { ContainerId = sample.ContainerId, AtUtc = minute };
                    rows[key] = row;
                }
                row.Count++;
                row.CpuSum += sample.CpuPercent;
                row.MemorySum += sample.MemoryUsage;
                if (sample.AtUtc >= row.LastSampleUtc)
                {
                    row.LastSampleUtc = sample.AtUtc;
                    row.NetworkRxBytes = sample.NetworkRxBytes;
                    row.NetworkTxBytes = sample.NetworkTxBytes;
                    row.BlockReadBytes = sample.BlockReadBytes;
                    row.BlockWriteBytes = sample.BlockWriteBytes;
                }
                _Dirty.Add(day);
            }
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<MetricHistoryBucket>> GetMetricHistoryAsync(string timeframe, string containerId, CancellationToken token)
        {
            return Task.FromResult(Query(timeframe, containerId, DateTime.UtcNow, token));
        }

        /// <summary>Reads a deterministic historical window, including gaps, at a supplied UTC time.</summary>
        /// <param name="timeframe">hour, day, week, or month.</param>
        /// <param name="containerId">Container ID, or empty for all Docker workloads.</param>
        /// <param name="now">Reference UTC time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Chronological buckets. Host CPU and memory sum containers per observed minute before averaging.</returns>
        /// <exception cref="ArgumentException">The timeframe is unsupported.</exception>
        /// <exception cref="ArgumentNullException">The container ID is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public IReadOnlyList<MetricHistoryBucket> Query(string timeframe, string containerId, DateTime now, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(containerId);
            token.ThrowIfCancellationRequested();
            MetricWindow window = new MetricWindow(timeframe, now);
            List<MetricHistoryBucket> buckets = new List<MetricHistoryBucket>(window.Count);
            for (int i = 0; i < window.Count; i++)
                buckets.Add(new MetricHistoryBucket { ContainerId = containerId, AtUtc = window.StartUtc.AddMinutes(i * window.Minutes), EndUtc = window.StartUtc.AddMinutes((i + 1) * window.Minutes) });
            lock (_Lock)
            {
                IEnumerable<MetricMinute> rows = _Days.Values.SelectMany(day => day.Values).Where(row => row.AtUtc >= window.StartUtc && row.AtUtc < window.EndUtc && (containerId.Length == 0 || row.ContainerId == containerId));
                foreach (IGrouping<int, MetricMinute> group in rows.GroupBy(row => (int)((row.AtUtc - window.StartUtc).TotalMinutes / window.Minutes)))
                {
                    token.ThrowIfCancellationRequested();
                    MetricHistoryBucket bucket = buckets[group.Key];
                    List<IGrouping<DateTime, MetricMinute>> minutes = group.Where(row => row.Count > 0).GroupBy(row => row.AtUtc).ToList();
                    if (minutes.Count == 0) continue;
                    bucket.CpuPercent = minutes.Sum(minute => minute.Sum(row => row.CpuSum / row.Count)) / minutes.Count;
                    bucket.MemoryUsage = (long)(minutes.Sum(minute => minute.Sum(row => row.MemorySum / row.Count)) / minutes.Count);
                    foreach (IGrouping<string, MetricMinute> container in group.GroupBy(row => row.ContainerId))
                    {
                        MetricMinute last = container.MaxBy(row => row.LastSampleUtc)!;
                        bucket.NetworkRxBytes += last.NetworkRxBytes;
                        bucket.NetworkTxBytes += last.NetworkTxBytes;
                        bucket.BlockReadBytes += last.BlockReadBytes;
                        bucket.BlockWriteBytes += last.BlockWriteBytes;
                    }
                    bucket.HasData = true;
                    bucket.SampleCount = group.Sum(row => row.Count);
                }
            }
            return buckets;
        }

        /// <summary>Atomically saves dirty day files and prunes expired observations. Safe to call concurrently.</summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion of the background disk write.</returns>
        /// <exception cref="IOException">Pending observations could not be persisted. They remain dirty for retry.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested before writing.</exception>
        public Task FlushAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                lock (_FlushLock)
                {
                    token.ThrowIfCancellationRequested();
                    Dictionary<string, string> pending = new Dictionary<string, string>(StringComparer.Ordinal);
                    string cutoff = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    lock (_Lock)
                    {
                        foreach (string day in _Days.Keys.Where(day => string.CompareOrdinal(day, cutoff) < 0).ToArray())
                        {
                            File.Delete(Path.Combine(_Directory, day + ".json"));
                            _Days.Remove(day); _Dirty.Remove(day);
                        }
                        foreach (string day in _Dirty) pending[day] = JsonSerializer.Serialize(_Days[day].Values);
                        _Dirty.Clear();
                    }
                    foreach (KeyValuePair<string, string> file in pending)
                    {
                        try
                        {
                            string target = Path.Combine(_Directory, file.Key + ".json");
                            File.WriteAllText(target + ".tmp", file.Value);
                            File.Move(target + ".tmp", target, true);
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                        {
                            lock (_Lock) foreach (string day in pending.Keys) _Dirty.Add(day);
                            throw;
                        }
                    }
                }
            }, token);
        }

        private static string Key(MetricMinute row) => row.ContainerId + ":" + row.AtUtc.Ticks.ToString(CultureInfo.InvariantCulture);
    }
}
