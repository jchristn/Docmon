namespace Docmon.Core.Models
{
    using System.Collections.Generic;
    /// <summary>A page of collection objects and continuation metadata.</summary>
    /// <typeparam name="T">Collection element type.</typeparam>
    public sealed class EnumerationResult<T>
    {
        /// <summary>Whether enumeration completed successfully.</summary>
        public bool Success { get; set; } = true;
        /// <summary>Requested maximum page size.</summary>
        public int MaxResults { get; set; }
        /// <summary>Matching records before paging.</summary>
        public long TotalRecords { get; set; }
        /// <summary>Matching records after this page.</summary>
        public long RecordsRemaining { get; set; }
        /// <summary>Opaque next-page token, or null on the final page.</summary>
        public string? ContinuationToken { get; set; }
        /// <summary>Whether this page ends the enumeration.</summary>
        public bool EndOfResults { get; set; }
        /// <summary>Objects in this page.</summary>
        public List<T> Objects { get; set; } = new List<T>();
        /// <summary>Enumeration duration in milliseconds.</summary>
        public double TotalMs { get; set; }
    }
}
