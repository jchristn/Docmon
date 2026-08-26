namespace Docmon.Core.Registries
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Docmon.Core.Registries.DockerHub;

    /// <summary>
    /// Selects the <see cref="IRegistryProvider"/> for a given registry host. Docker Hub ships
    /// registered by default; additional providers (GHCR, ECR, self-hosted) can be added with
    /// <see cref="Register"/> and are then chosen automatically by host. Thread safety: register
    /// providers at startup before concurrent resolution begins.
    /// </summary>
    public sealed class RegistryProviderFactory : IDisposable
    {
        private readonly Dictionary<string, IRegistryProvider> _Providers =
            new Dictionary<string, IRegistryProvider>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IDisposable> _Owned = new List<IDisposable>();
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryProviderFactory"/> class with the
        /// Docker Hub provider registered.
        /// </summary>
        /// <param name="credentials">The credential source shared by built-in providers, or null for anonymous access.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to share across built-in providers, or null to let each provider own one.</param>
        public RegistryProviderFactory(IRegistryCredentialSource? credentials = null, HttpClient? httpClient = null)
        {
            DockerHubRegistryProvider dockerHub = new DockerHubRegistryProvider(credentials, httpClient);
            Register(dockerHub);
            _Owned.Add(dockerHub);
        }

        /// <summary>
        /// Registers a provider under its <see cref="IRegistryProvider.RegistryHost"/>, replacing any
        /// existing registration for that host.
        /// </summary>
        /// <param name="provider">The provider to register. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
        public void Register(IRegistryProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _Providers[provider.RegistryHost] = provider;
        }

        /// <summary>
        /// Resolves the provider for a registry host.
        /// </summary>
        /// <param name="registryHost">The registry host, for example <c>docker.io</c>. Must not be null.</param>
        /// <returns>The matching provider, or null when no provider is registered for the host.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registryHost"/> is null.</exception>
        public IRegistryProvider? Resolve(string registryHost)
        {
            if (registryHost == null) throw new ArgumentNullException(nameof(registryHost));
            return _Providers.TryGetValue(registryHost, out IRegistryProvider? provider) ? provider : null;
        }

        /// <summary>
        /// Disposes any providers this factory created and owns.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed)
                return;

            foreach (IDisposable disposable in _Owned)
                disposable.Dispose();
            _Owned.Clear();
            _Disposed = true;
        }
    }
}
