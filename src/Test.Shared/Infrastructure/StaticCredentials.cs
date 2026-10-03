namespace Test.Shared.Infrastructure
{
    using Docmon.Core.Registries;

    /// <summary>
    /// A credential source that always returns the same credential and records the host it was asked for.
    /// </summary>
    internal sealed class StaticCredentials : IRegistryCredentialSource
    {
        private readonly RegistryCredential _Credential;

        internal StaticCredentials(RegistryCredential credential)
        {
            _Credential = credential;
        }

        internal string? LastHost { get; private set; }

        public RegistryCredential? GetCredential(string registryHost)
        {
            LastHost = registryHost;
            return _Credential;
        }
    }
}
