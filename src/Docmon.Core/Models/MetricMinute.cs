namespace Docmon.Core.Models
{
    using System;

    /// <summary>Persisted per-container minute aggregate, including raw sums for weighted historical means.</summary>
    public sealed class MetricMinute : ContainerStatsSample
    {
        /// <summary>Timestamp of the last observation used for cumulative counters.</summary>
        public DateTime LastSampleUtc { get; set; }

        /// <summary>Number of observations in the minute.</summary>
        public long Count { get; set; }
        /// <summary>Sum of observed CPU percentages.</summary>
        public double CpuSum { get; set; }
        /// <summary>Sum of observed memory usage in bytes.</summary>
        public double MemorySum { get; set; }
    }
}
