namespace Docmon.Core.Registries
{
    /// <summary>
    /// A credential source that always returns null, causing providers to use anonymous access. This is
    /// the Docmon v1 default and is sufficient for public Docker Hub images.
    /// </summary>
    public sealed class AnonymousCredentialSource : IRegistryCredentialSource
    {
        /// <summary>
        /// Returns null for every host, indicating anonymous access.
        /// </summary>
        /// <param name="registryHost">The registry host. Ignored.</param>
        /// <returns>Always null.</returns>
        public RegistryCredential? GetCredential(string registryHost)
        {
            return null;
        }
    }
}
