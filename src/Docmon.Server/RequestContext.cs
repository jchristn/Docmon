namespace Docmon.Server
{
    /// <summary>Authenticated identity for a single Docker machine. The configured bearer grants machine-wide control.</summary>
    public sealed class RequestContext
    {
        /// <summary>Stable operator identity. No bearer material is retained.</summary>
        public string PrincipalName { get; } = "machine-operator";
        /// <summary>Authentication scheme.</summary>
        public string AuthScheme { get; } = "Bearer";
    }
}
