namespace Docmon.Core.Models
{
    using System;

    /// <summary>A metric time bucket. CPU and memory are means; byte counters are last observed totals.</summary>
    public sealed class MetricHistoryBucket : ContainerStatsSample
    {
        /// <summary>Exclusive bucket end in UTC.</summary>
        public DateTime EndUtc { get; set; }
        /// <summary>True when this bucket contains observations. False represents a gap, never zero usage.</summary>
        public bool HasData { get; set; }
        /// <summary>Number of underlying observations across the selected scope.</summary>
        public long SampleCount { get; set; }
    }
}
