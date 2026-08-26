namespace Docmon.Core.Registries
{
    /// <summary>
    /// Supplies registry credentials on demand. Docmon v1 uses anonymous access for Docker Hub; this
    /// abstraction lets private-registry credential sources (Docker config, environment, prompts) be
    /// added later without changing the providers that consume them.
    /// </summary>
    public interface IRegistryCredentialSource
    {
        /// <summary>
        /// Gets the credential for a registry host, or null when access should be anonymous.
        /// </summary>
        /// <param name="registryHost">The registry host, for example <c>docker.io</c>. Must not be null.</param>
        /// <returns>The credential, or null for anonymous access.</returns>
        RegistryCredential? GetCredential(string registryHost);
    }
}
