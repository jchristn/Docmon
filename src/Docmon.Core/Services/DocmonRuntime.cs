namespace Docmon.Core.Services
{
    using System;
    using Docmon.Core.Registries;
    using Docmon.Core.Services.Implementations;
    using Docmon.Core.Services.Interfaces;

    /// <summary>Owns shared Docker service composition for local user interfaces. Thread safe services are shared.</summary>
    public sealed class DocmonRuntime : IDisposable
    {
        private readonly RegistryProviderFactory _Factory;
        /// <summary>Docker connection owned by this runtime.</summary>
        public DockerClientProvider Provider { get; }
        /// <summary>Container and image operations.</summary>
        public IDockerService Docker { get; }
        /// <summary>Container statistics.</summary>
        public IStatsStreamer Stats { get; }
        /// <summary>Daemon events.</summary>
        public IEventsMonitor Events { get; }
        /// <summary>Command execution.</summary>
        public IExecService Exec { get; }
        /// <summary>Archive transfers.</summary>
        public ITransferService Transfer { get; }
        /// <summary>Compose discovery and lifecycle.</summary>
        public IComposeService Compose { get; }
        /// <summary>Registry queries.</summary>
        public IRegistryService Registry { get; }
        /// <summary>Creates service composition for the supplied Docker endpoint, or the platform default.</summary>
        /// <param name="endpoint">Optional Docker endpoint.</param>
        public DocmonRuntime(string? endpoint = null)
        {
            Provider = new DockerClientProvider(endpoint);
            _Factory = new RegistryProviderFactory();
            Docker = new DockerService(Provider.Client);
            Stats = new StatsStreamer(Provider.Client);
            Events = new EventsMonitor(Provider.Client);
            Exec = new ExecService(Provider.Client);
            Transfer = new TransferService(Provider.Client);
            Compose = new ComposeService(Provider.Client);
            Registry = new RegistryService(_Factory);
        }
        /// <summary>Releases connections and registry resources.</summary>
        public void Dispose()
        {
            _Factory.Dispose();
            Provider.Dispose();
        }
    }
}
