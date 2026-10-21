namespace Docmon.Server
{
    /// <summary>Stable API error response.</summary>
    public sealed class ApiError
    {
        /// <summary>Machine readable code.</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>Operator-facing description.</summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>Correlation identifier.</summary>
        public string RequestId { get; set; } = string.Empty;
    }
}
