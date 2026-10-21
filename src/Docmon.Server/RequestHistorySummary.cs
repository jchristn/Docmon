namespace Docmon.Server
{
    using System;
    using System.Collections.Generic;
    /// <summary>A complete rolling request-history window, including empty buckets.</summary>
    public sealed class RequestHistorySummary
    {
        /// <summary>hour, day, week, or month (rolling thirty days).</summary>
        public string Timeframe { get; set; } = "hour";
        /// <summary>Bucket width in minutes.</summary>
        public int BucketMinutes { get; set; }
        /// <summary>Inclusive window start.</summary>
        public DateTime StartUtc { get; set; }
        /// <summary>Exclusive window end.</summary>
        public DateTime EndUtc { get; set; }
        /// <summary>Total requests in the window.</summary>
        public long TotalCount { get; set; }
        /// <summary>Successful requests.</summary>
        public long TotalSuccess { get; set; }
        /// <summary>Failed requests.</summary>
        public long TotalFailure { get; set; }
        /// <summary>Chronological time buckets.</summary>
        public List<RequestHistoryBucket> Buckets { get; set; } = new List<RequestHistoryBucket>();
        internal static RequestHistorySummary Create(string? timeframe, DateTime end)
        {
            string value = timeframe ?? "hour";
            int minutes = value switch { "hour" => 1, "day" => 15, "week" => 120, "month" => 480, _ => throw new ArgumentException("timeframe must be hour, day, week, or month.") };
            int count = value switch { "hour" => 60, "day" => 96, "week" => 84, _ => 90 };
            RequestHistorySummary result = new RequestHistorySummary { Timeframe = value, BucketMinutes = minutes, EndUtc = end, StartUtc = end.AddMinutes(-minutes * count) };
            for (int i = 0; i < count; i++) result.Buckets.Add(new RequestHistoryBucket { AtUtc = result.StartUtc.AddMinutes(i * minutes), EndUtc = result.StartUtc.AddMinutes((i + 1) * minutes) });
            return result;
        }
        internal void Add(RequestRecord record)
        {
            if (record.AtUtc < StartUtc || record.AtUtc >= EndUtc) return;
            int index = (int)((record.AtUtc - StartUtc).TotalMinutes / BucketMinutes);
            if (record.Status < 400) { Buckets[index].Ok++; TotalSuccess++; }
            else { Buckets[index].Failed++; TotalFailure++; }
            TotalCount++;
        }
    }
}
