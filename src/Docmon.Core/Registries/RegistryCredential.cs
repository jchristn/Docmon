namespace Docmon.Core.Registries
{
    /// <summary>
    /// A username and secret pair used to authenticate to a registry.
    /// </summary>
    public class RegistryCredential
    {
        /// <summary>
        /// Gets or sets the username or account identifier.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password, token, or other secret.
        /// </summary>
        public string Secret { get; set; } = string.Empty;
    }
}
