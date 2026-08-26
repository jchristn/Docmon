namespace Docmon.App.Monitoring
{
    using System;
    using Docmon.Core.Models;

    /// <summary>
    /// Rolling metric history for a single container: CPU, memory, and derived network and block-I/O
    /// rates. Cumulative counters from Docker are converted into per-second rates as samples arrive.
    /// </summary>
    public sealed class ContainerHistory
    {
        private long _LastNetRx = -1;
        private long _LastNetTx = -1;
        private long _LastBlockRead = -1;
        private long _LastBlockWrite = -1;
        private DateTime _LastAtUtc = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContainerHistory"/> class.
        /// </summary>
        /// <param name="capacity">The number of samples each series retains.</param>
        public ContainerHistory(int capacity)
        {
            Cpu = new MetricSeries(capacity);
            MemoryPercent = new MetricSeries(capacity);
            MemoryUsage = new MetricSeries(capacity);
            NetRxRate = new MetricSeries(capacity);
            NetTxRate = new MetricSeries(capacity);
            DiskRate = new MetricSeries(capacity);
        }

        /// <summary>Gets the CPU-percentage series.</summary>
        public MetricSeries Cpu { get; }

        /// <summary>Gets the memory-utilization-percentage series.</summary>
        public MetricSeries MemoryPercent { get; }

        /// <summary>Gets the memory-usage series, in bytes.</summary>
        public MetricSeries MemoryUsage { get; }

        /// <summary>Gets the network receive-rate series, in bytes per second.</summary>
        public MetricSeries NetRxRate { get; }

        /// <summary>Gets the network transmit-rate series, in bytes per second.</summary>
        public MetricSeries NetTxRate { get; }

        /// <summary>Gets the combined block-I/O rate series, in bytes per second.</summary>
        public MetricSeries DiskRate { get; }

        /// <summary>Gets the most recent memory limit, in bytes.</summary>
        public long MemoryLimit { get; private set; }

        /// <summary>
        /// Adds a stats sample, computing rates from the previous sample.
        /// </summary>
        /// <param name="sample">The sample to add. Must not be null.</param>
        public void Add(ContainerStatsSample sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            Cpu.Add(sample.CpuPercent);
            MemoryPercent.Add(sample.MemoryPercent);
            MemoryUsage.Add(sample.MemoryUsage);
            MemoryLimit = sample.MemoryLimit;

            double seconds = _LastAtUtc == DateTime.MinValue ? 0.0 : (sample.AtUtc - _LastAtUtc).TotalSeconds;
            if (seconds > 0.0 && _LastNetRx >= 0)
            {
                NetRxRate.Add(Rate(sample.NetworkRxBytes, _LastNetRx, seconds));
                NetTxRate.Add(Rate(sample.NetworkTxBytes, _LastNetTx, seconds));
                double read = Rate(sample.BlockReadBytes, _LastBlockRead, seconds);
                double write = Rate(sample.BlockWriteBytes, _LastBlockWrite, seconds);
                DiskRate.Add(read + write);
            }
            else
            {
                NetRxRate.Add(0.0);
                NetTxRate.Add(0.0);
                DiskRate.Add(0.0);
            }

            _LastNetRx = sample.NetworkRxBytes;
            _LastNetTx = sample.NetworkTxBytes;
            _LastBlockRead = sample.BlockReadBytes;
            _LastBlockWrite = sample.BlockWriteBytes;
            _LastAtUtc = sample.AtUtc;
        }

        private static double Rate(long current, long previous, double seconds)
        {
            double delta = current - previous;
            if (delta < 0.0)
                return 0.0;

            return delta / seconds;
        }
    }
}
