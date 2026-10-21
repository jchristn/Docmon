namespace Docmon.Server
{
    using System;
    /// <summary>Request counts in one contiguous UTC interval.</summary>
    public sealed class RequestHistoryBucket
    {
        /// <summary>Inclusive bucket start.</summary>
        public DateTime AtUtc { get; set; }
        /// <summary>Exclusive bucket end.</summary>
        public DateTime EndUtc { get; set; }
        /// <summary>Responses with status below 400.</summary>
        public long Ok { get; set; }
        /// <summary>Responses with status at least 400.</summary>
        public long Failed { get; set; }
    }
}
