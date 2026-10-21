namespace Docmon.Server
{
    using System;
    using System.Collections.Generic;
    /// <summary>Redacted metadata and bounded bodies for a completed request. Authentication secrets are removed.</summary>
    public sealed class RequestRecord
    {
        /// <summary>Correlation ID.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>UTC timestamp.</summary>
        public DateTime AtUtc { get; set; } = DateTime.UtcNow;
        /// <summary>HTTP verb.</summary>
        public string Method { get; set; } = string.Empty;
        /// <summary>Route path without query string.</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>HTTP status.</summary>
        public int Status { get; set; }
        /// <summary>Authenticated machine-wide principal, null for anonymous callers.</summary>
        public string? PrincipalName { get; set; }
        /// <summary>Request headers with authentication secrets redacted.</summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();
        /// <summary>Response headers with secrets redacted.</summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();
        /// <summary>Bounded and redacted JSON request body.</summary>
        public string RequestBody { get; set; } = string.Empty;
        /// <summary>Bounded and redacted JSON response body.</summary>
        public string ResponseBody { get; set; } = string.Empty;
        /// <summary>Whether either body exceeded the capture limit.</summary>
        public bool Truncated { get; set; }
        /// <summary>Elapsed time in milliseconds.</summary>
        public double ElapsedMs { get; set; }
    }
}
