namespace Docmon.Server
{
    /// <summary>Docker readiness response.</summary>
    public sealed class HealthResponse
    {
        /// <summary>healthy when Docker responds; unavailable otherwise.</summary>
        public string Status { get; set; } = "healthy";
    }
}
