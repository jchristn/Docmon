namespace Docmon.Core.Monitoring
{
    using System;

    /// <summary>Shared historical metric window definitions. Month means rolling thirty days.</summary>
    public sealed class MetricWindow
    {
        /// <summary>Bucket duration in minutes.</summary>
        public int Minutes { get; }
        /// <summary>Number of buckets in the window.</summary>
        public int Count { get; }
        /// <summary>Inclusive, minute-aligned window start.</summary>
        public DateTime StartUtc { get; }
        /// <summary>Exclusive window end, including the current partial minute.</summary>
        public DateTime EndUtc { get; }
        /// <summary>Creates a validated window ending at the supplied UTC time.</summary>
        /// <param name="timeframe">hour, day, week, or month.</param>
        /// <param name="now">Current UTC time.</param>
        /// <exception cref="ArgumentException">The timeframe is unsupported.</exception>
        public MetricWindow(string timeframe, DateTime now)
        {
            Minutes = timeframe switch { "hour" => 1, "day" => 15, "week" => 120, "month" => 480, _ => throw new ArgumentException("timeframe must be hour, day, week, or month.", nameof(timeframe)) };
            Count = timeframe switch { "hour" => 60, "day" => 96, "week" => 84, _ => 90 };
            EndUtc = new DateTime(now.ToUniversalTime().Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc).AddMinutes(1);
            StartUtc = EndUtc.AddMinutes(-Minutes * Count);
        }
    }
}
