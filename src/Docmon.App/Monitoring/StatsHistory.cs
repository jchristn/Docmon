namespace Docmon.App.Monitoring
{
    using System;
    using System.Collections.Generic;
    using Docmon.Core.Models;

    /// <summary>
    /// Aggregates rolling metric history across all monitored containers and maintains a deployment-wide
    /// aggregate. Intended to be accessed from the UI thread only: samples are posted to the UI thread by
    /// the controller and read during rendering, so no locking is required.
    /// </summary>
    public sealed class StatsHistory
    {
        private readonly int _Capacity;
        private readonly Dictionary<string, ContainerHistory> _Containers = new Dictionary<string, ContainerHistory>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="StatsHistory"/> class.
        /// </summary>
        /// <param name="capacity">The number of samples each series retains. Values below one are treated as one.</param>
        public StatsHistory(int capacity)
        {
            _Capacity = Math.Max(1, capacity);
            OverallCpu = new MetricSeries(_Capacity);
            OverallMemory = new MetricSeries(_Capacity);
            OverallNetRate = new MetricSeries(_Capacity);
            OverallDiskRate = new MetricSeries(_Capacity);
        }

        /// <summary>Gets the deployment-wide CPU-percentage series (sum across containers).</summary>
        public MetricSeries OverallCpu { get; }

        /// <summary>Gets the deployment-wide memory-usage series, in bytes.</summary>
        public MetricSeries OverallMemory { get; }

        /// <summary>Gets the deployment-wide network-rate series, in bytes per second.</summary>
        public MetricSeries OverallNetRate { get; }

        /// <summary>Gets the deployment-wide block-I/O-rate series, in bytes per second.</summary>
        public MetricSeries OverallDiskRate { get; }

        /// <summary>
        /// Adds a sample for its container, creating history for a newly seen container.
        /// </summary>
        /// <param name="sample">The sample. Must not be null.</param>
        public void AddSample(ContainerStatsSample sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            if (!_Containers.TryGetValue(sample.ContainerId, out ContainerHistory? history))
            {
                history = new ContainerHistory(_Capacity);
                _Containers[sample.ContainerId] = history;
            }

            history.Add(sample);
        }

        /// <summary>
        /// Gets the history for a container, or null when none has been recorded.
        /// </summary>
        /// <param name="containerId">The container ID. Must not be null.</param>
        /// <returns>The history, or null.</returns>
        public ContainerHistory? Get(string containerId)
        {
            if (containerId == null) throw new ArgumentNullException(nameof(containerId));
            return _Containers.TryGetValue(containerId, out ContainerHistory? history) ? history : null;
        }

        /// <summary>
        /// Appends one deployment-wide aggregate point by summing the latest per-container values. Called
        /// on the UI refresh cadence so the overall series advances at a steady rate.
        /// </summary>
        public void TickOverall()
        {
            double cpu = 0.0;
            double memory = 0.0;
            double net = 0.0;
            double disk = 0.0;

            foreach (ContainerHistory history in _Containers.Values)
            {
                cpu += history.Cpu.Latest;
                memory += history.MemoryUsage.Latest;
                net += history.NetRxRate.Latest + history.NetTxRate.Latest;
                disk += history.DiskRate.Latest;
            }

            OverallCpu.Add(cpu);
            OverallMemory.Add(memory);
            OverallNetRate.Add(net);
            OverallDiskRate.Add(disk);
        }

        /// <summary>
        /// Removes history for containers that are no longer present.
        /// </summary>
        /// <param name="liveContainerIds">The set of container IDs still present. Must not be null.</param>
        public void Prune(ICollection<string> liveContainerIds)
        {
            if (liveContainerIds == null) throw new ArgumentNullException(nameof(liveContainerIds));

            List<string> stale = new List<string>();
            foreach (string id in _Containers.Keys)
            {
                if (!liveContainerIds.Contains(id))
                    stale.Add(id);
            }

            foreach (string id in stale)
                _Containers.Remove(id);
        }
    }
}
