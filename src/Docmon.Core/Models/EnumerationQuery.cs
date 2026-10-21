namespace Docmon.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>Shared paging, ordering and filtering contract for collection reads.</summary>
    public class EnumerationQuery
    {
        private int _MaxResults = 100;
        private Dictionary<string, string> _Filters = new Dictionary<string, string>();
        /// <summary>Page size, from one through one thousand.</summary>
        public int MaxResults { get => _MaxResults; set { if (value < 1 || value > 1000) throw new ArgumentOutOfRangeException(nameof(MaxResults), "maxResults must be between 1 and 1000."); _MaxResults = value; } }
        /// <summary>Opaque token returned by the preceding page.</summary>
        public string? ContinuationToken { get; set; }
        /// <summary>Case-insensitive search across displayed scalar properties.</summary>
        public string? Search { get; set; }
        /// <summary>Camel-case scalar property to order by. Null preserves source order.</summary>
        public string? SortBy { get; set; }
        /// <summary>Ascending or Descending.</summary>
        public string Ordering { get; set; } = "Ascending";
        /// <summary>Optional container identifier filter.</summary>
        public string? ContainerIdFilter { get; set; }
        /// <summary>Inclusive lower UTC timestamp bound.</summary>
        public DateTime? StartUtc { get; set; }
        /// <summary>Inclusive upper UTC timestamp bound.</summary>
        public DateTime? EndUtc { get; set; }
        /// <summary>Case-insensitive substring filters keyed by scalar property names.</summary>
        public Dictionary<string, string> Filters { get => _Filters; set => _Filters = value ?? throw new ArgumentNullException(nameof(Filters)); }
    }
}
